#!/usr/bin/env python3
"""Adversarial local review tripwire for Veela's Achievement Ledger.

This script is intentionally conservative. It does not replace human/agent review;
it catches obvious Dalamud-policy and C# security bad-practice patterns before a
change is committed or pushed.
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Finding:
    severity: str
    path: str
    line: int | None
    rule: str
    message: str


CODE_SUFFIXES = {".cs", ".csproj", ".json", ".py", ".sln", ".sh"}
EXCLUDED_PREFIXES = ("docs/", ".hermes/", "bin/", "obj/", "released/")
ALLOWED_ACHIEVEMENT_REQUEST_FILES: set[str] = set()
SCANNER_IMPLEMENTATION_FILES = {
    "scripts/audit-ai-policy.py",
    "scripts/adversarial-code-review.py",
}

TREE_SCAN_PREFIXES = ("AchievementTracker/", "AchievementTracker.Tests/", "scripts/")

AGENTS_HARD_BLOCKER_TOKENS: list[tuple[str, str, str]] = [
    ("Dalamud.Hooking", "agents-hard-blocker", "AGENTS.md blocker: low-level hooks are not allowed."),
    ("HookFromAddress", "agents-hard-blocker", "AGENTS.md blocker: low-level hooks are not allowed."),
    ("Hook<", "agents-hard-blocker", "AGENTS.md blocker: low-level hooks are not allowed."),
    ("MemberFunctionPointers", "agents-hard-blocker", "AGENTS.md blocker: native function pointer binding is not allowed."),
    ("SigScanner", "agents-hard-blocker", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    ("SignatureAttribute", "agents-hard-blocker", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    ("[Signature", "agents-hard-blocker", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    ("ScanText", "agents-hard-blocker", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    ("ScanData", "agents-hard-blocker", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    ("GetStaticAddress", "agents-hard-blocker", "AGENTS.md blocker: raw-memory/static-address paths are not allowed."),
    ("RequestAchievementProgress", "agents-hard-blocker", "AGENTS.md blocker: plugin-originated achievement progress requests are not allowed."),
    ("HttpClient", "agents-hard-blocker", "AGENTS.md blocker: backend/network calls need explicit design/privacy review."),
    ("WebSocket", "agents-hard-blocker", "AGENTS.md blocker: backend/network calls need explicit design/privacy review."),
    ("ContentId", "agents-hard-blocker", "AGENTS.md blocker: content ID collection/use needs explicit privacy review."),
]

SECRET_RE = re.compile(
    r"(?i)(api[_-]?key|secret|password|passwd|token|private[_-]?key)\s*[:=]\s*['\"][^'\"]{8,}['\"]"
)

SECURITY_PATTERNS: list[tuple[str, str, str]] = [
    ("Process.Start", "command-execution", "Command/process execution is not expected in this plugin and can become command injection."),
    ("System.Diagnostics.Process", "command-execution", "Process execution API is not expected in this plugin."),
    ("Assembly.Load", "dynamic-loading", "Dynamic assembly loading is a code-loading risk."),
    ("Activator.CreateInstance", "dynamic-loading", "Dynamic creation should be reviewed for plugin/code-loading risk."),
    ("File.Open(", "file-access", "File access needs path validation and a clear local-file purpose."),
    ("File.ReadAll", "file-access", "File access needs path validation and a clear local-file purpose."),
    ("File.WriteAll", "file-access", "File writes need path validation and a clear local-file purpose."),
    ("Directory.Delete", "file-access", "Directory deletion is dangerous and needs explicit path constraints."),
    ("HttpClient", "network", "Network/backend communication is out of scope for V1 unless separately approved."),
    ("WebSocket", "network", "Network/backend communication is out of scope for V1 unless separately approved."),
]

POLICY_FAIL_TOKENS: list[tuple[str, str]] = [
    ("telemetry", "Telemetry is out of scope for V1 unless separately designed and approved."),
    ("analytics", "Analytics are out of scope for V1 unless separately designed and approved."),
    ("leaderboard", "Leaderboards imply backend/user data and are out of scope for V1."),
    ("ContentId", "ContentId use needs explicit privacy review."),
    ("RequestAchievementProgress", "Plugin-originated achievement progress requests are out of scope."),
]

AUTO_TRIGGER_TOKENS = [
    "Framework.Update",
    "IFramework",
    "PeriodicTimer",
    "System.Timers.Timer",
    "Task.Delay",
    "Task.Run",
    "AddonLifecycle.RegisterListener",
    "TerritoryChanged",
    "ClassJobChanged",
    "LevelChanged",
    "Login +=",
    "ZoneInit",
]

REQUEST_WRAPPER_TOKENS = [
    "RequestAchievementProgress",
    "RequestProgress(",
    ".RequestProgress(",
    "AchievementProgressSource.RequestProgress",
    "ProcessQueuedProgressRequests(",
    "ProgressRefreshQueue.Enqueue",
    "ProgressRequestThrottler",
]

SCANNER_SECURITY_PATTERNS: list[tuple[re.Pattern[str], str, str]] = [
    (re.compile(r"subprocess\.(run|Popen|call|check_call|check_output)\([^#\n]*shell\s*=\s*True"), "shell-injection", "Scanner scripts must not invoke subprocesses through a shell."),
    (re.compile(r"\bos\.system\s*\("), "shell-injection", "Scanner scripts must not use os.system."),
    (re.compile(r"\beval\s*\("), "dynamic-execution", "Scanner scripts must not use eval."),
    (re.compile(r"\bexec\s*\("), "dynamic-execution", "Scanner scripts must not use exec."),
]

SHELL_SECURITY_PATTERNS: list[tuple[re.Pattern[str], str, str]] = [
    (re.compile(r"(^|[;&|])\s*eval\s+"), "shell-injection", "Shell scripts must not use eval."),
    (re.compile(r"curl\b[^\n|]*\|\s*(sh|bash)\b"), "remote-code-execution", "Do not pipe downloaded content directly into a shell."),
    (re.compile(r"\brm\s+-rf\s+/(\s|$)"), "destructive-command", "Shell scripts must not recursively delete root."),
    (re.compile(r"\bsudo\b"), "privilege-escalation", "Local verification scripts should not require sudo."),
]

DOC_REQUIREMENTS = {
    "RequestAchievementProgress": [
        "plugin-development/interaction",
        "plugin-publishing/restrictions",
    ],
    "unsafe": ["plugin-development/interaction"],
    "ClientStructs": ["plugin-development/interaction"],
    "IClientState": ["api/Dalamud.Plugin.Services/Interfaces/IClientState"],
    "IUnlockState": ["api/Dalamud.Plugin.Services/Interfaces/IUnlockState"],
    "IDataManager": ["api/Dalamud.Plugin.Services/Interfaces/IDataManager"],
}


def run(args: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(args, check=False, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)


def is_code_path(path: str) -> bool:
    norm = path.replace("\\", "/")
    return not norm.startswith(EXCLUDED_PREFIXES) and Path(norm).suffix in CODE_SUFFIXES


def get_untracked_files() -> list[str]:
    untracked = run(["git", "ls-files", "--others", "--exclude-standard"])
    if untracked.returncode != 0:
        print(untracked.stderr, file=sys.stderr)
        raise SystemExit(untracked.returncode)
    return sorted(line for line in untracked.stdout.splitlines() if is_code_path(line))


def get_changed_files(base: str) -> list[str]:
    result = run(["git", "diff", "--name-only", base])
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)
    files = [line for line in result.stdout.splitlines() if is_code_path(line)]
    files.extend(get_untracked_files())
    return sorted(set(files))


def get_diff(base: str) -> str:
    result = run(["git", "diff", base, "--", ":(exclude)docs/**", ":(exclude).hermes/**"])
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)
    return result.stdout


def added_lines_by_file(diff: str) -> dict[str, list[tuple[int | None, str]]]:
    current_file = ""
    new_line: int | None = None
    output: dict[str, list[tuple[int | None, str]]] = {}

    hunk_re = re.compile(r"@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@")
    for line in diff.splitlines():
        if line.startswith("+++ b/"):
            current_file = line[len("+++ b/") :]
            output.setdefault(current_file, [])
            new_line = None
            continue
        match = hunk_re.match(line)
        if match:
            new_line = int(match.group(1))
            continue
        if not current_file or line.startswith("diff --git"):
            continue
        if line.startswith("+") and not line.startswith("+++"):
            output.setdefault(current_file, []).append((new_line, line[1:]))
            if new_line is not None:
                new_line += 1
        elif line.startswith("-") and not line.startswith("---"):
            continue
        elif new_line is not None:
            new_line += 1
    return output


def read_file(path: str) -> str:
    try:
        return Path(path).read_text(encoding="utf-8", errors="ignore")
    except FileNotFoundError:
        return ""


def include_untracked_as_added(added: dict[str, list[tuple[int | None, str]]]) -> None:
    for path in get_untracked_files():
        text = read_file(path)
        if not text:
            continue
        added[path] = [(line_no, line) for line_no, line in enumerate(text.splitlines(), start=1)]


def should_skip_literal_policy_scan(path: str) -> bool:
    return path in SCANNER_IMPLEMENTATION_FILES


def is_tree_scanned_path(path: str) -> bool:
    norm = path.replace("\\", "/")
    return (
        norm.startswith(TREE_SCAN_PREFIXES)
        and norm not in SCANNER_IMPLEMENTATION_FILES
        and Path(norm).suffix in CODE_SUFFIXES
    )


def get_tracked_tree_files() -> list[str]:
    result = run(["git", "ls-files"])
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)
    return sorted(line for line in result.stdout.splitlines() if is_tree_scanned_path(line))


def scan_current_tree_for_agents_blockers() -> list[Finding]:
    findings: list[Finding] = []
    for path in get_tracked_tree_files():
        for line_no, text in enumerate(read_file(path).splitlines(), start=1):
            lowered = text.lower()
            for token, rule, message in AGENTS_HARD_BLOCKER_TOKENS:
                if token.lower() in lowered:
                    findings.append(Finding("FAIL", path, line_no, rule, message))
    return findings


def scan(base: str) -> list[Finding]:
    findings: list[Finding] = []
    findings.extend(scan_current_tree_for_agents_blockers())
    diff = get_diff(base)
    added = added_lines_by_file(diff)
    include_untracked_as_added(added)
    files = get_changed_files(base)
    changed_text = "\n".join(read_file(path) for path in files)

    for path, lines in added.items():
        if not is_code_path(path):
            continue
        for line_no, text in lines:
            if SECRET_RE.search(text):
                findings.append(Finding("FAIL", path, line_no, "hardcoded-secret", "Possible hardcoded secret/credential in added code."))

            if Path(path).suffix == ".sh":
                for pattern, rule, message in SHELL_SECURITY_PATTERNS:
                    if pattern.search(text):
                        findings.append(Finding("FAIL", path, line_no, rule, message))

            if should_skip_literal_policy_scan(path):
                for pattern, rule, message in SCANNER_SECURITY_PATTERNS:
                    if pattern.search(text):
                        findings.append(Finding("FAIL", path, line_no, rule, message))
                continue

            for token, rule, message in SECURITY_PATTERNS:
                if token in text:
                    findings.append(Finding("FAIL", path, line_no, rule, message))
            for token, message in POLICY_FAIL_TOKENS:
                if token.lower() not in text.lower():
                    continue
                if token == "RequestAchievementProgress" and path in ALLOWED_ACHIEVEMENT_REQUEST_FILES:
                    continue
                findings.append(Finding("FAIL", path, line_no, "dalamud-policy", message))

    # Automatic game request heuristic: fail if request calls/wrappers are introduced
    # outside the adapter, or if request logic is paired with new timer/framework triggers.
    request_files = [
        path for path, lines in added.items()
        if not should_skip_literal_policy_scan(path)
        and any(any(token in text for token in REQUEST_WRAPPER_TOKENS) for _, text in lines)
    ]
    trigger_files = [
        path for path, lines in added.items()
        if not should_skip_literal_policy_scan(path)
        and any(any(token in text for token in AUTO_TRIGGER_TOKENS) for _, text in lines)
    ]
    disallowed_request_files = [path for path in request_files if path not in ALLOWED_ACHIEVEMENT_REQUEST_FILES]
    for path in disallowed_request_files:
        findings.append(Finding("FAIL", path, None, "auto-game-request", "Achievement progress request or wrapper was added outside the isolated adapter."))

    # Stronger polling guard: an added framework/timer/addon/login/zone/job trigger is suspicious
    # if the changed file already contains a request wrapper anywhere, even if that wrapper was
    # not introduced in this diff. This catches wiring an existing request method into an automatic event.
    for path in trigger_files:
        if path in ALLOWED_ACHIEVEMENT_REQUEST_FILES:
            continue
        file_text = read_file(path)
        if any(token in file_text for token in REQUEST_WRAPPER_TOKENS):
            findings.append(Finding("FAIL", path, None, "auto-game-request", "Automatic trigger added in a file containing achievement progress request wrappers; review for polling."))

    if request_files and trigger_files and any(path not in ALLOWED_ACHIEVEMENT_REQUEST_FILES for path in trigger_files):
        findings.append(Finding("FAIL", ",".join(trigger_files), None, "auto-game-request", "New timer/framework/event trigger appears in the same diff as achievement request logic; ensure no automatic polling."))

    # Event lifecycle heuristic: added subscriptions must have matching unsubscriptions somewhere in changed code.
    for path, lines in added.items():
        if not is_code_path(path) or should_skip_literal_policy_scan(path):
            continue
        for line_no, text in lines:
            if "+=" not in text:
                continue
            stripped = text.strip().rstrip(";")
            left = stripped.split("+=", 1)[0].strip()
            if "." not in left:
                continue
            unsubscribe = f"{left} -="
            file_text = read_file(path)
            if unsubscribe not in file_text:
                findings.append(Finding("FAIL", path, line_no, "event-lifecycle", f"Event subscription `{left} +=` has no matching `{left} -=` in the same file."))

    # Documentation requirements for sensitive APIs in changed code.
    lowered_changed_text = changed_text.lower()
    for token, required_docs in DOC_REQUIREMENTS.items():
        if token.lower() not in lowered_changed_text:
            continue
        missing = [doc for doc in required_docs if doc.lower() not in lowered_changed_text]
        if missing:
            findings.append(Finding("WARN", "changed-files", None, "missing-doc-citation", f"`{token}` appears in changed code without citation(s): {', '.join(missing)}"))

    return findings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diff", default="HEAD", help="Git revision/base to diff against, default HEAD")
    args = parser.parse_args()

    findings = scan(args.diff)
    has_fail = any(f.severity == "FAIL" for f in findings)
    has_warn = any(f.severity == "WARN" for f in findings)
    overall = "FAIL" if has_fail else "PASS WITH WARNINGS" if has_warn else "PASS"

    print(f"Overall: {overall}")
    if findings:
        print("Findings:")
        for f in findings:
            loc = f.path if f.line is None else f"{f.path}:{f.line}"
            print(f"- {f.severity}: {loc}: {f.rule} — {f.message}")
    else:
        print("No adversarial review tripwires found.")

    print("\nReminder: this script catches obvious patterns only. Run the independent adversarial reviewer agent before merge/submission.")
    return 1 if has_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
