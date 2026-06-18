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
    NativeAchievementJobKind JobKind = NativeAchievementJobKind.Single,
    ActivityUpdateKey? ActivityKey = null);

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
    private readonly Dictionary<Guid, ActivityJobInfo> activityJobs = new();
    private readonly HashSet<ActivityUpdateKey> activeActivityKeys = [];
    private readonly HashSet<ActivityUpdateKey> dirtyActivityKeys = [];
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
        => _ = this.EnqueueUpdateAllAndCount(achievementIds, reason, baseSpacing);

    public int EnqueueUpdateAllAndCount(IEnumerable<uint> achievementIds, string reason, TimeSpan baseSpacing)
        => this.EnqueueActions(achievementIds, reason, baseSpacing, NativeAchievementActionKind.Refresh);

    public int EnqueueActivityUpdateAll(
        IEnumerable<uint> achievementIds,
        string reason,
        TimeSpan baseSpacing,
        ActivityUpdateKey activityKey,
        TimeSpan initialDelay)
    {
        var normalizedIds = achievementIds.Where(id => id != 0).Distinct().ToList();
        if (normalizedIds.Count == 0)
        {
            return 0;
        }

        if (this.HasPendingOrActiveActivityKey(activityKey))
        {
            this.dirtyActivityKeys.Add(activityKey);
            this.UpdateLatestActivityJobIds(activityKey, normalizedIds);
            return 0;
        }

        return this.EnqueueActions(normalizedIds, reason, baseSpacing, NativeAchievementActionKind.Refresh, activityKey, initialDelay);
    }

    public bool IsActivityKeyDirty(ActivityUpdateKey activityKey)
        => this.dirtyActivityKeys.Contains(activityKey);

    public int ActiveOrPendingActivityKeyCount => this.activeActivityKeys
        .Concat(this.pendingRequests
            .Select(request => request.ActivityKey)
            .Where(key => key.HasValue)
            .Select(key => key!.Value))
        .Distinct()
        .Count();

    public void MarkActivityJobSettled(Guid jobId, DateTimeOffset now)
    {
        if (!this.activityJobs.TryGetValue(jobId, out var info)
            || this.HasPendingRequestsForJob(jobId))
        {
            return;
        }

        this.activityJobs.Remove(jobId);
        this.activeActivityKeys.Remove(info.Key);
        if (!this.dirtyActivityKeys.Remove(info.Key))
        {
            return;
        }

        this.EnqueueActions(info.AchievementIds, info.Reason, info.BaseSpacing, NativeAchievementActionKind.Refresh, info.Key, TimeSpan.Zero);
    }

    public bool EnqueueInspection(uint achievementId, string reason)
        => this.EnqueueActions([achievementId], reason, TimeSpan.Zero, NativeAchievementActionKind.Inspection) > 0;

    private int EnqueueActions(
        IEnumerable<uint> achievementIds,
        string reason,
        TimeSpan baseSpacing,
        NativeAchievementActionKind kind,
        ActivityUpdateKey? activityKey = null,
        TimeSpan initialDelay = default)
    {
        var now = this.nowProvider();
        var normalizedBaseSpacing = NormalizeBaseSpacing(baseSpacing);
        var cursor = this.pendingRequests.Count > 0 && this.nextBatchCursor > now ? this.nextBatchCursor : now;
        cursor += NormalizeBaseSpacing(initialDelay);
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
            this.pendingRequests.Add(new ScheduledAchievementProgressRequest(achievementId, dueAt, reason, kind, itemJobId, jobKind, activityKey));
            added++;
            cursor = dueAt + ImmutableActionSpacing + normalizedBaseSpacing + NormalizeJitter(this.jitterProvider());
        }

        this.nextBatchCursor = cursor;
        this.pendingRequests.Sort(static (left, right) => left.DueAt.CompareTo(right.DueAt));
        if (added > 0 && activityKey.HasValue)
        {
            this.activityJobs[jobId] = new ActivityJobInfo(activityKey.Value, normalizedIds.Distinct().ToList(), reason, normalizedBaseSpacing);
        }

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
        if (request.ActivityKey.HasValue)
        {
            this.activeActivityKeys.Add(request.ActivityKey.Value);
        }

        return true;
    }

    public void Clear()
    {
        this.pendingRequests.Clear();
        this.lastRequestedAt.Clear();
        this.activityJobs.Clear();
        this.activeActivityKeys.Clear();
        this.dirtyActivityKeys.Clear();
        this.nextBatchCursor = DateTimeOffset.MinValue;
    }

    private bool HasPendingOrActiveActivityKey(ActivityUpdateKey activityKey)
        => this.activeActivityKeys.Contains(activityKey)
            || this.pendingRequests.Any(request => request.ActivityKey == activityKey);

    private void UpdateLatestActivityJobIds(ActivityUpdateKey activityKey, IReadOnlyList<uint> achievementIds)
    {
        foreach (var (jobId, info) in this.activityJobs.ToList())
        {
            if (info.Key == activityKey)
            {
                this.activityJobs[jobId] = info with { AchievementIds = achievementIds };
            }
        }
    }

    private static NativeAchievementJobKind DetermineRefreshJobKind(int normalizedIdCount, string reason)
        => normalizedIdCount > 1
            || string.Equals(reason, "manual-update-all", StringComparison.Ordinal)
            || string.Equals(reason, "auto-update", StringComparison.Ordinal)
            || IsActivityReason(reason)
                ? NativeAchievementJobKind.Batch
                : NativeAchievementJobKind.Single;

    private static bool IsActivityReason(string reason)
        => reason.StartsWith("activity-", StringComparison.Ordinal);

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

    private readonly record struct ActivityJobInfo(
        ActivityUpdateKey Key,
        IReadOnlyList<uint> AchievementIds,
        string Reason,
        TimeSpan BaseSpacing);
}
