using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public enum NativeAchievementActionKind
{
    Refresh,
    Inspection,
}

public enum NativeAchievementJobKind
{
    Single,
    Batch,
    Inspection,
}

public readonly record struct ScheduledAchievementProgressRequest(
    uint AchievementId,
    DateTimeOffset DueAt,
    string Reason,
    NativeAchievementActionKind Kind = NativeAchievementActionKind.Refresh,
    Guid JobId = default,
    NativeAchievementJobKind JobKind = NativeAchievementJobKind.Single);

public sealed class AchievementProgressRequestScheduler
{
    public const int MaxPendingRequests = 100;
    public static readonly TimeSpan ImmutableActionSpacing = TimeSpan.FromSeconds(1);
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

    public bool HasPendingRequestsForJob(Guid jobId)
        => jobId != Guid.Empty && this.pendingRequests.Any(request => request.JobId == jobId);

    public int PendingCountForJob(Guid jobId)
        => jobId == Guid.Empty ? 0 : this.pendingRequests.Count(request => request.JobId == jobId);

    public DateTimeOffset? NextDueAt => this.pendingRequests.Count == 0
        ? null
        : this.pendingRequests.Min(request => request.DueAt);

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason)
        => this.EnqueueUpdateAll(achievementIds, reason, DefaultUpdateAllSpacing);

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason, TimeSpan baseSpacing)
        => this.EnqueueActions(achievementIds, reason, baseSpacing, NativeAchievementActionKind.Refresh);

    public bool EnqueueInspection(uint achievementId, string reason)
        => this.EnqueueActions([achievementId], reason, TimeSpan.Zero, NativeAchievementActionKind.Inspection) > 0;

    private int EnqueueActions(
        IEnumerable<uint> achievementIds,
        string reason,
        TimeSpan baseSpacing,
        NativeAchievementActionKind kind)
    {
        var now = this.nowProvider();
        var normalizedBaseSpacing = NormalizeBaseSpacing(baseSpacing);
        var cursor = this.pendingRequests.Count > 0 && this.nextBatchCursor > now ? this.nextBatchCursor : now;
        var normalizedIds = achievementIds.Where(id => id != 0).ToList();
        var jobId = Guid.NewGuid();
        var jobKind = kind == NativeAchievementActionKind.Inspection
            ? NativeAchievementJobKind.Inspection
            : DetermineRefreshJobKind(normalizedIds.Count, reason);
        var seen = new HashSet<uint>();
        var added = 0;

        foreach (var achievementId in normalizedIds)
        {
            if (this.pendingRequests.Count >= MaxPendingRequests)
            {
                break;
            }

            if (!seen.Add(achievementId) || this.pendingRequests.Any(request => request.AchievementId == achievementId && request.Kind == kind))
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

            var itemJobId = kind == NativeAchievementActionKind.Inspection ? Guid.NewGuid() : jobId;
            this.pendingRequests.Add(new ScheduledAchievementProgressRequest(achievementId, dueAt, reason, kind, itemJobId, jobKind));
            added++;
            cursor = dueAt + ImmutableActionSpacing + normalizedBaseSpacing + NormalizeJitter(this.jitterProvider());
        }

        this.nextBatchCursor = cursor;
        this.pendingRequests.Sort(static (left, right) => left.DueAt.CompareTo(right.DueAt));
        return added;
    }

    public void Requeue(ScheduledAchievementProgressRequest request, DateTimeOffset dueAt)
    {
        if (this.pendingRequests.Count >= MaxPendingRequests)
        {
            return;
        }

        this.pendingRequests.Add(request with { DueAt = dueAt });
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

    private static NativeAchievementJobKind DetermineRefreshJobKind(int normalizedIdCount, string reason)
        => normalizedIdCount > 1
            || string.Equals(reason, "manual-update-all", StringComparison.Ordinal)
            || string.Equals(reason, "auto-update", StringComparison.Ordinal)
            || string.Equals(reason, "activity-trigger", StringComparison.Ordinal)
                ? NativeAchievementJobKind.Batch
                : NativeAchievementJobKind.Single;

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
