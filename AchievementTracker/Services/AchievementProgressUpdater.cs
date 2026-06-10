using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementProgressUpdater
{
    private readonly AchievementProgressRequestScheduler scheduler;
    private readonly ClientAchievementProgressSource progressSource;
    private readonly Func<IReadOnlyList<uint>> autoUpdateIdsProvider;
    private readonly Func<bool> autoUpdateEnabledProvider;
    private readonly Func<int> autoUpdateIntervalSecondsProvider;
    private readonly Func<int> updateSpacingSecondsProvider;
    private readonly Action<string> debugLog;
    private DateTimeOffset nextAutoUpdateAt = DateTimeOffset.MinValue;

    public AchievementProgressUpdater(
        ClientAchievementProgressSource progressSource,
        Func<IReadOnlyList<uint>> autoUpdateIdsProvider,
        Func<bool> autoUpdateEnabledProvider,
        Func<int> autoUpdateIntervalSecondsProvider,
        Func<int> updateSpacingSecondsProvider,
        Action<string> debugLog)
    {
        this.scheduler = new AchievementProgressRequestScheduler();
        this.progressSource = progressSource;
        this.autoUpdateIdsProvider = autoUpdateIdsProvider;
        this.autoUpdateEnabledProvider = autoUpdateEnabledProvider;
        this.autoUpdateIntervalSecondsProvider = autoUpdateIntervalSecondsProvider;
        this.updateSpacingSecondsProvider = updateSpacingSecondsProvider;
        this.debugLog = debugLog;
    }

    public int PendingCount => this.scheduler.PendingCount;

    public DateTimeOffset? NextDueAt => this.scheduler.NextDueAt;

    public DateTimeOffset? NextAutoUpdateAt => this.autoUpdateEnabledProvider() && this.nextAutoUpdateAt != DateTimeOffset.MinValue
        ? this.nextAutoUpdateAt
        : null;

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason)
    {
        var now = DateTimeOffset.UtcNow;
        var ids = achievementIds.Where(id => id != 0).Distinct().ToList();
        if (IsUpdateAllReason(reason))
        {
            var beforeCount = ids.Count;
            ids = ids
                .Where(id => !this.progressSource.IsRecentlyObserved(id, now, ClientAchievementProgressSource.RecentlyObservedUpdateAllSkipThreshold))
                .ToList();
            var skippedCount = beforeCount - ids.Count;
            if (skippedCount > 0)
            {
                this.debugLog($"VAL DebugTrace QueueUpdateAllSkipRecentlyObserved reason={reason} skipped={skippedCount} thresholdSeconds={ClientAchievementProgressSource.RecentlyObservedUpdateAllSkipThreshold.TotalSeconds:0}");
            }
        }

        if (ids.Count == 0)
        {
            this.debugLog($"VAL DebugTrace QueueSkip reason={reason} no achievements selected");
            return;
        }

        var baseSpacingSeconds = Math.Clamp(this.updateSpacingSecondsProvider(), 0, 3600);
        this.scheduler.EnqueueUpdateAll(ids, reason, TimeSpan.FromSeconds(baseSpacingSeconds));
        this.debugLog($"VAL DebugTrace QueueUpdateAll reason={reason} count={ids.Count} pending={this.scheduler.PendingCount} spacingSeconds={baseSpacingSeconds} jitterSeconds=1-2 backoffSeconds=5");
    }

    public void Tick()
    {
        this.progressSource.UpdateCache();

        var now = DateTimeOffset.UtcNow;
        this.MaybeEnqueueAutoUpdate(now);

        if (this.scheduler.TryTakeDueRequest(now, out var request))
        {
            this.progressSource.RequestProgress(request.AchievementId, request.Reason);
            this.debugLog($"VAL DebugTrace RequestDequeued id={request.AchievementId} reason={request.Reason} pending={this.scheduler.PendingCount}");
        }
    }

    public void Clear()
    {
        this.scheduler.Clear();
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
    }

    public void ResetAutoUpdateCountdown()
    {
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.debugLog("VAL DebugTrace AutoUpdateReset");
    }

    private static bool IsUpdateAllReason(string reason)
        => string.Equals(reason, "manual-update-all", StringComparison.Ordinal)
            || string.Equals(reason, "auto-update", StringComparison.Ordinal);

    private void MaybeEnqueueAutoUpdate(DateTimeOffset now)
    {
        if (!this.autoUpdateEnabledProvider())
        {
            this.nextAutoUpdateAt = DateTimeOffset.MinValue;
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(this.autoUpdateIntervalSecondsProvider(), 1, 86400));
        if (this.nextAutoUpdateAt == DateTimeOffset.MinValue)
        {
            this.nextAutoUpdateAt = now + interval;
            this.debugLog($"VAL DebugTrace AutoUpdateArmed next={this.nextAutoUpdateAt:O} intervalSeconds={interval.TotalSeconds:0}");
            return;
        }

        if (now < this.nextAutoUpdateAt || this.scheduler.HasPendingRequests)
        {
            return;
        }

        var ids = this.autoUpdateIdsProvider();
        this.EnqueueUpdateAll(ids, "auto-update");
        this.nextAutoUpdateAt = now + interval;
        this.debugLog($"VAL DebugTrace AutoUpdateScheduled next={this.nextAutoUpdateAt:O} included={ids.Count}");
    }
}
