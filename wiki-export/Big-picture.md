# Big picture

Veela's Achievement Ledger is a Dalamud plugin with three main jobs:

1. Let the player choose achievements to track.
2. Help the player open the native FFXIV Achievement window/entry for those achievements.
3. Passively display progress that the client has already loaded or observed.

It does **not** directly request achievement progress from the server.

## Top-level object graph

```text
Plugin
├─ Configuration
│  ├─ tracked achievement IDs
│  ├─ preset lists
│  ├─ search/config options
│  └─ CosmicClassScoreCache
├─ TrackedAchievementStore
│  └─ in-memory ordered list of tracked IDs
├─ AchievementCatalog
│  └─ Lumina sheet lookup for names/categories/rows
├─ ClientAchievementProgressSource
│  └─ passive/observed achievement progress cache
├─ CosmicClassProgressProvider
│  └─ local WKS/Cosmic score reader + cache mapper
├─ NativeAchievementNavigator
│  └─ opens/closes native Achievement UI through AgentAchievement
├─ AchievementProgressService
│  └─ combines complete/incomplete/local-progress display logic
├─ PassiveAchievementProgressObserver
│  └─ hooks native progress/completion callbacks and caches observations
├─ TrackerWindow
│  └─ main `/val` window
└─ ConfigWindow
   └─ configure/search/presets/help window
```

## Runtime event flow

```text
Plugin constructor
├─ loads saved config
├─ normalizes config
├─ builds services/windows
├─ installs passive progress observer hooks
├─ registers /val command
├─ registers UI draw callbacks
├─ registers Framework.Update
└─ registers login/logout cache resets
```

## Main user flows

### Open the ledger

```text
User runs /val
└─ Plugin.OnCommand(...)
   └─ TrackerWindow.Toggle()
```

### Configure tracked achievements

```text
User clicks Configure or runs /val config
└─ Plugin.OpenConfigUi()
   └─ ConfigWindow.OpenConfig()
      └─ ConfigWindow.Draw()
```

### Update/open a tracked achievement

```text
User clicks Update Next or row reload icon
└─ Plugin.OpenAchievementForUpdate(achievementId)
   ├─ checks 5-second shared lockout
   ├─ NativeAchievementNavigator.OpenAchievement(achievementId)
   │  └─ AgentAchievement.Instance()->OpenById(achievementId)
   └─ starts 5-second lockout
```

### Passive progress observation

```text
Native game/client receives achievement progress
└─ PassiveAchievementProgressObserver.OnReceiveAchievementProgress(...)
   ├─ calls original game function first
   └─ ClientAchievementProgressSource.RecordObservedProgress(...)
```

### Cosmic Class progress

```text
Dalamud framework tick
└─ Plugin.OnFrameworkUpdate(...)
   └─ Plugin.RefreshCosmicCacheFromLiveState()
      ├─ if not Sinus Ardorum territory 1237: return
      ├─ if less than 30 seconds since last read: return
      └─ CosmicClassProgressProvider.RefreshCacheFromLiveScores()
         └─ TryReadLiveScores()
            └─ WKSManager.Instance()->State.Scores
```
