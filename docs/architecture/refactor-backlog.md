# Refactor Backlog

This backlog comes from the code-structure review. It is intentionally conservative: the current repo is mostly well separated, and native Achievement behavior is sensitive. Prefer docs and small pure extractions over broad reshuffles.

## Completed structure improvements

### Extract update eligibility policy

Implemented owner: `AchievementTracker/Services/UpdateEligibilityPolicy.cs`.

Why:

- It filters zero/duplicate IDs.
- It checks native-open eligibility through `AchievementCatalog`.
- It checks completion through `AchievementProgressService`.
- It removes completed/native-unsafe IDs from configured auto-update IDs.
- It emits several debug counters and can trigger config save/reset behavior.

Implemented shape:

- `UpdateEligibilityPolicy.Evaluate(...)` returns:
  - eligible IDs,
  - completed IDs removed from auto update,
  - native-unsafe IDs removed from auto update,
  - skip counts/reasons.
- `Plugin` remains responsible for applying config mutation, saving config, resetting countdown, and logging.

Tests cover:

- zero and duplicate IDs are ignored,
- native-unsafe rows are skipped and marked for auto-update removal,
- completed rows are skipped and marked for auto-update removal,
- eligible rows preserve order after distinct filtering,
- no config save/reset is requested when nothing is removed.

### Extract pure search/category index builder

Implemented owner: `AchievementTracker/Services/AchievementSearchIndex.cs`.

Why:

- It keeps category/subcategory grouping, query/category/completion filtering, display counts, and search sort order out of ImGui layout code.
- `TrackerWindow` remains responsible for controls, selections, drawing, and cache timing.
- `AchievementCatalog` remains responsible for Lumina/manual-viewability source data.

Implemented shape:

- `AchievementSearchIndex.GetSearchableAchievements(...)` filters out top-level Legacy rows.
- `AchievementSearchIndex.BuildResults(...)` returns searchable/category/query/completion counts plus sorted result rows.
- `AchievementSearchIndex.BuildCategoryGroups(...)` returns category/subcategory entries with current completion-count visibility.
- `AchievementSearchSortKey` carries game-order sort fields supplied by the UI/catalog boundary.

Tests cover:

- category/query/completion filter interactions,
- category/subcategory display counts,
- completion-filter count fallback while completion state is unloaded,
- game-order sort stability.

## Medium priority

No medium-priority refactors are currently queued. Keep future medium-priority items limited to pure, test-backed service extractions.

## Low priority

### Keep preset UI duplicated for now

Current owners: `TrackerWindow` Lists column and `ConfigWindow` preset/tracked management page.

Why:

- The mechanics are already centralized in `TrackedAchievementStore`, `TrackedAchievementPresetStore`, and `AutoUpdateSelection`.
- The remaining duplication is presentation-specific ImGui layout.

Only extract if a future UI feature needs identical controls in both windows.

### Avoid splitting `AchievementProgressUpdater` until a pure seam appears

Current owner: `AchievementProgressUpdater`.

Why:

- It is large, but it owns one coherent state machine: serialized native Achievement refresh/inspection actions.
- Supporting pure decisions are already partially extracted into scheduler/window policies.

Acceptable future extractions:

- pure status text formatting,
- pure lifecycle decisions with existing/new tests,
- additional scheduler policies if queue behavior expands.

Avoid:

- moving native open/park/restore sequencing across multiple services,
- introducing another queue/throttler,
- changing timing without tests and in-game validation.

## Non-goals

- No broad namespace/layout rewrite.
- No dependency injection framework unless the project grows substantially.
- No committed external source snapshots or raw analyzer output.
- No public-safe branch changes that imply direct progress requests, polling loops, network telemetry, or official Dalamud-submission safety for experimental features.
