# Data model map

Version: `v0.2.0.20`

## Related function map entries

- [`Plugin()` load wiring](Function-call-map#plugin-constructor)
- [`Configuration.Save()` and tracked/preset stores](Function-call-map#configuration-and-stores)
- [`CosmicClassProgressProvider` cache methods](Function-call-map#cosmicclassprogressprovider)
- [`ClientAchievementProgressSource` observed-progress cache](Function-call-map#clientachievementprogresssource)

## Navigation outline

- [Persistence summary](#persistence-summary)
- [Load timing](#load-timing)
- [Save timing](#save-timing)
- [Configuration.cs](#configurationcs)
- [TrackedAchievementStore.cs](#trackedachievementstorecs)
- [TrackedAchievementPresetStore.cs](#trackedachievementpresetstorecs)
- [CosmicClassScoreCache.cs](#cosmicclassscorecachecs)
- [Observed progress cache](#observed-progress-cache)
- [Dalamud best-practice notes](#dalamud-best-practice-notes)

## Persistence summary

```text
Persistent file-backed state 🟢/🟡
└─ Configuration object
   ├─ TrackedAchievementIds
   ├─ TrackedAchievementPresets
   ├─ CosmicClassScoreCache
   └─ HideCompletedInSearch

In-memory-only state 🟢/🟠
├─ TrackedAchievementStore.achievementIds 🟢
├─ ClientAchievementProgressSource.cachedProgress 🟠
├─ ClientAchievementProgressSource.observationDeadlines 🟠
└─ Plugin lockout/cosmic refresh timestamps 🟢
```

The plugin follows the normal Dalamud pattern: one serializable `IPluginConfiguration` object is loaded with `PluginInterface.GetPluginConfig()` and saved with `PluginInterface.SavePluginConfig(configuration)`.

## Load timing

```text
Dalamud creates Plugin 🟡
└─ Plugin.Plugin() constructor 🟢
   ├─ LoadAndNormalizeConfiguration() 🟢/🟡
   │  ├─ PluginInterface.GetPluginConfig() 🟡
   │  ├─ if null: new Configuration() 🟢
   │  └─ Configuration.Normalize() 🟢
   ├─ CreateTrackedAchievementStore() 🟢
   │  └─ TrackedAchievementStore.LoadFrom(Configuration.TrackedAchievementIds) 🟢
   └─ new CosmicClassProgressProvider(Configuration.CosmicClassScoreCache, SaveConfiguration) 🟠
```

## Save timing

```text
User edits tracked list 🟢
└─ Plugin.SaveTrackedAchievements() 🟢/🟡
   ├─ Configuration.TrackedAchievementIds = TrackedAchievementStore.ToConfigList() 🟢
   └─ Configuration.Save() -> PluginInterface.SavePluginConfig(this) 🟡

User edits presets/search setting 🟢
└─ Plugin.SaveConfiguration() 🟢/🟡
   └─ Configuration.Save() -> PluginInterface.SavePluginConfig(this) 🟡

Cosmic live scores observed 🟠
└─ CosmicClassProgressProvider.SaveScoresToCache(liveScores) 🟠
   ├─ CosmicClassScoreCache.Scores = values 🟢
   ├─ CosmicClassScoreCache.ObservedAtUnixSeconds = now 🟢
   └─ saveCache callback -> Plugin.SaveConfiguration() 🟢/🟡
```

## `Configuration.cs`

Saved plugin settings. This is a VAL-owned class implementing Dalamud's `IPluginConfiguration`; it is not extending a native FFXIV class.

```text
Configuration 🟢
├─ Version
├─ TrackedAchievementIds
├─ TrackedAchievementPresets
├─ CosmicClassScoreCache
└─ HideCompletedInSearch
```

Methods:

```text
Normalize() 🟢
├─ TrackedAchievementPresetStore.Normalize(TrackedAchievementPresets) 🟢
└─ CosmicClassProgressProvider.Normalize(CosmicClassScoreCache) 🟠

Save() 🟢/🟡
└─ Plugin.PluginInterface.SavePluginConfig(this) 🟡
```

## `TrackedAchievementStore.cs`

In-memory ordered tracked list. It is intentionally separate from `Configuration` so UI operations can be expressed as small list operations.

```text
LoadFrom(ids) 🟢
ToConfigList() 🟢
TryAdd(id) 🟢
Remove(id) 🟢
MoveToTop/MoveUp/MoveDown/MoveToBottom(id) 🟢
```

Persistence happens only when caller invokes [`Plugin.SaveTrackedAchievements()`](Function-call-map#configuration-and-stores).

## `TrackedAchievementPresetStore.cs`

Static helper for named saved lists.

```text
SavePreset(name, ids) 🟢
RenamePreset(oldName, newName) 🟢
DeletePreset(name) 🟢
FindPreset(name) 🟢
Normalize(presets) 🟢
```

This store sanitizes names and IDs, but it does not write files directly. The caller modifies `Configuration.TrackedAchievementPresets` and then calls [`Plugin.SaveConfiguration()`](Function-call-map#configuration-and-stores).

## `CosmicClassScoreCache.cs`

Persistent snapshot for Cosmic score planning.

```text
CosmicClassScoreCache 🟢
├─ Scores: 11 integer class scores
└─ ObservedAtUnixSeconds: cache timestamp
```

Writer:

```text
CosmicClassProgressProvider.SaveScoresToCache(liveScores) 🟠
```

Reader:

```text
CosmicClassProgressProvider.TryReadCachedScores() 🟢
```

## Observed progress cache

`ClientAchievementProgressSource` keeps observed normal achievement progress in memory only:

```text
cachedProgress: Dictionary<uint, ObservedAchievementProgress> 🟠
observationDeadlines: Dictionary<uint, DateTimeOffset> 🟠
observedCompletions: HashSet<uint> 🟠
```

These values are not saved to plugin config. They reset on plugin reload/login/logout.

## Dalamud best-practice notes

- ✅ Uses `IPluginConfiguration` and `PluginInterface.SavePluginConfig` for persistent plugin state.
- ✅ Keeps config serializable and VAL-owned; no native objects are saved.
- ✅ Uses store classes for list/preset manipulation, then explicitly saves config.
- ✅ Keeps ClientStructs/native reads isolated and writes only ordinary numeric cache values to config.
- ✅ Does not write arbitrary files for presets or cache.
- ⚠️ Cosmic cache can be stale out of zone by design; UI should treat it as planning data, not authoritative live server state.
