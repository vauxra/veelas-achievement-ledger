using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public readonly record struct ScheduledAchievementProgressRequest(uint AchievementId, DateTimeOffset DueAt, string Reason);

public sealed class AchievementProgressRequestScheduler
{
    public static readonly TimeSpan PerAchievementBackoff = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DefaultUpdateAllSpacing = TimeSpan.FromSeconds(15);

    private readonly Func<DateTimeOffset> nowProvider;
    private readonly Func<TimeSpan> jitterProvider;
    private readonly List<ScheduledAchievementProgressRequest> pendingRequests = [];
    private readonly Dictionary<uint, DateTimeOffset> lastRequestedAt = new();
    private DateTimeOffset nextBatchCursor = DateTimeOffset.MinValue;

    public AchievementProgressRequestScheduler(Func<DateTimeOffset>? nowProvider = null, Func<TimeSpan>? jitterProvider = null)
    {
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        this.jitterProvider = jitterProvider ?? CreateDefaultJitter;
    }

    public int PendingCount => this.pendingRequests.Count;

    public bool HasPendingRequests => this.pendingRequests.Count > 0;

    public DateTimeOffset? NextDueAt => this.pendingRequests.Count == 0
        ? null
        : this.pendingRequests.Min(request => request.DueAt);

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason)
        => this.EnqueueUpdateAll(achievementIds, reason, DefaultUpdateAllSpacing);

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason, TimeSpan baseSpacing)
    {
        var now = this.nowProvider();
        var normalizedBaseSpacing = NormalizeBaseSpacing(baseSpacing);
        var cursor = this.pendingRequests.Count > 0 && this.nextBatchCursor > now ? this.nextBatchCursor : now;
        var seen = new HashSet<uint>();

        foreach (var achievementId in achievementIds.Where(id => id != 0))
        {
            if (!seen.Add(achievementId) || this.pendingRequests.Any(request => request.AchievementId == achievementId))
            {
                continue;
            }

            var dueAt = cursor;
            if (this.lastRequestedAt.TryGetValue(achievementId, out var lastRequest))
            {
                var backoffUntil = lastRequest + PerAchievementBackoff;
                if (backoffUntil > dueAt)
                {
                    dueAt = backoffUntil;
                }
            }

            this.pendingRequests.Add(new ScheduledAchievementProgressRequest(achievementId, dueAt, reason));
            cursor = dueAt + normalizedBaseSpacing + NormalizeJitter(this.jitterProvider());
        }

        this.nextBatchCursor = cursor;
        this.pendingRequests.Sort(static (left, right) => left.DueAt.CompareTo(right.DueAt));
    }

    public bool TryTakeDueRequest(DateTimeOffset now, out ScheduledAchievementProgressRequest request)
    {
        var index = this.pendingRequests.FindIndex(candidate => candidate.DueAt <= now);
        if (index < 0)
        {
            request = default;
            return false;
        }

        request = this.pendingRequests[index];
        this.pendingRequests.RemoveAt(index);
        this.lastRequestedAt[request.AchievementId] = now;
        return true;
    }

    public void Clear()
    {
        this.pendingRequests.Clear();
        this.lastRequestedAt.Clear();
        this.nextBatchCursor = DateTimeOffset.MinValue;
    }

    private static TimeSpan CreateDefaultJitter()
    {
        // Always jitter request spacing by roughly 1-2 seconds, even when base spacing is 0.
        return TimeSpan.FromMilliseconds(Random.Shared.Next(1000, 2001));
    }

    private static TimeSpan NormalizeBaseSpacing(TimeSpan baseSpacing)
    {
        if (baseSpacing < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return baseSpacing;
    }

    private static TimeSpan NormalizeJitter(TimeSpan jitter)
    {
        if (jitter < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return jitter;
    }
}
