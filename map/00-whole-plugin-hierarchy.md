# Whole plugin hierarchy

Version: `v0.2.0.20`

This is the broad map of the entire plugin. Read it top-to-bottom like a Python package map.

## Repository layout

```text
AchievementTracker/
├─ Plugin.cs                         # main app object, service wiring, commands, framework callbacks
├─ Configuration.cs                  # saved plugin config/settings via Dalamud plugin config
├─ VeelasAchievementLedger.json      # Dalamud plugin manifest
├─ images/icon.png                   # plugin icon
├─ Models/                           # small data/value objects
│  ├─ AchievementInfo.cs             # display info for one achievement
│  ├─ AchievementProgress.cs         # progress states and display text
│  ├─ CosmicClassScoreCache.cs       # saved Cosmic score cache
│  ├─ TrackedAchievement.cs          # tracked achievement item
│  └─ TrackedAchievementPreset.cs    # named saved tracked list
├─ Services/                         # work/helper classes
│  ├─ AchievementCatalog.cs          # reads Lumina achievement/category data
│  ├─ AchievementProgressService.cs  # decides what progress text to show
│  ├─ ClientAchievementProgressSource.cs
│  │                                  # bounded observed-progress cache after user-guided opens
│  ├─ CosmicClassProgressProvider.cs # reads local WKS scores + maps Cosmic achievements
│  ├─ IAchievementProgressSource.cs  # interface for progress source
│  ├─ NativeAchievementNavigator.cs  # opens/closes native Achievement UI
│  ├─ TrackedAchievementPresetStore.cs
│  │                                  # preset save/rename/delete/load helpers
│  └─ TrackedAchievementStore.cs     # ordered tracked achievement IDs
└─ Windows/                          # ImGui UI
   ├─ ConfigWindow.cs                # configure/search/presets/help window
   └─ TrackerWindow.cs               # main /val tracker window
```

## Main dependency graph

```text
Plugin 🟢
├─ owns Configuration 🟢
├─ owns TrackedAchievementStore 🟢
├─ owns AchievementCatalog 🟢
│  └─ uses IDataManager / Lumina sheets 🟢
├─ owns ClientAchievementProgressSource 🟠
│  └─ reads Achievement.Instance() local progress slot only during bounded observation windows
├─ owns CosmicClassProgressProvider 🟠
│  ├─ reads WKSManager.Instance() local scores
│  └─ writes Configuration.CosmicClassScoreCache through Plugin.SaveConfiguration 🟢
├─ owns NativeAchievementNavigator 🟠
│  └─ uses AgentAchievement.Instance() native Achievement UI
├─ owns AchievementProgressService 🟢
│  ├─ uses IUnlockState 🟢
│  ├─ uses ClientAchievementProgressSource 🟠
│  └─ uses CosmicClassProgressProvider 🟠
├─ owns TrackerWindow 🟢
│  └─ calls Plugin/service methods from main UI buttons
└─ owns ConfigWindow 🟢
   └─ calls Plugin/service methods from config/search/preset UI
```

## Startup lifecycle

```text
Dalamud loads plugin 🟢
└─ new Plugin() 🟢
   ├─ LoadAndNormalizeConfiguration()
   │  └─ PluginInterface.GetPluginConfig() 🟢
   ├─ Configuration.Normalize()
   ├─ TrackedAchievementStore.LoadFrom(config IDs)
   ├─ create catalog/progress/navigation/Cosmic services
   ├─ create windows
   ├─ WindowSystem.AddWindow(...) 🟢
   ├─ CommandManager.AddHandler("/val", OnCommand) 🟢
   ├─ register UI draw/open callbacks 🟢
   ├─ register Framework.Update 🟢
   └─ register ClientState login/logout resets 🟢
```

## Runtime boundaries

- 🟢 Most code is plugin-owned UI, models, stores, and formatting.
- 🟢 Dalamud services provide config persistence, commands, UI draw callbacks, Lumina data, unlock/completion checks, zone/login state, and framework ticks.
- 🟠 Native adapters are isolated to three files: `NativeAchievementNavigator`, `ClientAchievementProgressSource`, and `CosmicClassProgressProvider`.
- 🔴 Current mainline should not contain hook observer classes, `Dalamud.Hooking`, signatures, raw-memory scans, or direct achievement-progress request queues.
