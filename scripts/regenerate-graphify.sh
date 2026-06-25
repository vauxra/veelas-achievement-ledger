#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
cd "$repo_root"

if ! command -v uvx >/dev/null 2>&1; then
  echo "uvx is required to run Graphify. Install uv/uvx first." >&2
  exit 1
fi

echo "== Regenerate Graphify orientation graph =="
echo "Repository: $repo_root"
echo

echo "== Remove old graphify-out =="
rm -rf graphify-out

echo "== Extract code/project graph =="
uvx --from graphifyy graphify extract . --no-cluster --out .

echo "== Cluster graph without LLM labels =="
uvx --from graphifyy graphify cluster-only . --graph graphify-out/graph.json --no-label

echo "== Export call-flow HTML =="
uvx --from graphifyy graphify export callflow-html

echo "== Graphify hook status (hooks should stay uninstalled) =="
uvx --from graphifyy graphify hook status

echo "== Generated files =="
find graphify-out -maxdepth 2 -type f -printf '%p %s bytes\n' | sort

echo "== Git status for graphify-out =="
git status --short graphify-out .graphifyignore
