# Function call map

This map follows the refactored code layout. The goal is to show what each important method does and what it calls.

## Reading convention

Each section starts with the file/class, then lists call chains like:

```text
MethodA()
└─ MethodB()
   └─ Service.MethodC()
```

A method named `Draw...` is UI code. It is redrawn every frame, but the inside of `if (ImGui.Button(...))` only runs when you click that button.

## `Plugin.cs` — app entry point / wiring

### Constructor: `Plugin()`

Purpose: create the application object, services, and windows.

```text
Plugin()
├─ LoadAndNormalizeConfiguration()
├─ CreateTrackedAchievementStore()
├─ new AchievementCatalog(DataManager)
├─ new ClientAchievementProgressSource()
├─ new CosmicClassProgressProvider(...)
├─ new NativeAchievementNavigator()
├─ new AchievementProgressService(...)
├─ new TrackerWindow(this)
├─ new ConfigWindow(this)
├─ InstallPassiveAchievementObserver()
├─ RegisterWindows()
├─ RegisterCommand()
└─ RegisterDalamudCallbacks()
```

Risk notes:

- Constructor itself is low risk.
- It wires higher-risk services, but does not directly call native/game functions except service construction.

### `RegisterDalamudCallbacks()` / `UnregisterDalamudCallbacks()`

Purpose: subscribe/unsubscribe from Dalamud events cleanly.

```text
RegisterDalamudCallbacks()
├─ UiBuilder.Draw += WindowSystem.Draw
├─ UiBuilder.OpenMainUi += ToggleMainUi
├─ UiBuilder.OpenConfigUi += ToggleConfigUi
├─ Framework.Update += OnFrameworkUpdate
├─ ClientState.Login += ResetProgressState
└─ ClientState.Logout += ResetProgressStateOnLogout
```

Risk notes:

- `Framework.Update` is used only for the gated Cosmic local-score cache check.
- Dispose unregisters the same callbacks so there should not be a lingering timer/event subscription.

### `OpenAchievementForUpdate(uint achievementId)`

Purpose: shared lockout-protected path for update-intent native opens.

```text
OpenAchievementForUpdate(id)
├─ CanOpenAchievementForUpdate
├─ NativeAchievementNavigator.OpenAchievement(id)
└─ set nextAchievementUpdateOpenAt
```

Risk notes:

- Calls native Achievement UI only after user action.
- Does not call `direct achievement-progress request API`.

### `RefreshCosmicCacheFromLiveState()`

Purpose: gate the Cosmic score cache update.

```text
RefreshCosmicCacheFromLiveState()
├─ IsInSinusArdorum()
│  └─ ClientState.TerritoryType == 1237
├─ CosmicCacheRefreshIsDue()
└─ CosmicClassProgressProvider.RefreshCacheFromLiveScores()
```

Risk notes:

- Local ClientStructs read happens in `CosmicClassProgressProvider`, not here.
- This method only decides whether that read is allowed now.

### `OnCommand(string command, string args)`

Purpose: route `/val` commands.

```text
OnCommand(...)
├─ config/configure/man → OpenConfigUi()
├─ help/? → OpenConfigUi(help: true)
└─ anything else → ToggleMainUi()
```

## `TrackerWindow.cs` — main `/val` window

### `Draw()`

Purpose: draw the main tracker window.

```text
Draw()
├─ AchievementProgressSource.UpdateCache()
├─ DrawTopButtons()
└─ DrawTrackedAchievementList()
```

### `DrawTopButtons()`

```text
DrawTopButtons()
├─ DrawConfigureButton()
├─ DrawUpdateNextButton()
├─ DrawCloseAchievementsButton()
├─ DrawUpdateOpenLockoutStatus()
└─ ImGui.Separator()
```

Button calls:

```text
Configure → Plugin.ToggleConfigUi()
Update Next → OpenNextTrackedAchievementForUpdate()
Close Achievements → NativeAchievementNavigator.CloseAchievements()
```

### `DrawAchievement(uint achievementId)`

```text
DrawAchievement(id)
├─ AchievementCatalog.TryGet(id)
├─ GetProgressText(id)
│  └─ AchievementProgressService.GetProgress(row)
├─ GetLastObservedText(id)
│  └─ ClientAchievementProgressSource.TryGetObservation(id)
├─ DrawRowUpdateButton(id)
│  └─ OpenNativeAchievementForUpdate(id)
├─ DrawRowInspectButton(id)
│  └─ NativeAchievementNavigator.OpenAchievement(id)
└─ draw name/progress/last-observed text
```

## `ConfigWindow.cs` — configuration/search/presets/help

### `Draw()`

```text
Draw()
├─ DrawHeader()
├─ DrawLeftNavigation()
└─ DrawSelectedPage()
   ├─ DrawTrackedAchievementsPage()
   └─ DrawHelp()
```

### Preset controls

```text
DrawPresetControls()
├─ EnsureSelectedPresetIsValid()
├─ DrawPresetNameInput()
├─ DrawPresetSaveButton()
├─ DrawPresetPicker()
├─ DrawPresetReadButton()
├─ DrawPresetRenameButton()
└─ DrawPresetDeleteButton()
```

Preset write paths call `Plugin.SaveConfiguration()`.

### Tracked achievements page

```text
DrawTrackedAchievementsPage()
├─ DrawPresetControls()
├─ DrawTrackedManagement()
│  └─ DrawTrackedAchievementRow(id)
│     ├─ DrawMoveButton(...)
│     ├─ DrawTrackedRemoveButton(id)
│     ├─ DrawTrackedUpdateButton(id)
│     ├─ DrawInspectButton(id)
│     └─ DrawManagedAchievement(id)
└─ DrawSearchAndAdd()
   └─ DrawSearchResultRow(result)
      ├─ DrawSearchResultAction(...)
      └─ DrawSearchResultDetails(result)
```

Risk notes:

- Move/add/remove/presets are plugin-config changes only.
- Reload/update buttons call `Plugin.OpenAchievementForUpdate`, which is shared-lockout and user-guided.
- Magnifying-glass buttons call native Achievement UI open without treating it as an update action.

## `NativeAchievementNavigator.cs` — native Achievement UI

```text
OpenAchievement(id)
├─ AgentAchievement.Instance()
└─ agent->OpenById(id)

CloseAchievements()
├─ AgentAchievement.Instance()
└─ agent->Hide()
```

Risk: medium because it uses ClientStructs/native agent calls. Safety: direct user action only; no achievement progress request.

## `PassiveAchievementProgressObserver.cs` — passive hooks

```text
constructor
├─ Hook ReceiveAchievementProgress
├─ Hook SetAchievementCompleted
└─ Enable hooks

OnReceiveAchievementProgress(...)
├─ receiveHook.Original(...)
└─ progressSource.RecordObservedProgress(...)

OnSetAchievementCompleted(...)
├─ completedHook.Original(...)
└─ progressSource.RecordObservedCompletion(...)
```

Risk: medium-high because hooks are native/interop. Safety: original callback runs first; plugin only caches observed results.

## `ClientAchievementProgressSource.cs` — local observed progress cache

```text
UpdateCache()
├─ Achievement.Instance()
├─ read ProgressRequestState / ProgressAchievementId / ProgressCurrent / ProgressMax
└─ RecordObservedProgress(...)
```

Risk: medium local ClientStructs read. Safety: no request method is called.

## `CosmicClassProgressProvider.cs` — Cosmic score mapping

```text
RefreshCacheFromLiveScores()
└─ TryReadLiveScores()
   ├─ WKSManager.Instance()
   ├─ manager->IsLoaded
   ├─ manager->State.Scores.ToArray()
   └─ SaveScoresToCache(liveScores)
```

```text
GetProgress(achievementId)
├─ GetRule(achievementId)
├─ TryReadLiveScores() or TryReadCachedScores()
├─ CalculateCurrentScore(scores, rule)
└─ AchievementProgress.Numeric(current, target)
```

Risk: medium local ClientStructs read. Safety: no server/network request.
