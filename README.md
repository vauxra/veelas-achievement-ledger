# Veela's Achievement Ledger Ex
[![AI-DECLARATION: copilot](https://img.shields.io/badge/䷼%20AI--DECLARATION-copilot-fee2e2?labelColor=fee2e2)](https://ai-declaration.md)

> [!CAUTION]
> **FLASH / UI MOTION WARNING — EXPERIMENTAL BUILD.**
>
> This build can briefly open, shrink, move, restore, and close the native FFXIV Achievement window while running update tasks. If flashing, sudden UI motion, or rapid window changes bother you, disable timed/event-triggered updates and use manual native Achievement opens instead.
>
> This is an experimental Ex branch build for private/testing use, not a normal Dalamud repository submission build. Use it only if you understand and accept the risk, including possible account consequences.
>
> Current progress refreshes use native Achievement UI opens plus passive reads of the already-populated local progress slot. The plugin source does **not** call `RequestAchievementProgress`, create remote-service calls, capture packets, synthesize addon submissions, execute actions, automate movement, or perform gameplay botting.
>
> It still uses timers/event triggers, native UI opens, window parking/rescaling, and isolated ClientStructs reads, which remain experimental and may be discouraged for normal submission.

A lightweight experimental achievement organizer and tracker for FFXIV.

## Status

Experimental build on `val-experimental`. Current features:

- `/val` opens the ledger.
- `/val config`, `/val configure`, and `/val man` open configuration.
- `/val help` and `/val ?` open configuration directly to Help.
- Track up to 20 achievements.
- Search by name or category.
- Hide completed achievements from search.
- Reorder tracked achievements with Top, Up, Down, and Bottom controls.
- Save, read, rename, and delete reusable tracked-achievement presets.
- Selecting a preset loads it immediately; the Read button reloads the selected preset on demand.
- Choose which tracked rows are included in timed auto update.
- Queue native Achievement UI assisted updates with row reload, **Update All**, timed auto update, or enabled event triggers.
- Timed auto update and event-triggered updates are mutually exclusive; enabling one disables the other.
- The native Achievement window is temporarily parked at a very small scale during queued updates; restoring original scale/position after updates is configurable.
- Magnifying-glass Open in Achievements buttons restore the parked native Achievement window scale/position before showing the selected entry.
- A reset-scale button opens the native Achievement window and restores its scale if a test leaves it shrunk.
- Show completion status, known target counts, observed progress, and supported Cosmic Class score progress.
- Cache Cosmic Class score values after they are observed in Cosmic content so they remain visible outside the zone.
- Use the magnifying-glass button to open the native Achievement entry.

The plugin has no direct backend/network integration, remote analytics, cloud sync, packet capture, movement automation, action-use automation, or synthetic addon submission flow.

## Basic use

1. Run `/val`.
2. Click **Configure** and add achievements from **Tracked Achievements**.
3. Optional: save your current tracked list as a preset.
4. Use the row reload icon or **Update All** to open native Achievement entries and cache observed progress.
5. Optional: enable either timed auto update or event triggers on the experimental branch; both cannot be enabled at once.
6. Use the magnifying-glass button when you want to inspect the native Achievement entry.
7. If the native Achievement window ever stays shrunk, use **Reset native Achievement window scale** in Config → Auto update.

Tracked achievements, presets, auto-update settings, and cached Cosmic Class scores are saved between logouts.

## Cosmic Class score progress

Cosmic Class achievements are handled as a special local-progress case. The game exposes Cosmic score values through the local WKS/Cosmic state rather than the ordinary achievement progress slot.

When Cosmic state is loaded, VAL reads the local score array, caches the full 11-class set, and displays matching achievement progress as `current / target`. Outside the zone, VAL reuses the last cached scores so the config/search view can still help plan play time.

The current class-index mapping is based on the observed 11-value WKS score array and the local achievement row order. It still needs in-game validation against non-zero scores:

1. Carpenter
2. Blacksmith
3. Armorer
4. Goldsmith
5. Leatherworker
6. Weaver
7. Alchemist
8. Culinarian
9. Miner
10. Botanist
11. Fisher

## Experimental behavior notes

This branch deliberately differs from the safer public/beta design:

- Update tasks can be queued from **Update All**, timed auto update, or event triggers.
- Update tasks open the native Achievement UI for matching tracked achievements, then VAL passively reads the local progress slot populated by that native UI.
- Timed auto update and event-triggered updates cannot both be enabled at the same time.
- The native Achievement window may briefly shrink, move, restore, and close during queued updates; restoring original scale/position after updates is configurable.
- Magnifying-glass Open in Achievements buttons restore the parked native Achievement window scale/position before inspection.
- Auto-update timing uses seconds and waits for the first countdown before the first cycle.
- `Stop Update Tasks` disables auto update and clears pending update tasks.
- Help includes Cosmic diagnostics for test feedback.

Do not present this branch as official-submission-safe without removing or redesigning the experimental timer/event-trigger/window-parking behavior.

## Development notes

This project used AI-assisted development, with human review and testing. See:

- [`AI-DECLARATION.md`](AI-DECLARATION.md)
- [`docs/development/due-diligence.md`](docs/development/due-diligence.md)
- [`docs/cosmic-class-achievement-progress-research.md`](docs/cosmic-class-achievement-progress-research.md)
- [`docs/research/automation-risk-analysis.md`](docs/research/automation-risk-analysis.md)

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
- Dalamud plugin restrictions: <https://dalamud.dev/plugin-publishing/restrictions>
- AI policy: <https://dalamud.dev/plugin-publishing/ai-policy>
