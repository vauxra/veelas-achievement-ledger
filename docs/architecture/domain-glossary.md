# Domain Glossary

This glossary gives stable names to Achieve Ex+ concepts so code, tests, docs, and AI agents use the same words.

## Achievement identity

### Achievement ID

A `uint` Lumina `Achievement.RowId`. It is the stable ID stored in tracked lists, presets, auto-update selection, queue requests, and debug logs.

### Manually viewable achievement

An achievement that appears in the normal in-game Achievement UI. `AchievementCatalog` filters for non-hidden categories, non-hidden names, known player-visible achievement kinds, nonblank name/category/kind text, and nonzero icons.

### Native-safe achievement

A manually viewable achievement that Achieve Ex+ is willing to open through the native Achievement UI. `AchievementCatalog.CanOpenInNativeAchievementUi` is the owner of this decision. Native-unsafe achievements must not be queued for refresh/inspection.

### Tracked achievement

An achievement ID in `TrackedAchievementStore`. Tracked IDs persist through `Configuration.TrackedAchievementIds` and survive login/logout. Tracked ordering is user-controlled and capped by `TrackedAchievementStore.MaxTrackedAchievements`.

### Preset / list

A named, sanitized saved list of tracked achievement IDs in `Configuration.TrackedAchievementPresets`. `TrackedAchievementPresetStore` owns name and ID sanitation plus save/rename/delete/copy rules.

### Category path

The display path from Lumina category metadata, such as `Crafting & Gathering > Miner`. `AchievementCategoryPath` owns parsing this into top-level category and final subcategory, plus matching activity-trigger categories without partial text matches.

## Progress concepts

### Completion state

Boolean complete/incomplete state from Dalamud `IUnlockState`. `AchievementProgressService` treats `IUnlockState` as authoritative when `IUnlockState.IsAchievementListLoaded` is true.

### Observed ordinary progress

Numeric `(current, max)` progress for ordinary achievements observed from the native Achievement progress slot or passive hook. `ClientAchievementProgressSource` caches these observations by achievement ID. This cache is in-memory and resets on login/logout.

### Observed completion

A completion signal recorded by `ClientAchievementProgressSource.RecordObservedCompletion` or by recording progress where `current >= max`. It can make `AchievementProgressService` display completion even before/alongside numeric progress.

### Required target

A static target value inferred from `Achievement.Data[0].RowId` when available. It is a denominator/goal, not the player's current progress.

### Cosmic/WKS progress

Experimental progress for Cosmic class score achievements. `CosmicClassProgressProvider` reads live WKS class scores from `WKSManager` and falls back to `CosmicClassScoreCache` persisted in plugin config. Cosmic progress is a read-only augmenter and should not be routed through ordinary activity-trigger refresh candidates.

### Data not available

A deliberate display state for progress that cannot be known from the currently available live/cache data. Do not replace it with guessed or stale ordinary progress.

## Refresh/update concepts

### Inspection

A user-visible native Achievement open, usually from a magnifying-glass/search action. Inspection should restore a parked native Achievement window and leave the user-facing native UI usable.

### Refresh

A queued native Achievement open intended to observe progress for one achievement. Refreshes are serialized by `AchievementProgressUpdater` and scheduled by `AchievementProgressRequestScheduler`.

### Update eligibility

The semantic filtering step before queueing refreshes. `UpdateEligibilityPolicy` owns this pure decision: remove zero/duplicate IDs, skip native-unsafe achievements, skip completed achievements, preserve eligible order, and report which skipped IDs should be removed from auto-update selection. `Plugin` applies logging/config side effects from the result.

### Auto-update selection

The subset of tracked achievement IDs selected for timed auto-update. `AutoUpdateSelection.SelectIncludedTrackedAchievements` owns inclusion logic. `Configuration.AutoUpdateAchievementIds` stores the configured selection.

### Timed auto update

Experimental recurring refresh cycle driven from `IFramework.Update` through `AchievementProgressUpdater.MaybeEnqueueAutoUpdate`. It should use the same eligibility and native queue as manual refreshes.

### Activity-triggered update

Experimental refresh caused by local craft/gather activity. `AchievementActivityUpdateObserver` receives Dalamud chat/log events, `AchievementActivityUpdateClassifier` maps known log message IDs to trigger/category, and the unified queue handles refresh execution.

### Activity key

A `(triggerName, categoryName)` value used to coalesce duplicate same-category activity bursts while preserving FIFO order across different classes/actions.

## Native Achievement UI concepts

### Native Achievement agent

The game's `AgentAchievement` surface used by `NativeAchievementNavigator` to show/hide/open achievement entries. It is a native/ClientStructs boundary.

### Native Achievement addon

The visible Achievement window addon returned by `IGameGui.GetAddonByName("Achievement", 1)`. It must be checked for null/ready/visible/address before native pointer dereference.

### Parked window

A native Achievement window temporarily scaled and moved to a tiny position for background refreshes that opened the window from closed state. `NativeAchievementNavigator` stores the last restorable user state and applies park/restore/reset operations.

### Native circuit breaker

A session-local safety state in `AchievementProgressUpdater` that stops native Achievement actions after repeated native open/refresh failures. It clears the queue and prevents retry loops.

## Branch and safety language

### Public-safe shape

The restrained shape intended for publishable/beta-safe branches: `/achex` opens the ledger, row reload opens native Achievement entries, progress is cached only when native UI returns it, and tracked IDs persist while observed ordinary progress resets on login/logout.

### Experimental branch surface

Features allowed only when clearly labeled experimental: native refresh queues, timed auto update, activity-triggered update, WKS/Cosmic reads, passive hooks, debug instrumentation, and local analyzer/tooling spikes. These still require lifecycle, privacy, crash, and security review.
