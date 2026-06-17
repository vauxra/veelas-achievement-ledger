# Whole plugin hierarchy

This is the broad map of the entire plugin. Read it top-to-bottom like a Python package map.

## Repository layout

```text
AchievementTracker/
├─ Plugin.cs                         # main app object, service wiring, commands, framework callbacks
├─ Configuration.cs                  # saved plugin config/settings
├─ AchieveExPlus.json      # Dalamud plugin manifest
├─ images/
│  └─ icon.png                       # plugin icon
├─ Models/                           # small data/achexue objects
│  ├─ AchievementInfo.cs             # display info for one achievement
│  ├─ AchievementProgress.cs         # progress states and display text
│  ├─ CosmicClassScoreCache.cs       # saved Cosmic score cache
│  ├─ TrackedAchievement.cs          # tracked achievement item
│  └─ TrackedAchievementPreset.cs    # named saved tracked list
├─ Services/                         # work/helper classes
│  ├─ AchievementCatalog.cs          # reads Lumina achievement/category data
│  ├─ AchievementProgressService.cs  # decides what progress text to show
│  ├─ ClientAchievementProgressSource.cs
│  │                                  # passive in-memory observed progress cache
│  ├─ CosmicClassProgressProvider.cs # reads local WKS scores + maps Cosmic achievements
│  ├─ IAchievementProgressSource.cs  # interface for progress source
│  ├─ NativeAchievementNavigator.cs  # opens/closes native Achievement UI
│  ├─ PassiveAchievementProgressObserver.cs
│  │                                  # hooks native callbacks, caches observations
│  ├─ TrackedAchievementPresetStore.cs
│  │                                  # preset save/rename/delete/load helpers
│  └─ TrackedAchievementStore.cs     # ordered tracked achievement IDs
└─ Windows/                          # ImGui UI
   ├─ ConfigWindow.cs                # configure/search/presets/help window
   └─ TrackerWindow.cs               # main /achex tracker window
```

## Main dependency graph

```text
Plugin
├─ owns Configuration
├─ owns TrackedAchievementStore
├─ owns AchievementCatalog
│  └─ uses IDataManager / Lumina sheets
├─ owns ClientAchievementProgressSource
│  └─ reads Achievement.Instance() local progress slot
├─ owns CosmicClassProgressProvider
│  ├─ reads WKSManager.Instance() local scores
│  └─ writes Configuration.CosmicClassScoreCache through Plugin.SaveConfiguration
├─ owns NativeAchievementNavigator
│  └─ uses AgentAchievement.Instance()
├─ owns AchievementProgressService
│  ├─ uses IUnlockState
│  ├─ uses ClientAchievementProgressSource
│  └─ uses CosmicClassProgressProvider
├─ owns PassiveAchievementProgressObserver
│  ├─ hooks ReceiveAchievementProgress
│  └─ hooks SetAchievementCompleted
├─ owns TrackerWindow
│  └─ calls Plugin/service methods from main UI buttons
└─ owns ConfigWindow
   └─ calls Plugin/service methods from config/search/preset UI
```

## Startup lifecycle

```text
Dalamud loads plugin
└─ new Plugin()
   ├─ load Configuration from PluginInterface
   ├─ Configuration.Normalize()
   ├─ TrackedAchievementStore.LoadFrom(config IDs)
   ├─ create services
   ├─ create windows
   ├─ InstallPassiveAchievementObserver()
   ├─ WindowSystem.AddWindow(...)
   ├─ CommandManager.AddHandler("/achex", OnCommand)
   ├─ register UI draw/open callbacks
   ├─ register Framework.Update
   └─ register ClientState.Login/Logout cache resets
```

## Shutdown lifecycle

```text
Dalamud unloads plugin
└─ Plugin.Dispose()
   ├─ unregister UI callbacks
   ├─ unregister Framework.Update
   ├─ unregister ClientState.Login/Logout
   ├─ CommandManager.RemoveHandler("/achex")
   ├─ PassiveAchievementProgressObserver.Dispose()
   │  ├─ dispose receive hook
   │  └─ dispose completed hook
   └─ WindowSystem.RemoveAllWindows()
```

## User command hierarchy

```text
/achex
└─ Plugin.OnCommand(command, args)
   ├─ no args / unknown args
   │  └─ ToggleMainUi()
   │     └─ TrackerWindow.Toggle()
   ├─ config / configure / man
   │  └─ OpenConfigUi(help: false)
   │     └─ ConfigWindow.OpenConfig()
   └─ help / ?
      └─ OpenConfigUi(help: true)
         └─ ConfigWindow.OpenHelp()
```

## Main window hierarchy

```text
TrackerWindow.Draw()
├─ update passive local progress cache
│  └─ AchievementProgressSource.UpdateCache()
├─ Configure button
│  └─ Plugin.ToggleConfigUi()
├─ Update Next button
│  ├─ disabled during update-open lockout
│  ├─ GetNextTrackedAchievementId()
│  │  ├─ choose first unobserved tracked ID
│  │  └─ otherwise choose oldest observed tracked ID
│  └─ OpenNativeAchievementForUpdate(id)
│     └─ Plugin.OpenAchievementForUpdate(id)
├─ Close Achievements button
│  └─ NativeAchievementNavigator.CloseAchievements()
├─ lockout status text
└─ tracked achievement list
   └─ DrawAchievement(id)
      ├─ AchievementCatalog.TryGet(id)
      ├─ AchievementCatalog.TryGetRow(id)
      ├─ AchievementProgressService.GetProgress(row)
      ├─ reload icon
      │  └─ Plugin.OpenAchievementForUpdate(id)
      ├─ magnifying glass icon
      │  └─ NativeAchievementNavigator.OpenAchievement(id)
      ├─ achievement name
      ├─ progress text
      └─ last observed text
```

## Config window hierarchy

```text
ConfigWindow.Draw()
├─ draw left navigation
│  ├─ Organization
│  └─ Help
├─ Organization selected
│  ├─ preset management
│  │  ├─ save preset
│  │  ├─ select/load preset
│  │  ├─ read selected preset
│  │  ├─ rename preset
│  │  └─ delete preset
│  ├─ tracked achievement organization
│  │  └─ per tracked row
│  │     ├─ Top → MoveToTop → SaveTrackedAchievements
│  │     ├─ Up → MoveUp → SaveTrackedAchievements
│  │     ├─ Down → MoveDown → SaveTrackedAchievements
│  │     ├─ Bottom → MoveToBottom → SaveTrackedAchievements
│  │     ├─ Remove → RemoveTrackedAchievement → SaveTrackedAchievements
│  │     ├─ reload → Plugin.OpenAchievementForUpdate
│  │     ├─ magnifying glass → NativeAchievementNavigator.OpenAchievement
│  │     └─ name/category/Cosmic progress display
│  └─ search and add
│     ├─ search text input
│     ├─ hide completed checkbox
│     ├─ clear button
│     └─ search result row
│        ├─ Add → TrackedAchievementStore.Add → SaveTrackedAchievements
│        └─ magnifying glass → NativeAchievementNavigator.OpenAchievement
└─ Help selected
   └─ draw player-facing help text
```

## Data flow: tracked achievement list

```text
Configuration.TrackedAchievementIds  # saved on disk
└─ Plugin constructor
   └─ TrackedAchievementStore.LoadFrom(...)
      └─ UI edits store in memory
         └─ Plugin.SaveTrackedAchievements()
            ├─ Configuration.TrackedAchievementIds = store.ToConfigList()
            └─ Configuration.Save()
```

## Data flow: presets

```text
Configuration.Presets
└─ TrackedAchievementPresetStore helpers
   ├─ SavePreset(name, current tracked IDs)
   ├─ RenamePreset(old, new)
   ├─ DeletePreset(name)
   └─ TryGetPreset(name)
      └─ TrackedAchievementStore.LoadFrom(preset IDs)
```

## Data flow: normal achievement progress

```text
Player opens native Achievement UI through plugin or manually
└─ game/client receives progress
   └─ PassiveAchievementProgressObserver hook sees callback
      ├─ calls original game callback first
      └─ ClientAchievementProgressSource records progress
         └─ AchievementProgressService.GetProgress(row)
            └─ UI displays progress text
```

## Data flow: Cosmic Class progress

```text
Framework.Update
└─ Plugin.RefreshCosmicCacheFromLiveState()
   ├─ only if in Sinus Ardorum / TerritoryTypeId 1237
   ├─ only when interval has elapsed
   └─ CosmicClassProgressProvider.RefreshCacheFromLiveScores()
      └─ TryReadLiveScores()
         ├─ WKSManager.Instance()
         ├─ manager->IsLoaded
         ├─ manager->State.Scores
         ├─ save changed scores to Configuration.CosmicClassScoreCache
         └─ AchievementProgressService uses provider for Cosmic achievement rows
```

## Safety boundary map

```text
Allowed user-guided actions
├─ AgentAchievement.OpenById(id)
└─ AgentAchievement.Hide()

Allowed passive/local reads
├─ Achievement.Instance() progress slot
├─ ReceiveAchievementProgress hook after original callback
├─ SetAchievementCompleted hook after original callback
└─ WKSManager.Instance().State.Scores in Sinus Ardorum

Avoided dangerous actions
├─ direct achievement-progress request API
├─ automatic Update All queue
├─ timed auto-update of achievements
├─ event-triggered update automation
├─ packet capture
├─ backend/network/telemetry
├─ ContentId storage/transmission
└─ synthetic addon callback/fire events
```

## If you are reading C# like Python

- A `class` file is usually one module-like unit.
- Constructor name matches class name: `public Plugin()` is like `def __init__(self):`.
- `this.foo` is like `self.foo`.
- `private` means "helper only used inside this class."
- `public` means other classes may call it.
- `void` means returns nothing.
- `bool` means returns true/false.
- `uint` means non-negative integer.
- `?` after a type means it can be null, like `Optional[...]` in Python typing.
