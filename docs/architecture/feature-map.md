# Feature Map

Use this map to locate the current owner of a feature before adding code.

| Feature | User-visible behavior | Primary orchestration | Service/model owners | Tests to extend first |
|---|---|---|---|---|
| Slash command/windows | `/achex` opens/toggles the ledger/config help paths. | `Plugin.OnCommand`, `TrackerWindow`, `ConfigWindow` | n/a | Build/UI smoke only unless command parsing grows. |
| Tracked achievement list | Add/remove/reorder tracked achievement IDs; persist across logouts. | `TrackerWindow`, `ConfigWindow`, `Plugin.SaveTrackedAchievements` | `TrackedAchievementStore`, `Configuration` | `AchievementTracker.Tests/Program.cs` tracked store tests. |
| Lists/presets | Save, load, rename, delete named achievement lists. | `TrackerWindow`, `ConfigWindow` | `TrackedAchievementPresetStore`, `TrackedAchievementPreset` | Preset store tests. |
| Search and categories | Search manually viewable achievements, group by category/subcategory, filter by completion. | `TrackerWindow`, `ConfigWindow` | `AchievementCatalog`, `SearchCompletionFilterPolicy`, `AchievementProgressService` | Completion filter/search policy tests; add pure search index tests if extracting. |
| Completion/progress display | Show complete/incomplete/numeric/current-unavailable states. | Windows render progress text. | `AchievementProgress`, `AchievementProgressService`, `ClientAchievementProgressSource`, `CosmicClassProgressProvider` | Progress display and Cosmic override tests. |
| Row open/inspection | Open a native Achievement entry for inspection. | `Plugin.OpenNativeAchievementForInspection`, windows | `AchievementProgressUpdater`, `AchievementProgressRequestScheduler`, `NativeAchievementNavigator`, `NativeAchievementWindowScalePolicy` | Scheduler/window policy/inspection tests. |
| Manual update all/one | Queue one or more eligible tracked achievements through native Achievement UI. | `Plugin.EnqueueUpdate*`, windows | `AchievementProgressUpdater`, `AchievementProgressRequestScheduler`, `ClientAchievementProgressSource`, `NativeAchievementNavigator` | Scheduler/updater tests. |
| Timed auto update | Experimental timed refresh cycles for selected tracked IDs. | `Plugin.OnFrameworkUpdate`, `ConfigWindow`, `TrackerWindow` | `AchievementProgressUpdater`, `AutoUpdateSelection`, `AutoUpdateQueueStatusRow` | Auto-selection/scheduler/status tests. |
| Activity-triggered update | Experimental craft/gather activity log events enqueue matching tracked IDs. | `AchievementActivityUpdateObserver`, `Plugin.InstallActivityUpdateObserver` | `AchievementActivityUpdateClassifier`, `ActivityTriggerDelayPolicy`, `ActivityTriggerCandidateSelection`, scheduler dirty-key behavior | Classifier, delay, candidate-selection, dirty-key scheduler tests. |
| Cosmic class progress | Show Cosmic/WKS achievement progress from live/cached class scores. | `Plugin.RefreshCosmicCacheFromLiveState`, windows | `CosmicClassProgressProvider`, `CosmicClassScoreCache`, `AchievementProgressService` | Cosmic rule/cache/progress override tests. |
| Native Achievement window park/restore | Shrink/park/restore/close native Achievement window around queued refreshes. | `AchievementProgressUpdater` | `NativeAchievementNavigator`, `NativeAchievementWindowScalePolicy`, `NativeAchievementUpdateWindowPolicy` | Window policy and updater lifecycle tests. |
| Passive progress observation | Capture native progress/completion events and cache observations. | `Plugin` constructs observer; `Dispose` tears it down. | `PassiveAchievementProgressObserver`, `ClientAchievementProgressSource` | Progress-source/cache tests; hook lifecycle review. |
| UI customization | Configure navigation buttons, tracked row icons, column order/widths. | `ConfigWindow`, `TrackerWindow` | `Configuration`, `MainPanelColumnWidthDefaults`, `TrackedToolbarIconPresentation`, `TrackedUpdateIndicatorPolicy` | UI policy/config normalization tests. |

## Common change paths

### Add a new tracked-row button

1. Add UI drawing/wiring in `TrackerWindow` and/or `ConfigWindow`.
2. Reuse `Plugin` methods if the action affects queue/config/native behavior.
3. Put reusable decision logic in a service and test it.
4. Update this feature map if the new action becomes a durable feature.

### Change refresh behavior

1. Start in `AchievementProgressUpdater` and `AchievementProgressRequestScheduler`.
2. Do not add another queue or timer.
3. Keep native open/show/hide/park/restore in `NativeAchievementNavigator`.
4. Add/extend tests before changing timing, dedupe, dirty-key, or native lifecycle decisions.

### Change search/completion behavior

1. Use `AchievementCatalog` for Lumina/manual-viewability rules.
2. Use `SearchCompletionFilterPolicy` for completion-filter semantics.
3. Use `AchievementProgressService` for progress/completion display state.
4. If `TrackerWindow` category grouping grows, extract a pure search/index builder and test it.

### Change Cosmic/WKS behavior

1. Keep WKS reads in `CosmicClassProgressProvider`.
2. Do not route Cosmic achievements through ordinary activity-trigger refresh candidates.
3. Update cache normalization and tests if score shape changes.

### Add configuration

1. Add the serializable property to `Configuration`.
2. Normalize/migrate it in `NormalizeAutoUpdateSettings()` or a dedicated helper.
3. Save through plugin config save paths.
4. Add tests for pure normalization if possible.
