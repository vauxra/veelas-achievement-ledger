using AchievementTracker;
using AchievementTracker.Models;
using AchievementTracker.Services;

var tests = new List<(string Name, Action Body)>
{
    ("Add allows up to twenty unique achievement ids", AddAllowsUpToTwentyUniqueAchievementIds),
    ("Add rejects duplicate achievement ids", AddRejectsDuplicateAchievementIds),
    ("Remove deletes the selected achievement id", RemoveDeletesSelectedAchievementId),
    ("MoveToTop reorders an item to the start", MoveToTopReordersItemToStart),
    ("MoveUp reorders an item toward the start", MoveUpReordersItemTowardStart),
    ("MoveDown reorders an item toward the end", MoveDownReordersItemTowardEnd),
    ("MoveToBottom reorders an item to the end", MoveToBottomReordersItemToEnd),
    ("LoadFrom sanitizes duplicates and trims to twenty", LoadFromSanitizesDuplicatesAndTrimsToTwenty),
    ("Preset save sanitizes names and achievement ids", PresetSaveSanitizesNamesAndAchievementIds),
    ("Preset rename and delete cover CRUD", PresetRenameAndDeleteCoverCrud),
    ("Progress display formats all safe states", ProgressDisplayFormatsAllSafeStates),
    ("Update all spaces queued requests by base seconds plus jitter", UpdateAllSpacesQueuedRequestsByBaseSecondsPlusJitter),
    ("Update all keeps jitter when base spacing is zero", UpdateAllKeepsJitterWhenBaseSpacingIsZero),
    ("Native refresh batch limiter caps at queue capacity", NativeRefreshBatchLimiterCapsAtQueueCapacity),
    ("Request scheduler applies five second per-achievement backoff", RequestSchedulerAppliesFiveSecondPerAchievementBackoff),
    ("Request scheduler caps pending actions at one hundred", RequestSchedulerCapsPendingActionsAtOneHundred),
    ("Request scheduler serializes refreshes and inspections in one queue", RequestSchedulerSerializesRefreshesAndInspectionsInOneQueue),
    ("Request scheduler assigns one job to update all batch", RequestSchedulerAssignsOneJobToUpdateAllBatch),
    ("Request scheduler gives inspections separate jobs", RequestSchedulerGivesInspectionsSeparateJobs),
    ("Scale policy closes only at refresh job end", ScalePolicyClosesOnlyAtRefreshJobEnd),
    ("Scale policy parks refresh only when native window was closed", ScalePolicyParksRefreshOnlyWhenNativeWindowWasClosed),
    ("Scale policy restores inspection actions", ScalePolicyRestoresInspectionActions),
    ("Scale policy rejects parked scales as user restore state", ScalePolicyRejectsParkedScalesAsUserRestoreState),
    ("Scale policy restores when idle only for parked windows", ScalePolicyRestoresWhenIdleOnlyForParkedWindows),
    ("Scale policy restores only on user-visible reopen", ScalePolicyRestoresOnlyOnUserVisibleReopen),
    ("Configuration defaults use requested main column widths", ConfigurationDefaultsUseRequestedMainColumnWidths),
    ("Tracked display evaluates cosmic progress overrides", TrackedDisplayEvaluatesCosmicProgressOverrides),
    ("Cosmic progress override parses achievement details", CosmicProgressOverrideParsesAchievementDetails),
    ("Auto updater selects only explicitly included tracked achievements", AutoUpdaterSelectsOnlyExplicitlyIncludedTrackedAchievements),
    ("Update eligibility keeps distinct eligible ids in order", UpdateEligibilityKeepsDistinctEligibleIdsInOrder),
    ("Update eligibility reports completed and native-unsafe removals", UpdateEligibilityReportsCompletedAndNativeUnsafeRemovals),
    ("Update eligibility leaves config untouched when skipped ids are not auto-updated", UpdateEligibilityLeavesConfigUntouchedWhenSkippedIdsAreNotAutoUpdated),
    ("Completion filters wait for loaded achievement state", CompletionFiltersWaitForLoadedAchievementState),
    ("Completion-filtered counts fall back to all while unloaded", CompletionFilteredCountsFallBackToAllWhileUnloaded),
    ("Lumina search all does not wait for loaded achievement state", LuminaSearchAllDoesNotWaitForLoadedAchievementState),
    ("Achievement search index filters category query and completion", AchievementSearchIndexFiltersCategoryQueryAndCompletion),
    ("Achievement search index counts categories with unloaded completion fallback", AchievementSearchIndexCountsCategoriesWithUnloadedCompletionFallback),
    ("Achievement search index hides zero-count incomplete categories only when configured", AchievementSearchIndexHidesZeroCountIncompleteCategoriesOnlyWhenConfigured),
    ("Achievement search index keeps game order stable", AchievementSearchIndexKeepsGameOrderStable),
    ("Native update batches do not park achievement windows", NativeUpdateBatchesDoNotParkAchievementWindows),
    ("Active refresh polls progress slot fallback", ActiveRefreshPollsProgressSlotFallback),
    ("Progress slot fallback does not restamp unchanged loaded slot", ProgressSlotFallbackDoesNotRestampUnchangedLoadedSlot),
    ("Activity classifier ignores text-only activity messages", ActivityClassifierIgnoresTextOnlyActivityMessages),
    ("Activity classifier matches known log message ids", ActivityClassifierMatchesKnownLogMessageIds),
    ("Activity classifier uses verified crafting success ids only", ActivityClassifierUsesVerifiedCraftingSuccessIdsOnly),
    ("Crafting log completion configuration path is removed", CraftingLogCompletionConfigurationPathIsRemoved),
    ("Activity trigger delay policy delays only crafting", ActivityTriggerDelayPolicyDelaysOnlyCrafting),
    ("Activity scheduler marks same pending key dirty", ActivitySchedulerMarksSamePendingKeyDirty),
    ("Activity scheduler single duplicate does not queue dirty final pass", ActivitySchedulerSingleDuplicateDoesNotQueueDirtyFinalPass),
    ("Activity scheduler two duplicates queue dirty final pass", ActivitySchedulerTwoDuplicatesQueueDirtyFinalPass),
    ("Activity scheduler appends dirty final pass behind later keys", ActivitySchedulerAppendsDirtyFinalPassBehindLaterKeys),
    ("Activity scheduler queues different keys normally", ActivitySchedulerQueuesDifferentKeysNormally),
    ("Manual scheduler requests are not activity-key coalesced", ManualSchedulerRequestsAreNotActivityKeyCoalesced),
    ("Achievement category path splits top-level and subcategory", AchievementCategoryPathSplitsTopLevelAndSubcategory),
    ("Achievement category path matches exact category or final subcategory", AchievementCategoryPathMatchesExactCategoryOrFinalSubcategory),
    ("Activity classifier selects tracked achievements by category path", ActivityClassifierSelectsTrackedAchievementsByCategoryPath),
    ("Activity trigger candidates exclude cosmic class achievements", ActivityTriggerCandidatesExcludeCosmicClassAchievements),
    ("Tracked toolbar hidden state shows default eye", TrackedToolbarHiddenStateShowsDefaultEye),
    ("Tracked toolbar shown state shows red eye", TrackedToolbarShownStateShowsRedEye),
    ("Tracked update indicator shows working while queue active", TrackedUpdateIndicatorShowsWorkingWhileQueueActive),
    ("Tracked update indicator shows needs update when stale and idle", TrackedUpdateIndicatorShowsNeedsUpdateWhenStaleAndIdle),
    ("Tracked update indicator shows all updated when idle without stale rows", TrackedUpdateIndicatorShowsAllUpdatedWhenIdleWithoutStaleRows),
    ("Auto update status row formats running queue", AutoUpdateStatusRowFormatsRunningQueue),
    ("Auto update status row formats idle queue", AutoUpdateStatusRowFormatsIdleQueue),
};

foreach (var test in tests)
{
    test.Body();
    Console.WriteLine($"PASS {test.Name}");
}

static void AddAllowsUpToTwentyUniqueAchievementIds()
{
    var store = new TrackedAchievementStore();

    for (uint id = 1; id <= 20; id++)
    {
        AssertTrue(store.TryAdd(id), $"{id} should be added");
    }

    AssertFalse(store.TryAdd(21), "21 should be rejected because max tracked achievements is twenty");
    AssertSequence(store.AchievementIds, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
}

static void AddRejectsDuplicateAchievementIds()
{
    var store = new TrackedAchievementStore();

    AssertTrue(store.TryAdd(42), "first add should pass");
    AssertFalse(store.TryAdd(42), "duplicate add should fail");

    AssertSequence(store.AchievementIds, [42]);
}

static void RemoveDeletesSelectedAchievementId()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30]);

    AssertTrue(store.Remove(20), "existing id should be removed");
    AssertFalse(store.Remove(99), "missing id should not be removed");

    AssertSequence(store.AchievementIds, [10, 30]);
}

static void MoveToTopReordersItemToStart()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30, 40]);

    AssertTrue(store.MoveToTop(30), "30 should move to top");
    AssertFalse(store.MoveToTop(30), "top item cannot move to top again");

    AssertSequence(store.AchievementIds, [30, 10, 20, 40]);
}

static void MoveUpReordersItemTowardStart()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30]);

    AssertTrue(store.MoveUp(30), "30 should move up");
    AssertFalse(store.MoveUp(10), "first item cannot move up");

    AssertSequence(store.AchievementIds, [10, 30, 20]);
}

static void MoveDownReordersItemTowardEnd()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30]);

    AssertTrue(store.MoveDown(10), "10 should move down");
    AssertFalse(store.MoveDown(30), "last item cannot move down");

    AssertSequence(store.AchievementIds, [20, 10, 30]);
}

static void MoveToBottomReordersItemToEnd()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30, 40]);

    AssertTrue(store.MoveToBottom(20), "20 should move to bottom");
    AssertFalse(store.MoveToBottom(20), "bottom item cannot move to bottom again");

    AssertSequence(store.AchievementIds, [10, 30, 40, 20]);
}

static void LoadFromSanitizesDuplicatesAndTrimsToTwenty()
{
    var store = new TrackedAchievementStore();
    var ids = new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 2, 3 };

    store.LoadFrom(ids);

    AssertSequence(store.AchievementIds, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
}

static void PresetSaveSanitizesNamesAndAchievementIds()
{
    var presets = new List<TrackedAchievementPreset>();
    var dirtyName = "  Miner\nSet  ";
    var ids = new uint[] { 1, 2, 2, 0, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };

    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, dirtyName, ids, out var savedName), "preset should save");
    AssertEqual("MinerSet", savedName);
    AssertEqualInt(1, presets.Count);
    AssertEqual("MinerSet", presets[0].Name);
    AssertSequence(presets[0].AchievementIds, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
}

static void PresetRenameAndDeleteCoverCrud()
{
    var presets = new List<TrackedAchievementPreset>();
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "List A", [10, 20], out _), "initial save should pass");
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "List B", [30, 40], out _), "second save should pass");
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "List A", [50], out _), "same name should update");
    AssertEqualInt(2, presets.Count);
    AssertSequence(TrackedAchievementPresetStore.FindPreset(presets, "List A")!.AchievementIds, [50]);

    AssertFalse(TrackedAchievementPresetStore.RenamePreset(presets, "List A", "List B", out _), "rename should reject duplicate names");
    AssertTrue(TrackedAchievementPresetStore.RenamePreset(presets, "List A", "Renamed", out var renamedTo), "rename should pass");
    AssertEqual("Renamed", renamedTo);
    AssertTrue(TrackedAchievementPresetStore.DeletePreset(presets, "Renamed"), "delete should pass");
    AssertFalse(TrackedAchievementPresetStore.DeletePreset(presets, "Renamed"), "delete should reject missing preset");
}

static void ProgressDisplayFormatsAllSafeStates()
{
    AssertEqual("Open Achievements to load status", AchievementProgress.CompletionListNotLoaded().ToDisplayText());
    AssertEqual("Complete", AchievementProgress.Complete().ToDisplayText());
    AssertEqual("Incomplete", AchievementProgress.Incomplete().ToDisplayText());
    AssertEqual("437 / 1,000", AchievementProgress.Numeric(437, 1000).ToDisplayText());
    AssertEqual("Current unavailable / 1,500", AchievementProgress.TargetKnown(1500).ToDisplayText());
    AssertEqual("Progress unavailable", AchievementProgress.Unavailable().ToDisplayText());
    AssertEqual("Data not available", AchievementProgress.DataNotAvailable().ToDisplayText());
}

static void UpdateAllSpacesQueuedRequestsByBaseSecondsPlusJitter()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(
        () => now,
        () => TimeSpan.FromMilliseconds(750));

    scheduler.EnqueueUpdateAll([101, 102, 103], "test", TimeSpan.FromSeconds(7));

    AssertEqualInt(3, scheduler.PendingCount);
    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first request should be due immediately");
    AssertEqualUInt(101u, first.AchievementId);

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(8), out _), "second request should include immutable spacing and jitter beyond base spacing");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(8).AddMilliseconds(750), out var second), "second request should be due after immutable spacing, base spacing, and jitter");
    AssertEqualUInt(102u, second.AchievementId);

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(17), out _), "third request should include cumulative immutable spacing and jitter");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(17).AddMilliseconds(500), out var third), "third request should be due after cumulative immutable spacing, base spacing, and jitter");
    AssertEqualUInt(103u, third.AchievementId);
}

static void UpdateAllKeepsJitterWhenBaseSpacingIsZero()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(
        () => now,
        () => TimeSpan.FromSeconds(1.5));

    scheduler.EnqueueUpdateAll([201, 202], "zero-spacing", TimeSpan.Zero);

    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first request should be due immediately");
    AssertEqualUInt(201u, first.AchievementId);
    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(2), out _), "second request should still wait for immutable spacing plus jitter");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(2.5), out var second), "second request should be due after immutable spacing plus jitter");
    AssertEqualUInt(202u, second.AchievementId);
}

static void NativeRefreshBatchLimiterCapsAtQueueCapacity()
{
    var ids = Enumerable.Range(1, 150).Select(id => (uint)id).ToList();
    var limited = AchievementProgressUpdater.LimitNativeRefreshBatch(ids);

    AssertEqualInt(AchievementProgressRequestScheduler.MaxPendingRequests, limited.Count);
    AssertEqualUInt(1u, limited[0]);
    AssertEqualUInt(100u, limited[^1]);
}

static void RequestSchedulerAppliesFiveSecondPerAchievementBackoff()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(
        () => now,
        () => TimeSpan.Zero);

    scheduler.EnqueueUpdateAll([777], "first");
    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first request should be due");
    AssertEqualUInt(777u, first.AchievementId);

    scheduler.EnqueueUpdateAll([777], "second");
    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(4), out _), "same achievement should respect five second backoff");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(5), out var second), "same achievement should be available after backoff");
    AssertEqualUInt(777u, second.AchievementId);
}


static void RequestSchedulerCapsPendingActionsAtOneHundred()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(
        () => now,
        () => TimeSpan.Zero);

    scheduler.EnqueueUpdateAll(Enumerable.Range(1, 150).Select(id => (uint)id), "spam", TimeSpan.Zero);

    AssertEqualInt(AchievementProgressRequestScheduler.MaxPendingRequests, scheduler.PendingCount);
}

static void RequestSchedulerSerializesRefreshesAndInspectionsInOneQueue()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(
        () => now,
        () => TimeSpan.FromSeconds(1));

    scheduler.EnqueueUpdateAll([101, 102], "refresh", TimeSpan.FromSeconds(2));
    AssertTrue(scheduler.EnqueueInspection(201, "inspect"), "inspection should enter the same queue");

    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first refresh should be due immediately");
    AssertEqualUInt(101u, first.AchievementId);
    AssertEqual(NativeAchievementActionKind.Refresh.ToString(), first.Kind.ToString());

    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(4), out var second), "second refresh should follow immutable spacing, configured spacing, and jitter");
    AssertEqualUInt(102u, second.AchievementId);
    AssertEqual(NativeAchievementActionKind.Refresh.ToString(), second.Kind.ToString());

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(7), out _), "inspection should also respect immutable queue spacing and jitter");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(8), out var third), "inspection should be serialized after refreshes");
    AssertEqualUInt(201u, third.AchievementId);
    AssertEqual(NativeAchievementActionKind.Inspection.ToString(), third.Kind.ToString());
}



static void RequestSchedulerAssignsOneJobToUpdateAllBatch()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);

    scheduler.EnqueueUpdateAll([301, 302, 303], "manual-update-all", TimeSpan.Zero);

    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first batch request should be due");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(1), out var second), "second batch request should be due after immutable spacing");
    AssertEqual(first.JobId.ToString(), second.JobId.ToString());
    AssertEqual(NativeAchievementJobKind.Batch.ToString(), first.JobKind.ToString());
    AssertTrue(scheduler.HasPendingRequestsForJob(first.JobId), "third batch item should remain in the same job");
}

static void RequestSchedulerGivesInspectionsSeparateJobs()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);

    AssertTrue(scheduler.EnqueueInspection(401, "inspect-a"), "first inspection should queue");
    AssertTrue(scheduler.EnqueueInspection(402, "inspect-b"), "second inspection should queue");

    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first inspection should be due");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(1), out var second), "second inspection should be due after immutable spacing");
    AssertFalse(first.JobId == second.JobId, "inspection clicks should not share jobs");
    AssertEqual(NativeAchievementJobKind.Inspection.ToString(), first.JobKind.ToString());
}

static void ScalePolicyClosesOnlyAtRefreshJobEnd()
{
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldCloseAfterRefreshJobItem(NativeAchievementJobKind.Batch, nativeWindowWasAlreadyOpen: false, hasPendingSameJob: true), "batch should stay open between items");
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldCloseAfterRefreshJobItem(NativeAchievementJobKind.Batch, nativeWindowWasAlreadyOpen: false, hasPendingSameJob: false), "batch should close after final item if plugin opened it");
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldCloseAfterRefreshJobItem(NativeAchievementJobKind.Single, nativeWindowWasAlreadyOpen: false, hasPendingSameJob: false), "single closed-window update should close after its item");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldCloseAfterRefreshJobItem(NativeAchievementJobKind.Batch, nativeWindowWasAlreadyOpen: true, hasPendingSameJob: false), "already-open batch should not auto-close");
}

static void ScalePolicyParksRefreshOnlyWhenNativeWindowWasClosed()
{
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldParkForAction(NativeAchievementActionKind.Refresh, nativeWindowWasAlreadyOpen: false), "closed-window refresh should park");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldParkForAction(NativeAchievementActionKind.Refresh, nativeWindowWasAlreadyOpen: true), "already-open refresh should not park");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldParkForAction(NativeAchievementActionKind.Inspection, nativeWindowWasAlreadyOpen: false), "inspection should not park");
}

static void ScalePolicyRestoresInspectionActions()
{
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldRestoreForAction(NativeAchievementActionKind.Inspection), "inspection should restore");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldRestoreForAction(NativeAchievementActionKind.Refresh), "refresh should not restore at start");
}

static void ScalePolicyRejectsParkedScalesAsUserRestoreState()
{
    AssertFalse(NativeAchievementWindowScalePolicy.IsRestorableUserScale(NativeAchievementNavigator.ParkedScale), "current tiny parked scale should not be restored as a user scale");
    AssertFalse(NativeAchievementWindowScalePolicy.IsRestorableUserScale(0.55f), "legacy parked scale should not be restored as a user scale");
    AssertTrue(NativeAchievementWindowScalePolicy.IsRestorableUserScale(0.56f), "ordinary user scales above legacy parking should be restorable");
    AssertTrue(NativeAchievementWindowScalePolicy.IsRestorableUserScale(1.0f), "default scale should be restorable");
}

static void ScalePolicyRestoresWhenIdleOnlyForParkedWindows()
{
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldRestoreWhenIdle(hasActiveRequest: false, hasPendingRequests: false, hasParkedWindow: true), "idle parked window should restore");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldRestoreWhenIdle(hasActiveRequest: true, hasPendingRequests: false, hasParkedWindow: true), "active request should not restore");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldRestoreWhenIdle(hasActiveRequest: false, hasPendingRequests: true, hasParkedWindow: true), "pending queue should not restore");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldRestoreWhenIdle(hasActiveRequest: false, hasPendingRequests: false, hasParkedWindow: false), "unparked idle state should not restore");
}

static void ScalePolicyRestoresOnlyOnUserVisibleReopen()
{
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldRestoreWhenPlayerOpenedPanel(hasParkedWindow: true, nativeWindowIsOpen: true, nativeWindowIsStillParked: false, hasActiveOrPendingWork: true), "manual/open-in-achievements visible reopen during queue should restore");
    AssertFalse(NativeAchievementWindowScalePolicy.ShouldRestoreWhenPlayerOpenedPanel(hasParkedWindow: true, nativeWindowIsOpen: true, nativeWindowIsStillParked: true, hasActiveOrPendingWork: true), "parked background queue window should stay parked");
    AssertTrue(NativeAchievementWindowScalePolicy.ShouldRestoreWhenPlayerOpenedPanel(hasParkedWindow: true, nativeWindowIsOpen: true, nativeWindowIsStillParked: true, hasActiveOrPendingWork: false), "manual reopen after queue should restore even if addon kept parked scale");
}

static void ConfigurationDefaultsUseRequestedMainColumnWidths()
{
    var defaults = MainPanelColumnWidthDefaults.Create();
    AssertTrue(Math.Abs(defaults["Lists"] - 270f) < 0.001f, "Lists width should default to 270");
    AssertTrue(Math.Abs(defaults["Search Categories"] - 320f) < 0.001f, "Search Categories width should default to 320");
    AssertTrue(Math.Abs(defaults["Search Results"] - 550f) < 0.001f, "Search Results width should default to 550");
    AssertTrue(Math.Abs(defaults["Tracked Achievements"] - 320f) < 0.001f, "Tracked Achievements width should default to 320");
}

static void TrackedDisplayEvaluatesCosmicProgressOverrides()
{
    AssertTrue(TrackedProgressDisplayPolicy.ShouldEvaluateProgress(hasObservedProgress: false, isComplete: false, hasCosmicProgressOverride: true), "cosmic override should display without a normal observation");
    AssertFalse(TrackedProgressDisplayPolicy.ShouldEvaluateProgress(hasObservedProgress: false, isComplete: false, hasCosmicProgressOverride: false), "ordinary rows remain not updated until observed/complete");
}

static void CosmicProgressOverrideParsesAchievementDetails()
{
    var scores = new[] { 123456, 0, 0, 0, 0, 0, 0, 0, 222222, 0, 0 };
    AssertTrue(CosmicClassProgressProvider.TryCreateProgressOverride("Carpenter", "Earn a cosmic class score of 500,000 points as a carpenter.", scores, out var carpenter), "carpenter cosmic description should parse");
    AssertEqual("123,456 / 500,000", carpenter.ToDisplayText());
    AssertTrue(CosmicClassProgressProvider.TryCreateProgressOverride("Miner", "Earn a cosmic class score of 150,000 points as a miner.", scores, out var miner), "miner cosmic description should parse");
    AssertEqual("222,222 / 150,000", miner.ToDisplayText());
    AssertTrue(CosmicClassProgressProvider.TryCreateProgressOverride("Carpenter", "Earn 500,000 tool mastery points as a carpenter.", scores, out var mastery), "tool mastery descriptions should use cosmic class scores");
    AssertEqual("123,456 / 500,000", mastery.ToDisplayText());
    AssertFalse(CosmicClassProgressProvider.TryCreateProgressOverride("Carpenter", "Synthesize 1,000 items.", scores, out _), "non-cosmic descriptions should not override");
}

static void AutoUpdaterSelectsOnlyExplicitlyIncludedTrackedAchievements()
{
    AssertSequence(AutoUpdateSelection.SelectIncludedTrackedAchievements([1, 2, 3, 4], [2, 4, 999]), [2, 4]);
}

static void UpdateEligibilityKeepsDistinctEligibleIdsInOrder()
{
    var result = UpdateEligibilityPolicy.Evaluate(
        [0, 42, 42, 7, 9, 7],
        _ => NativeAchievementOpenEligibility.Eligible,
        _ => false,
        []);

    AssertSequence(result.EligibleAchievementIds, [42, 7, 9]);
    AssertEqualInt(0, result.CompletedAchievementIds.Count);
    AssertEqualInt(0, result.NativeUnsafeAchievementIds.Count);
    AssertFalse(result.ShouldRemoveAutoUpdateEntries, "no skipped ids were configured for auto update");
}

static void UpdateEligibilityReportsCompletedAndNativeUnsafeRemovals()
{
    var result = UpdateEligibilityPolicy.Evaluate(
        [10, 20, 30, 40],
        id => id == 20
            ? NativeAchievementOpenEligibility.Ineligible("hidden category")
            : NativeAchievementOpenEligibility.Eligible,
        id => id == 30,
        [20, 30, 99]);

    AssertSequence(result.EligibleAchievementIds, [10, 40]);
    AssertSequence(result.CompletedAchievementIds, [30]);
    AssertSequence(result.AutoUpdateAchievementIdsToRemove, [20, 30]);
    AssertTrue(result.ShouldRemoveAutoUpdateEntries, "skipped configured ids should be removed from auto update");
    AssertEqualInt(1, result.NativeUnsafeAchievementIds.Count);
    AssertEqualUInt(20u, result.NativeUnsafeAchievementIds[0].AchievementId);
    AssertEqual("hidden category", result.NativeUnsafeAchievementIds[0].Reason);
}

static void UpdateEligibilityLeavesConfigUntouchedWhenSkippedIdsAreNotAutoUpdated()
{
    var result = UpdateEligibilityPolicy.Evaluate(
        [10, 20, 30],
        id => id == 20
            ? NativeAchievementOpenEligibility.Ineligible("not in native UI")
            : NativeAchievementOpenEligibility.Eligible,
        id => id == 30,
        [10]);

    AssertSequence(result.EligibleAchievementIds, [10]);
    AssertSequence(result.CompletedAchievementIds, [30]);
    AssertEqualInt(1, result.NativeUnsafeAchievementIds.Count);
    AssertSequence(result.AutoUpdateAchievementIdsToRemove, []);
    AssertFalse(result.ShouldRemoveAutoUpdateEntries, "skipped ids not in auto update should not trigger config mutation");
}

static void CompletionFiltersWaitForLoadedAchievementState()
{
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: false, updateInProgress: false), "All should not need live completion state");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: false, updateInProgress: false), "Completed should wait for completion state");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Incomplete", completionStateLoaded: false, updateInProgress: false), "Incomplete should wait for completion state");
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: true, updateInProgress: false), "Completed should work after load");
}

static void CompletionFilteredCountsFallBackToAllWhileUnloaded()
{
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Completed", completionStateLoaded: false, isComplete: false), "category/search counts should stay all-counts until completion state is loaded");
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Incomplete", completionStateLoaded: false, isComplete: true), "category/search counts should stay all-counts until completion state is loaded");
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Completed", completionStateLoaded: true, isComplete: true), "completed counts include complete rows after load");
    AssertFalse(SearchCompletionFilterPolicy.MatchesForCount("Completed", completionStateLoaded: true, isComplete: false), "completed counts exclude incomplete rows after load");
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Incomplete", completionStateLoaded: true, isComplete: false), "incomplete counts include incomplete rows after load");
    AssertFalse(SearchCompletionFilterPolicy.MatchesForCount("Incomplete", completionStateLoaded: true, isComplete: true), "incomplete counts exclude complete rows after load");
}

static void LuminaSearchAllDoesNotWaitForLoadedAchievementState()
{
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: false, updateInProgress: false), "Lumina-only All search should not wait for the session's initial achievement load");
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: true, updateInProgress: true), "Lumina-only All search should not pause while update tasks are running");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: false, updateInProgress: false), "Completed search still needs loaded completion state");
}

static void AchievementSearchIndexFiltersCategoryQueryAndCompletion()
{
    var achievements = new[]
    {
        SearchInfo(1, "Mine 10 items", "Crafting & Gathering > Miner"),
        SearchInfo(2, "Harvest 20 items", "Crafting & Gathering > Botanist"),
        SearchInfo(3, "Mine impostor", "Battle > Miner Impostor"),
        SearchInfo(4, "Legacy mine", "Legacy > Events"),
    };
    var sortKeys = SearchSortKeys(
        (1u, new AchievementSearchSortKey(0, 2, 2, 1)),
        (2u, new AchievementSearchSortKey(0, 1, 1, 2)),
        (3u, new AchievementSearchSortKey(1, 1, 1, 3)),
        (4u, new AchievementSearchSortKey(0, 0, 0, 4)));

    var results = AchievementSearchIndex.BuildResults(
        AchievementSearchIndex.GetSearchableAchievements(achievements),
        new AchievementSearchQueryState(
            "mine",
            CategoryFilterAll: false,
            SelectedCategoryFilters: [],
            SelectedSubcategoryFilters: [AchievementCategoryPath.BuildSubcategoryFilterKey("Crafting & Gathering", "Miner")],
            SearchCompletionFilterPolicy.Incomplete,
            AchievementSearchSortMode.GameOrder),
        isComplete: id => id != 1,
        getSortKey: info => sortKeys[info.Id]);

    AssertEqualInt(3, results.SearchableCount);
    AssertEqualInt(1, results.CategoryFilteredCount);
    AssertEqualInt(1, results.QueryFilteredCount);
    AssertEqualInt(1, results.CompletionFilteredCount);
    AssertSequence(results.Results.Select(info => info.Id).ToList(), [1]);
}

static void AchievementSearchIndexCountsCategoriesWithUnloadedCompletionFallback()
{
    var achievements = AchievementSearchIndex.GetSearchableAchievements(
    [
        SearchInfo(1, "Mine 10 items", "Crafting & Gathering > Miner"),
        SearchInfo(2, "Harvest 20 items", "Crafting & Gathering > Botanist"),
        SearchInfo(3, "Win battles", "Battle > Field Operations"),
    ]);
    var sortKeys = SearchSortKeys(
        (1u, new AchievementSearchSortKey(0, 2, 2, 1)),
        (2u, new AchievementSearchSortKey(0, 1, 1, 2)),
        (3u, new AchievementSearchSortKey(1, 1, 1, 3)));

    var unloadedGroups = AchievementSearchIndex.BuildCategoryGroups(
        achievements,
        SearchCompletionFilterPolicy.Completed,
        completionStateLoaded: false,
        isComplete: id => id == 2,
        getSortKey: info => sortKeys[info.Id]);

    var craftingUnloaded = unloadedGroups.Single(group => group.Category == "Crafting & Gathering");
    AssertEqualInt(2, craftingUnloaded.DisplayCount);
    AssertEqualInt(1, craftingUnloaded.CountEntriesForSubcategory("Miner"));
    AssertEqualInt(1, craftingUnloaded.CountEntriesForSubcategory("Botanist"));

    var loadedGroups = AchievementSearchIndex.BuildCategoryGroups(
        achievements,
        SearchCompletionFilterPolicy.Completed,
        completionStateLoaded: true,
        isComplete: id => id == 2,
        getSortKey: info => sortKeys[info.Id]);

    var craftingLoaded = loadedGroups.Single(group => group.Category == "Crafting & Gathering");
    AssertEqualInt(1, craftingLoaded.DisplayCount);
    AssertEqualInt(0, craftingLoaded.CountEntriesForSubcategory("Miner"));
    AssertEqualInt(1, craftingLoaded.CountEntriesForSubcategory("Botanist"));
}

static void AchievementSearchIndexHidesZeroCountIncompleteCategoriesOnlyWhenConfigured()
{
    var achievements = AchievementSearchIndex.GetSearchableAchievements(
    [
        SearchInfo(1, "Mine 10 items", "Crafting & Gathering > Miner"),
        SearchInfo(2, "Harvest 20 items", "Crafting & Gathering > Botanist"),
        SearchInfo(3, "Win battles", "Battle > Field Operations"),
    ]);
    var sortKeys = SearchSortKeys(
        (1u, new AchievementSearchSortKey(0, 1, 1, 1)),
        (2u, new AchievementSearchSortKey(0, 1, 2, 2)),
        (3u, new AchievementSearchSortKey(1, 1, 1, 3)));
    var groups = AchievementSearchIndex.BuildCategoryGroups(
        achievements,
        SearchCompletionFilterPolicy.Incomplete,
        completionStateLoaded: true,
        isComplete: id => id is 1 or 2,
        getSortKey: info => sortKeys[info.Id]);
    var hideZeroCountEntries = AchievementSearchIndex.ShouldHideZeroCountCategories(
        SearchCompletionFilterPolicy.Incomplete,
        hideZeroCountIncompleteCategories: true);

    AssertTrue(hideZeroCountEntries, "incomplete filter plus enabled config should hide zero-count entries");
    AssertSequence(groups.Where(group => group.ShouldShow(hideZeroCountEntries)).Select(group => group.Entries[0].Info.Id).ToList(), [3]);

    var craftingGroup = groups.Single(group => group.Category == "Crafting & Gathering");
    AssertFalse(craftingGroup.ShouldShowSubcategory("Miner", hideZeroCountEntries), "zero-count subcategories should hide when configured");
    AssertFalse(AchievementSearchIndex.ShouldHideZeroCountCategories(SearchCompletionFilterPolicy.Completed, hideZeroCountIncompleteCategories: true), "completed filter should not use the incomplete zero-count hiding toggle");
    AssertFalse(AchievementSearchIndex.ShouldHideZeroCountCategories(SearchCompletionFilterPolicy.Incomplete, hideZeroCountIncompleteCategories: false), "disabled config should leave zero-count categories visible");
}

static void AchievementSearchIndexKeepsGameOrderStable()
{
    var achievements = AchievementSearchIndex.GetSearchableAchievements(
    [
        SearchInfo(10, "Second", "Battle"),
        SearchInfo(20, "First", "Crafting & Gathering > Miner"),
        SearchInfo(30, "Third by row", "Crafting & Gathering > Miner"),
    ]);
    var sortKeys = SearchSortKeys(
        (10u, new AchievementSearchSortKey(1, 1, 1, 10)),
        (20u, new AchievementSearchSortKey(0, 1, 1, 20)),
        (30u, new AchievementSearchSortKey(0, 1, 1, 30)));

    var results = AchievementSearchIndex.BuildResults(
        achievements,
        new AchievementSearchQueryState(
            string.Empty,
            CategoryFilterAll: true,
            SelectedCategoryFilters: [],
            SelectedSubcategoryFilters: [],
            SearchCompletionFilterPolicy.All,
            AchievementSearchSortMode.GameOrder),
        isComplete: _ => false,
        getSortKey: info => sortKeys[info.Id]);

    AssertSequence(results.Results.Select(info => info.Id).ToList(), [20, 30, 10]);
}

static void NativeUpdateBatchesDoNotParkAchievementWindows()
{
    AssertFalse(NativeAchievementUpdateWindowPolicy.ShouldParkDuringBatch(batchWindowWasOpenBeforeStart: false, completedAtLeastOneRequest: false), "queued native refreshes should not shrink/move the native Achievement window before first row settles");
    AssertFalse(NativeAchievementUpdateWindowPolicy.ShouldParkDuringBatch(batchWindowWasOpenBeforeStart: false, completedAtLeastOneRequest: true), "queued native refreshes should remain geometry-neutral even after one row settles");
    AssertFalse(NativeAchievementUpdateWindowPolicy.ShouldParkDuringBatch(batchWindowWasOpenBeforeStart: true, completedAtLeastOneRequest: true), "player-opened Achievement windows should never be parked by update batches");
}

static void ActiveRefreshPollsProgressSlotFallback()
{
    var source = File.ReadAllText("AchievementTracker/Services/AchievementProgressUpdater.cs");
    AssertTrue(source.Contains("TryGetFreshObservation(request.AchievementId, request.StartedAt", StringComparison.Ordinal), "active native refresh should poll the ClientStructs progress slot fallback instead of relying only on passive hook cache");
    AssertFalse(source.Contains("TryGetFreshCachedObservation(request.AchievementId, request.StartedAt", StringComparison.Ordinal), "active native refresh must not be hook-cache-only because hook install/firing failures make manual Update All and row refresh time out");
}

static void ProgressSlotFallbackDoesNotRestampUnchangedLoadedSlot()
{
    var previous = new ClientAchievementProgressSource.ProgressSlotFingerprint(750, 10, 100);
    var same = new ClientAchievementProgressSource.ProgressSlotFingerprint(750, 10, 100);
    var changedProgress = new ClientAchievementProgressSource.ProgressSlotFingerprint(750, 11, 100);
    var changedAchievement = new ClientAchievementProgressSource.ProgressSlotFingerprint(751, 10, 500);

    AssertTrue(ClientAchievementProgressSource.ShouldRecordProgressSlotObservation(previousSlotWasLoaded: false, previous, same), "first loaded slot sample should be recorded");
    AssertFalse(ClientAchievementProgressSource.ShouldRecordProgressSlotObservation(previousSlotWasLoaded: true, previous, same), "unchanged loaded slot should not get a new timestamp because that makes active refreshes close early on stale progress");
    AssertTrue(ClientAchievementProgressSource.ShouldRecordProgressSlotObservation(previousSlotWasLoaded: true, previous, changedProgress), "changed current progress should be recorded");
    AssertTrue(ClientAchievementProgressSource.ShouldRecordProgressSlotObservation(previousSlotWasLoaded: true, previous, changedAchievement), "changed achievement id should be recorded");
}

static void ActivityClassifierIgnoresTextOnlyActivityMessages()
{
    var textCases = new[]
    {
        ("You finish mining.", 16u),
        ("You finish quarrying.", 16u),
        ("You finish logging.", 17u),
        ("You finish harvesting.", 17u),
        ("You finish gathering.", 16u),
        ("You reel in a fish.", 18u),
        ("You catch a fish.", 18u),
        ("Synthesis succeeds.", 10u),
        ("You craft an item.", 8u),
    };

    foreach (var (text, classJobId) in textCases)
    {
        AssertFalse(AchievementActivityUpdateClassifier.TryClassify(0, text, classJobId, out _), $"text-only '{text}' should not classify");
    }
}

static void ActivityClassifierMatchesKnownLogMessageIds()
{
    AssertActivityClassification(1067, 0, "Miner", AchievementActivityUpdateClassifier.MiningTrigger);
    AssertActivityClassification(1068, 0, "Miner", AchievementActivityUpdateClassifier.QuarryingTrigger);
    AssertActivityClassification(1069, 0, "Botanist", AchievementActivityUpdateClassifier.LoggingTrigger);
    AssertActivityClassification(1070, 0, "Botanist", AchievementActivityUpdateClassifier.HarvestingTrigger);
    AssertActivityClassification(1114, 0, "Fisher", AchievementActivityUpdateClassifier.FishingTrigger);
    AssertActivityClassification(3576, 0, "Fisher", AchievementActivityUpdateClassifier.SpearfishingTrigger);
}

static void ActivityClassifierUsesVerifiedCraftingSuccessIdsOnly()
{
    AssertActivityClassification(1156, 10, "Armorer", AchievementActivityUpdateClassifier.CraftingTrigger);
    AssertActivityClassification(1158, 10, "Armorer", AchievementActivityUpdateClassifier.CraftingTrigger);

    AssertFalse(AchievementActivityUpdateClassifier.TryClassify(1159, "", 10, out _), "unverified 1159 should not classify");
    AssertFalse(AchievementActivityUpdateClassifier.TryClassify(1144, "", 10, out _), "crafting failure 1144 should not classify");
    AssertFalse(AchievementActivityUpdateClassifier.TryClassify(1223, "", 10, out _), "crafting failure 1223 should not classify");
    AssertFalse(AchievementActivityUpdateClassifier.TryClassify(1178, "", 10, out _), "crafting log completion should not classify");
}

static void CraftingLogCompletionConfigurationPathIsRemoved()
{
    var configurationSource = File.ReadAllText("AchievementTracker/Configuration.cs");
    var configWindowSource = File.ReadAllText("AchievementTracker/Windows/ConfigWindow.cs");

    AssertFalse(configurationSource.Contains("TriggerOnCraftingLogActivities", StringComparison.Ordinal), "crafting-log completion config flag should be removed");
    AssertFalse(configWindowSource.Contains("Crafting log completion", StringComparison.Ordinal), "crafting-log completion UI checkbox should be removed");
}

static void ActivityTriggerDelayPolicyDelaysOnlyCrafting()
{
    AssertEqualInt(6, (int)ActivityTriggerDelayPolicy.GetInitialDelay(AchievementActivityUpdateClassifier.CraftingTrigger).TotalSeconds);
    AssertEqualInt(0, (int)ActivityTriggerDelayPolicy.GetInitialDelay(AchievementActivityUpdateClassifier.MiningTrigger).TotalSeconds);
    AssertEqualInt(0, (int)ActivityTriggerDelayPolicy.GetInitialDelay(AchievementActivityUpdateClassifier.FishingTrigger).TotalSeconds);
}

static void ActivitySchedulerMarksSamePendingKeyDirty()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);
    var key = new ActivityUpdateKey("Crafting", "Carpenter");

    AssertEqualInt(2, scheduler.EnqueueActivityUpdateAll([101, 102], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));
    AssertEqualInt(0, scheduler.EnqueueActivityUpdateAll([101, 102], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));

    AssertEqualInt(2, scheduler.PendingCount);
    AssertTrue(scheduler.IsActivityKeyDirty(key), "duplicate pending key should be dirty");
    AssertEqualInt(1, scheduler.GetActivityKeyDirtyCount(key));
}

static void ActivitySchedulerSingleDuplicateDoesNotQueueDirtyFinalPass()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);
    var key = new ActivityUpdateKey("Crafting", "Carpenter");

    AssertEqualInt(1, scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));
    AssertEqualInt(0, scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));

    AssertTrue(scheduler.TryTakeDueRequest(now, out var original), "original carpenter request should be due");
    AssertEqualUInt(101u, original.AchievementId);
    scheduler.MarkActivityJobSettled(original.JobId, now);

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(5), out _), "one duplicate should be absorbed without dirty final pass");
}

static void ActivitySchedulerTwoDuplicatesQueueDirtyFinalPass()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);
    var key = new ActivityUpdateKey("Crafting", "Carpenter");

    AssertEqualInt(1, scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));
    AssertEqualInt(0, scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));
    AssertEqualInt(0, scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero));

    AssertTrue(scheduler.TryTakeDueRequest(now, out var original), "original carpenter request should be due");
    AssertEqualUInt(101u, original.AchievementId);
    scheduler.MarkActivityJobSettled(original.JobId, now);

    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(5), out var finalPass), "two duplicates should queue dirty final pass at same-id backoff");
    AssertEqualUInt(101u, finalPass.AchievementId);
    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(5), out _), "two duplicates should queue exactly one dirty final pass");
}

static void ActivitySchedulerAppendsDirtyFinalPassBehindLaterKeys()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);
    var carpenter = new ActivityUpdateKey("Crafting", "Carpenter");
    var armorer = new ActivityUpdateKey("Crafting", "Armorer");

    scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, carpenter, TimeSpan.Zero);
    scheduler.EnqueueActivityUpdateAll([201], "activity-trigger", TimeSpan.Zero, armorer, TimeSpan.Zero);
    scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, carpenter, TimeSpan.Zero);
    scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, carpenter, TimeSpan.Zero);

    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "carpenter first should be due");
    AssertEqualUInt(101u, first.AchievementId);
    scheduler.MarkActivityJobSettled(first.JobId, now);

    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(1), out var second), "armorer should stay before dirty final carpenter pass");
    AssertEqualUInt(201u, second.AchievementId);
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(5), out var third), "dirty final pass should append behind armorer and respect same-id backoff");
    AssertEqualUInt(101u, third.AchievementId);
}

static void ActivitySchedulerQueuesDifferentKeysNormally()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);

    AssertEqualInt(1, scheduler.EnqueueActivityUpdateAll([101], "activity-log-message-Carpenter", TimeSpan.Zero, new ActivityUpdateKey("Crafting", "Carpenter"), TimeSpan.Zero));
    AssertEqualInt(1, scheduler.EnqueueActivityUpdateAll([201], "activity-log-message-Armorer", TimeSpan.Zero, new ActivityUpdateKey("Crafting", "Armorer"), TimeSpan.Zero));

    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first activity request should be due");
    AssertEqual(NativeAchievementJobKind.Batch.ToString(), first.JobKind.ToString());
    AssertEqualInt(1, scheduler.PendingCount);
}

static void ManualSchedulerRequestsAreNotActivityKeyCoalesced()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(() => now, () => TimeSpan.Zero);
    var key = new ActivityUpdateKey("Crafting", "Carpenter");

    scheduler.EnqueueActivityUpdateAll([101], "activity-trigger", TimeSpan.Zero, key, TimeSpan.Zero);
    scheduler.EnqueueUpdateAll([201], "manual-update-all", TimeSpan.Zero);
    AssertEqualInt(2, scheduler.PendingCount);
    AssertFalse(scheduler.IsActivityKeyDirty(key), "manual update all should not mark activity key dirty");
}

static void AchievementCategoryPathSplitsTopLevelAndSubcategory()
{
    var simple = AchievementCategoryPath.Parse("Miner");
    AssertEqual("Miner", simple.Category);
    AssertEqual(string.Empty, simple.Subcategory);

    var nested = AchievementCategoryPath.Parse("Crafting & Gathering > Botanist > Harvesting");
    AssertEqual("Crafting & Gathering", nested.Category);
    AssertEqual("Harvesting", nested.Subcategory);

    var empty = AchievementCategoryPath.Parse("  >  ");
    AssertEqual(string.Empty, empty.Category);
    AssertEqual(string.Empty, empty.Subcategory);
}

static void AchievementCategoryPathMatchesExactCategoryOrFinalSubcategory()
{
    AssertTrue(AchievementCategoryPath.MatchesCategory("Miner", "Miner"), "exact category should match");
    AssertTrue(AchievementCategoryPath.MatchesCategory("Crafting & Gathering > Miner", "Miner"), "final subcategory should match");
    AssertFalse(AchievementCategoryPath.MatchesCategory("Battle > Miner Impostor", "Miner"), "partial subcategory text should not match");
    AssertFalse(AchievementCategoryPath.MatchesCategory("", "Miner"), "blank path should not match");
}

static void AssertActivityClassification(uint logMessageId, uint classJobId, string expectedCategory, string expectedTrigger)
{
    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(logMessageId, "text ignored", classJobId, out var category, out var trigger), $"log id {logMessageId} should classify");
    AssertEqual(expectedCategory, category);
    AssertEqual(expectedTrigger, trigger);
}

static void ActivityClassifierSelectsTrackedAchievementsByCategoryPath()
{
    var categories = new Dictionary<uint, string>
    {
        [1] = "Crafting & Gathering > Miner",
        [2] = "Crafting & Gathering > Botanist",
        [3] = "Battle > Miner Impostor",
    };

    var matches = AchievementActivityUpdateClassifier.SelectTrackedIdsForCategory([1, 2, 3], id => categories[id], "Miner");
    AssertSequence(matches, [1]);
}

static void ActivityTriggerCandidatesExcludeCosmicClassAchievements()
{
    var selected = ActivityTriggerCandidateSelection.ExcludeCosmicClassAchievements(
        [101, 3704, 101, 0, 3728, 202],
        CosmicClassProgressProvider.IsCosmicClassAchievement);

    AssertSequence(selected, [101, 202]);
}

static void TrackedToolbarHiddenStateShowsDefaultEye()
{
    var presentation = TrackedToolbarIconPresentation.ForHiddenState(hidden: true);

    AssertEqual("Eye", presentation.IconName);
    AssertEqual("Default", presentation.ColorName);
    AssertEqual("Show tracked achievement icons.", presentation.Tooltip);
}

static void TrackedToolbarShownStateShowsRedEye()
{
    var presentation = TrackedToolbarIconPresentation.ForHiddenState(hidden: false);

    AssertEqual("Eye", presentation.IconName);
    AssertEqual("Red", presentation.ColorName);
    AssertEqual("Hide tracked achievement icons.", presentation.Tooltip);
}

static void TrackedUpdateIndicatorShowsWorkingWhileQueueActive()
{
    AssertEqual(TrackedUpdateIndicatorState.Working.ToString(), TrackedUpdateIndicatorPolicy.GetState(pendingCount: 1, isUpdateInProgress: false, staleTrackedCount: 0).ToString());
    AssertEqual(TrackedUpdateIndicatorState.Working.ToString(), TrackedUpdateIndicatorPolicy.GetState(pendingCount: 0, isUpdateInProgress: true, staleTrackedCount: 0).ToString());
}

static void TrackedUpdateIndicatorShowsNeedsUpdateWhenStaleAndIdle()
{
    AssertEqual(TrackedUpdateIndicatorState.NeedsUpdate.ToString(), TrackedUpdateIndicatorPolicy.GetState(pendingCount: 0, isUpdateInProgress: false, staleTrackedCount: 1).ToString());
}

static void TrackedUpdateIndicatorShowsAllUpdatedWhenIdleWithoutStaleRows()
{
    AssertEqual(TrackedUpdateIndicatorState.AllUpdated.ToString(), TrackedUpdateIndicatorPolicy.GetState(pendingCount: 0, isUpdateInProgress: false, staleTrackedCount: 0).ToString());
}

static void AutoUpdateStatusRowFormatsRunningQueue()
{
    var text = AutoUpdateQueueStatusRow.Format(isRunning: true, tasksLeft: 3, elapsed: TimeSpan.FromSeconds(65));

    AssertEqual("Status: Running — 3 tasks left — running 1m 05s", text);
}

static void AutoUpdateStatusRowFormatsIdleQueue()
{
    var text = AutoUpdateQueueStatusRow.Format(isRunning: false, tasksLeft: 0, elapsed: null);

    AssertEqual("Status: Idle — 0 tasks left — running 0s", text);
}

static AchievementInfo SearchInfo(uint id, string name, string category)
    => new(id, name, $"Description for {name}", Points: 0, category);

static Dictionary<uint, AchievementSearchSortKey> SearchSortKeys(params (uint Id, AchievementSearchSortKey Key)[] entries)
    => entries.ToDictionary(entry => entry.Id, entry => entry.Key);

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'");
    }
}

static void AssertEqualUInt(uint expected, uint actual)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}

static void AssertEqualInt(int expected, int actual)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}

static void AssertTrue(bool value, string message)
{
    if (!value)
    {
        throw new InvalidOperationException($"Expected true: {message}");
    }
}

static void AssertFalse(bool value, string message)
{
    if (value)
    {
        throw new InvalidOperationException($"Expected false: {message}");
    }
}

static void AssertSequence(IReadOnlyList<uint> actual, IReadOnlyList<uint> expected)
{
    if (!actual.SequenceEqual(expected))
    {
        throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
    }
}
