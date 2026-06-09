# Veela's Achievement Ledger

A lightweight assistive achievement organizer and tracker for FFXIV.

## Status

Beta build. Current features:

- `/val` opens the tracker.
- Track up to 5 achievements.
- Search by name or category.
- Show completion status and known target counts.
- Open tracked achievements in the game's Achievement window.
- Update progress from the game Achievement window.

The plugin has no backend, telemetry, cloud sync, or gameplay automation.

## Basic use

1. Run `/val`.
2. Click **Configure** and add achievements.
3. Click **Open** beside an achievement, or **Open next**.
4. Wait for the entry to load.

If progress looks stale, open the entry again.

## Issue reports

Include:

- achievement name or ID,
- expected progress,
- shown progress,
- whether **Open** was used,
- `DebugTrace` logs if diagnostics were enabled.

## Development notes

This project used AI-assisted development, with human review and testing. See:

- [`AI-DECLARATION.md`](AI-DECLARATION.md)
- [`docs/development/due-diligence.md`](docs/development/due-diligence.md)

## Build

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
./scripts/verify-local.sh HEAD
```

Outputs:

- `AchievementTracker/bin/Debug/AchievementTracker.dll`
- `AchievementTracker/bin/Release/AchievementTracker.dll`

## References

- Dalamud docs: <https://dalamud.dev>
- Dalamud API: <https://dalamud.dev/api>
- AI policy: <https://dalamud.dev/plugin-publishing/ai-policy>
