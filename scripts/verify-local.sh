#!/usr/bin/env bash
set -euo pipefail

BASE="${1:-HEAD}"
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

echo "== Dalamud docs freshness =="
python3 scripts/refresh-dalamud-docs.py --check-latest
# The freshness checker rewrites generated cache metadata even when content is unchanged.
# Reset that generated churn so later diff-based policy checks focus on authored changes.
if ! git diff --quiet -- docs/docs-cache; then
    echo "Resetting generated docs-cache freshness output before diff-based audits."
    git checkout -- docs/docs-cache
fi

echo "== Unit tests =="
dotnet run --project AchievementTracker.Tests/AchievementTracker.Tests.csproj

echo "== Debug build =="
dotnet build AchievementTracker.sln -c Debug

echo "== Release build =="
dotnet build AchievementTracker.sln -c Release

echo "== CodeQL C# security/quality scan =="
./scripts/codeql-local.sh

echo "== Dalamud policy / AI tripwire =="
python3 scripts/audit-ai-policy.py --diff "$BASE"

echo "== Adversarial code review tripwire =="
python3 scripts/adversarial-code-review.py --diff "$BASE"

echo "== Whitespace diff check =="
git diff --check

echo "Verification complete. For merge/submission, also run the independent reviewer prompt in docs/ai-policy-audits/adversarial-code-review-agent.md against the diff and script outputs."
