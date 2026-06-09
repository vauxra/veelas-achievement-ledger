# Beta Cleanup Checklist

Branch: `beta-cleanup`
Date: 2026-06-09

## Baseline

`./scripts/verify-local.sh HEAD` passes on `beta-cleanup`.

Current beta branch starts from `master` at:

- `3a88ca6 debug: trace gameplay activity surfaces`
- `b1fbb07 debug: trace achievement UI agent activity`
- `8fb069a feat: add passive achievement progress diagnostics`

The packet-capture research commit remains isolated on `pcap-experiments`.

## Release posture

Core functionality is stable enough for beta cleanup:

- `/val` opens the tracker.
- Up to 5 achievements can be tracked.
- Completion status is local via `IUnlockState`.
- Target counts come from local Lumina data when available.
- Numeric progress refresh remains manual/user-triggered, queued, de-duplicated, jittered, and throttled.
- Passive `ReceiveAchievementProgress` observation can cache progress when the native Achievement UI requests it.
- No automatic gameplay-triggered refreshes.
- No stale markers; users rely on the Refresh button.

## P0 — beta risk reduction

### 1. Hide or trim exploratory diagnostics

Files:

- `AchievementTracker/Services/ActivityDebugSurfaces.cs`
- `AchievementTracker/Services/AchievementProgressDebugHooks.cs`
- `AchievementTracker/Plugin.cs`
- `AchievementTracker/Windows/ConfigWindow.cs`

Problem:

The current `Enable debug logging` checkbox enables broad exploratory behavior:

- ClientStructs hooks.
- native Achievement addon/agent lifecycle tracing.
- chat/log message tracing.
- condition/client-state tracing.

Even with default-off opt-in, this is heavier than expected for a beta user-facing toggle.

Options:

1. Remove broad exploratory diagnostics from beta and keep only narrow progress/button logs.
2. Keep diagnostics but rename the toggle to `Advanced diagnostics` and add explicit warning text.
3. Hide advanced diagnostics behind a separate developer-only flag or compile-time symbol.

Recommended first cleanup:

- Reword the config UI to make diagnostics clearly advanced/local/logging-heavy.
- Consider splitting normal `DebugTrace` progress logs from deeper `AdvancedDiagnostics` hooks in a later cleanup commit.

### 2. Reassess injected service footprint

File:

- `AchievementTracker/Plugin.cs`

Current debug-only services:

- `IGameInteropProvider`
- `IAddonLifecycle`
- `IFramework`
- `IChatGui`
- `ICondition`

These are only required for diagnostics. If diagnostics stay, document them clearly. If diagnostics are trimmed, remove unneeded service injection.

## P1 — user-facing beta polish

### 3. README beta wording

File:

- `README.md`

Current README says the V1 implementation scaffold is in progress. For beta, update wording to:

- distributed beta,
- manual refresh only,
- no automation/polling,
- progress can be unavailable until achievements are loaded or refreshed,
- how to open and test the plugin.

### 4. Manifest metadata

Files:

- `AchievementTracker/AchievementTracker.csproj`
- `AchievementTracker/AchievementTracker.json`

Current version:

- `0.0.0.1`

Cleanup:

- bump to a beta version such as `0.1.0.0`, if desired.
- update description/punchline to mention selected achievements and manual progress refresh.

### 5. Release/test instructions

File:

- `README.md` or new `docs/release/beta-test.md`

Add tester instructions:

1. Load Debug or Release DLL via `/xldev`.
2. Run `/val`.
3. Add up to five achievements.
4. Open the native Achievement window if completion status is not loaded.
5. Use `Refresh Progress` for numeric progress.
6. Report bugs with achievement IDs, expected values, and whether Refresh was pressed.

## P2 — UI polish without policy changes

### 6. Refresh button affordance

File:

- `AchievementTracker/Windows/TrackerWindow.cs`

Ideas:

- Rename `Refresh Progress` to `Refresh tracked progress`.
- Disable/no-op visibly when no achievements are tracked.
- Add compact feedback for queue/in-flight completion, without adding stale markers or automatic refresh.

### 7. Search result clarity

File:

- `AchievementTracker/Windows/ConfigWindow.cs`

Add category text to search results so similar achievements are easier to distinguish.

### 8. Config copy polish

File:

- `AchievementTracker/Windows/ConfigWindow.cs`

Current debug text says only button/queue/request/progress values are logged, but diagnostics also log chat/log/condition/client-state when enabled. Fix copy if diagnostics remain.

## P3 — test/release automation

### 9. Add Release build to verification script

File:

- `scripts/verify-local.sh`

Current script runs Debug build but not Release build. Add Release build to prevent beta-only packaging/build issues.

### 10. More unit tests for beta invariants

File:

- `AchievementTracker.Tests/Program.cs`

Candidates:

- progress display/fallback precedence if easy to isolate,
- configuration sanitize/migration behavior,
- queue/throttler invariants for manual refresh.

## Suggested first cleanup commit

Small, low-risk first commit:

1. Update README beta wording and tester instructions.
2. Update manifest metadata/version.
3. Rename/reword `Enable debug logging` to `Advanced diagnostics` with warning text.
4. Add Release build to `scripts/verify-local.sh`.
5. Verify with `./scripts/verify-local.sh HEAD`.

Then evaluate whether to remove or split the deeper diagnostic hooks before beta distribution.
