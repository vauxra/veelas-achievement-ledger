# Graph Report - .  (2026-06-24)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 501 nodes · 1000 edges · 28 communities (24 shown, 4 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `f18ee509`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 15|Community 15]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]

## God Nodes (most connected - your core abstractions)
1. `TrackerWindow` - 59 edges
2. `Plugin` - 45 edges
3. `AchievementProgressUpdater` - 45 edges
4. `ConfigWindow` - 39 edges
5. `AchievementProgressRequestScheduler` - 29 edges
6. `CosmicClassProgressProvider` - 28 edges
7. `ClientAchievementProgressSource` - 23 edges
8. `NativeAchievementNavigator` - 16 edges
9. `AchievementCatalog` - 11 edges
10. `TrackedAchievementStore` - 11 edges

## Surprising Connections (you probably didn't know these)
- `Plugin` --references--> `AchievementActivityUpdateObserver`  [EXTRACTED]
  AchievementTracker/Plugin.cs → AchievementTracker/Services/AchievementActivityUpdateObserver.cs
- `Plugin` --references--> `PassiveAchievementProgressObserver`  [EXTRACTED]
  AchievementTracker/Plugin.cs → AchievementTracker/Services/PassiveAchievementProgressObserver.cs
- `ConfigWindow` --references--> `Plugin`  [EXTRACTED]
  AchievementTracker/Windows/ConfigWindow.cs → AchievementTracker/Plugin.cs
- `TrackerWindow` --references--> `Plugin`  [EXTRACTED]
  AchievementTracker/Windows/TrackerWindow.cs → AchievementTracker/Plugin.cs
- `AchievementProgressUpdater` --references--> `AchievementProgressRequestScheduler`  [EXTRACTED]
  AchievementTracker/Services/AchievementProgressUpdater.cs → AchievementTracker/Services/AchievementProgressRequestScheduler.cs

## Import Cycles
- None detected.

## Communities (28 total, 4 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.08
Nodes (7): DateTime, FontAwesomeIcon, SearchResultsCache, TrackedUpdateIndicatorPolicy, TrackedUpdateIndicatorState, Window, TrackerWindow

### Community 1 - "Community 1"
Cohesion: 0.09
Nodes (16): Achievement, AchievementProgress, CosmicAchievementRule, GeneratedRegex, IUnlockState, Complete(), CompletionListNotLoaded(), DataNotAvailable() (+8 more)

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (10): Configuration, Dictionary, int, IPluginConfiguration, List, TrackedAchievementPreset, ActivityTriggerCandidateSelection, AutoUpdateSelection (+2 more)

### Community 3 - "Community 3"
Cohesion: 0.08
Nodes (3): Plugin, IDalamudPlugin, IFramework

### Community 4 - "Community 4"
Cohesion: 0.12
Nodes (4): Action, ConfigSection, List<string>, ConfigWindow

### Community 5 - "Community 5"
Cohesion: 0.12
Nodes (13): AchievementInfo, AchievementSearchCategoryGroup, AchievementSearchQueryState, AchievementSearchResults, AchievementSearchSortKey, AchievementSearchSortMode, Func, IDataManager (+5 more)

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (7): ActivityUpdateKey, Guid, IEnumerable, AchievementProgressRequestScheduler, ActivityTriggerDelayPolicy, AutoUpdateQueueStatusRow, TimeSpan

### Community 7 - "Community 7"
Cohesion: 0.16
Nodes (5): ActiveNativeAchievementRequest, DateTimeOffset, NativeUpdateJobState, ScheduledAchievementProgressRequest, AchievementProgressUpdater

### Community 8 - "Community 8"
Cohesion: 0.09
Nodes (9): float, IGameGui, NativeAchievementActionKind, NativeAchievementJobKind, ParkedAchievementWindowState, MainPanelColumnWidthDefaults, NativeAchievementNavigator, NativeAchievementWindowScalePolicy (+1 more)

### Community 9 - "Community 9"
Cohesion: 0.11
Nodes (10): HashSet, IReadOnlyCollection, IReadOnlyDictionary, NativeAchievementOpenEligibility, AchievementActivityUpdateClassifier, SearchCompletionFilterPolicy, Ineligible(), UpdateEligibilityPolicy (+2 more)

### Community 10 - "Community 10"
Cohesion: 0.11
Nodes (9): bool, Hook, IChatGui, IDisposable, ILogMessage, AchievementActivityUpdateObserver, PassiveAchievementProgressObserver, ImRaiiShim (+1 more)

### Community 11 - "Community 11"
Cohesion: 0.14
Nodes (4): ObservedAchievementProgress, ProgressSlotFingerprint, ClientAchievementProgressSource, IAchievementProgressSource

### Community 12 - "Community 12"
Cohesion: 0.27
Nodes (16): CompletedProcess, added_lines_by_file(), apply_experimental_mode(), Finding, get_changed_files(), get_diff(), get_untracked_files(), include_untracked_as_added() (+8 more)

### Community 13 - "Community 13"
Cohesion: 0.14
Nodes (13): contentHash, requested, resolved, type, dependencies, net10.0-windows7.0, contentHash, requested (+5 more)

### Community 14 - "Community 14"
Cohesion: 0.33
Nodes (4): AchievementTracker.Tests, net10.0-windows7.0, Dalamud.NET.Sdk/15.0.0, Microsoft.NET.Sdk

### Community 15 - "Community 15"
Cohesion: 0.50
Nodes (3): AchievementCategoryPath, MatchesCategory(), Parse()

### Community 16 - "Community 16"
Cohesion: 0.80
Nodes (4): Path, main(), patch_manifest(), patch_zip()

### Community 17 - "Community 17"
Cohesion: 0.60
Nodes (4): is_scanned_path(), main(), Pattern, run_git_diff()

### Community 18 - "Community 18"
Cohesion: 0.50
Nodes (3): codeql-build.sh script, DOTNET_ROOT, PATH

### Community 19 - "Community 19"
Cohesion: 0.50
Nodes (3): codeql-local.sh script, DOTNET_ROOT, PATH

### Community 20 - "Community 20"
Cohesion: 0.50
Nodes (3): verify-local.sh script, DOTNET_ROOT, PATH

## Knowledge Gaps
- **23 isolated node(s):** `net10.0-windows7.0`, `Microsoft.NET.Sdk`, `Dalamud.NET.Sdk/15.0.0`, `version`, `type` (+18 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **4 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `TrackerWindow` connect `Community 0` to `Community 2`, `Community 3`, `Community 5`, `Community 9`, `Community 10`?**
  _High betweenness centrality (0.203) - this node is a cross-community bridge._
- **Why does `Plugin` connect `Community 3` to `Community 0`, `Community 4`, `Community 6`, `Community 7`, `Community 9`, `Community 10`?**
  _High betweenness centrality (0.158) - this node is a cross-community bridge._
- **Why does `AchievementProgressUpdater` connect `Community 7` to `Community 2`, `Community 4`, `Community 5`, `Community 6`, `Community 8`, `Community 9`, `Community 10`, `Community 11`?**
  _High betweenness centrality (0.148) - this node is a cross-community bridge._
- **What connects `net10.0-windows7.0`, `Microsoft.NET.Sdk`, `Dalamud.NET.Sdk/15.0.0` to the rest of the system?**
  _24 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.08215488215488216 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.08985507246376812 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.08826945412311266 - nodes in this community are weakly interconnected._