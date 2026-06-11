#!/usr/bin/env python3
"""Local tripwire audit for Dalamud policy and AI-use risk patterns."""
from __future__ import annotations

import argparse
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path


@dataclass(frozen=True)
class Pattern:
    token: str
    severity: str
    message: str


FAIL_PATTERNS = [
    Pattern("fflogs", "FAIL", "FFLogs direction is explicitly out of scope for official Dalamud plugins."),
    Pattern("dps meter", "FAIL", "DPS meter/parser direction is explicitly out of scope."),
    Pattern("damage parser", "FAIL", "Damage parser direction is explicitly out of scope."),
    Pattern("telemetry", "FAIL", "Telemetry is out of scope for v1 unless separately designed and approved."),
    Pattern("analytics", "FAIL", "Analytics are out of scope for v1 unless separately designed and approved."),
    Pattern("leaderboard", "FAIL", "Leaderboards imply backend/user data and are out of scope for v1."),
    Pattern("websocket", "FAIL", "Network/backend communication is out of scope for v1."),
    Pattern("httpclient", "FAIL", "Network/backend communication is out of scope for v1."),
    Pattern("contentid", "FAIL", "ContentId storage/transmission needs explicit privacy review and is out of scope for v1."),
    Pattern("ai-generated", "FAIL", "AI-generated assets are disallowed for this project."),
]

WARN_PATTERNS = [
    Pattern("unsafe", "WARN", "Unsafe code requires explicit design review."),
    Pattern("signature", "WARN", "Signature scanning/raw memory work requires explicit design review."),
    Pattern("hook", "WARN", "Hooks/raw memory work require explicit design review."),
    Pattern("clientstructs", "WARN", "Client Structs usage requires explicit design review and docs references."),
    Pattern("pvp", "WARN", "PvP-sensitive behavior needs policy review."),
    Pattern("PluginService", "WARN", "New Dalamud service usage should cite official docs."),
    Pattern("GetExcelSheet", "WARN", "Lumina sheet usage should cite IDataManager docs."),
    Pattern("IsAchievementComplete", "WARN", "Achievement completion usage should cite IUnlockState docs."),
]


EXCLUDED_PREFIXES = ("docs/", ".hermes/", "bin/", "obj/", "released/", "map/", "wiki-export/")
EXCLUDED_EXACT = {"scripts/audit-ai-policy.py", "scripts/adversarial-code-review.py"}
INCLUDED_UNTRACKED_SUFFIXES = (".cs", ".csproj", ".sln", ".json", ".py")
INCLUDED_TREE_SUFFIXES = (".cs", ".csproj", ".json", ".py", ".sh")
TREE_SCAN_PREFIXES = ("AchievementTracker/", "AchievementTracker.Tests/", "scripts/")
TREE_SCAN_EXACT_EXCLUDES = {
    "scripts/audit-ai-policy.py",
    "scripts/adversarial-code-review.py",
}

AGENTS_HARD_BLOCKERS = [
    Pattern("Dalamud.Hooking", "FAIL", "AGENTS.md blocker: low-level hooks are not allowed."),
    Pattern("HookFromAddress", "FAIL", "AGENTS.md blocker: low-level hooks are not allowed."),
    Pattern("Hook<", "FAIL", "AGENTS.md blocker: low-level hooks are not allowed."),
    Pattern("MemberFunctionPointers", "FAIL", "AGENTS.md blocker: native function pointer binding is not allowed."),
    Pattern("SigScanner", "FAIL", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    Pattern("SignatureAttribute", "FAIL", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    Pattern("[Signature", "FAIL", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    Pattern("ScanText", "FAIL", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    Pattern("ScanData", "FAIL", "AGENTS.md blocker: signatures/signature scanning are not allowed."),
    Pattern("GetStaticAddress", "FAIL", "AGENTS.md blocker: raw-memory/static-address paths are not allowed."),
    Pattern("RequestAchievementProgress", "FAIL", "AGENTS.md blocker: plugin-originated achievement progress requests are not allowed."),
    Pattern("HttpClient", "FAIL", "AGENTS.md blocker: backend/network calls need explicit design/privacy review."),
    Pattern("WebSocket", "FAIL", "AGENTS.md blocker: backend/network calls need explicit design/privacy review."),
    Pattern("ContentId", "FAIL", "AGENTS.md blocker: content ID collection/use needs explicit privacy review."),
]


def is_scanned_path(path: str) -> bool:
    normalized = path.replace("\\", "/")
    return not normalized.startswith(EXCLUDED_PREFIXES) and normalized not in EXCLUDED_EXACT


def run_git_diff(base: str) -> str:
    result = subprocess.run(["git", "diff", base, "--", ":(exclude)*.md", ":(exclude)docs/**", ":(exclude).hermes/**", ":(exclude)released/**", ":(exclude)scripts/audit-ai-policy.py", ":(exclude)scripts/adversarial-code-review.py"], check=False, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)

    untracked = subprocess.run(["git", "ls-files", "--others", "--exclude-standard"], check=False, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if untracked.returncode != 0:
        print(untracked.stderr, file=sys.stderr)
        raise SystemExit(untracked.returncode)

    chunks = [result.stdout]
    for raw_path in untracked.stdout.splitlines():
        if not is_scanned_path(raw_path) or not raw_path.endswith(INCLUDED_UNTRACKED_SUFFIXES):
            continue
        path = Path(raw_path)
        if path.is_file():
            chunks.append(f"\n--- untracked file: {raw_path} ---\n")
            chunks.append(path.read_text(encoding="utf-8", errors="ignore"))

    return "\n".join(chunks)


def is_tree_scanned_path(path: str) -> bool:
    normalized = path.replace("\\", "/")
    return (
        normalized.startswith(TREE_SCAN_PREFIXES)
        and normalized not in TREE_SCAN_EXACT_EXCLUDES
        and normalized.endswith(INCLUDED_TREE_SUFFIXES)
    )


def run_git_ls_files() -> list[str]:
    result = subprocess.run(["git", "ls-files"], check=False, text=True, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode != 0:
        print(result.stderr, file=sys.stderr)
        raise SystemExit(result.returncode)
    return [line for line in result.stdout.splitlines() if is_tree_scanned_path(line)]


def scan_current_tree_for_agents_blockers() -> list[tuple[Pattern, str]]:
    findings: list[tuple[Pattern, str]] = []
    for raw_path in run_git_ls_files():
        path = Path(raw_path)
        if not path.is_file():
            continue
        text = path.read_text(encoding="utf-8", errors="ignore")
        for line_no, line in enumerate(text.splitlines(), start=1):
            for pattern in AGENTS_HARD_BLOCKERS:
                if pattern.token.lower() in line.lower():
                    findings.append((pattern, f"{pattern.message} ({raw_path}:{line_no})"))
    return findings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--diff", default="HEAD", help="Git revision/base to diff against, default HEAD")
    args = parser.parse_args()

    diff = run_git_diff(args.diff)
    lowered = diff.lower()
    findings: list[tuple[Pattern, str]] = []
    findings.extend(scan_current_tree_for_agents_blockers())

    for pattern in FAIL_PATTERNS + WARN_PATTERNS:
        token = pattern.token.lower()
        if token not in lowered:
            continue

        if pattern.severity == "WARN":
            if token == "pluginservice" and ("plugin-development/project-layout" in lowered or "api/dalamud.plugin.services/interfaces/iclientstate" in lowered):
                continue
            if token == "getexcelsheet" and "api/dalamud.plugin.services/interfaces/idatamanager" in lowered:
                continue
            if token == "isachievementcomplete" and "api/dalamud.plugin.services/interfaces/iunlockstate" in lowered:
                continue
            if token in {"unsafe", "clientstructs"} and "plugin-development/interaction" in lowered and "plugin-publishing/restrictions" in lowered:
                continue
            if token == "hook" and "plugin-development/interaction" in lowered:
                continue

        findings.append((pattern, pattern.message))

    has_fail = any(pattern.severity == "FAIL" for pattern, _ in findings)
    has_warn = any(pattern.severity == "WARN" for pattern, _ in findings)

    if has_fail:
        overall = "FAIL"
    elif has_warn:
        overall = "PASS WITH WARNINGS"
    else:
        overall = "PASS"

    print(f"Overall: {overall}")
    if findings:
        print("Findings:")
        for pattern, message in findings:
            print(f"- {pattern.severity}: token '{pattern.token}' — {message}")
    else:
        print("No hard-fail or warning policy tripwires found.")

    print("\nReminder: this script is a tripwire. Human/agent review against docs is still required before official submission.")
    return 1 if has_fail else 0


if __name__ == "__main__":
    raise SystemExit(main())
