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
    ("Scale policy restores when idle only for parked windows", ScalePolicyRestoresWhenIdleOnlyForParkedWindows),
    ("Scale policy restores only on user-visible reopen", ScalePolicyRestoresOnlyOnUserVisibleReopen),
    ("Configuration defaults use requested main column widths", ConfigurationDefaultsUseRequestedMainColumnWidths),
    ("Tracked display evaluates cosmic progress overrides", TrackedDisplayEvaluatesCosmicProgressOverrides),
    ("Cosmic progress override parses achievement details", CosmicProgressOverrideParsesAchievementDetails),
    ("Auto updater selects only explicitly included tracked achievements", AutoUpdaterSelectsOnlyExplicitlyIncludedTrackedAchievements),
    ("Completion filters wait for loaded achievement state", CompletionFiltersWaitForLoadedAchievementState),
    ("Lumina search all does not wait for loaded achievement state", LuminaSearchAllDoesNotWaitForLoadedAchievementState),
    ("Native update batches do not park achievement windows", NativeUpdateBatchesDoNotParkAchievementWindows),
    ("Activity classifier matches finish mining to miner category", ActivityClassifierMatchesFinishMiningToMinerCategory),
    ("Activity classifier selects tracked achievements by category path", ActivityClassifierSelectsTrackedAchievementsByCategoryPath),
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

static void CompletionFiltersWaitForLoadedAchievementState()
{
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: false, updateInProgress: false), "Completed search should wait for loaded completion state");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Incomplete", completionStateLoaded: false, updateInProgress: false), "Incomplete search should wait for loaded completion state");
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: true, updateInProgress: false), "Completed search can run after completion state loads");
    AssertTrue(SearchCompletionFilterPolicy.Matches("Completed", isComplete: true), "completed filter should match completed rows");
    AssertFalse(SearchCompletionFilterPolicy.Matches("Completed", isComplete: false), "completed filter should reject incomplete rows");
    AssertTrue(SearchCompletionFilterPolicy.Matches("Incomplete", isComplete: false), "incomplete filter should match incomplete rows");
    AssertFalse(SearchCompletionFilterPolicy.Matches("Incomplete", isComplete: true), "incomplete filter should reject completed rows");
}

static void LuminaSearchAllDoesNotWaitForLoadedAchievementState()
{
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: false, updateInProgress: false), "Lumina-only All search should not wait for the session's initial achievement load");
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: true, updateInProgress: true), "Lumina-only All search should not pause while update tasks are running");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: false, updateInProgress: false), "Completed search still needs loaded completion state");
}

static void NativeUpdateBatchesDoNotParkAchievementWindows()
{
    AssertFalse(NativeAchievementUpdateWindowPolicy.ShouldParkDuringBatch(batchWindowWasOpenBeforeStart: false, completedAtLeastOneRequest: false), "queued native refreshes should not shrink/move the native Achievement window before first row settles");
    AssertFalse(NativeAchievementUpdateWindowPolicy.ShouldParkDuringBatch(batchWindowWasOpenBeforeStart: false, completedAtLeastOneRequest: true), "queued native refreshes should remain geometry-neutral even after one row settles");
    AssertFalse(NativeAchievementUpdateWindowPolicy.ShouldParkDuringBatch(batchWindowWasOpenBeforeStart: true, completedAtLeastOneRequest: true), "player-opened Achievement windows should never be parked by update batches");
}

static void ActivityClassifierMatchesFinishMiningToMinerCategory()
{
    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(1067, "", 0, out var fromLogId), "log id 1067 should classify as miner");
    AssertEqual("Miner", fromLogId);

    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(1068, "", 0, out var fromQuarry), "log id 1068 should classify as miner");
    AssertEqual("Miner", fromQuarry);

    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(0, "You finish mining.", 16, out var fromText), "finish mining text should classify as miner");
    AssertEqual("Miner", fromText);

    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(1069, "", 0, out var fromLogging), "log id 1069 should classify as botanist");
    AssertEqual("Botanist", fromLogging);

    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(1070, "", 0, out var fromHarvesting), "log id 1070 should classify as botanist");
    AssertEqual("Botanist", fromHarvesting);

    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(1158, "", 10, out var fromCrafting), "craft success should classify by current crafter job");
    AssertEqual("Armorer", fromCrafting);

    AssertTrue(AchievementActivityUpdateClassifier.TryClassify(3512, "", 0, out var fromFishing), "fish catch should classify as fisher");
    AssertEqual("Fisher", fromFishing);
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
