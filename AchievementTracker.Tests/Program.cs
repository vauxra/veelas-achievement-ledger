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
    ("Progress request throttler blocks repeated requests inside cooldown", ProgressRequestThrottlerBlocksRepeatedRequestsInsideCooldown),
    ("Progress request throttler clear resets cooldown state", ProgressRequestThrottlerClearResetsCooldownState),
    ("Progress refresh queue de-duplicates and drains sequentially", ProgressRefreshQueueDeduplicatesAndDrainsSequentially),
    ("Progress refresh queue applies cumulative jitter before readiness", ProgressRefreshQueueAppliesCumulativeJitterBeforeReadiness),
    ("Progress refresh queue clear drops pending work", ProgressRefreshQueueClearDropsPendingWork),
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

static void ProgressRequestThrottlerBlocksRepeatedRequestsInsideCooldown()
{
    var throttler = new ProgressRequestThrottler(TimeSpan.FromSeconds(30));
    var now = new DateTimeOffset(2026, 6, 8, 22, 0, 0, TimeSpan.Zero);

    AssertTrue(throttler.TryMarkRequest(137, now), "first request should be allowed");
    AssertTrue(!throttler.TryMarkRequest(137, now.AddSeconds(10)), "same achievement should be throttled inside cooldown");
    AssertTrue(throttler.TryMarkRequest(138, now.AddSeconds(10)), "different achievement should be allowed");
    AssertTrue(throttler.TryMarkRequest(137, now.AddSeconds(31)), "same achievement should be allowed after cooldown");
}

static void ProgressRequestThrottlerClearResetsCooldownState()
{
    var throttler = new ProgressRequestThrottler(TimeSpan.FromSeconds(30));
    var now = new DateTimeOffset(2026, 6, 8, 22, 0, 0, TimeSpan.Zero);

    AssertTrue(throttler.TryMarkRequest(137, now), "first request should be allowed");
    throttler.Clear();
    AssertTrue(throttler.TryMarkRequest(137, now.AddSeconds(1)), "clear should remove old per-achievement cooldown state");
}

static void ProgressRefreshQueueDeduplicatesAndDrainsSequentially()
{
    var queue = new ProgressRefreshQueue();

    queue.Enqueue([137, 138, 137, 139]);

    AssertTrue(queue.TryPeek(out var first), "first queued item should exist");
    AssertEqualUInt(137u, first);
    queue.Dequeue();
    AssertTrue(queue.TryPeek(out var second), "second queued item should exist");
    AssertEqualUInt(138u, second);
    queue.Dequeue();
    AssertTrue(queue.TryPeek(out var third), "third queued item should exist");
    AssertEqualUInt(139u, third);
    queue.Dequeue();
    AssertTrue(!queue.TryPeek(out _), "queue should be empty after three unique ids");
}

static void ProgressRefreshQueueClearDropsPendingWork()
{
    var queue = new ProgressRefreshQueue();

    queue.Enqueue([137, 138, 139]);
    queue.Clear();

    AssertTrue(!queue.TryPeek(out _), "clear should drop queued achievement refresh work");
    queue.Enqueue([137]);
    AssertTrue(queue.TryPeek(out var firstAfterClear), "queue should accept an id again after clear");
    AssertEqualUInt(137u, firstAfterClear);
}

static void ProgressRefreshQueueAppliesCumulativeJitterBeforeReadiness()
{
    var queue = new ProgressRefreshQueue(() => TimeSpan.FromMilliseconds(500));
    var now = new DateTimeOffset(2026, 6, 9, 12, 0, 0, TimeSpan.Zero);

    queue.Enqueue([137, 138], now);

    AssertFalse(queue.TryPeekReady(now.AddMilliseconds(499), out _), "first request should wait for jitter delay");
    AssertTrue(queue.TryPeekReady(now.AddMilliseconds(500), out var first), "first request should become ready after first jitter delay");
    AssertEqualUInt(137u, first);
    queue.Dequeue();

    AssertFalse(queue.TryPeekReady(now.AddMilliseconds(999), out _), "second request should wait for cumulative jitter delay");
    AssertTrue(queue.TryPeekReady(now.AddMilliseconds(1000), out var second), "second request should become ready after second jitter delay");
    AssertEqualUInt(138u, second);
}

static void AssertEqualUInt(uint expected, uint actual)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'");
    }
}

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'");
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
