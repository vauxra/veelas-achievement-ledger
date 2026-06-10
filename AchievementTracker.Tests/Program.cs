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

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'");
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
