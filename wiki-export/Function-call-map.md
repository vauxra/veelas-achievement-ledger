# Function call map

Version: `v0.2.0.20`

This map follows the refactored code layout. The goal is to show what each important method does, what it calls, and which layer it touches.

## Reading convention

- `A += B` in older docs meant “subscribe B as an event handler on event A.” In this page it is written explicitly as **subscribe**.
- `Draw...` methods are ImGui UI code. They are redrawn every frame, but the inside of `if (ImGui.Button(...))` only runs on the click frame.
- TLP labels: 🟢 plugin/domain, 🟡 Dalamud managed, 🟠 native/ClientStructs adapter, 🔴 blocked/deprecated.

## `Plugin.cs` — app entry point / wiring

<a id="plugin-constructor"></a>

### `Plugin()` 🟢/🟡

Purpose: create the application object, services, and windows.

```text
Plugin()
├─ LoadAndNormalizeConfiguration() 🟢/🟡
├─ CreateTrackedAchievementStore() 🟢
├─ new AchievementCatalog(DataManager) 🟢/🟡
├─ new ClientAchievementProgressSource() 🟠
├─ new CosmicClassProgressProvider(cache, SaveConfiguration) 🟠
├─ new NativeAchievementNavigator() 🟠
├─ new AchievementProgressService(UnlockState, progressSource, cosmicProvider) 🟢/🟡/🟠
├─ new TrackerWindow(this) 🟢
├─ new ConfigWindow(this) 🟢
├─ RegisterWindows() 🟡
├─ RegisterCommand() 🟡
└─ RegisterDalamudCallbacks() 🟡
```

See also: [Whole plugin hierarchy](Whole-plugin-hierarchy), [Data model map](Data-model-map).

<a id="register-dalamud-callbacks"></a>

### `RegisterDalamudCallbacks()` / `UnregisterDalamudCallbacks()` 🟡

Purpose: subscribe/unsubscribe from Dalamud events cleanly.

```text
RegisterDalamudCallbacks()
├─ subscribe UiBuilder.Draw to WindowSystem.Draw
├─ subscribe UiBuilder.OpenMainUi to ToggleMainUi
├─ subscribe UiBuilder.OpenConfigUi to ToggleConfigUi
├─ subscribe Framework.Update to OnFrameworkUpdate
├─ subscribe ClientState.Login to ResetProgressState
└─ subscribe ClientState.Logout to ResetProgressStateOnLogout
```

TLP: 🟡 because this uses Dalamud lifecycle services. Dispose unregisters the same callbacks.

<a id="plugin-openachievementforupdate"></a>

### `OpenAchievementForUpdate(uint achievementId)` 🟢/🟠

Purpose: shared lockout-protected path for update-intent native opens.

```text
OpenAchievementForUpdate(id)
├─ CanOpenAchievementForUpdate 🟢
├─ NativeAchievementNavigator.OpenAchievement(id) 🟠
├─ ClientAchievementProgressSource.BeginObservation(id, 8 seconds) 🟠
└─ set nextAchievementUpdateOpenAt 🟢
```

Method links: [Big picture native open path](Big-picture#native-achievement-open-path), [Safety map](Safety-map#native-achievement-ui-actions).

<a id="plugin-onframeworkupdate"></a>

### `OnFrameworkUpdate(IFramework framework)` 🟡/🟠

```text
OnFrameworkUpdate(framework)
├─ ClientAchievementProgressSource.UpdateCache() 🟠
└─ RefreshCosmicCacheFromLiveState() 🟠
```

This is not a server polling loop. It reads local state only.

<a id="plugin-refreshcosmiccachefromlivestate"></a>

### `RefreshCosmicCacheFromLiveState()` 🟢/🟡/🟠

```text
RefreshCosmicCacheFromLiveState()
├─ IsInSinusArdorum() uses ClientState.TerritoryType 🟡
├─ CosmicCacheRefreshIsDue() 🟢
└─ CosmicClassProgressProvider.RefreshCacheFromLiveScores() 🟠
```

See: [Cosmic Class cache flow](Cosmic-Class-cache-flow).

<a id="nativeachievementnavigator"></a>

## `NativeAchievementNavigator.cs` — native Achievement UI adapter 🟠

Custom VAL class; not derived from a system component. It wraps ClientStructs native UI agent calls.

```text
OpenAchievement(achievementId)
├─ reject id == 0
├─ AgentAchievement.Instance() 🟠
├─ agent null-check
└─ agent->OpenById(achievementId) 🟠

CloseAchievements()
├─ AgentAchievement.Instance() 🟠
├─ agent null-check
└─ agent->Hide() 🟠
```

<a id="clientachievementprogresssource"></a>

## `ClientAchievementProgressSource.cs` — bounded observation cache 🟠

```text
BeginObservation(id, duration) 🟠
└─ records a deadline for this exact id

UpdateCache() 🟠
├─ PruneExpiredObservations()
├─ Achievement.Instance() 🟠
└─ TryRecordObservedSlot(
      ProgressRequestState == Loaded,
      ProgressAchievementId,
      ProgressCurrent,
      ProgressMax,
      "Achievement state slot")

TryRecordObservedSlot(...) 🟠
├─ require loaded, id != 0, max != 0
├─ require active observation window for same id
├─ RecordObservedProgress(id, current, max, source)
└─ remove observation window
```

No hook/event interception is used here.

<a id="achievementprogressservice"></a>

## `AchievementProgressService.cs` — progress decision service 🟢/🟡/🟠

```text
GetProgress(Achievement row)
├─ if CosmicClassProgressProvider.Handles(row.RowId) 🟠
│  └─ CosmicClassProgressProvider.GetProgress(row.RowId) 🟠
├─ if progressSource.TryGetProgress(row.RowId, out current, out max) 🟠
│  └─ AchievementProgress.Numeric(current, max) 🟢
├─ if !UnlockState.IsAchievementListLoaded 🟡
│  └─ CompletionListNotLoaded or TargetKnown 🟢
├─ if UnlockState.IsAchievementComplete(row) 🟡
│  └─ Complete 🟢
└─ TargetKnown / Incomplete / Unavailable 🟢
```

<a id="cosmicclassprogressprovider"></a>

## `CosmicClassProgressProvider.cs` — Cosmic score adapter 🟠

```text
GetProgress(achievementId)
├─ GetRule(achievementId) maps 3702-3739 to class indexes/targets 🟢
├─ TryReadLiveScores() 🟠
│  ├─ WKSManager.Instance() 🟠
│  ├─ manager->IsLoaded 🟠
│  ├─ manager->State.Scores.ToArray() 🟠
│  └─ SaveScoresToCache(liveScores) 🟢/🟡
├─ TryReadCachedScores() 🟢
├─ CalculateCurrentScore(scores, rule) 🟢
└─ AchievementProgress.Numeric(current, target) or DataNotAvailable 🟢
```

See: [Cosmic Class cache flow](Cosmic-Class-cache-flow).

<a id="configuration-and-stores"></a>

## `Configuration.cs` and stores — saved/in-memory state 🟢/🟡

```text
Configuration.Save() 🟢/🟡
└─ Plugin.PluginInterface.SavePluginConfig(this) 🟡

TrackedAchievementStore.LoadFrom(ids) 🟢
└─ in-memory sanitized ordered ID list

TrackedAchievementPresetStore.SavePreset/Rename/Delete/Normalize 🟢
└─ modifies Configuration.TrackedAchievementPresets in memory; Plugin.SaveConfiguration persists
```

See: [Data model map](Data-model-map).

<a id="trackerwindow"></a>

## `TrackerWindow.cs` — main UI 🟢

```text
Draw()
├─ AchievementProgressSource.UpdateCache() 🟠
├─ DrawTopButtons()
└─ DrawTrackedAchievementList()

DrawRowUpdateButton(id)
└─ Plugin.OpenAchievementForUpdate(id) 🟢/🟠

DrawRowInspectButton(id)
└─ NativeAchievementNavigator.OpenAchievement(id) 🟠

GetProgressText(id)
├─ AchievementCatalog.TryGetRow(id) 🟢/🟡
└─ AchievementProgressService.GetProgress(row) 🟢/🟡/🟠
```

<a id="configwindow"></a>

## `ConfigWindow.cs` — config/search/preset UI 🟢

```text
Draw()
├─ DrawHeader()
├─ DrawLeftNavigation()
└─ DrawSelectedPage()
   ├─ DrawTrackedAchievementsPage()
   └─ DrawHelp()

DrawTrackedAchievementRow(id)
├─ move/remove/update/inspect buttons 🟢
├─ Plugin.OpenAchievementForUpdate(id) for update 🟢/🟠
└─ NativeAchievementNavigator.OpenAchievement(id) for inspect 🟠

DrawSearchResultRow(result)
├─ TrackedAchievementStore.TryAdd(result.Id) 🟢
├─ Plugin.SaveTrackedAchievements() 🟢/🟡
└─ DrawCosmicProgressIfAvailable(result.Id) 🟠 for Cosmic IDs
```
