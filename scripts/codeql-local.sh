#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export PATH="$HOME/.local/bin:$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

CODEQL_BIN="${CODEQL_BIN:-codeql}"
CODEQL_PACK="${CODEQL_PACK:-codeql/csharp-queries}"
CODEQL_SUITE_NAME="${CODEQL_SUITE_NAME:-csharp-security-and-quality.qls}"
# Keep the CodeQL database outside the source tree so CodeQL does not recursively
# index its own working files while source-root points at this repository.
CODEQL_DB_DIR="${CODEQL_DB_DIR:-${TMPDIR:-/tmp}/achievement-tracker-codeql-db-${USER:-user}/csharp}"
CODEQL_RESULTS_DIR="${CODEQL_RESULTS_DIR:-.codeql-results}"
CODEQL_SARIF="${CODEQL_SARIF:-$CODEQL_RESULTS_DIR/csharp-security-and-quality.sarif}"
BUILD_COMMAND="${CODEQL_BUILD_COMMAND:-./scripts/codeql-build.sh}"

if ! command -v "$CODEQL_BIN" >/dev/null 2>&1; then
  echo "CodeQL CLI not found. Install it locally, e.g. symlink ~/.local/bin/codeql to the CodeQL CLI." >&2
  exit 127
fi

mkdir -p "$CODEQL_RESULTS_DIR" "$(dirname "$CODEQL_DB_DIR")"

# Query packs are distributed separately from the CLI. This is idempotent when the pack is already installed.
"$CODEQL_BIN" pack download "$CODEQL_PACK" >/dev/null

SUITE_PATH="$(python3 - <<'PY'
from pathlib import Path
import os
home = Path.home()
suite = os.environ.get('CODEQL_SUITE_NAME', 'csharp-security-and-quality.qls')
roots = [home / '.codeql' / 'packages' / 'codeql' / 'csharp-queries']
for root in roots:
    if not root.exists():
        continue
    matches = sorted(root.glob(f'*/codeql-suites/{suite}'), reverse=True)
    if matches:
        print(matches[0])
        raise SystemExit(0)
raise SystemExit(1)
PY
)"

if [ -z "$SUITE_PATH" ]; then
  echo "Could not resolve CodeQL C# query suite: $CODEQL_SUITE_NAME" >&2
  exit 1
fi

rm -rf "$CODEQL_DB_DIR"
"$CODEQL_BIN" database create "$CODEQL_DB_DIR" \
  --language=csharp \
  --source-root "$ROOT" \
  --command "$BUILD_COMMAND" \
  --overwrite \
  --quiet

"$CODEQL_BIN" database analyze "$CODEQL_DB_DIR" "$SUITE_PATH" \
  --format=sarifv2.1.0 \
  --output="$CODEQL_SARIF" \
  --sarif-category=codeql-csharp \
  --quiet

python3 - <<'PY'
import json
import sys
from pathlib import Path

sarif_path = Path('.codeql-results/csharp-security-and-quality.sarif')
if not sarif_path.exists():
    print('CodeQL SARIF output missing.', file=sys.stderr)
    raise SystemExit(1)

data = json.loads(sarif_path.read_text(encoding='utf-8'))
rule_index = {}
results = []
for run in data.get('runs', []):
    for tool_rule in run.get('tool', {}).get('driver', {}).get('rules', []):
        rule_index[tool_rule.get('id')] = tool_rule
    results.extend(run.get('results', []))

def first_location(result):
    loc = result.get('locations', [{}])[0].get('physicalLocation', {})
    uri = loc.get('artifactLocation', {}).get('uri', '?')
    line = loc.get('region', {}).get('startLine', '?')
    return f'{uri}:{line}'

def is_blocking(result):
    # Fail prebuild only on high-confidence security/error findings. Keep style/maintainability
    # recommendations visible but non-blocking for this small plugin.
    level = result.get('level', '').lower()
    rule = rule_index.get(result.get('ruleId'), {})
    props = dict(rule.get('properties', {}))
    props.update(result.get('properties', {}))
    try:
        security_severity = float(props.get('security-severity', 0) or 0)
    except (TypeError, ValueError):
        security_severity = 0
    problem_severity = str(props.get('problem.severity', '')).lower()
    return level == 'error' or problem_severity == 'error' or security_severity >= 7.0

blocking = [r for r in results if is_blocking(r)]
print(f'CodeQL results: {len(results)} total; {len(blocking)} blocking')
for result in results[:25]:
    marker = 'BLOCK' if result in blocking else 'INFO'
    message = result.get('message', {}).get('text', '').replace('\n', ' ')
    print(f'- {marker}: {result.get("ruleId")}: {first_location(result)}: {message[:220]}')

if len(results) > 25:
    print(f'... {len(results) - 25} additional result(s) omitted; see {sarif_path}')

raise SystemExit(1 if blocking else 0)
PY
