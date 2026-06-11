# Big picture

Version: `v0.2.0.33`

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
├─ Configuration 🟢 persisted by PluginInterface 🟢
├─ TrackedAchievementStore 🟢 in-memory ordered IDs
├─ AchievementCatalog 🟢 + IDataManager/Lumina 🟢
├─ ClientAchievementProgressSource 🟡 bounded local progress-slot observation
├─ CosmicClassProgressProvider 🟡 WKSManager score reads + config cache
├─ NativeAchievementNavigator 🟡 AgentAchievement native UI adapter
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
   │  └─ ClientAchievementProgressSource.TryGetCachedObservation(id) 🟡
   └─ Plugin.OpenAchievementForUpdate(achievementId) 🟢/🟡
      ├─ checks CanOpenAchievementForUpdate adaptive pacing 🟢
      ├─ NativeAchievementNavigator.IsAchievementWindowOpen() 🟡
      ├─ NativeAchievementNavigator.OpenAchievement(achievementId) 🟡
      │  └─ AgentAchievement.Instance()->OpenById(achievementId) 🟡
      ├─ ClientAchievementProgressSource.BeginObservation(achievementId, 15s) 🟡
      └─ sets adaptive pacing: open window = 1s cooldown; closed window = data wait for 6-15s 🟢
```

### Reload a specific tracked row

```text
User clicks row sync/reload icon 🟢
└─ TrackerWindow.DrawRowUpdateButton(id) or ConfigWindow.DrawTrackedUpdateButton(id) 🟢
   └─ Plugin.OpenAchievementForUpdate(id) 🟢/🟡
```

### Inspect/open without update intent

```text
User clicks magnifying glass 🟢
└─ TrackerWindow.OpenNativeAchievement(id) or ConfigWindow.DrawInspectButton(id) 🟢
   └─ NativeAchievementNavigator.OpenAchievement(id) 🟡
      └─ AgentAchievement.Instance()->OpenById(id) 🟡
```

This path intentionally does not start the update lockout/observation timer.

### Close native Achievements window

```text
User clicks Close Achievements 🟢
└─ TrackerWindow.DrawCloseAchievementsButton() 🟢
   └─ NativeAchievementNavigator.CloseAchievements() 🟡
      └─ AgentAchievement.Instance()->Hide() 🟡
```

### Add achievement from search

```text
User searches in ConfigWindow 🟢
└─ ConfigWindow.GetVisibleSearchResults() 🟢
   ├─ AchievementCatalog.Search(query) 🟢
   │  └─ AchievementCatalog.IsManuallyViewable(id) 🟢
   │     ├─ requires visible AchievementCategory.HideCategory == false 🟢
   │     └─ rejects AchievementHideCondition HideAchievement/HideName 🟢
   └─ AchievementProgressService.IsComplete(row) 🟢
User clicks Add 🟢
└─ AchievementCatalog.IsManuallyViewable(achievementId) 🟢
   └─ TrackedAchievementStore.TryAdd(achievementId) 🟢
   └─ Plugin.SaveTrackedAchievements() 🟢
      ├─ Configuration.TrackedAchievementIds = store.ToConfigList() 🟢
      └─ Configuration.Save() -> PluginInterface.SavePluginConfig(this) 🟢
```

Hidden or non-manually-viewable Lumina rows are intentionally excluded from selection. Example: old hidden Seasonal Event achievements with `AchievementCategory.HideCategory = true` are not offered for tracking because the native Achievement menu cannot manually display them.

### Remove/reorder tracked achievements

```text
User clicks X / Top / Up / Down / Bottom 🟢
└─ TrackedAchievementStore.Remove/Move...(id) 🟢
   └─ Plugin.SaveTrackedAchievements() 🟢
```

### Save/read/rename/delete presets

```text
User clicks preset icons 🟢
└─ TrackedAchievementPresetStore.SavePreset/Rename/Delete/FindPreset(...) 🟢
   ├─ read/load filters ids through AchievementCatalog.IsManuallyViewable(id) 🟢
   └─ Plugin.SaveConfiguration() 🟢
      └─ PluginInterface.SavePluginConfig(Configuration) 🟢
```

### Toggle hide-completed search filter

```text
User checks Hide completed 🟢
└─ Configuration.HideCompletedInSearch = value 🟢
   └─ Plugin.SaveConfiguration() 🟢
```

## Where achievement IDs come from

Achievement IDs enter the system from two places:

1. **Search results**: `AchievementCatalog.Search(query)` reads Lumina `Achievement` rows through Dalamud `IDataManager`, then keeps only rows that `AchievementCatalog.IsManuallyViewable(id)` accepts. Each result has an `Id`/`RowId`. When the player clicks Add, that ID is checked again before it becomes tracked.
2. **Saved config**: `PluginInterface.GetPluginConfig()` loads `Configuration.TrackedAchievementIds`, then `CreateTrackedAchievementStore()` filters saved IDs through `AchievementCatalog.IsManuallyViewable(id)` before `TrackedAchievementStore.LoadFrom(ids)` builds the in-memory ordered list.

The manually-viewable filter rejects:

- missing/invalid Achievement rows
- blank achievement names
- hidden categories (`AchievementCategory.HideCategory`)
- hide conditions that hide the achievement or its name (`HideAchievement`, `HideName`)

After that, most UI flows use IDs from `TrackedAchievementStore.AchievementIds`.

## Achievement getter/setter hierarchy

```text
AchievementCatalog.TryGetRow(id) 🟢
└─ returns Lumina Achievement row metadata: name, category, data target rows

AchievementProgressService.GetProgress(row) 🟢
├─ CosmicClassProgressProvider.GetProgress(id) 🟡 for Cosmic IDs 3702-3739
├─ ClientAchievementProgressSource.TryGetProgress(id, out current, out max) 🟡
├─ IUnlockState.IsAchievementListLoaded / IsAchievementComplete(row) 🟢
└─ AchievementProgress.TargetKnown(...) from Lumina target data 🟢

TrackedAchievementStore.TryAdd/Remove/Move... 🟢
└─ changes in-memory list only until Plugin.SaveTrackedAchievements() persists it

Configuration.Save() 🟢
└─ PluginInterface.SavePluginConfig(this) writes Dalamud plugin config using standard Dalamud persistence
```

## Native Achievement open path

`NativeAchievementNavigator` is custom plugin code. It is not a subclass/derivation of a Dalamud class. It is a small wrapper we own around the ClientStructs/native `AgentAchievement` surface.

```text
Plugin.OpenAchievementForUpdate(achievementId) 🟢
└─ NativeAchievementNavigator.OpenAchievement(achievementId) 🟡 custom VAL adapter
   └─ AgentAchievement.Instance()->OpenById(achievementId) 🟡 FFXIV native UI agent call
```

## Cosmic Class path

```text
AchievementProgressService.GetProgress(row) 🟢
└─ CosmicClassProgressProvider.GetProgress(row.RowId) 🟡
   ├─ map achievement ID 3702-3739 to class score indexes + target 🟢
   ├─ TryReadLiveScores() 🟡
   │  └─ WKSManager.Instance()->State.Scores 🟡
   ├─ SaveScoresToCache(...) 🟢
   │  └─ Plugin.SaveConfiguration() -> SavePluginConfig 🟢
   └─ fallback to cached scores or Data not available 🟢
```
