using AchievementTracker.Models;
using AchievementTracker.Services;

var tests = new List<(string Name, Action Body)>
{
    ("Add allows up to five unique achievement ids", AddAllowsUpToFiveUniqueAchievementIds),
    ("Add rejects duplicate achievement ids", AddRejectsDuplicateAchievementIds),
    ("Remove deletes the selected achievement id", RemoveDeletesSelectedAchievementId),
    ("MoveUp reorders an item toward the start", MoveUpReordersItemTowardStart),
    ("MoveDown reorders an item toward the end", MoveDownReordersItemTowardEnd),
    ("LoadFrom sanitizes duplicates and trims to five", LoadFromSanitizesDuplicatesAndTrimsToFive),
    ("Progress display formats all safe states", ProgressDisplayFormatsAllSafeStates),
    ("Update all spaces queued requests by at least fifteen seconds plus jitter", UpdateAllSpacesQueuedRequestsByAtLeastFifteenSecondsPlusJitter),
    ("Request scheduler applies five second per-achievement backoff", RequestSchedulerAppliesFiveSecondPerAchievementBackoff),
    ("Auto updater selects only explicitly included tracked achievements", AutoUpdaterSelectsOnlyExplicitlyIncludedTrackedAchievements),
};

foreach (var test in tests)
{
    test.Body();
    Console.WriteLine($"PASS {test.Name}");
}

static void AddAllowsUpToFiveUniqueAchievementIds()
{
    var store = new TrackedAchievementStore();

    AssertTrue(store.TryAdd(1), "1 should be added");
    AssertTrue(store.TryAdd(2), "2 should be added");
    AssertTrue(store.TryAdd(3), "3 should be added");
    AssertTrue(store.TryAdd(4), "4 should be added");
    AssertTrue(store.TryAdd(5), "5 should be added");
    AssertFalse(store.TryAdd(6), "6 should be rejected because max tracked achievements is five");

    AssertSequence(store.AchievementIds, [1, 2, 3, 4, 5]);
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

static void LoadFromSanitizesDuplicatesAndTrimsToFive()
{
    var store = new TrackedAchievementStore();

    store.LoadFrom([1, 2, 2, 3, 4, 5, 6]);

    AssertSequence(store.AchievementIds, [1, 2, 3, 4, 5]);
}

static void ProgressDisplayFormatsAllSafeStates()
{
    AssertEqual("Open Achievements to load status", AchievementProgress.CompletionListNotLoaded().ToDisplayText());
    AssertEqual("Complete", AchievementProgress.Complete().ToDisplayText());
    AssertEqual("Incomplete", AchievementProgress.Incomplete().ToDisplayText());
    AssertEqual("437 / 1,000", AchievementProgress.Numeric(437, 1000).ToDisplayText());
    AssertEqual("Current unavailable / 1,500", AchievementProgress.TargetKnown(1500).ToDisplayText());
    AssertEqual("Progress unavailable", AchievementProgress.Unavailable().ToDisplayText());
}

static void UpdateAllSpacesQueuedRequestsByAtLeastFifteenSecondsPlusJitter()
{
    var now = new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero);
    var scheduler = new AchievementProgressRequestScheduler(
        () => now,
        () => TimeSpan.FromMilliseconds(750));

    scheduler.EnqueueUpdateAll([101, 102, 103], "test");

    AssertEqualInt(3, scheduler.PendingCount);
    AssertTrue(scheduler.TryTakeDueRequest(now, out var first), "first request should be due immediately");
    AssertEqualUInt(101u, first.AchievementId);

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(15), out _), "second request should include jitter beyond 15 seconds");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(15).AddMilliseconds(750), out var second), "second request should be due after spacing plus jitter");
    AssertEqualUInt(102u, second.AchievementId);

    AssertFalse(scheduler.TryTakeDueRequest(now.AddSeconds(31), out _), "third request should include cumulative jitter");
    AssertTrue(scheduler.TryTakeDueRequest(now.AddSeconds(31).AddMilliseconds(500), out var third), "third request should be due after cumulative spacing plus jitter");
    AssertEqualUInt(103u, third.AchievementId);
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
