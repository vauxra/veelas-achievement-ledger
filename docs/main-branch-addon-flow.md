# Main-branch addon big picture and flow

This document maps the current `origin/main` addon shape for Achieve Ex+. It is intentionally implementation-oriented: each tree names the file and method/function chain, then states what the final call reads, writes, returns, or touches.

## Top-level architecture

```text
AchievementTracker/Plugin.cs
└─ Plugin : IDalamudPlugin
   ├─ owns Dalamud services injected with [PluginService]
   │  ├─ IDalamudPluginInterface PluginInterface
   │  ├─ ICommandManager CommandManager
   │  ├─ IDataManager DataManager
   │  ├─ IUnlockState UnlockState
   │  ├─ IClientState ClientState
   │  └─ IFramework Framework
   ├─ owns plugin state/services
   │  ├─ Configuration
   │  ├─ AchievementCatalog
   │  ├─ TrackedAchievementStore
   │  ├─ ClientAchievementProgressSource through IAchievementProgressSource
   │  ├─ CosmicClassProgressProvider
   │  ├─ NativeAchievementNavigator
   │  └─ AchievementProgressService
   ├─ owns windows
   │  ├─ TrackerWindow
   │  └─ ConfigWindow
   └─ owns timing state for user-guided native Achievement opens
      ├─ pendingAchievementUpdateId
      ├─ achievementWindowWasOpenForCurrentUpdate
      ├─ achievementUpdateMinimumOpenAt
      └─ achievementUpdateMaximumOpenAt
```

Safety shape on `main`:

```text
User clicks button
└─ plugin opens the native Achievement UI through AgentAchievement.OpenById(achievementId)
   └─ ClientAchievementProgressSource watches the already-loaded local Achievement progress slot briefly
      └─ cache updates only if the slot reports the same achievementId during the observation window
```

The main branch does **not** call direct achievement-progress request APIs, run background refresh queues, poll server state, capture packets, or send telemetry/backend data.

## Startup and shutdown flow

```text
AchievementTracker/Plugin.cs
└─ Plugin.Plugin()
   ├─ LoadAndNormalizeConfiguration()
   │  ├─ PluginInterface.GetPluginConfig()
   │  │  └─ returns Configuration if present, otherwise null
   │  ├─ new Configuration() when no saved config exists
   │  ├─ Configuration.Normalize()
   │  │  ├─ ensures CosmicClassScoreCache is non-null
   │  │  └─ TrackedAchievementPresetStore.Normalize(TrackedAchievementPresets)
   │  │     └─ sanitizes names, de-dupes names, sanitizes IDs, caps preset count
   │  └─ returns normalized Configuration
   ├─ new AchievementCatalog(DataManager)
   │  └─ stores IDataManager for Lumina Achievement sheet reads
   ├─ CreateTrackedAchievementStore()
   │  ├─ new TrackedAchievementStore()
   │  ├─ Configuration.TrackedAchievementIds.Where(AchievementCatalog.IsManuallyViewable)
   │  │  └─ filters saved IDs against visible native Achievement categories/hide conditions
   │  ├─ TrackedAchievementStore.LoadFrom(filteredIds)
   │  │  └─ clears in-memory list and TryAdd()s up to 20 IDs
   │  └─ returns TrackedAchievementStore
   ├─ new ClientAchievementProgressSource()
   │  └─ creates in-memory observed progress cache + observation deadline table
   ├─ AchievementProgressSource = ClientAchievementProgressSource
   ├─ new CosmicClassProgressProvider(Configuration.CosmicClassScoreCache, SaveConfiguration)
   │  └─ normalizes the saved Cosmic score cache
   ├─ new NativeAchievementNavigator()
   │  └─ wraps ClientStructs AgentAchievement native UI calls
   ├─ new AchievementProgressService(UnlockState, AchievementProgressSource, CosmicClassProgressProvider)
   │  └─ combines completion state, observed progress, and Cosmic score progress into display models
   ├─ new TrackerWindow(this)
   ├─ new ConfigWindow(this)
   ├─ RegisterWindows()
   │  ├─ WindowSystem.AddWindow(TrackerWindow)
   │  └─ WindowSystem.AddWindow(ConfigWindow)
   ├─ RegisterCommand()
   │  └─ CommandManager.AddHandler("/achex", OnCommand)
   └─ RegisterDalamudCallbacks()
      ├─ PluginInterface.UiBuilder.Draw += WindowSystem.Draw
      ├─ PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi
      ├─ PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi
      ├─ Framework.Update += OnFrameworkUpdate
      ├─ ClientState.Login += ResetProgressState
      └─ ClientState.Logout += ResetProgressStateOnLogout
```

```text
AchievementTracker/Plugin.cs
└─ Plugin.Dispose()
   ├─ UnregisterDalamudCallbacks()
   │  ├─ removes UiBuilder.Draw handler
   │  ├─ removes UiBuilder.OpenMainUi handler
   │  ├─ removes UiBuilder.OpenConfigUi handler
   │  ├─ removes Framework.Update handler
   │  ├─ removes ClientState.Login handler
   │  └─ removes ClientState.Logout handler
   ├─ CommandManager.RemoveHandler("/achex")
   └─ WindowSystem.RemoveAllWindows()
```

## Slash command and window routing

```text
AchievementTracker/Plugin.cs
└─ OnCommand(command, args)
   ├─ args.Trim().ToLowerInvariant()
   ├─ "config" | "configure" | "man"
   │  └─ OpenConfigUi(help: false)
   │     └─ AchievementTracker/Windows/ConfigWindow.cs
   │        └─ ConfigWindow.OpenConfig()
   │           ├─ selectedSection = TrackedAchievements
   │           └─ IsOpen = true
   ├─ "help" | "?"
   │  └─ OpenConfigUi(help: true)
   │     └─ ConfigWindow.OpenHelp()
   │        ├─ selectedSection = Help
   │        └─ IsOpen = true
   └─ default
      └─ ToggleMainUi()
         └─ TrackerWindow.Toggle()
            └─ toggles the `/achex` live tracker window
```

Dalamud UI callbacks follow the same helpers:

```text
PluginInterface.UiBuilder.OpenMainUi
└─ Plugin.ToggleMainUi()
   └─ TrackerWindow.Toggle()

PluginInterface.UiBuilder.OpenConfigUi
└─ Plugin.ToggleConfigUi()
   └─ ConfigWindow.Toggle()
```

## Main `/achex` window draw flow

```text
AchievementTracker/Windows/TrackerWindow.cs
└─ TrackerWindow.Draw()
   ├─ plugin.AchievementProgressSource.UpdateCache()
   │  └─ AchievementTracker/Services/ClientAchievementProgressSource.cs
   │     └─ ClientAchievementProgressSource.UpdateCache()
   │        ├─ PruneExpiredObservations()
   │        │  └─ removes observation deadlines whose DateTimeOffset <= now
   │        ├─ returns immediately if no active observation windows exist
   │        ├─ Achievement.Instance()
   │        │  └─ touches local ClientStructs Achievement singleton only
   │        └─ TryRecordObservedSlot(
   │              achievement->ProgressRequestState == Loaded,
   │              achievement->ProgressAchievementId,
   │              achievement->ProgressCurrent,
   │              achievement->ProgressMax,
   │              "Achievement state slot")
   │           ├─ returns false unless loaded, achievementId != 0, max != 0, and ID has active observation
   │           ├─ RecordObservedProgress(achievementId, current, max, source)
   │           │  ├─ writes cachedProgress[achievementId] = ObservedAchievementProgress(current, max, now, source)
   │           │  └─ writes observedCompletions when current >= max
   │           ├─ removes observation deadline for that achievementId
   │           └─ returns true
   ├─ DrawTopButtons()
   └─ DrawTrackedAchievementList()
```

Top buttons:

```text
TrackerWindow.DrawTopButtons()
├─ DrawConfigureButton()
│  └─ ImGui.Button("Configure")
│     └─ plugin.ToggleConfigUi()
│        └─ ConfigWindow.Toggle()
├─ DrawUpdateNextButton()
│  ├─ reads plugin.CanOpenAchievementForUpdate
│  │  └─ Plugin.AchievementUpdateOpenRemaining == TimeSpan.Zero
│  │     └─ Plugin.GetAchievementUpdateOpenAt(now)
│  │        └─ returns next allowed time or clears expired lockout
│  └─ ImGui.Button("Update Next")
│     └─ OpenNextTrackedAchievementForUpdate()
│        └─ see "User-guided update open flow"
├─ DrawCloseAchievementsButton()
│  └─ ImGui.Button("Close Achievements")
│     └─ plugin.NativeAchievementNavigator.CloseAchievements()
│        └─ AchievementTracker/Services/NativeAchievementNavigator.cs
│           └─ NativeAchievementNavigator.CloseAchievements()
│              ├─ AgentAchievement.Instance()
│              ├─ returns false if native agent is null
│              ├─ agent->Hide()
│              │  └─ touches the native Achievement UI agent only
│              └─ returns true
└─ DrawUpdateOpenLockoutStatus()
   └─ reads plugin.AchievementUpdateOpenStatusText
      └─ returns "Request cooldown. (Ns)", "Waiting for data. (Ns)", "Waiting for data.", or empty string
```

Tracked row display:

```text
TrackerWindow.DrawTrackedAchievementList()
└─ plugin.TrackedAchievements.AchievementIds.ToList()
   ├─ empty -> ImGui.TextWrapped("No achievements tracked...")
   └─ foreach achievementId -> DrawAchievement(achievementId)
      ├─ plugin.AchievementCatalog.TryGet(achievementId, out info)
      │  └─ AchievementCatalog.TryGet()
      │     ├─ DataManager.GetExcelSheet<Achievement>()
      │     ├─ sheet.TryGetRow(achievementId, out achievement)
      │     ├─ ToInfo(achievement)
      │     │  └─ returns AchievementInfo(id, name, description, points, categoryPath)
      │     └─ returns false with "Unknown achievement #id" fallback if missing
      ├─ GetProgressText(achievementId)
      │  ├─ AchievementCatalog.TryGetRow(achievementId, out row)
      │  ├─ AchievementProgressService.GetProgress(row)
      │  │  └─ see "Progress display decision tree"
      │  └─ AchievementProgress.ToDisplayText()
      │     └─ returns display text such as "Complete", "1,000 / 5,000", or "Current unavailable / 5,000"
      ├─ GetLastObservedText(achievementId)
      │  └─ ClientAchievementProgressSource.TryGetCachedObservation(id, out observation)
      │     ├─ true -> returns "updated {age}"
      │     └─ false -> returns "not updated yet"
      ├─ DrawRowUpdateButton(achievementId)
      │  └─ reload icon -> OpenNativeAchievementForUpdate(achievementId)
      │     └─ see "User-guided update open flow"
      ├─ DrawRowInspectButton(achievementId)
      │  └─ magnifying glass -> OpenNativeAchievement(achievementId)
      │     └─ plugin.NativeAchievementNavigator.OpenAchievement(achievementId)
      │        └─ AgentAchievement.OpenById(achievementId); returns true/false
      └─ draws info.Name, progressText, and updatedText
```

## User-guided update open flow

This is the main branch's assisted progress-update path. It opens the native Achievement entry and observes local client state; it does not call direct progress request methods.

```text
TrackerWindow.OpenNextTrackedAchievementForUpdate()
└─ GetNextTrackedAchievementId()
   ├─ reads plugin.TrackedAchievements.AchievementIds
   ├─ first unobserved ID where !ClientAchievementProgressSource.TryGetCachedObservation(id, out _)
   │  └─ returns that ID when one exists
   └─ otherwise orders tracked IDs by cached ObservedAt ascending
      └─ returns least-recently observed ID
```

```text
TrackerWindow.OpenNativeAchievementForUpdate(achievementId)
└─ plugin.OpenAchievementForUpdate(achievementId)
   └─ AchievementTracker/Plugin.cs
      └─ Plugin.OpenAchievementForUpdate(achievementId)
         ├─ CanOpenAchievementForUpdate
         │  ├─ false -> returns false; UI shows cooldown message
         │  └─ true -> continues
         ├─ NativeAchievementNavigator.IsAchievementWindowOpen()
         │  └─ AchievementTracker/Services/NativeAchievementNavigator.cs
         │     └─ AgentAchievement.Instance()
         │        └─ returns agent != null && (agent->IsAgentActive() || agent->IsAddonShown())
         ├─ NativeAchievementNavigator.OpenAchievement(achievementId)
         │  └─ AgentAchievement.Instance()
         │     ├─ null -> returns false
         │     ├─ agent->OpenById(achievementId)
         │     │  └─ touches native Achievement UI; does not call RequestAchievementProgress directly
         │     └─ returns true
         ├─ pendingAchievementUpdateId = achievementId
         ├─ achievementWindowWasOpenForCurrentUpdate = previous IsAchievementWindowOpen() result
         ├─ achievementUpdateMinimumOpenAt = now + GetAchievementUpdateMinimumLockout(wasOpen)
         │  └─ wasOpen ? 1 second : 6 seconds
         ├─ achievementUpdateMaximumOpenAt = now + 15 seconds
         ├─ ClientAchievementProgressSource.BeginObservation(achievementId, 15 seconds)
         │  ├─ ignores achievementId == 0 or non-positive duration
         │  ├─ PruneExpiredObservations()
         │  └─ observationDeadlines[achievementId] = now + duration
         └─ returns true
```

The config window uses the same update path:

```text
AchievementTracker/Windows/ConfigWindow.cs
└─ DrawTrackedUpdateButton(achievementId)
   └─ reload icon -> OpenAchievementForUpdate(achievementId)
      └─ plugin.OpenAchievementForUpdate(achievementId)
         └─ same chain as above
```

## Update-open lockout/status flow

```text
AchievementTracker/Plugin.cs
└─ AchievementUpdateOpenRemaining
   └─ GetAchievementUpdateOpenAt(now) - now
      └─ clamps negative values to TimeSpan.Zero
```

```text
Plugin.GetAchievementUpdateOpenAt(now)
├─ if pendingAchievementUpdateId == 0 OR now >= achievementUpdateMaximumOpenAt
│  ├─ ClearAchievementUpdateLockout()
│  └─ returns DateTimeOffset.MinValue
├─ if now < achievementUpdateMinimumOpenAt
│  └─ returns achievementUpdateMinimumOpenAt
├─ if achievementWindowWasOpenForCurrentUpdate
│  ├─ ClearAchievementUpdateLockout()
│  └─ returns DateTimeOffset.MinValue
├─ if ClientAchievementProgressSource.HasActiveObservation(pendingAchievementUpdateId)
│  ├─ PruneExpiredObservations()
│  └─ returns achievementUpdateMaximumOpenAt
└─ otherwise
   ├─ ClearAchievementUpdateLockout()
   └─ returns DateTimeOffset.MinValue
```

```text
Plugin.AchievementUpdateOpenStatusText
├─ if GetAchievementUpdateOpenAt(now) <= now
│  └─ returns empty string
├─ if now < achievementUpdateMinimumOpenAt AND native window was closed before the update
│  └─ returns "Waiting for data. (Ns)" using maximum timeout remaining
├─ if now < achievementUpdateMinimumOpenAt AND native window was already open
│  └─ returns "Request cooldown. (Ns)" using minimum cooldown remaining
├─ if native window was already open after minimum cooldown
│  └─ returns empty string
├─ if pending ID still has active observation
│  └─ returns "Waiting for data. (Ns)" using maximum timeout remaining
└─ returns "Waiting for data."
```

## Progress display decision tree

```text
AchievementTracker/Services/AchievementProgressService.cs
└─ AchievementProgressService.GetProgress(Achievement achievement)
   ├─ cosmicClassProgressProvider?.Handles(achievement.RowId) == true
   │  └─ CosmicClassProgressProvider.GetProgress(achievement.RowId)
   │     └─ see "Cosmic Class score flow"
   ├─ GetRequiredTarget(achievement)
   │  ├─ achievement.Data.FirstOrDefault()
   │  ├─ if firstDataRow.RowId > 1 and <= int.MaxValue -> returns target count
   │  └─ otherwise -> returns null
   ├─ IsComplete(achievement) OR progressSource.IsObservedComplete(achievement.RowId)
   │  ├─ IsComplete()
   │  │  └─ UnlockState.IsAchievementListLoaded && UnlockState.IsAchievementComplete(achievement)
   │  ├─ progressSource.IsObservedComplete()
   │  │  └─ ClientAchievementProgressSource observedCompletions.Contains(achievementId)
   │  ├─ target exists -> returns AchievementProgress.Numeric(target, target)
   │  └─ no target -> returns AchievementProgress.Complete()
   ├─ progressSource.TryGetProgress(achievement.RowId, out current, out max)
   │  └─ ClientAchievementProgressSource.TryGetProgress()
   │     ├─ UpdateCache()
   │     ├─ cachedProgress.TryGetValue(achievementId, out progress)
   │     ├─ true -> returns current/max from local observation cache
   │     └─ false -> returns false with current=0, max=0
   ├─ if !UnlockState.IsAchievementListLoaded
   │  ├─ target exists -> returns AchievementProgress.TargetKnown(target)
   │  └─ no target -> returns AchievementProgress.CompletionListNotLoaded()
   └─ completion list loaded but not complete and no numeric observation
      ├─ target exists -> returns AchievementProgress.TargetKnown(target)
      └─ no target -> returns AchievementProgress.Incomplete()
```

```text
AchievementTracker/Models/AchievementProgress.cs
└─ AchievementProgress.ToDisplayText()
   ├─ CompletionListNotLoaded -> "Open Achievements to load status"
   ├─ Complete -> "Complete"
   ├─ Incomplete -> "Incomplete"
   ├─ Numeric(current, required) -> "{current:N0} / {required:N0}"
   ├─ TargetKnown(required) -> "Current unavailable / {required:N0}"
   ├─ DataNotAvailable -> "Data not available"
   └─ otherwise -> "Progress unavailable"
```

## Config window flow

```text
AchievementTracker/Windows/ConfigWindow.cs
└─ ConfigWindow.Draw()
   ├─ DrawHeader()
   │  └─ ImGui.Button("Open Achieve Ex+") -> plugin.OpenMainUi() -> TrackerWindow.IsOpen = true
   ├─ DrawLeftNavigation()
   │  ├─ DrawNavItem("Tracked Achievements") -> selectedSection = TrackedAchievements
   │  └─ DrawNavItem("Help") -> selectedSection = Help
   └─ DrawSelectedPage()
      ├─ TrackedAchievements -> DrawTrackedAchievementsPage()
      └─ Help -> DrawHelp()
```

Tracked Achievements page:

```text
ConfigWindow.DrawTrackedAchievementsPage()
├─ DrawPresetControls()
│  ├─ EnsureSelectedPresetIsValid()
│  │  ├─ TrackedAchievementPresetStore.Normalize(Configuration.TrackedAchievementPresets)
│  │  └─ chooses first valid preset when selectedPresetName no longer exists
│  ├─ DrawPresetNameInput()
│  │  └─ ImGui.InputTextWithHint -> TrackedAchievementPresetStore.SanitizeName(input)
│  ├─ DrawPresetSaveButton()
│  │  └─ TrackedAchievementPresetStore.SavePreset(presets, presetNameInput, trackedIds, out savedName)
│  │     ├─ SanitizeName(rawName)
│  │     ├─ SanitizeAchievementIds(achievementIds)
│  │     ├─ updates existing preset if name already exists
│  │     ├─ otherwise appends new TrackedAchievementPreset until MaxPresets
│  │     └─ returns true/false; on true ConfigWindow calls plugin.SaveConfiguration()
│  ├─ DrawPresetPicker()
│  │  └─ selecting item -> LoadSelectedPreset()
│  ├─ DrawPresetReadButton()
│  │  └─ LoadSelectedPreset()
│  ├─ DrawPresetRenameButton()
│  │  └─ TrackedAchievementPresetStore.RenamePreset(...)
│  │     └─ changes preset.Name when target exists and new name is valid/non-conflicting
│  └─ DrawPresetDeleteButton()
│     └─ TrackedAchievementPresetStore.DeletePreset(...)
│        └─ removes matching preset from Configuration.TrackedAchievementPresets
├─ left child DrawTrackedManagement()
│  └─ see "Tracked list management"
└─ right child DrawSearchAndAdd()
   └─ see "Search/add flow"
```

Loading a preset:

```text
ConfigWindow.LoadSelectedPreset()
├─ TrackedAchievementPresetStore.FindPreset(Configuration.TrackedAchievementPresets, selectedPresetName)
│  └─ returns preset or null by case-insensitive name
├─ if null -> returns
├─ plugin.TrackedAchievements.LoadFrom(preset.AchievementIds.Where(AchievementCatalog.IsManuallyViewable))
│  ├─ clears in-memory tracked list
│  └─ TryAdd()s up to 20 manually viewable IDs
└─ plugin.SaveTrackedAchievements()
   ├─ Configuration.TrackedAchievementIds = TrackedAchievementStore.ToConfigList()
   └─ Configuration.Save()
      └─ PluginInterface.SavePluginConfig(Configuration)
```

Tracked list management:

```text
ConfigWindow.DrawTrackedManagement()
└─ foreach plugin.TrackedAchievements.AchievementIds -> DrawTrackedAchievementRow(achievementId)
   ├─ DrawMoveButton("Top")
   │  └─ TrackedAchievementStore.MoveToTop(achievementId)
   │     └─ removes ID at current index and inserts it at index 0; returns true/false
   ├─ DrawMoveButton("Up")
   │  └─ TrackedAchievementStore.MoveUp(achievementId)
   │     └─ swaps ID with previous slot; returns true/false
   ├─ DrawMoveButton("Down")
   │  └─ TrackedAchievementStore.MoveDown(achievementId)
   │     └─ swaps ID with next slot; returns true/false
   ├─ DrawMoveButton("Bottom")
   │  └─ TrackedAchievementStore.MoveToBottom(achievementId)
   │     └─ removes ID and appends it; returns true/false
   ├─ any successful move -> plugin.SaveTrackedAchievements()
   │  └─ writes ordered IDs to Configuration.TrackedAchievementIds and SavePluginConfig
   ├─ DrawTrackedRemoveButton(achievementId)
   │  └─ RemoveTrackedAchievement(achievementId)
   │     ├─ TrackedAchievementStore.Remove(achievementId)
   │     │  └─ removes ID from in-memory list; returns true/false
   │     └─ on true plugin.SaveTrackedAchievements()
   ├─ DrawTrackedUpdateButton(achievementId)
   │  └─ plugin.OpenAchievementForUpdate(achievementId)
   ├─ DrawInspectButton(achievementId)
   │  └─ plugin.NativeAchievementNavigator.OpenAchievement(achievementId)
   └─ DrawManagedAchievement(achievementId)
      ├─ AchievementCatalog.TryGet(achievementId, out info)
      ├─ ImGui.TextWrapped(info.Name)
      ├─ DrawCosmicProgressIfAvailable(achievementId)
      │  └─ if CosmicClassProgressProvider.Handles(id): AchievementProgressService.GetProgress(row).ToDisplayText()
      └─ DrawCategoryPath(info.CategoryName)
```

Search/add flow:

```text
ConfigWindow.DrawSearchAndAdd()
├─ DrawHideCompletedCheckbox()
│  └─ ImGui.Checkbox("Hide completed", ref hideCompleted)
│     └─ writes Configuration.HideCompletedInSearch and plugin.SaveConfiguration()
├─ DrawSearchInput()
│  ├─ ImGui.InputText("##AchievementSearch", ref searchQuery, 128)
│  └─ Clear button -> searchQuery = string.Empty
├─ if searchQuery.Trim().Length < 2 -> returns helper text
├─ GetVisibleSearchResults()
│  ├─ AchievementCatalog.Search(searchQuery, 200)
│  │  ├─ DataManager.GetExcelSheet<Achievement>()
│  │  ├─ sheet.Select(ToInfo)
│  │  ├─ filters blank names
│  │  ├─ filters AchievementCatalog.IsManuallyViewable(info.Id)
│  │  │  ├─ TryGetRow(id, out achievement)
│  │  │  ├─ requires valid category and category.HideCategory == false
│  │  │  ├─ rejects hideCondition.HideAchievement or hideCondition.HideName
│  │  │  └─ requires non-blank achievement.Name
│  │  ├─ filters by name/category contains query
│  │  ├─ orders by name
│  │  └─ returns up to 200 AchievementInfo rows
│  ├─ when Configuration.HideCompletedInSearch: filters !IsComplete(result.Id)
│  │  └─ AchievementProgressService.IsComplete(row)
│  │     └─ UnlockState.IsAchievementListLoaded && UnlockState.IsAchievementComplete(row)
│  ├─ Take(25)
│  └─ returns List<AchievementInfo>
└─ foreach result -> DrawSearchResultRow(result)
   ├─ alreadyTracked = TrackedAchievements.AchievementIds.Contains(result.Id)
   ├─ canAdd = count < 20 && !alreadyTracked
   ├─ canAdd -> DrawSearchAddButton(result.Id)
   │  └─ Add button
   │     ├─ AchievementCatalog.IsManuallyViewable(result.Id)
   │     ├─ TrackedAchievementStore.TryAdd(result.Id)
   │     │  ├─ false if duplicate or already at MaxTrackedAchievements
   │     │  ├─ otherwise appends ID
   │     │  └─ returns true
   │     └─ on true plugin.SaveTrackedAchievements()
   ├─ alreadyTracked -> DrawSearchRemoveButton(result.Id)
   │  └─ removes and saves through RemoveTrackedAchievement(result.Id)
   └─ full list -> DrawSearchFullLabel(result.Id)
      └─ shows Full plus inspect button
```

## Cosmic Class score flow

Cosmic score progress is read-only local state. It is refreshed from live WKS/Cosmic data only while the character is in Sinus Ardorum, and it persists the last observed 11 score values in plugin config.

```text
AchievementTracker/Plugin.cs
└─ Framework.Update event
   └─ Plugin.OnFrameworkUpdate(IFramework framework)
      ├─ ClientAchievementProgressSource.UpdateCache()
      │  └─ updates user-guided achievement observation cache when an observation window is active
      └─ RefreshCosmicCacheFromLiveState()
         ├─ IsInSinusArdorum()
         │  └─ ClientState.TerritoryType == 1237
         ├─ if not in zone
         │  ├─ nextCosmicCacheRefreshAt = DateTimeOffset.MinValue
         │  └─ returns
         ├─ CosmicCacheRefreshIsDue()
         │  └─ DateTimeOffset.UtcNow >= nextCosmicCacheRefreshAt
         ├─ nextCosmicCacheRefreshAt = now + 30 seconds
         └─ CosmicClassProgressProvider.RefreshCacheFromLiveScores()
            └─ CosmicClassProgressProvider.TryReadLiveScores(saveWhenAvailable: true)
               ├─ WKSManager.Instance()
               ├─ returns null if manager is null or !manager->IsLoaded
               ├─ manager->State.Scores.ToArray()
               │  └─ touches local WKS/Cosmic ClientStructs score array only
               ├─ requires at least 11 scores
               ├─ clamps first 11 scores to non-negative ints
               ├─ if scores differ from cache -> SaveScoresToCache(liveScores)
               │  ├─ cache.Scores = liveScores.ToList()
               │  ├─ cache.UpdatedAtUtc = DateTimeOffset.UtcNow
               │  └─ saveCache()
               │     └─ Plugin.SaveConfiguration()
               │        ├─ Configuration.Normalize()
               │        └─ Configuration.Save() -> PluginInterface.SavePluginConfig(Configuration)
               └─ returns int[] liveScores or null
```

Display path for Cosmic achievement rows:

```text
AchievementProgressService.GetProgress(achievement)
└─ CosmicClassProgressProvider.Handles(achievement.RowId)
   └─ GetRule(achievementId) is not null
      └─ achievement IDs 3702-3739 map to Single/Any/Every class-score rules
└─ CosmicClassProgressProvider.GetProgress(achievementId)
   ├─ GetRule(achievementId)
   │  └─ returns CosmicAchievementRule(scoreIndexes, targetScore, aggregation) or null
   ├─ TryReadLiveScores() ?? TryReadCachedScores()
   │  ├─ live read may update saved cache as above
   │  └─ cached read requires cache.UpdatedAtUtc and exactly 11 saved scores
   ├─ no live/cache data -> AchievementProgress.DataNotAvailable()
   ├─ CalculateCurrentScore(scores, rule)
   │  ├─ Every rules use minimum score among selected indexes
   │  └─ Single/Any rules use maximum score among selected indexes
   └─ returns AchievementProgress.Numeric(current, targetScore)
```

## Persistence and reset flow

```text
AchievementTracker/Configuration.cs
└─ Configuration : IPluginConfiguration
   ├─ Version
   ├─ TrackedAchievementIds
   ├─ TrackedAchievementPresets
   ├─ CosmicClassScoreCache
   │  ├─ Scores
   │  └─ UpdatedAtUtc
   └─ HideCompletedInSearch
```

Writes to Dalamud plugin config:

```text
Plugin.SaveTrackedAchievements()
├─ Configuration.TrackedAchievementIds = TrackedAchievementStore.ToConfigList()
└─ Configuration.Save()
   └─ PluginInterface.SavePluginConfig(Configuration)

Plugin.SaveConfiguration()
├─ Configuration.Normalize()
└─ Configuration.Save()
   └─ PluginInterface.SavePluginConfig(Configuration)
```

Login/logout reset only clears observed achievement-progress state, not the saved tracked list/presets/Cosmic cache:

```text
AchievementTracker/Plugin.cs
├─ ClientState.Login += ResetProgressState
│  └─ ResetProgressState()
│     └─ AchievementProgressSource.ClearCache()
│        └─ ClientAchievementProgressSource.ClearCache()
│           ├─ cachedProgress.Clear()
│           ├─ observedCompletions.Clear()
│           └─ observationDeadlines.Clear()
└─ ClientState.Logout += ResetProgressStateOnLogout
   └─ ResetProgressState()
      └─ same ClearCache() chain
```

## File index

```text
AchievementTracker/Plugin.cs
└─ Entrypoint, service construction, command routing, window registration, update-open lockout, Framework/ClientState callbacks, Cosmic cache refresh scheduling.

AchievementTracker/Windows/TrackerWindow.cs
└─ Main `/achex` live tracker UI: top buttons, tracked rows, Update Next choice, per-row update/inspect buttons, progress/last-observed display.

AchievementTracker/Windows/ConfigWindow.cs
└─ Config UI: navigation, tracked list management, presets, search/add/remove, Help page, config-window update/inspect buttons.

AchievementTracker/Services/AchievementCatalog.cs
└─ Lumina Achievement sheet lookup/search and manual-viewability filtering.

AchievementTracker/Services/AchievementProgressService.cs
└─ Converts completion state, target counts, observed local progress, and Cosmic score progress into AchievementProgress models.

AchievementTracker/Services/ClientAchievementProgressSource.cs
└─ Bounded observation windows and in-memory cache for the local Achievement progress slot.

AchievementTracker/Services/NativeAchievementNavigator.cs
└─ User-triggered native Achievement UI open/close wrapper around AgentAchievement.

AchievementTracker/Services/CosmicClassProgressProvider.cs
└─ Cosmic Class achievement ID-to-score mapping plus live/cached local WKS score reads.

AchievementTracker/Services/TrackedAchievementStore.cs
└─ In-memory tracked achievement ID list: add/remove/reorder/load/export.

AchievementTracker/Services/TrackedAchievementPresetStore.cs
└─ Preset sanitization, normalization, save, rename, delete, and lookup.

AchievementTracker/Configuration.cs
└─ Persistent plugin config and SavePluginConfig wrapper.

AchievementTracker/Models/*.cs
└─ Data shapes for achievement info, progress display state, tracked presets, tracked rows, and Cosmic score cache.
```
