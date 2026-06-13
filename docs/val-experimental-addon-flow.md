# Val-experimental addon big picture and flow

This document maps the current `val-experimental` branch for Achieve Ex+. It is intentionally implementation-oriented: each tree names the file and method/function chain, then states what the final call reads, writes, returns, queues, or touches.

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
   │  ├─ IGameInteropProvider GameInteropProvider
   │  ├─ IFramework Framework
   │  ├─ IPluginLog PluginLog
   │  ├─ IGameGui GameGui
   │  ├─ IChatGui ChatGui
   │  └─ IObjectTable ObjectTable
   ├─ owns plugin state/services
   │  ├─ Configuration
   │  ├─ AchievementCatalog
   │  ├─ TrackedAchievementStore
   │  ├─ ClientAchievementProgressSource through IAchievementProgressSource
   │  ├─ CosmicClassProgressProvider
   │  ├─ NativeAchievementNavigator
   │  ├─ AchievementProgressUpdater
   │  └─ AchievementProgressService
   ├─ owns windows
   │  ├─ TrackerWindow
   │  └─ ConfigWindow
   ├─ owns experimental observers
   │  ├─ PassiveAchievementProgressObserver
   │  └─ AchievementActivityUpdateObserver
   └─ owns native Achievement window recovery flags
      ├─ pendingNativeAchievementScaleReset
      ├─ pendingNativeAchievementScaleResetUntil
      ├─ pendingNativeAchievementInspectionRestore
      └─ pendingNativeAchievementInspectionRestoreUntil
```

Experimental behavior shape:

```text
Manual row update / Update All / timed auto update / enabled event trigger
└─ Plugin.EnqueueUpdateOne/All(...)
   └─ AchievementProgressUpdater.EnqueueUpdateAll(...)
      └─ AchievementProgressRequestScheduler stores due native Achievement opens
         └─ Framework.Update -> AchievementProgressUpdater.Tick()
            └─ NativeAchievementNavigator.OpenAchievement(id)
               └─ AgentAchievement.OpenById(id)
                  └─ NativeAchievementNavigator.TryParkAchievementWindow()
                     └─ IGameGui.GetAddonByName("Achievement", 1)
                        └─ AtkUnitBase.SetScale(0.1375) + SetPosition(20, 20)
                           └─ ClientAchievementProgressSource reads the local Achievement progress slot
                              └─ batch closes/restores/leaves native window according to settings and prior state
```

Important branch-specific caveats:

```text
val-experimental
├─ does not call Achievement.RequestAchievementProgress directly
├─ does queue native Achievement UI opens from timed/event/manual triggers
├─ does park/rescale/move the native Achievement window during queued updates
├─ does use isolated ClientStructs reads for Achievement progress and WKS/Cosmic scores
├─ does install passive hooks for ReceiveAchievementProgress and SetAchievementCompleted
└─ is experimental/private-testing behavior, not a public-safe mainline flow
```

## Startup and shutdown flow

```text
AchievementTracker/Plugin.cs
└─ Plugin.Plugin()
   ├─ PluginInterface.GetPluginConfig() as Configuration ?? new Configuration()
   ├─ Configuration.NormalizeAutoUpdateSettings()
   │  ├─ migrates ExperimentalAutoUpdateIntervalMinutes -> ExperimentalAutoUpdateIntervalSeconds
   │  ├─ clamps ExperimentalAutoUpdateIntervalSeconds to 1..86400
   │  ├─ clamps ExperimentalUpdateSpacingSeconds to 0..3600
   │  ├─ if timed auto update and event triggers are both enabled -> disables event triggers
   │  ├─ ensures CosmicClassScoreCache is non-null
   │  └─ TrackedAchievementPresetStore.Normalize(TrackedAchievementPresets)
   ├─ new AchievementCatalog(DataManager)
   │  └─ stores IDataManager for Lumina Achievement sheet reads
   ├─ new TrackedAchievementStore()
   │  └─ LoadFrom(Configuration.TrackedAchievementIds.Where(AchievementCatalog.IsManuallyViewable))
   │     └─ filters saved IDs to manually viewable Achievement rows and caps at 20
   ├─ new ClientAchievementProgressSource(DebugLog)
   │  └─ stores in-memory cached progress/completion observations and optional debug logger
   ├─ AchievementProgressSource = ClientAchievementProgressSource
   ├─ new CosmicClassProgressProvider(Configuration.CosmicClassScoreCache, SaveConfiguration)
   │  └─ normalizes saved Cosmic score cache
   ├─ new NativeAchievementNavigator(GameGui)
   │  └─ wraps AgentAchievement plus IGameGui addon parking/restoring/resetting
   ├─ new AchievementProgressUpdater(...)
   │  ├─ receives progress source and native navigator
   │  ├─ receives delegates for included Auto IDs, auto-enabled, interval seconds, spacing seconds, restore-after-updates
   │  └─ owns AchievementProgressRequestScheduler and active native request state
   ├─ new AchievementProgressService(UnlockState, AchievementProgressSource, CosmicClassProgressProvider)
   ├─ new TrackerWindow(this)
   ├─ new ConfigWindow(this)
   ├─ InstallPassiveAchievementObserver()
   │  └─ new PassiveAchievementProgressObserver(GameInteropProvider, ClientAchievementProgressSource, TriggerOnAchievementCompletion)
   │     ├─ HookFromAddress(ReceiveAchievementProgress, OnReceiveAchievementProgress)
   │     ├─ HookFromAddress(SetAchievementCompleted, OnSetAchievementCompleted)
   │     ├─ receiveHook.Enable()
   │     └─ completedHook.Enable()
   ├─ InstallActivityUpdateObserver()
   │  └─ new AchievementActivityUpdateObserver(ChatGui, candidate IDs, category lookup, current job, trigger-enabled check, enqueue callback, DebugLog)
   │     ├─ ChatGui.LogMessage += OnLogMessage
   │     └─ ChatGui.ChatMessageUnhandled += OnChatMessageUnhandled
   ├─ WindowSystem.AddWindow(TrackerWindow)
   ├─ WindowSystem.AddWindow(ConfigWindow)
   ├─ CommandManager.AddHandler("/achex", OnCommand)
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
   ├─ removes UiBuilder, Framework, ClientState, and command handlers
   ├─ passiveAchievementProgressObserver?.Dispose()
   │  └─ disposes receiveHook and completedHook
   ├─ activityUpdateObserver?.Dispose()
   │  └─ unsubscribes ChatGui.LogMessage and ChatGui.ChatMessageUnhandled
   └─ WindowSystem.RemoveAllWindows()
```

## Slash command and window routing

```text
AchievementTracker/Plugin.cs
└─ OnCommand(command, args)
   ├─ "config" | "configure" | "man"
   │  └─ OpenConfigUi(help: false)
   │     └─ AchievementTracker/Windows/ConfigWindow.cs
   │        └─ ConfigWindow.OpenConfig()
   │           ├─ selectedSection = AutoUpdate
   │           └─ IsOpen = true
   ├─ "help" | "?"
   │  └─ OpenConfigUi(help: true)
   │     └─ ConfigWindow.OpenHelp()
   │        ├─ selectedSection = Help
   │        └─ IsOpen = true
   └─ default
      └─ ToggleMainUi()
         └─ TrackerWindow.Toggle()
            └─ toggles the `Achieve Ex+` live window
```

## Main `/achex` window flow

```text
AchievementTracker/Windows/TrackerWindow.cs
└─ TrackerWindow.Draw()
   ├─ plugin.AchievementProgressSource.UpdateCache()
   │  └─ ClientAchievementProgressSource.UpdateCache()
   │     ├─ Achievement.Instance()
   │     ├─ reads ProgressRequestState, ProgressAchievementId, ProgressCurrent, ProgressMax
   │     ├─ logs AchieveEx DebugTrace ProgressSlot when changed and debug logging enabled
   │     └─ if Loaded and max != 0 -> cachedProgress[achievementId] = ObservedAchievementProgress(...)
   ├─ Configure button
   │  └─ plugin.ToggleConfigUi() -> ConfigWindow.Toggle()
   ├─ Update All button
   │  └─ plugin.EnqueueUpdateAllTracked("manual-update-all")
   │     └─ AchievementProgressUpdater.EnqueueUpdateAll(TrackedAchievements.AchievementIds, "manual-update-all")
   ├─ Stop Update Tasks button
   │  └─ plugin.StopAutoUpdateAndClearQueue()
   │     ├─ Configuration.ExperimentalAutoUpdateEnabled = false
   │     ├─ SaveConfiguration()
   │     ├─ AchievementProgressUpdater.Clear()
   │     └─ logs AchieveEx DebugTrace AutoUpdateStopped
   ├─ Auto update checkbox
   │  ├─ writes Configuration.ExperimentalAutoUpdateEnabled
   │  ├─ SaveConfiguration()
   │  └─ ResetAutoUpdateCountdownIfActive()
   ├─ DrawQueueStatus()
   │  ├─ reads AchievementProgressUpdater.PendingCount + NextDueAt
   │  └─ reads AchievementProgressUpdater.NextAutoUpdateAt
   └─ foreach tracked achievementId -> DrawAchievement(achievementId)
      ├─ AchievementCatalog.TryGet(id, out info)
      ├─ AchievementCatalog.TryGetRow(id, out row)
      ├─ AchievementProgressService.GetProgress(row).ToDisplayText()
      ├─ reload icon -> plugin.EnqueueUpdateOne(id, "manual-row-update")
      ├─ magnifying glass -> plugin.OpenNativeAchievementForInspection(id)
      └─ ClientAchievementProgressSource.TryGetObservation(id, out observation)
         ├─ true -> displays updated age
         └─ false -> displays "not updated yet"
```

## Config window flow

```text
AchievementTracker/Windows/ConfigWindow.cs
└─ ConfigWindow.Draw()
   ├─ Open Achieve Ex+ button -> plugin.OpenMainUi() -> TrackerWindow.IsOpen = true
   ├─ displays flash/UI-motion warning text
   ├─ DrawLeftNavigation()
   │  ├─ Auto update
   │  ├─ Tracked Achievements
   │  └─ Help
   └─ selected page
      ├─ AutoUpdate -> DrawAutoUpdatePage()
      ├─ TrackedAchievements -> DrawTrackedAchievementsPage()
      └─ Help -> DrawHelp()
```

Auto update page:

```text
ConfigWindow.DrawAutoUpdatePage()
├─ left column DrawExperimentalAutoUpdateSettings()
│  ├─ Enable auto update checkbox
│  │  ├─ writes Configuration.ExperimentalAutoUpdateEnabled
│  │  ├─ if enabled: Configuration.TriggerAutoUpdatesEnabled = false
│  │  ├─ if enabled: plugin.ClearUpdateQueue("auto-update-enabled")
│  │  ├─ SaveConfiguration()
│  │  └─ ResetAutoUpdateCountdownIfActive()
│  ├─ Stop Update Tasks button -> StopAutoUpdateAndClearQueue()
│  ├─ Seconds between auto update cycles input
│  │  ├─ clamps to 1..86400
│  │  ├─ SaveConfiguration()
│  │  └─ ResetAutoUpdateCountdownIfActive()
│  ├─ Base seconds between update calls input
│  │  ├─ clamps to 0..3600
│  │  ├─ SaveConfiguration()
│  │  └─ ResetAutoUpdateCountdownIfActive()
│  ├─ Debug prints checkbox
│  │  └─ writes Configuration.ExperimentalDebugLoggingEnabled
│  ├─ Restore Achievement window scale/position after updates checkbox
│  │  └─ writes Configuration.RestoreNativeAchievementWindowAfterUpdates
│  └─ Reset native Achievement window scale button
│     └─ plugin.ResetNativeAchievementWindowScale()
└─ right column DrawTriggerAutoUpdateSettings()
   ├─ Enable event-triggered updates checkbox
   │  ├─ writes Configuration.TriggerAutoUpdatesEnabled
   │  ├─ if enabled: Configuration.ExperimentalAutoUpdateEnabled = false
   │  ├─ if enabled: plugin.ClearUpdateQueue("event-trigger-enabled")
   │  └─ SaveConfiguration()
   ├─ Event triggers only update achievements checked Auto checkbox
   │  └─ writes Configuration.TriggerUpdatesRespectAutoUpdateSelection
   ├─ Achievement completion events mark tracked achievements complete checkbox
   │  └─ writes Configuration.TriggerOnAchievementCompletion
   ├─ All event types checkbox
   │  └─ toggles all Miner/Botanist/Fisher/Crafter group and child flags
   ├─ All Miner -> Mining + Quarrying
   ├─ All Botanist -> Logging + Harvesting
   ├─ All Fisher -> Fishing + Spearfishing
   └─ All Crafters -> Successful synthesis + Crafting log completion
```

Tracked Achievements page:

```text
ConfigWindow.DrawTrackedAchievementsPage()
├─ DrawPresetControls()
│  ├─ preset name input -> TrackedAchievementPresetStore.SanitizeName(input)
│  ├─ save -> TrackedAchievementPresetStore.SavePreset(...)
│  │  └─ saves current tracked IDs into Configuration.TrackedAchievementPresets, then SaveConfiguration()
│  ├─ preset picker selection -> LoadSelectedPreset()
│  ├─ read -> LoadSelectedPreset()
│  ├─ rename -> TrackedAchievementPresetStore.RenamePreset(...), then SaveConfiguration()
│  └─ delete -> TrackedAchievementPresetStore.DeletePreset(...), then SaveConfiguration()
├─ DrawAutoUpdateBulkControls()
│  ├─ Include all tracked in auto update
│  │  ├─ Configuration.AutoUpdateAchievementIds = current tracked IDs
│  │  ├─ SaveConfiguration()
│  │  └─ ResetAutoUpdateCountdownIfActive()
│  └─ Include none
│     ├─ Configuration.AutoUpdateAchievementIds.Clear()
│     ├─ SaveConfiguration()
│     └─ ResetAutoUpdateCountdownIfActive()
├─ left column DrawTrackedManagement()
│  └─ foreach tracked achievementId
│     ├─ Top/Up/Down/Bottom -> TrackedAchievementStore.Move*(), then SaveTrackedAchievements()
│     ├─ remove -> RemoveTrackedAchievement(id)
│     │  ├─ TrackedAchievementStore.Remove(id)
│     │  ├─ removes id from Configuration.AutoUpdateAchievementIds
│     │  ├─ SaveTrackedAchievements()
│     │  ├─ SaveConfiguration()
│     │  └─ ResetAutoUpdateCountdownIfActive() if auto entry was removed
│     ├─ reload icon -> plugin.EnqueueUpdateOne(id, "config-row-update")
│     ├─ magnifying glass -> plugin.OpenNativeAchievementForInspection(id)
│     ├─ Auto checkbox -> DrawAutoUpdateIncludeCheckbox(id)
│     │  ├─ adds/removes id in Configuration.AutoUpdateAchievementIds
│     │  ├─ SaveConfiguration()
│     │  └─ ResetAutoUpdateCountdownIfActive()
│     └─ DrawManagedAchievement(id)
│        ├─ AchievementCatalog.TryGet(id, out info)
│        ├─ DrawCosmicProgressIfAvailable(id)
│        └─ draws category path
└─ right column DrawSearchAndAdd()
   ├─ Hide completed checkbox -> writes Configuration.HideCompletedInSearch and SaveConfiguration()
   ├─ search input + Clear
   ├─ AchievementCatalog.Search(query, 200)
   │  └─ see "Catalog/search flow"
   ├─ filters completed when HideCompletedInSearch is true
   └─ foreach result
      ├─ Add button
      │  ├─ AchievementCatalog.IsManuallyViewable(result.Id)
      │  ├─ TrackedAchievementStore.TryAdd(result.Id)
      │  ├─ SaveTrackedAchievements()
      │  ├─ adds result.Id to Configuration.AutoUpdateAchievementIds by default if missing
      │  ├─ SaveConfiguration()
      │  └─ ResetAutoUpdateCountdownIfActive()
      ├─ already tracked -> remove button -> RemoveTrackedAchievement(result.Id)
      └─ magnifying glass -> plugin.OpenNativeAchievementForInspection(result.Id)
```

## Queued native Achievement update flow

```text
Plugin.EnqueueUpdateAllTracked(reason)
└─ AchievementProgressUpdater.EnqueueUpdateAll(TrackedAchievements.AchievementIds, reason)

Plugin.EnqueueUpdateAchievements(ids, reason)
└─ AchievementProgressUpdater.EnqueueUpdateAll(ids, reason)

Plugin.EnqueueUpdateOne(id, reason)
└─ AchievementProgressUpdater.EnqueueUpdateAll([id], reason)
```

```text
AchievementTracker/Services/AchievementProgressUpdater.cs
└─ AchievementProgressUpdater.EnqueueUpdateAll(achievementIds, reason)
   ├─ ids = non-zero distinct IDs
   ├─ if reason is manual-update-all or auto-update
   │  └─ filters out IDs observed within ClientAchievementProgressSource.RecentlyObservedUpdateAllSkipThreshold
   │     └─ threshold = 30 seconds
   ├─ if no IDs remain -> debug log QueueSkip and return
   ├─ baseSpacingSeconds = clamp(Configuration.ExperimentalUpdateSpacingSeconds, 0..3600)
   ├─ scheduler.EnqueueUpdateAll(ids, reason, TimeSpan.FromSeconds(baseSpacingSeconds))
   │  └─ AchievementProgressRequestScheduler.EnqueueUpdateAll(...)
   │     ├─ starts cursor at now or existing nextBatchCursor
   │     ├─ skips duplicate IDs and IDs already pending
   │     ├─ applies 5-second same-achievement backoff from lastRequestedAt
   │     ├─ pendingRequests.Add(ScheduledAchievementProgressRequest(id, dueAt, reason))
   │     ├─ cursor = dueAt + baseSpacing + jitter
   │     │  └─ jitter defaults to random 1-2 seconds even when base spacing is 0
   │     ├─ nextBatchCursor = cursor
   │     └─ pendingRequests.Sort by DueAt
   └─ debug log QueueUpdateAll count/pending/spacing/jitter/backoff
```

```text
Framework.Update
└─ Plugin.OnFrameworkUpdate(framework)
   ├─ AchievementProgressUpdater.Tick()
   │  ├─ ClientAchievementProgressSource.UpdateCache()
   │  ├─ MaybeEnqueueAutoUpdate(now)
   │  ├─ ProcessActiveNativeRequest(now)
   │  ├─ if no active request and scheduler.TryTakeDueRequest(now, out request)
   │  │  └─ StartNativeRequest(request, now)
   │  └─ FinishBatchIfIdle()
   ├─ TryCompletePendingNativeAchievementScaleReset()
   ├─ TryCompletePendingNativeAchievementInspectionRestore()
   └─ RefreshCosmicCacheFromLiveState()
```

Start/process/finish native request:

```text
AchievementProgressUpdater.StartNativeRequest(request, now)
├─ if batch not in progress
│  ├─ nativeWindowWasOpenBeforeBatch = NativeAchievementNavigator.IsOpen
│  ├─ nativeWindowOpenedByVal = !nativeWindowWasOpenBeforeBatch
│  └─ batchInProgress = true
├─ nativeWindowOpenBeforeRequest = NativeAchievementNavigator.IsOpen
├─ coldOpen = !nativeWindowOpenBeforeRequest
├─ NativeAchievementNavigator.OpenAchievement(request.AchievementId)
│  ├─ AgentAchievement.Instance()
│  ├─ returns false if null
│  ├─ agent->OpenById(achievementId)
│  └─ returns true
├─ if coldOpen -> nativeWindowOpenedByVal = true
├─ minimumWait = coldOpen ? 5 seconds : 1 second
├─ maximumWait = coldOpen ? 15 seconds : 5 seconds
├─ activeNativeRequest = ActiveNativeAchievementRequest(id, reason, startedAt, minimumCompleteAt, timeoutAt, coldOpen)
├─ NativeAchievementNavigator.TryParkAchievementWindow()
│  └─ IGameGui.GetAddonByName("Achievement", 1)
│     ├─ requires addon ready, visible, address non-zero
│     ├─ stores original X/Y/Scale into parkedState if not already stored
│     ├─ AtkUnitBase.SetScale(0.1375f, false)
│     ├─ AtkUnitBase.SetPosition(20, 20)
│     └─ returns true/false
└─ debug log NativeOpenSent
```

```text
AchievementProgressUpdater.ProcessActiveNativeRequest(now)
├─ if no activeNativeRequest -> return
├─ if NativeAchievementNavigator.HasParkedWindow == false
│  └─ NativeAchievementNavigator.TryParkAchievementWindow()
│     └─ retries parking after the native addon becomes ready/visible
├─ if now < request.MinimumCompleteAt -> return
├─ progressSource.TryGetFreshObservation(request.AchievementId, request.StartedAt, out progress)
│  ├─ ClientAchievementProgressSource.UpdateCache()
│  ├─ cachedProgress.TryGetValue(id, out progress)
│  └─ returns true only if progress.ObservedAt >= request.StartedAt
├─ if fresh observation exists
│  ├─ debug log NativeOpenLoaded current/max/source/elapsed
│  └─ activeNativeRequest = null
└─ if now >= request.TimeoutAt
   ├─ debug log NativeOpenTimeout
   └─ activeNativeRequest = null
```

```text
AchievementProgressUpdater.FinishBatchIfIdle(force = false)
├─ if no batch in progress -> return
├─ if not force and active/pending requests remain -> return
├─ restoreAfterUpdate = Configuration.RestoreNativeAchievementWindowAfterUpdates
├─ if Achieve Ex+ opened the native window and it was not open before the batch
│  └─ NativeAchievementNavigator.CloseAchievementWindow(restoreAfterUpdate)
│     ├─ if restoreAfterUpdate: RestoreParkedAchievementWindow()
│     │  ├─ IGameGui.GetAddonByName("Achievement", 1)
│     │  ├─ AtkUnitBase.SetScale(originalScale, false)
│     │  ├─ AtkUnitBase.SetPosition(originalX, originalY)
│     │  └─ clears parkedState
│     ├─ AgentAchievement.Instance()
│     ├─ if agent exists and IsOpen: agent->Hide()
│     └─ returns true/false
└─ otherwise the player already had the native window open
   └─ if restoreAfterUpdate: NativeAchievementNavigator.RestoreParkedAchievementWindow()
      └─ restores original scale/position and leaves the Achievement window open
```

## Timed auto update flow

```text
AchievementProgressUpdater.MaybeEnqueueAutoUpdate(now)
├─ if Configuration.ExperimentalAutoUpdateEnabled is false
│  ├─ nextAutoUpdateAt = DateTimeOffset.MinValue
│  └─ return
├─ interval = clamp(Configuration.ExperimentalAutoUpdateIntervalSeconds, 1..86400)
├─ if nextAutoUpdateAt == MinValue
│  ├─ nextAutoUpdateAt = now + interval
│  └─ return
├─ if now < nextAutoUpdateAt OR scheduler has pending requests OR active request exists -> return
├─ ids = Configuration.GetAutoUpdateTrackedAchievementIds()
│  └─ AutoUpdateSelection.SelectIncludedTrackedAchievements(TrackedAchievementIds, AutoUpdateAchievementIds)
├─ EnqueueUpdateAll(ids, "auto-update")
│  └─ queues only included Auto rows and skips rows observed in the last 30s
├─ nextAutoUpdateAt = now + interval
└─ debug log AutoUpdateScheduled
```

Mutual exclusion is enforced in both config normalization and UI writes:

```text
Configuration.NormalizeAutoUpdateSettings()
└─ if ExperimentalAutoUpdateEnabled && TriggerAutoUpdatesEnabled
   └─ TriggerAutoUpdatesEnabled = false

ConfigWindow.DrawExperimentalAutoUpdateSettings()
└─ enabling timed auto update
   ├─ TriggerAutoUpdatesEnabled = false
   └─ ClearUpdateQueue("auto-update-enabled")

ConfigWindow.DrawTriggerAutoUpdateSettings()
└─ enabling event-triggered updates
   ├─ ExperimentalAutoUpdateEnabled = false
   └─ ClearUpdateQueue("event-trigger-enabled")
```

## Event-triggered update flow

```text
AchievementTracker/Services/AchievementActivityUpdateObserver.cs
└─ constructor
   ├─ ChatGui.LogMessage += OnLogMessage
   └─ ChatGui.ChatMessageUnhandled += OnChatMessageUnhandled
```

```text
OnLogMessage(ILogMessage message)
└─ TryQueueCategoryUpdate(message.LogMessageId, message.FormatLogMessageForDebugging().ToString(), "activity-log-message")

OnChatMessageUnhandled(IChatMessage message)
└─ TryQueueCategoryUpdate(0, message.Message.TextValue, "activity-chat-message")
```

```text
TryQueueCategoryUpdate(logMessageId, messageText, reason)
├─ currentClassJobId = ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0
├─ AchievementActivityUpdateClassifier.TryClassify(logMessageId, messageText, currentClassJobId, out categoryName, out triggerName)
│  ├─ log IDs 1067/1068 -> Miner Mining/Quarrying
│  ├─ log IDs 1069/1070 -> Botanist Logging/Harvesting
│  ├─ fishing/spearfishing/crafting log ID sets -> Fisher/Crafter categories
│  └─ fallback text matching uses message text + current class/job category
├─ IsActivityTriggerEnabled(triggerName)
│  ├─ requires Configuration.TriggerAutoUpdatesEnabled
│  └─ checks matching group + child trigger flags
├─ matchingIds = SelectTrackedIdsForCategory(candidate IDs, category lookup, categoryName)
│  ├─ candidate IDs are either all tracked rows or Auto-included tracked rows
│  └─ category path must equal categoryName or end with ` > categoryName`
├─ 2-second per-category dedupe using lastQueuedAtByCategory
└─ enqueueUpdate(matchingIds, $"{reason}-{categoryName}")
   └─ Plugin.EnqueueUpdateAchievements(...)
      └─ AchievementProgressUpdater.EnqueueUpdateAll(...)
```

## Passive progress observation and progress display

```text
AchievementTracker/Services/PassiveAchievementProgressObserver.cs
├─ OnReceiveAchievementProgress(Achievement* thisPtr, uint id, uint current, uint max)
│  ├─ receiveHook.Original(thisPtr, id, current, max)
│  └─ ClientAchievementProgressSource.RecordObservedProgress(id, current, max, "Achievement window")
│     ├─ writes cachedProgress[id] = ObservedAchievementProgress(current, max, now, source)
│     ├─ if current >= max -> observedCompletions.Add(id)
│     └─ debug log RecordObservedProgress
└─ OnSetAchievementCompleted(Achievement* thisPtr, uint achievementId)
   ├─ completedHook.Original(thisPtr, achievementId)
   └─ if Configuration.TriggerOnAchievementCompletion
      └─ ClientAchievementProgressSource.RecordObservedCompletion(achievementId, "Achievement completed")
         ├─ cachedProgress.Remove(achievementId)
         ├─ observedCompletions.Add(achievementId)
         └─ debug log RecordObservedCompletion
```

```text
AchievementTracker/Services/AchievementProgressService.cs
└─ GetProgress(Achievement achievement)
   ├─ if CosmicClassProgressProvider.Handles(achievement.RowId)
   │  └─ CosmicClassProgressProvider.GetProgress(achievement.RowId)
   ├─ requiredTarget = first achievement.Data row ID when > 1
   ├─ if UnlockState says complete OR ClientAchievementProgressSource.IsObservedComplete(id)
   │  ├─ target exists -> AchievementProgress.Numeric(target, target)
   │  └─ no target -> AchievementProgress.Complete()
   ├─ if ClientAchievementProgressSource.TryGetProgress(id, out current, out max)
   │  └─ AchievementProgress.Numeric(current, max)
   ├─ if UnlockState.IsAchievementListLoaded is false
   │  ├─ target exists -> TargetKnown(target)
   │  └─ no target -> CompletionListNotLoaded()
   └─ otherwise
      ├─ target exists -> TargetKnown(target)
      └─ no target -> Incomplete()
```

## Native window inspection, restore, and reset flow

Magnifying-glass inspection should not leave the Achievement window parked/shrunk:

```text
Plugin.OpenNativeAchievementForInspection(achievementId)
├─ normalizedBeforeOpen = NativeAchievementNavigator.RestoreParkedAchievementWindowOrResetScale()
│  ├─ RestoreParkedAchievementWindow() if parkedState exists and addon is ready
│  └─ otherwise, if no parkedState: ResetAchievementWindowScale()
├─ opened = NativeAchievementNavigator.OpenAchievement(achievementId)
│  └─ AgentAchievement.OpenById(achievementId)
├─ normalizedAfterOpen = opened && !normalizedBeforeOpen && RestoreParkedAchievementWindowOrResetScale()
├─ pendingNativeAchievementInspectionRestore = opened && !normalizedBeforeOpen && !normalizedAfterOpen
├─ pendingNativeAchievementInspectionRestoreUntil = now + 5 seconds
└─ Framework.Update later calls TryCompletePendingNativeAchievementInspectionRestore()
   ├─ ShowAchievementWindow() if needed
   ├─ RestoreParkedAchievementWindowOrResetScale()
   └─ clears pending flag when normalized or timed out
```

Manual recovery button:

```text
Plugin.ResetNativeAchievementWindowScale()
├─ shown = NativeAchievementNavigator.IsOpen || NativeAchievementNavigator.ShowAchievementWindow()
│  └─ ShowAchievementWindow() calls AgentAchievement.Show()
├─ reset = shown && NativeAchievementNavigator.ResetAchievementWindowScale()
│  ├─ IGameGui.GetAddonByName("Achievement", 1)
│  ├─ AtkUnitBase.SetScale(1.0f, false)
│  ├─ clears parkedState
│  └─ returns true/false
├─ if shown but reset failed: pendingNativeAchievementScaleReset = true for 5 seconds
└─ Framework.Update later calls TryCompletePendingNativeAchievementScaleReset()
   ├─ ShowAchievementWindow() if needed
   ├─ ResetAchievementWindowScale()
   └─ clears pending flag when reset or timed out
```

## Catalog/search flow

```text
AchievementTracker/Services/AchievementCatalog.cs
├─ Search(query, limit)
│  ├─ DataManager.GetExcelSheet<Achievement>()
│  ├─ Select(ToInfo)
│  ├─ filters blank names
│  ├─ filters IsManuallyViewable(info.Id)
│  ├─ filters name/category contains query
│  ├─ orders by name
│  └─ returns up to limit AchievementInfo rows
├─ TryGet(achievementId, out AchievementInfo)
│  ├─ DataManager.GetExcelSheet<Achievement>()
│  ├─ sheet.TryGetRow(achievementId, out achievement)
│  ├─ ToInfo(achievement) on success
│  └─ returns fallback `Unknown achievement #id` on failure
├─ TryGetRow(achievementId, out Achievement)
│  └─ DataManager.GetExcelSheet<Achievement>().TryGetRow(...)
└─ IsManuallyViewable(achievementId)
   ├─ rejects missing achievement rows
   ├─ rejects invalid or hidden AchievementCategory
   ├─ rejects AchievementHideCondition.HideAchievement / HideName
   └─ requires non-blank achievement.Name
```

## Cosmic Class score flow

```text
Plugin.OnFrameworkUpdate()
└─ RefreshCosmicCacheFromLiveState()
   ├─ every 5 seconds
   └─ CosmicClassProgressProvider.RefreshCacheFromLiveScores()
      └─ TryReadLiveScores(saveWhenAvailable: true)
         ├─ WKSManager.Instance()
         ├─ returns null if manager null or !manager->IsLoaded
         ├─ manager->State.Scores.ToArray()
         ├─ requires at least 11 scores
         ├─ clamps first 11 scores to non-negative ints
         ├─ if different from cache -> SaveScoresToCache(liveScores)
         │  ├─ cache.Scores = liveScores.ToList()
         │  ├─ cache.UpdatedAtUtc = DateTimeOffset.UtcNow
         │  └─ saveCache() -> Plugin.SaveConfiguration()
         └─ returns int[] liveScores or null
```

```text
CosmicClassProgressProvider.GetProgress(achievementId)
├─ GetRule(achievementId)
│  └─ achievement IDs 3702-3739 map to Single/Any/Every class-score rules
├─ TryReadLiveScores() ?? TryReadCachedScores()
├─ no scores -> AchievementProgress.DataNotAvailable()
├─ Single/Any rules -> current = max selected score
├─ Every rules -> current = min selected score
└─ returns AchievementProgress.Numeric(current, targetScore)
```

## Persistence and reset flow

```text
AchievementTracker/Configuration.cs
└─ Configuration : IPluginConfiguration
   ├─ TrackedAchievementIds
   ├─ TrackedAchievementPresets
   ├─ CosmicClassScoreCache
   ├─ HideCompletedInSearch
   ├─ ExperimentalAutoUpdateEnabled
   ├─ ExperimentalAutoUpdateIntervalSeconds
   ├─ ExperimentalUpdateSpacingSeconds
   ├─ TriggerAutoUpdatesEnabled
   ├─ TriggerUpdatesRespectAutoUpdateSelection
   ├─ TriggerOnAchievementCompletion
   ├─ TriggerOnMiner/Botanist/Fisher/Crafter group flags and child flags
   ├─ AutoUpdateAchievementIds
   ├─ ExperimentalDebugLoggingEnabled
   └─ RestoreNativeAchievementWindowAfterUpdates
```

```text
Plugin.SaveTrackedAchievements()
├─ Configuration.TrackedAchievementIds = TrackedAchievementStore.ToConfigList()
├─ Configuration.AutoUpdateAchievementIds = Configuration.GetAutoUpdateTrackedAchievementIds()
│  └─ keeps Auto include IDs limited to currently tracked rows
└─ Configuration.Save()
   └─ PluginInterface.SavePluginConfig(Configuration)

Plugin.SaveConfiguration()
├─ Configuration.NormalizeAutoUpdateSettings()
└─ Configuration.Save()
   └─ PluginInterface.SavePluginConfig(Configuration)
```

```text
ClientState.Login / ClientState.Logout
└─ Plugin.ResetProgressState()
   ├─ AchievementProgressSource.ClearCache()
   │  └─ ClientAchievementProgressSource clears cachedProgress and observedCompletions
   ├─ AchievementProgressUpdater.Clear()
   │  ├─ scheduler.Clear()
   │  ├─ activeNativeRequest = null
   │  ├─ nextAutoUpdateAt = MinValue
   │  └─ FinishBatchIfIdle(force: true)
   ├─ pendingNativeAchievementInspectionRestore = false
   └─ pendingNativeAchievementScaleReset = false
```

## File index

```text
AchievementTracker/Plugin.cs
└─ Entrypoint, dependency wiring, command routing, observer installation, enqueue helpers, stop/reset helpers, framework tick orchestration, Cosmic cache tick.

AchievementTracker/Windows/TrackerWindow.cs
└─ Main Ex window: Configure, Update All, Stop Update Tasks, Auto update checkbox, queue status, row update/inspect buttons, progress display.

AchievementTracker/Windows/ConfigWindow.cs
└─ Auto update settings, event-trigger settings, tracked list/search/presets, auto-include controls, flash warning/help text, native reset button.

AchievementTracker/Services/AchievementProgressUpdater.cs
└─ Queued native Achievement open executor, timed auto-update countdown, cold/warm wait windows, window close/restore behavior.

AchievementTracker/Services/AchievementProgressRequestScheduler.cs
└─ Due-time queue, spacing, 1-2s jitter, 5s same-achievement backoff, pending request ordering.

AchievementTracker/Services/NativeAchievementNavigator.cs
└─ AgentAchievement open/show/hide plus Achievement addon parking/restoring/reset through IGameGui and AtkUnitBase.

AchievementTracker/Services/ClientAchievementProgressSource.cs
└─ Local Achievement progress slot reader and in-memory progress/completion cache.

AchievementTracker/Services/PassiveAchievementProgressObserver.cs
└─ Experimental passive hooks for ReceiveAchievementProgress and SetAchievementCompleted; records returned progress/completion after forwarding originals.

AchievementTracker/Services/AchievementActivityUpdateObserver.cs
└─ Chat/log event subscriber that classifies activity and queues category-matching tracked updates.

AchievementTracker/Services/AchievementActivityUpdateClassifier.cs
└─ Log-message and text classifier for mining/quarrying/logging/harvesting/fishing/spearfishing/crafting events.

AchievementTracker/Services/AchievementCatalog.cs
└─ Lumina Achievement search, lookup, category path, manual-viewability filtering.

AchievementTracker/Services/AchievementProgressService.cs
└─ Display-progress decision tree combining IUnlockState, cached observations, completion hooks, target counts, and Cosmic progress.

AchievementTracker/Services/CosmicClassProgressProvider.cs
└─ WKS/Cosmic score reads, cache persistence, and Cosmic Class achievement progress rules.

AchievementTracker/Services/TrackedAchievementStore.cs
└─ In-memory tracked ID list: add, remove, reorder, load, export.

AchievementTracker/Services/TrackedAchievementPresetStore.cs
└─ Preset sanitize/normalize/save/rename/delete/find helpers.

AchievementTracker/Services/AutoUpdateSelection.cs
└─ Selects tracked IDs included in timed auto update from TrackedAchievementIds and AutoUpdateAchievementIds.

AchievementTracker/Configuration.cs
└─ Persistent plugin config and auto-update normalization.

AchievementTracker/Models/*.cs
└─ Data shapes for achievement info, display progress, tracked rows, presets, and Cosmic score cache.
```
