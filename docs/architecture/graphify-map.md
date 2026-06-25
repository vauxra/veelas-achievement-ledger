# Graphify Map

`graphify-out/` is a generated AI-orientation graph. It helps future agents locate hubs and relationships quickly, but it is not the source of truth for design decisions. Use this page to translate Graphify's generated community numbers into Achieve Ex+ concepts.

Community numbers are generated. Re-check `graphify-out/GRAPH_REPORT.md` and update this page after running `bash scripts/regenerate-graphify.sh` when architecture or major service topology changes.

## Current graph snapshot

- Graph built from branch slice ending after the `AchievementSearchIndex` extraction and local regenerated output.
- Current report: `graphify-out/GRAPH_REPORT.md`.
- Current interactive graph: `graphify-out/graph.html`.
- Current call-flow view: `graphify-out/achieve-ex-callflow.html`.
- Current shape: 500 nodes, 999 edges, 28 communities.

## Community legend

| Community | Human meaning | Representative nodes | Use it for |
|---|---|---|---|
| 0 | Tracker UI shell and row/status display wiring | `TrackerWindow`, `SearchResultsCache`, `TrackedUpdateIndicatorPolicy` | Finding ImGui layout, row drawing, toolbar/status wiring, and search cache timing. |
| 1 | Achievement progress display plus Cosmic rules | `Achievement`, `AchievementProgress`, `CosmicAchievementRule`, `CosmicClassProgressProvider`, `IUnlockState` | Understanding progress formatting, completion checks, and Cosmic score overrides. |
| 2 | Catalog/search index result shaping | `AchievementInfo`, `AchievementSearchIndex`, `AchievementSearchCategoryGroup`, `AchievementSearchQueryState`, `AchievementSearchSortKey`, `IDataManager` | Finding Lumina/manual-viewable rows, category/query/completion result shaping, and game-order search sort keys. |
| 3 | Configuration, tracked list, and preset persistence | `Configuration`, `TrackedAchievementStore`, `TrackedAchievementPresetStore`, `AutoUpdateSelection` | Finding persisted tracked IDs, presets/lists, config normalization, and auto-update selections. |
| 4 | Plugin composition and Dalamud lifecycle | `Plugin`, `IDalamudPlugin`, `IFramework` | Finding service construction, commands, event subscription, tick fan-out, config saves, and top-level orchestration. |
| 5 | Configuration window UI | `ConfigWindow`, `ConfigSection`, `Window` | Finding settings UI, search/add controls in config, and presentation-only settings paths. |
| 6 | Native progress updater state machine | `AchievementProgressUpdater`, `ActiveNativeAchievementRequest`, `NativeUpdateJobState`, `ScheduledAchievementProgressRequest` | Understanding serialized refresh/inspection work and the fragile native Achievement update loop. |
| 7 | Scheduler, dedupe, backoff, and queue status | `AchievementProgressRequestScheduler`, `ActivityUpdateKey`, `ActivityTriggerDelayPolicy`, `AutoUpdateQueueStatusRow` | Understanding queue spacing, dirty activity keys, manual-vs-activity request behavior, and status text. |
| 8 | Native Achievement window navigation and parking | `NativeAchievementNavigator`, `IGameGui`, `ParkedAchievementWindowState`, `NativeAchievementActionKind` | Finding native UI open/park/restore/close behavior. Keep ClientStructs/native ordering changes conservative. |
| 9 | Hooks, chat, and passive observers | `PassiveAchievementProgressObserver`, `AchievementActivityUpdateObserver`, `Hook`, `IChatGui`, `ILogMessage` | Finding event/hook subscriptions and disposal-sensitive observer paths. |
| 10 | Pure eligibility/activity policies | `UpdateEligibilityPolicy`, `SearchCompletionFilterPolicy`, `AchievementActivityUpdateClassifier`, `NativeAchievementOpenEligibility` | Finding testable decision logic for update eligibility, completion filters, and activity-trigger selection. |
| 11 | Observed ordinary progress cache/source | `ClientAchievementProgressSource`, `ObservedAchievementProgress`, `ProgressSlotFingerprint`, `IAchievementProgressSource` | Understanding native-observed progress caching, slot fingerprinting, and login/logout reset behavior. |
| 12 | Local policy/review scripts | `adversarial-code-review.py`, `audit-ai-policy.py`, helper functions | Finding tripwire logic and generated-output exclusions. |
| 13 | Project/package manifest concepts | package lock and dependency metadata nodes | Usually low-signal for feature work; useful when dependency/project-file changes are involved. |
| 14 | Test project and SDK metadata | `AchievementTracker.Tests`, `Dalamud.NET.Sdk/15.0.0`, `Microsoft.NET.Sdk` | Understanding test project/build framework context. |
| 15 | Category path semantics | `AchievementCategoryPath`, `Parse()`, `MatchesCategory()` | Finding the shared owner for top-level/final-subcategory category matching. |
| 16-20 | Utility scripts | manifest patcher, audit helpers, CodeQL/verify shell scripts | Finding local build, package, verification, and scanner plumbing. |

## Current god nodes

Graphify reports these as the most connected nodes:

1. `TrackerWindow`
2. `Plugin`
3. `AchievementProgressUpdater`
4. `ConfigWindow`
5. `AchievementProgressRequestScheduler`
6. `CosmicClassProgressProvider`
7. `ClientAchievementProgressSource`
8. `NativeAchievementNavigator`
9. `AchievementCatalog`
10. `TrackedAchievementStore`

Treat god nodes as orientation starting points, not automatic refactor targets. `TrackerWindow` and `ConfigWindow` are expected UI hubs; `Plugin` is expected composition/lifecycle glue; `AchievementProgressUpdater` is intentionally a single serialized native state machine until a pure tested seam appears.

## Query examples

```bash
uvx --from graphifyy graphify query "what owns update eligibility" --graph graphify-out/graph.json
uvx --from graphifyy graphify query "native achievement window parking" --graph graphify-out/graph.json
uvx --from graphifyy graphify path "Plugin" "AchievementProgressUpdater" --graph graphify-out/graph.json
uvx --from graphifyy graphify explain "AchievementCategoryPath" --graph graphify-out/graph.json
```

For exact C# references/types, switch from Graphify to Roslyn/SharpToolsMCP or direct source inspection.
