# Achieve Ex+ 
[![AI-DECLARATION: copilot](https://img.shields.io/badge/䷼%20AI--DECLARATION-copilot-fee2e2?labelColor=fee2e2)](https://ai-declaration.md)

A lightweight assistive achievement organizer and tracker for FFXIV.

## Status

Beta build. Current features:

- `/achex` opens the ledger.
- `/achex config`, `/achex configure`, and `/achex man` open configuration.
- `/achex help` and `/achex ?` open Help.
- Track up to 20 achievements.
- Search by name or category.
- Hide completed achievements from search by default.
- Save, select/load, read, rename, and delete reusable tracked-achievement presets.
- Reorder tracked achievements with Top, Up, Down, and Bottom controls.
- Show completion status, known target counts, observed numeric progress, and when progress was last observed.
- Use the reload icon or **Update Next** to open tracked achievements in the game Achievement window.
- Shared 5-second safety lockout for update-intent opens from **Update Next** and row reload buttons.
- Close the native Achievements window from the ledger with **Close Achievements**.
- Open rows/search results in the native Achievement window with the magnifying-glass button.
- Show cached Cosmic Class score progress after the score data has been observed in Cosmic content.
- Custom plugin icon is included in the manifest and custom repository metadata.

The plugin has no backend, telemetry, cloud sync, packet capture, gameplay automation, plugin-originated progress request queue, scheduled refresh loop, or game-event-driven refresh automation.

## Basic use

1. Run `/achex`.
2. Click **Configure** and add achievements.
3. Optional: save your current tracked list as a preset, or load a saved preset.
4. Click the reload icon beside an achievement, or **Update Next**.
5. Wait for the native Achievement entry to load; progress updates when the game returns data.
6. Use **Close Achievements** when you want to close the native Achievement window from the ledger.
7. Use the magnifying-glass button when you want to inspect the native Achievement entry without treating it as an update action.

Tracked achievements, presets, and cached Cosmic score data are saved between logouts.

## Cosmic Class progress

Some Cosmic Class achievements only expose normal achievement completion as complete/incomplete. When WKS/Cosmic score data is available locally, Achieve Ex+ reads the local class score cache and maps those scores to the related Cosmic Class achievements for planning.

This is read-only local ClientStructs state. It does not request achievement progress from the server.

## Development notes

This project used AI-assisted development, with human review and testing. See:

- [`AI-DECLARATION.md`](AI-DECLARATION.md)
- [`docs/development/due-diligence.md`](docs/development/due-diligence.md)
- [`docs/cosmic-class-achievement-progress-research.md`](docs/cosmic-class-achievement-progress-research.md)

## Build

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
./scripts/verify-local.sh HEAD
```

Outputs:

- `AchievementTracker/bin/Debug/AchieveExPlus.dll`
- `AchievementTracker/bin/Release/AchieveExPlus.dll`

## References

- Dalamud docs: <https://dalamud.dev>
- Dalamud API: <https://dalamud.dev/api>
- AI policy: <https://dalamud.dev/plugin-publishing/ai-policy>
