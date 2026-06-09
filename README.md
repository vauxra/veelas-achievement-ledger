# Veela's Achievement Ledger

A lightweight assistive achievement organizer and tracker for FFXIV.

## Status

Beta build. Current features:

- `/val` opens the ledger.
- Track up to 5 achievements.
- Search by name or category.
- Hide completed achievements from search by default.
- Show completion status and known target counts.
- Use the reload icon or **Update Next** to open tracked achievements in the game Achievement window.

The plugin has no backend, telemetry, cloud sync, or gameplay automation.

## Basic use

1. Run `/val`.
2. Click **Configure** and add achievements.
3. Click the reload icon beside an achievement, or **Update Next**.
4. Wait for the game entry to load; progress updates when the game returns data.

Tracked achievements are saved between logouts.

## Issue reports

Include:

- achievement name or ID,
- expected progress,
- shown progress,
- whether the reload icon or **Update Next** was used.

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

- `AchievementTracker/bin/Debug/VeelasAchievementLedger.dll`
- `AchievementTracker/bin/Release/VeelasAchievementLedger.dll`

## References

- Dalamud docs: <https://dalamud.dev>
- Dalamud API: <https://dalamud.dev/api>
- AI policy: <https://dalamud.dev/plugin-publishing/ai-policy>
