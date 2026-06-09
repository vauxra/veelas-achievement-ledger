#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"

dotnet clean "$ROOT/AchievementTracker.sln" -c Debug
dotnet build "$ROOT/AchievementTracker.sln" -c Debug --no-incremental
