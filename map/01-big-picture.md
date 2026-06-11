# Big picture

Version: `v0.2.0.20`

## Related function map entries

- [`Plugin.OpenAchievementForUpdate(...)`](./02-function-call-map.md#plugin-openachievementforupdate)
- [`NativeAchievementNavigator.OpenAchievement(...)`](./02-function-call-map.md#nativeachievementnavigator)
- [`ClientAchievementProgressSource.BeginObservation(...)`](./02-function-call-map.md#clientachievementprogresssource)
- [`AchievementProgressService.GetProgress(...)`](./02-function-call-map.md#achievementprogressservice)
- [`CosmicClassProgressProvider.GetProgress(...)`](./02-function-call-map.md#cosmicclassprogressprovider)
- [`Configuration.Save()` and tracked/preset stores](./02-function-call-map.md#configuration-and-stores)

## Navigation outline

- [What the plugin does](#what-the-plugin-does)
- [Top-level object graph](#top-level-object-graph)
- [All user actions](#all-user-actions)
- [Where achievement IDs come from](#where-achievement-ids-come-from)
- [Achievement getter/setter hierarchy](#achievement-gettersetter-hierarchy)
- [Native Achievement open path](#native-achievement-open-path)
- [Cosmic Class path](#cosmic-class-path)

## What the plugin does

Veela's Achievement Ledger is a Dalamud plugin with three main jobs:

1. Let the player choose achievements to track.
2. Help the player open the native FFXIV Achievement window/entry for those achievements.
3. Display progress from safe sources the client already has or that were observed during a bounded user-guided window.

It does **not** directly request achievement progress from the server in current mainline.

## Top-level object graph

```text
Plugin 🟢
├─ Configuration 🟢 persisted by PluginInterface 🟡
├─ TrackedAchievementStore 🟢 in-memory ordered IDs
├─ AchievementCatalog 🟢 + IDataManager/Lumina 🟡
├─ ClientAchievementProgressSource 🟠 bounded local progress-slot observation
├─ CosmicClassProgressProvider 🟠 WKSManager score reads + config cache
├─ NativeAchievementNavigator 🟠 AgentAchievement native UI adapter
├─ AchievementProgressService 🟢 progress decision service
├─ TrackerWindow 🟢 main /val window
└─ ConfigWindow 🟢 config/search/presets/help window
```

## All user actions

### Open the ledger

```text
User runs /val 🟢
└─ Plugin.OnCommand(command, args) 🟢
   └─ TrackerWindow.Toggle() 🟢
```

### Open configuration

```text
User clicks Configure or runs /val config|configure|man 🟢
└─ Plugin.OpenConfigUi(help: false) 🟢
   └─ ConfigWindow.OpenConfig() 🟢
```

### Open Help directly

```text
User runs /val help or /val ? 🟢
└─ Plugin.OpenConfigUi(help: true) 🟢
   └─ ConfigWindow.OpenHelp() 🟢
```

### Update/open the next tracked achievement

```text
User clicks Update Next 🟢
└─ TrackerWindow.OpenNextTrackedAchievementForUpdate() 🟢
   ├─ TrackerWindow.GetNextTrackedAchievementId() 🟢
   │  ├─ TrackedAchievementStore.AchievementIds 🟢
   │  └─ ClientAchievementProgressSource.TryGetCachedObservation(id) 🟠
   └─ Plugin.OpenAchievementForUpdate(achievementId) 🟢/🟠
      ├─ checks CanOpenAchievementForUpdate lockout 🟢
      ├─ NativeAchievementNavigator.OpenAchievement(achievementId) 🟠
      │  └─ AgentAchievement.Instance()->OpenById(achievementId) 🟠
      ├─ ClientAchievementProgressSource.BeginObservation(achievementId, 8s) 🟠
      └─ sets nextAchievementUpdateOpenAt 🟢
```

### Reload a specific tracked row

```text
User clicks row sync/reload icon 🟢
└─ TrackerWindow.DrawRowUpdateButton(id) or ConfigWindow.DrawTrackedUpdateButton(id) 🟢
   └─ Plugin.OpenAchievementForUpdate(id) 🟢/🟠
```

### Inspect/open without update intent

```text
User clicks magnifying glass 🟢
└─ TrackerWindow.OpenNativeAchievement(id) or ConfigWindow.DrawInspectButton(id) 🟢
   └─ NativeAchievementNavigator.OpenAchievement(id) 🟠
      └─ AgentAchievement.Instance()->OpenById(id) 🟠
```

This path intentionally does not start the update lockout/observation timer.

### Close native Achievements window

```text
User clicks Close Achievements 🟢
└─ TrackerWindow.DrawCloseAchievementsButton() 🟢
   └─ NativeAchievementNavigator.CloseAchievements() 🟠
      └─ AgentAchievement.Instance()->Hide() 🟠
```

### Add achievement from search

```text
User searches in ConfigWindow 🟢
└─ ConfigWindow.GetVisibleSearchResults() 🟢
   ├─ AchievementCatalog.Search(query) 🟢/🟡
   └─ AchievementProgressService.IsComplete(row) 🟢/🟡
User clicks Add 🟢
└─ TrackedAchievementStore.TryAdd(achievementId) 🟢
   └─ Plugin.SaveTrackedAchievements() 🟢/🟡
      ├─ Configuration.TrackedAchievementIds = store.ToConfigList() 🟢
      └─ Configuration.Save() -> PluginInterface.SavePluginConfig(this) 🟡
```

### Remove/reorder tracked achievements

```text
User clicks X / Top / Up / Down / Bottom 🟢
└─ TrackedAchievementStore.Remove/Move...(id) 🟢
   └─ Plugin.SaveTrackedAchievements() 🟢/🟡
```

### Save/read/rename/delete presets

```text
User clicks preset icons 🟢
└─ TrackedAchievementPresetStore.SavePreset/Rename/Delete/FindPreset(...) 🟢
   └─ Plugin.SaveConfiguration() 🟢/🟡
      └─ PluginInterface.SavePluginConfig(Configuration) 🟡
```

### Toggle hide-completed search filter

```text
User checks Hide completed 🟢
└─ Configuration.HideCompletedInSearch = value 🟢
   └─ Plugin.SaveConfiguration() 🟢/🟡
```

## Where achievement IDs come from

Achievement IDs enter the system from two places:

1. **Search results**: [`AchievementCatalog.Search(query)`](./02-function-call-map.md#achievementprogressservice) reads Lumina `Achievement` rows through Dalamud `IDataManager`. Each result has an `Id`/`RowId`. When the player clicks Add, that ID becomes tracked.
2. **Saved config**: `PluginInterface.GetPluginConfig()` loads `Configuration.TrackedAchievementIds`, then [`TrackedAchievementStore.LoadFrom(ids)`](./02-function-call-map.md#configuration-and-stores) builds the in-memory ordered list.

After that, most UI flows use IDs from `TrackedAchievementStore.AchievementIds`.

## Achievement getter/setter hierarchy

```text
AchievementCatalog.TryGetRow(id) 🟢/🟡
└─ returns Lumina Achievement row metadata: name, category, data target rows

AchievementProgressService.GetProgress(row) 🟢
├─ CosmicClassProgressProvider.GetProgress(id) 🟠 for Cosmic IDs 3702-3739
├─ ClientAchievementProgressSource.TryGetProgress(id, out current, out max) 🟠
├─ IUnlockState.IsAchievementListLoaded / IsAchievementComplete(row) 🟡
└─ AchievementProgress.TargetKnown(...) from Lumina target data 🟢/🟡

TrackedAchievementStore.TryAdd/Remove/Move... 🟢
└─ changes in-memory list only until Plugin.SaveTrackedAchievements() persists it

Configuration.Save() 🟢/🟡
└─ PluginInterface.SavePluginConfig(this) writes Dalamud plugin config using standard Dalamud persistence
```

## Native Achievement open path

`NativeAchievementNavigator` is custom plugin code. It is not a subclass/derivation of a Dalamud class. It is a small wrapper we own around the ClientStructs/native `AgentAchievement` surface.

```text
Plugin.OpenAchievementForUpdate(achievementId) 🟢
└─ NativeAchievementNavigator.OpenAchievement(achievementId) 🟠 custom VAL adapter
   └─ AgentAchievement.Instance()->OpenById(achievementId) 🟠 FFXIV native UI agent call
```

## Cosmic Class path

```text
AchievementProgressService.GetProgress(row) 🟢
└─ CosmicClassProgressProvider.GetProgress(row.RowId) 🟠
   ├─ map achievement ID 3702-3739 to class score indexes + target 🟢
   ├─ TryReadLiveScores() 🟠
   │  └─ WKSManager.Instance()->State.Scores 🟠
   ├─ SaveScoresToCache(...) 🟢/🟡
   │  └─ Plugin.SaveConfiguration() -> SavePluginConfig 🟡
   └─ fallback to cached scores or Data not available 🟢
```
