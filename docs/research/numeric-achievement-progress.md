# Numeric Achievement Progress Research

Date: 2026-06-08

## Goal

Find safe, documented or local-data-backed ways to show progress for tracked achievements beyond simple complete/incomplete state.

## Sources checked

- Cached `IDataManager` docs: `docs/docs-cache/dalamud/api-IDataManager.md`
- Cached `IUnlockState` docs: `docs/docs-cache/dalamud/api-IUnlockState.md`
- Local Lumina assemblies under `/home/developer/.xlcore/dalamud/Hooks/dev/`
- Local game data under `/mintData/games/.xlcore/ffxiv/game/sqpack`

## Findings so far

### Completion state

Dalamud exposes safe completion state through `IUnlockState`:

- `IsAchievementListLoaded`
- `IsAchievementComplete(Achievement row)`

This remains the only safe current-state API identified so far.

### Required target count

Local Lumina `Achievement` rows expose a `Data` collection. For many count-based achievements, `Data[0].RowId` appears to be the required target count.

Examples from local data:

- `Going with the Grain: Amateur` — `Data[0] = 50`
- `Going with the Grain: Initiate` — `Data[0] = 300`
- `Going with the Grain: Apprentice` — `Data[0] = 750`
- `Going with the Grain: Journeyman` — `Data[0] = 1500`
- `Going with the Grain: Artisan` — `Data[0] = 3000`

The plugin now uses this safe local-data denominator as `TargetKnown` when current progress is not available.

### Current progress count

No safe documented current-progress API has been identified yet.

Current plugin behavior:

- User clicks `Refresh Progress` in the live panel to request progress for tracked achievements.
- Requests are manually triggered only and throttled per achievement to avoid automatic polling.
- The client exposes one current progress response slot (`ProgressAchievementId`, `ProgressCurrent`, `ProgressMax`), not one slot per tracked achievement.
- The plugin queues tracked achievements, requests one at a time, and caches returned values per achievement so rows do not overwrite each other.
- If the client has returned current progress: `current / max`
- If achievement list is not loaded and target count is known: `Current unavailable / N`
- If achievement list is not loaded and target count is unknown: `Open Achievements to load status`
- If complete and target count is known: `N / N`
- If complete and target count is unknown: `Complete`
- If incomplete and target count is known but no requested result has returned yet: `Current unavailable / N`
- If incomplete and target count is unknown: `Incomplete`

## Next research steps

Before implementing live current-progress numbers, investigate:

1. Whether a documented Dalamud service exposes achievement progress counters.
2. Whether the in-game Achievements UI exposes current progress through safe addon/UI state after the user opens it.
3. Whether Client Structs expose achievement progress in a stable and policy-safe way.

Do not implement raw memory, hooks, signature scanning, or unsafe Client Structs progress reading without a separate design review and policy audit.
