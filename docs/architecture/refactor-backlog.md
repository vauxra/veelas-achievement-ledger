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

## Medium priority

No medium-priority refactors are currently queued. Keep future medium-priority items limited to pure, test-backed service extractions.

## Low priority

### Extract pure search/category index builder

Current owner: `TrackerWindow` search/category methods and cache state.

Why:

- Category/subcategory grouping, completion counts, and sort keys are mostly pure data transformations.
- If config search or another UI screen needs the same grouping, duplication risk increases.

Potential shape:

- `AchievementSearchIndex` or `AchievementSearchViewModelBuilder` consumes catalog rows plus completion/progress callbacks and returns grouped/searchable result models.

Tests to add first:

- category/subcategory counts,
- completion-filter fallback when completion state is unloaded,
- game-order sort stability,
- query/category/completion filter interactions.

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
