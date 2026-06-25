# Service Boundaries

Use this file to decide where new code belongs. The default rule is: UI owns presentation, `Plugin` owns Dalamud lifecycle/orchestration, services own reusable mechanics, and models/config own value state.

## Layer ownership

| Layer | Owner files | Should own | Should not own |
|---|---|---|---|
| Dalamud/bootstrap orchestration | `AchievementTracker/Plugin.cs` | service construction, command routing, event subscribe/unsubscribe, login/logout reset, framework tick fan-out, config save calls | duplicate queue/search/preset/native-window mechanics that can be pure services |
| UI orchestration | `AchievementTracker/Windows/TrackerWindow.cs`, `AchievementTracker/Windows/ConfigWindow.cs` | ImGui layout, button/checkbox intent, immediate UI state, tooltips, calling plugin/service entrypoints | native Achievement sequencing, queue spacing, ID-only trigger classification, preset sanitation rules |
| Domain/service mechanics | `AchievementTracker/Services/*.cs` | reusable operations with explicit inputs/outputs: catalog/search, progress display, queue scheduling, native window policy, activity classification, preset sanitation, Cosmic score reads | product navigation decisions, long-lived Dalamud event ownership, arbitrary config mutation without caller intent |
| Serializable/value state | `AchievementTracker/Models/*.cs`, `AchievementTracker/Configuration.cs` | persisted payloads, value objects, display-state records, config normalization | Framework events, UI drawing, native pointers |
| Verification/policy | `scripts/*`, `docs/ai-policy-audits/*`, `AGENTS.md` | local verification, branch policy, tripwire behavior | runtime feature code |

## Existing service owners

| Service/file | Owns |
|---|---|
| `AchievementCatalog` | Lumina achievement lookup, manually viewable achievement filtering, native-open eligibility. |
| `AchievementProgressService` | Converts completion state, observed numeric progress, and Cosmic overrides into display progress. |
| `IAchievementProgressSource` / `ClientAchievementProgressSource` | Observed ordinary achievement progress cache and passive native progress-slot reads. |
| `CosmicClassProgressProvider` | Experimental WKS/Cosmic score read/cache/override behavior. Keep this isolated from ordinary progress refresh queues. |
| `AchievementProgressRequestScheduler` | Pure scheduling, spacing, dedupe, dirty activity-key final pass, and pending request ordering. |
| `AchievementProgressUpdater` | Serialized native Achievement refresh/inspection state machine, queue run lifecycle, circuit breaker, and update status text. |
| `NativeAchievementNavigator` | Narrow unsafe/native adapter for `AgentAchievement`, `IGameGui`, and Achievement addon position/scale/show/hide. |
| `NativeAchievementWindowScalePolicy` / `NativeAchievementUpdateWindowPolicy` | Pure native Achievement window park/restore/close decisions. |
| `PassiveAchievementProgressObserver` | Experimental passive ClientStructs hooks that feed `ClientAchievementProgressSource`. |
| `AchievementActivityUpdateClassifier` | ID-only chat/log/class-job classification for gather/craft activity triggers. |
| `AchievementActivityUpdateObserver` | Dalamud chat event subscription and conversion into queue requests. |
| `ActivityTriggerCandidateSelection` | Filtering ordinary trigger candidates, especially excluding Cosmic/WKS achievements. |
| `ActivityTriggerDelayPolicy` | Initial delay policy for activity-triggered refreshes. |
| `AutoUpdateSelection` | Configured auto-update ID inclusion logic. |
| `TrackedAchievementStore` | In-memory tracked ID ordering and max-count rules. |
| `TrackedAchievementPresetStore` | Preset/list sanitize/save/rename/delete/copy rules. |
| `SearchCompletionFilterPolicy` | Completion-filter loaded-state and match decisions. |
| `TrackedToolbarIconPresentation`, `TrackedUpdateIndicatorPolicy`, `AutoUpdateQueueStatusRow` | Small display/status policies that keep UI conditionals testable. |

## Duplication rules

Before adding a new service or helper, search for an existing owner:

- Queueing/spacing/dedupe belongs in `AchievementProgressRequestScheduler` or `AchievementProgressUpdater`.
- Native Achievement open/show/hide/park/restore belongs in `NativeAchievementNavigator` plus `NativeAchievementWindowScalePolicy`.
- Progress display belongs in `AchievementProgress`, `AchievementProgressService`, and related display policies.
- Search/category/completion filtering should use `AchievementCatalog` and `SearchCompletionFilterPolicy` first.
- Preset/list sanitation belongs in `TrackedAchievementPresetStore`; tracked ordering belongs in `TrackedAchievementStore`.
- Activity-trigger ID classification belongs in `AchievementActivityUpdateClassifier`, not UI or `Plugin`.
- Cosmic/WKS score behavior belongs in `CosmicClassProgressProvider`, not the ordinary progress queue.

## Current intentional duplication

Some UI controls appear in both the main window and config window. This is intentional presentation duplication because each screen has different layout needs. Shared behavior should remain in the services above. Do not extract generic ImGui components unless one future feature truly needs identical behavior in both windows.

## Refactor threshold

Refactor only when one of these is true:

1. A behavior is implemented twice outside UI layout.
2. A new feature would add a second owner for queueing, native window policy, search filtering, preset rules, or progress formatting.
3. A pure decision can be extracted with tests before touching native/Dalamud sequencing.
4. A file grows because unrelated responsibilities are being mixed.

Do not split `AchievementProgressUpdater` or native window sequencing casually. Its complexity is mostly a single native-action state machine and should stay together unless a test-backed extraction removes pure decisions without changing ordering.
