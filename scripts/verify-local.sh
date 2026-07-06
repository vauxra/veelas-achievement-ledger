#!/usr/bin/env bash
set -euo pipefail

BASE="${1:-HEAD}"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

echo "== Unit tests =="
dotnet run --project AchievementTracker.Tests/AchievementTracker.Tests.csproj

echo "== Clean Debug/Release outputs =="
dotnet clean AchievementTracker.sln -c Debug >/dev/null
dotnet clean AchievementTracker.sln -c Release >/dev/null
rm -rf AchievementTracker/bin/Debug AchievementTracker/bin/Release

echo "== Debug build =="
dotnet build AchievementTracker.sln -c Debug

echo "== Release build =="
dotnet build AchievementTracker.sln -c Release

echo "== Release package sanity =="
python3 - <<'PY'
import json
import sys
import zipfile
from pathlib import Path

manifest_path = Path("AchievementTracker/bin/Release/AchieveEx.json")
zip_path = Path("AchievementTracker/bin/Release/AchieveEx/latest.zip")
if not manifest_path.exists() or not zip_path.exists():
    print("Missing generated release manifest or latest.zip.", file=sys.stderr)
    raise SystemExit(1)
manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
expected = {
    "AchieveEx.dll",
    "AchieveEx.deps.json",
    "AchieveEx.json",
}
with zipfile.ZipFile(zip_path) as zf:
    names = set(zf.namelist())
if manifest.get("InternalName") != "AchieveEx":
    print(f"Unexpected InternalName: {manifest.get('InternalName')}", file=sys.stderr)
    raise SystemExit(1)
if names != expected:
    print(f"Unexpected latest.zip contents: {sorted(names)}", file=sys.stderr)
    raise SystemExit(1)
print("Release package contains only AchieveEx DLL/deps/manifest.")
PY

echo "== CodeQL C# security/quality scan =="
./scripts/codeql-local.sh

echo "== Dalamud policy / AI tripwire =="
python3 scripts/audit-ai-policy.py --diff "$BASE"

echo "== Adversarial code review tripwire =="
python3 scripts/adversarial-code-review.py --diff "$BASE"

echo "== Whitespace diff check =="
git diff --check

echo "Verification complete. For merge/submission, also run the independent reviewer prompt in docs/ai-policy-audits/adversarial-code-review-agent.md against the diff and script outputs."
