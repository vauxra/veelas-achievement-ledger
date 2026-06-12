using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementProgressUpdater
{
    private static readonly TimeSpan ColdNativeWindowMinimumWait = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ColdNativeWindowMaximumWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WarmNativeInputBuffer = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WarmNativeMaximumWait = TimeSpan.FromSeconds(5);

    private readonly AchievementProgressRequestScheduler scheduler;
    private readonly ClientAchievementProgressSource progressSource;
    private readonly NativeAchievementNavigator nativeAchievementNavigator;
    private readonly Func<IReadOnlyList<uint>> autoUpdateIdsProvider;
    private readonly Func<bool> autoUpdateEnabledProvider;
    private readonly Func<int> autoUpdateIntervalSecondsProvider;
    private readonly Func<int> updateSpacingSecondsProvider;
    private readonly Action<string> debugLog;
    private DateTimeOffset nextAutoUpdateAt = DateTimeOffset.MinValue;
    private ActiveNativeAchievementRequest? activeNativeRequest;
    private bool batchInProgress;
    private bool nativeWindowWasOpenBeforeBatch;
    private bool nativeWindowOpenedByVal;

    public AchievementProgressUpdater(
        ClientAchievementProgressSource progressSource,
        NativeAchievementNavigator nativeAchievementNavigator,
        Func<IReadOnlyList<uint>> autoUpdateIdsProvider,
        Func<bool> autoUpdateEnabledProvider,
        Func<int> autoUpdateIntervalSecondsProvider,
        Func<int> updateSpacingSecondsProvider,
        Action<string> debugLog)
    {
        this.scheduler = new AchievementProgressRequestScheduler();
        this.progressSource = progressSource;
        this.nativeAchievementNavigator = nativeAchievementNavigator;
        this.autoUpdateIdsProvider = autoUpdateIdsProvider;
        this.autoUpdateEnabledProvider = autoUpdateEnabledProvider;
        this.autoUpdateIntervalSecondsProvider = autoUpdateIntervalSecondsProvider;
        this.updateSpacingSecondsProvider = updateSpacingSecondsProvider;
        this.debugLog = debugLog;
    }

    public int PendingCount => this.scheduler.PendingCount + (this.activeNativeRequest.HasValue ? 1 : 0);

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
        this.debugLog($"VAL DebugTrace QueueUpdateAll reason={reason} count={ids.Count} pending={this.PendingCount} spacingSeconds={baseSpacingSeconds} jitterSeconds=1-2 backoffSeconds=5 executor=native-agent");
    }

    public void Tick()
    {
        this.progressSource.UpdateCache();

        var now = DateTimeOffset.UtcNow;
        this.MaybeEnqueueAutoUpdate(now);
        this.ProcessActiveNativeRequest(now);

        if (!this.activeNativeRequest.HasValue && this.scheduler.TryTakeDueRequest(now, out var request))
        {
            this.StartNativeRequest(request, now);
        }

        this.FinishBatchIfIdle();
    }

    public void Clear()
    {
        this.scheduler.Clear();
        this.activeNativeRequest = null;
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.FinishBatchIfIdle(force: true);
    }

    public void ResetAutoUpdateCountdown()
    {
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.debugLog("VAL DebugTrace AutoUpdateReset");
    }

    private static bool IsUpdateAllReason(string reason)
        => string.Equals(reason, "manual-update-all", StringComparison.Ordinal)
            || string.Equals(reason, "auto-update", StringComparison.Ordinal);

    private void StartNativeRequest(ScheduledAchievementProgressRequest request, DateTimeOffset now)
    {
        if (!this.batchInProgress)
        {
            this.nativeWindowWasOpenBeforeBatch = this.nativeAchievementNavigator.IsOpen;
            this.nativeWindowOpenedByVal = !this.nativeWindowWasOpenBeforeBatch;
            this.batchInProgress = true;
            this.debugLog($"VAL DebugTrace NativeBatchStart wasOpen={this.nativeWindowWasOpenBeforeBatch}");
        }

        var nativeWindowOpenBeforeRequest = this.nativeAchievementNavigator.IsOpen;
        var coldOpen = !nativeWindowOpenBeforeRequest;
        if (!this.nativeAchievementNavigator.OpenAchievement(request.AchievementId))
        {
            this.debugLog($"VAL DebugTrace NativeOpenFailed id={request.AchievementId} reason={request.Reason} pending={this.scheduler.PendingCount}");
            return;
        }

        if (coldOpen)
        {
            this.nativeWindowOpenedByVal = true;
        }

        var minimumWait = coldOpen ? ColdNativeWindowMinimumWait : WarmNativeInputBuffer;
        var maximumWait = coldOpen ? ColdNativeWindowMaximumWait : WarmNativeMaximumWait;
        this.activeNativeRequest = new ActiveNativeAchievementRequest(
            request.AchievementId,
            request.Reason,
            now,
            now + minimumWait,
            now + maximumWait,
            coldOpen);

        var parked = this.nativeAchievementNavigator.TryParkAchievementWindow();
        this.debugLog($"VAL DebugTrace NativeOpenSent id={request.AchievementId} reason={request.Reason} cold={coldOpen} parked={parked} minWaitSeconds={minimumWait.TotalSeconds:0} maxWaitSeconds={maximumWait.TotalSeconds:0} pending={this.scheduler.PendingCount}");
    }

    private void ProcessActiveNativeRequest(DateTimeOffset now)
    {
        if (!this.activeNativeRequest.HasValue)
        {
            return;
        }

        var request = this.activeNativeRequest.Value;
        if (!this.nativeAchievementNavigator.HasParkedWindow && this.nativeAchievementNavigator.TryParkAchievementWindow())
        {
            this.debugLog($"VAL DebugTrace NativeWindowParked id={request.AchievementId} scale=0.55 x=20 y=20");
        }

        if (now < request.MinimumCompleteAt)
        {
            return;
        }

        if (this.progressSource.TryGetFreshObservation(request.AchievementId, request.StartedAt, out var progress))
        {
            this.debugLog($"VAL DebugTrace NativeOpenLoaded id={request.AchievementId} reason={request.Reason} current={progress.Current} max={progress.Max} source={progress.Source} elapsedMs={(now - request.StartedAt).TotalMilliseconds:0}");
            this.activeNativeRequest = null;
            return;
        }

        if (now >= request.TimeoutAt)
        {
            this.debugLog($"VAL DebugTrace NativeOpenTimeout id={request.AchievementId} reason={request.Reason} cold={request.ColdOpen} elapsedMs={(now - request.StartedAt).TotalMilliseconds:0}");
            this.activeNativeRequest = null;
        }
    }

    private void FinishBatchIfIdle(bool force = false)
    {
        if (!this.batchInProgress)
        {
            return;
        }

        if (!force && (this.activeNativeRequest.HasValue || this.scheduler.HasPendingRequests))
        {
            return;
        }

        if (this.nativeWindowOpenedByVal && !this.nativeWindowWasOpenBeforeBatch)
        {
            var closed = this.nativeAchievementNavigator.CloseAchievementWindow();
            this.debugLog($"VAL DebugTrace NativeBatchAutoClose closed={closed}");
        }
        else
        {
            var restored = this.nativeAchievementNavigator.RestoreParkedAchievementWindow();
            this.debugLog($"VAL DebugTrace NativeBatchLeaveOpen reason=window-was-open-before-batch restored={restored}");
        }

        this.batchInProgress = false;
        this.nativeWindowWasOpenBeforeBatch = false;
        this.nativeWindowOpenedByVal = false;
    }

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

        if (now < this.nextAutoUpdateAt || this.scheduler.HasPendingRequests || this.activeNativeRequest.HasValue)
        {
            return;
        }

        var ids = this.autoUpdateIdsProvider();
        this.EnqueueUpdateAll(ids, "auto-update");
        this.nextAutoUpdateAt = now + interval;
        this.debugLog($"VAL DebugTrace AutoUpdateScheduled next={this.nextAutoUpdateAt:O} included={ids.Count}");
    }

    private readonly record struct ActiveNativeAchievementRequest(
        uint AchievementId,
        string Reason,
        DateTimeOffset StartedAt,
        DateTimeOffset MinimumCompleteAt,
        DateTimeOffset TimeoutAt,
        bool ColdOpen);
}
