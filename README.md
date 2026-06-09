# Achievement Tracker Mod

A Final Fantasy XIV achievement tracker plugin for Dalamud/XIVLauncher.

## Release status

Achievement Tracker is intended for distributed beta releases. The current build:

- Opens a small movable/resizable tracker with `/achtrack`.
- Tracks up to 5 selected achievements.
- Saves selected achievement IDs/order through Dalamud config.
- Searches Lumina achievement data via Dalamud `IDataManager`.
- Shows completion status through `IUnlockState` once the achievement list is loaded.
- Shows known target counts for many count-based achievements from Lumina data.
- Provides a manual **Refresh tracked progress** button for numeric current/max progress.

Numeric progress is intentionally manual-refresh only. The plugin does not poll, automate gameplay, or make automatic/unprompted achievement-progress requests.

## Testing and issue reports

1. Install or load the plugin, then run `/achtrack` in game.
2. Open **Configure** and add up to 5 achievements.
3. If completion status is unavailable, open the native game Achievement window once so Dalamud's achievement list is loaded.
4. Press **Refresh tracked progress** when you want current numeric progress for tracked achievements.

When reporting issues, include:

- achievement ID/name,
- expected progress,
- displayed progress,
- whether **Refresh tracked progress** was pressed,
- any `DebugTrace` logs if Advanced diagnostics were enabled.

## Privacy and policy posture

- No backend, telemetry, cloud sync, leaderboard, or user directory.
- No automatic progress polling or gameplay automation.
- No AI-generated user-facing assets are included.
- Advanced diagnostics are opt-in and write Dalamud log lines; they may include gameplay/chat/log context useful for debugging.

## Development methodology

This project used AI-assisted development with human direction, testing, and review. See:

- [`AI-DECLARATION.md`](AI-DECLARATION.md)
- [`docs/development/due-diligence.md`](docs/development/due-diligence.md)

Those docs summarize the development guardrails, Dalamud policy checks, testing, CodeQL scans, and review process used for release preparation.

## Development build

This development environment uses a user-local .NET SDK:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

Full verification:

```bash
./scripts/verify-local.sh HEAD
```

Build outputs:

- Debug plugin DLL: `AchievementTracker/bin/Debug/AchievementTracker.dll`
- Release plugin DLL: `AchievementTracker/bin/Release/AchievementTracker.dll`

## Docs and policy notes

- Docs cache: [`docs/docs-cache/`](docs/docs-cache/)
- Research notes: [`docs/research/`](docs/research/)
- AI/policy audit materials: [`docs/ai-policy-audits/`](docs/ai-policy-audits/)
- Official Dalamud docs: <https://dalamud.dev>
- API docs: <https://dalamud.dev/api>
- AI policy: <https://dalamud.dev/plugin-publishing/ai-policy>
