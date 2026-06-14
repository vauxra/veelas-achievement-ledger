using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementProgressUpdater
{
    private static readonly TimeSpan NativeOpenInputBuffer = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan NativeOpenCooldown = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan SameAchievementBackoff = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RefreshMinimumWait = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RefreshMaximumWait = TimeSpan.FromSeconds(15);
    private const int MaxConsecutiveNativeFailures = 3;

    private readonly AchievementProgressRequestScheduler scheduler;
    private readonly ClientAchievementProgressSource progressSource;
    private readonly NativeAchievementNavigator nativeAchievementNavigator;
    private readonly Func<IReadOnlyList<uint>> autoUpdateIdsProvider;
    private readonly Func<bool> autoUpdateEnabledProvider;
    private readonly Func<int> autoUpdateIntervalSecondsProvider;
    private readonly Func<int> updateSpacingSecondsProvider;
    private readonly Action<string> debugLog;
    private readonly Dictionary<uint, DateTimeOffset> lastNativeOpenByAchievementId = new();
    private DateTimeOffset nextAutoUpdateAt = DateTimeOffset.MinValue;
    private ActiveNativeAchievementRequest? activeNativeRequest;
    private uint pendingInspectionAchievementId;
    private DateTimeOffset pendingInspectionDueAt = DateTimeOffset.MinValue;
    private DateTimeOffset nextNativeOpenAllowedAt = DateTimeOffset.MinValue;
    private int consecutiveNativeFailures;
    private bool nativeCircuitBroken;
    private string statusText = string.Empty;

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

    public int PendingCount => this.scheduler.PendingCount
        + (this.activeNativeRequest.HasValue ? 1 : 0)
        + (this.pendingInspectionAchievementId != 0 ? 1 : 0);

    public DateTimeOffset? NextDueAt
    {
        get
        {
            var dueValues = new List<DateTimeOffset>();
            if (this.scheduler.NextDueAt.HasValue)
            {
                dueValues.Add(this.scheduler.NextDueAt.Value);
            }

            if (this.pendingInspectionAchievementId != 0 && this.pendingInspectionDueAt != DateTimeOffset.MinValue)
            {
                dueValues.Add(this.pendingInspectionDueAt);
            }

            return dueValues.Count == 0 ? null : dueValues.Min();
        }
    }

    public DateTimeOffset? NextAutoUpdateAt => this.autoUpdateEnabledProvider() && this.nextAutoUpdateAt != DateTimeOffset.MinValue
        ? this.nextAutoUpdateAt
        : null;

    public bool IsUpdateInProgress => this.activeNativeRequest.HasValue
        || this.scheduler.HasPendingRequests
        || this.pendingInspectionAchievementId != 0;

    public bool IsNativeCircuitBroken => this.nativeCircuitBroken;

    public string StatusText => this.nativeCircuitBroken
        ? "Native Achievement actions paused for this session after repeated failures."
        : this.statusText;

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason)
    {
        if (this.nativeCircuitBroken)
        {
            this.debugLog($"AchieveEx DebugTrace QueueRejectedCircuitBroken reason={reason}");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var ids = achievementIds.Where(id => id != 0).Distinct().ToList();
        if (IsUpdateAllReason(reason))
        {
            var beforeCount = ids.Count;
            ids = ids
                .Where(id => !this.progressSource.IsRecentlyObserved(id, now, ClientAchievementProgressSource.RecentlyObservedUpdateAllSkipThreshold))
                .Where(id => !this.WasNativeOpenedRecently(id, now))
                .ToList();
            var skippedCount = beforeCount - ids.Count;
            if (skippedCount > 0)
            {
                this.debugLog($"AchieveEx DebugTrace QueueSkipRecentlyObserved reason={reason} skipped={skippedCount} observedThresholdSeconds={ClientAchievementProgressSource.RecentlyObservedUpdateAllSkipThreshold.TotalSeconds:0} nativeBackoffSeconds={SameAchievementBackoff.TotalSeconds:0}");
            }
        }

        if (ids.Count == 0)
        {
            this.debugLog($"AchieveEx DebugTrace QueueSkip reason={reason} no achievements selected");
            return;
        }

        var baseSpacingSeconds = Math.Max(6, Math.Clamp(this.updateSpacingSecondsProvider(), 0, 3600));
        this.scheduler.EnqueueUpdateAll(ids, reason, TimeSpan.FromSeconds(baseSpacingSeconds));
        this.statusText = $"Progress queue: {this.PendingCount} pending.";
        this.debugLog($"AchieveEx DebugTrace QueueProgressRefresh reason={reason} count={ids.Count} pending={this.PendingCount} spacingSeconds={baseSpacingSeconds} sameIdBackoffSeconds={SameAchievementBackoff.TotalSeconds:0} executor=native-coordinator");
    }

    public void QueueInspection(uint achievementId, string reason)
    {
        if (achievementId == 0)
        {
            return;
        }

        if (this.nativeCircuitBroken)
        {
            this.debugLog($"AchieveEx DebugTrace NativeInspectionRejectedCircuitBroken id={achievementId} reason={reason}");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        this.pendingInspectionAchievementId = achievementId;
        this.pendingInspectionDueAt = Max(now + NativeOpenInputBuffer, this.nextNativeOpenAllowedAt, this.GetSameAchievementBackoffUntil(achievementId));
        this.statusText = "Native Achievement open queued.";
        this.debugLog($"AchieveEx DebugTrace NativeInspectionQueued id={achievementId} reason={reason} openAt={this.pendingInspectionDueAt:O} cooldownSeconds={NativeOpenCooldown.TotalSeconds:0} coalescedLastWins=true");
    }

    public void Tick()
    {
        this.progressSource.UpdateCache();

        if (this.nativeCircuitBroken)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        this.MaybeEnqueueAutoUpdate(now);
        this.ProcessActiveNativeRequest(now);

        if (this.activeNativeRequest.HasValue || now < this.nextNativeOpenAllowedAt)
        {
            return;
        }

        if (this.scheduler.TryTakeDueRequest(now, out var request))
        {
            this.StartNativeRefresh(request, now);
            return;
        }

        if (this.pendingInspectionAchievementId != 0 && now >= this.pendingInspectionDueAt)
        {
            this.StartInspection(now);
        }
    }

    public void Clear()
    {
        this.scheduler.Clear();
        this.activeNativeRequest = null;
        this.pendingInspectionAchievementId = 0;
        this.pendingInspectionDueAt = DateTimeOffset.MinValue;
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.statusText = string.Empty;
    }

    public void ResetAutoUpdateCountdown()
    {
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.debugLog("AchieveEx DebugTrace AutoUpdateReset");
    }

    private static bool IsUpdateAllReason(string reason)
        => string.Equals(reason, "manual-update-all", StringComparison.Ordinal)
            || string.Equals(reason, "auto-update", StringComparison.Ordinal);

    private bool WasNativeOpenedRecently(uint achievementId, DateTimeOffset now)
        => this.lastNativeOpenByAchievementId.TryGetValue(achievementId, out var lastOpen)
            && now - lastOpen >= TimeSpan.Zero
            && now - lastOpen < SameAchievementBackoff;

    private DateTimeOffset GetSameAchievementBackoffUntil(uint achievementId)
        => this.lastNativeOpenByAchievementId.TryGetValue(achievementId, out var lastOpen)
            ? lastOpen + SameAchievementBackoff
            : DateTimeOffset.MinValue;

    private void StartNativeRefresh(ScheduledAchievementProgressRequest request, DateTimeOffset now)
    {
        var dueAt = Max(now, this.nextNativeOpenAllowedAt, this.GetSameAchievementBackoffUntil(request.AchievementId));
        if (dueAt > now)
        {
            this.scheduler.EnqueueUpdateAll([request.AchievementId], request.Reason, dueAt - now);
            this.debugLog($"AchieveEx DebugTrace NativeRefreshDeferred id={request.AchievementId} reason={request.Reason} dueAt={dueAt:O}");
            return;
        }

        if (!this.nativeAchievementNavigator.OpenAchievement(request.AchievementId))
        {
            this.RegisterNativeFailure($"open-failed-refresh-{request.AchievementId}");
            this.debugLog($"AchieveEx DebugTrace NativeRefreshOpenFailed id={request.AchievementId} reason={request.Reason} pending={this.scheduler.PendingCount}");
            return;
        }

        this.lastNativeOpenByAchievementId[request.AchievementId] = now;
        this.activeNativeRequest = new ActiveNativeAchievementRequest(
            request.AchievementId,
            request.Reason,
            now,
            now + RefreshMinimumWait,
            now + RefreshMaximumWait);
        this.statusText = "Waiting for data.";
        this.debugLog($"AchieveEx DebugTrace NativeRefreshOpenSent id={request.AchievementId} reason={request.Reason} minWaitSeconds={RefreshMinimumWait.TotalSeconds:0.0} maxWaitSeconds={RefreshMaximumWait.TotalSeconds:0} pending={this.scheduler.PendingCount}");
    }

    private void StartInspection(DateTimeOffset now)
    {
        var achievementId = this.pendingInspectionAchievementId;
        this.pendingInspectionAchievementId = 0;
        this.pendingInspectionDueAt = DateTimeOffset.MinValue;

        if (achievementId == 0)
        {
            return;
        }

        if (!this.nativeAchievementNavigator.OpenAchievement(achievementId))
        {
            this.RegisterNativeFailure($"open-failed-inspect-{achievementId}");
            this.debugLog($"AchieveEx DebugTrace NativeInspectionOpenFailed id={achievementId}");
            return;
        }

        this.RegisterNativeSuccess();
        this.lastNativeOpenByAchievementId[achievementId] = now;
        this.nextNativeOpenAllowedAt = now + NativeOpenCooldown;
        this.statusText = "Native Achievement opened.";
        this.debugLog($"AchieveEx DebugTrace NativeInspectionOpen id={achievementId} opened=true cooldownSeconds={NativeOpenCooldown.TotalSeconds:0}");
    }

    private void ProcessActiveNativeRequest(DateTimeOffset now)
    {
        if (!this.activeNativeRequest.HasValue)
        {
            return;
        }

        var request = this.activeNativeRequest.Value;
        if (now < request.MinimumCompleteAt)
        {
            return;
        }

        if (this.progressSource.TryGetFreshObservation(request.AchievementId, request.StartedAt, out var progress))
        {
            this.debugLog($"AchieveEx DebugTrace NativeRefreshLoaded id={request.AchievementId} reason={request.Reason} current={progress.Current} max={progress.Max} source={progress.Source} elapsedMs={(now - request.StartedAt).TotalMilliseconds:0}");
            this.activeNativeRequest = null;
            this.RegisterNativeSuccess();
            this.MarkNativeRequestSettled(now, request.Reason);
            return;
        }

        if (now >= request.TimeoutAt)
        {
            this.debugLog($"AchieveEx DebugTrace NativeRefreshTimeout id={request.AchievementId} reason={request.Reason} elapsedMs={(now - request.StartedAt).TotalMilliseconds:0}");
            this.activeNativeRequest = null;
            this.RegisterNativeFailure($"timeout-{request.AchievementId}");
            this.MarkNativeRequestSettled(now, request.Reason);
        }
    }

    private void MarkNativeRequestSettled(DateTimeOffset now, string reason)
    {
        this.nextNativeOpenAllowedAt = now + NativeOpenCooldown;
        this.statusText = this.scheduler.HasPendingRequests ? $"Progress queue: {this.scheduler.PendingCount} pending." : string.Empty;
        this.debugLog($"AchieveEx DebugTrace NativeOpenCooldown reason={reason} nextOpenAt={this.nextNativeOpenAllowedAt:O} cooldownSeconds={NativeOpenCooldown.TotalSeconds:0}");
    }

    private void RegisterNativeSuccess()
        => this.consecutiveNativeFailures = 0;

    private void RegisterNativeFailure(string reason)
    {
        this.consecutiveNativeFailures++;
        this.nextNativeOpenAllowedAt = DateTimeOffset.UtcNow + NativeOpenCooldown;
        this.debugLog($"AchieveEx DebugTrace NativeFailure reason={reason} consecutive={this.consecutiveNativeFailures} max={MaxConsecutiveNativeFailures}");
        if (this.consecutiveNativeFailures >= MaxConsecutiveNativeFailures)
        {
            this.nativeCircuitBroken = true;
            this.scheduler.Clear();
            this.activeNativeRequest = null;
            this.pendingInspectionAchievementId = 0;
            this.statusText = "Native Achievement actions paused for this session after repeated failures.";
            this.debugLog("AchieveEx DebugTrace NativeCircuitBreakerTripped queueCleared=true");
        }
    }

    private void MaybeEnqueueAutoUpdate(DateTimeOffset now)
    {
        if (!this.autoUpdateEnabledProvider())
        {
            this.nextAutoUpdateAt = DateTimeOffset.MinValue;
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(this.autoUpdateIntervalSecondsProvider(), 60, 86400));
        if (this.nextAutoUpdateAt == DateTimeOffset.MinValue)
        {
            this.nextAutoUpdateAt = now + interval;
            this.debugLog($"AchieveEx DebugTrace AutoUpdateArmed next={this.nextAutoUpdateAt:O} intervalSeconds={interval.TotalSeconds:0}");
            return;
        }

        if (now < this.nextAutoUpdateAt || this.scheduler.HasPendingRequests || this.activeNativeRequest.HasValue || this.pendingInspectionAchievementId != 0)
        {
            return;
        }

        var ids = this.autoUpdateIdsProvider();
        this.EnqueueUpdateAll(ids, "auto-update");
        this.nextAutoUpdateAt = now + interval;
        this.debugLog($"AchieveEx DebugTrace AutoUpdateScheduled next={this.nextAutoUpdateAt:O} included={ids.Count}");
    }

    private static DateTimeOffset Max(params DateTimeOffset[] values)
        => values.Max();

    private readonly record struct ActiveNativeAchievementRequest(
        uint AchievementId,
        string Reason,
        DateTimeOffset StartedAt,
        DateTimeOffset MinimumCompleteAt,
        DateTimeOffset TimeoutAt);
}
