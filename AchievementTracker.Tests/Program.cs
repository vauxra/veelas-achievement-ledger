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
    ("Request scheduler applies five second per-achievement backoff", RequestSchedulerAppliesFiveSecondPerAchievementBackoff),
    ("Auto updater selects only explicitly included tracked achievements", AutoUpdaterSelectsOnlyExplicitlyIncludedTrackedAchievements),
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

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(7), out _), "second request should include jitter beyond base spacing");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(7).AddMilliseconds(750), out var second), "second request should be due after spacing plus jitter");
    AssertEqualUInt(102u, second.AchievementId);

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(15), out _), "third request should include cumulative jitter");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(15).AddMilliseconds(500), out var third), "third request should be due after cumulative spacing plus jitter");
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
    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(1), out _), "second request should still wait for jitter");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(1.5), out var second), "second request should be due after jitter");
    AssertEqualUInt(202u, second.AchievementId);
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

static void AutoUpdaterSelectsOnlyExplicitlyIncludedTrackedAchievements()
{
    AssertSequence(AutoUpdateSelection.SelectIncludedTrackedAchievements([1, 2, 3, 4], [2, 4, 999]), [2, 4]);
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
