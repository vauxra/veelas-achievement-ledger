using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementProgressUpdater
{
    private static readonly TimeSpan NativeOpenInputBuffer = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan NativeOpenCooldown = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan SameAchievementBackoff = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RefreshMinimumWait = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RefreshMaximumWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PostProgressSettleMinimum = TimeSpan.FromSeconds(30);
    private const int MaxConsecutiveNativeFailures = 3;
    private const int MaximumNativeRefreshesPerEnqueue = 1;

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
        + (this.activeNativeRequest.HasValue ? 1 : 0);

    public DateTimeOffset? NextDueAt => this.scheduler.NextDueAt;

    public DateTimeOffset? NextAutoUpdateAt => this.autoUpdateEnabledProvider() && this.nextAutoUpdateAt != DateTimeOffset.MinValue
        ? this.nextAutoUpdateAt
        : null;

    public bool IsUpdateInProgress => this.activeNativeRequest.HasValue
        || this.scheduler.HasPendingRequests;

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

        var candidateCount = ids.Count;
        ids = LimitNativeRefreshBatch(ids).ToList();
        if (candidateCount > ids.Count)
        {
            this.debugLog($"AchieveEx DebugTrace QueueBatchLimited reason={reason} candidates={candidateCount} queued={ids.Count} limit={MaximumNativeRefreshesPerEnqueue} note=successive-native-refresh-crash-guard-no-direct-server-call");
        }

        var baseSpacingSeconds = Math.Max(30, Math.Clamp(this.updateSpacingSecondsProvider(), 0, 3600));
        this.scheduler.EnqueueUpdateAll(ids, reason, TimeSpan.FromSeconds(baseSpacingSeconds));
        this.statusText = $"Progress queue: {this.PendingCount} pending.";
        this.debugLog($"AchieveEx DebugTrace QueueProgressRefresh reason={reason} count={ids.Count} candidates={candidateCount} pending={this.PendingCount} spacingSeconds={baseSpacingSeconds} sameIdBackoffSeconds={SameAchievementBackoff.TotalSeconds:0} postProgressSettleSeconds={PostProgressSettleMinimum.TotalSeconds:0} executor=native-open-crash-guard-no-direct-server-call");
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

        var queued = this.scheduler.EnqueueInspection(achievementId, reason);
        this.statusText = queued ? "Native Achievement open queued." : "Native Achievement open already queued or queue is full.";
        this.debugLog($"AchieveEx DebugTrace NativeInspectionQueued id={achievementId} reason={reason} queued={queued} pending={this.PendingCount} maxPending={AchievementProgressRequestScheduler.MaxPendingRequests} inputBufferMs={NativeOpenInputBuffer.TotalMilliseconds:0}");
    }

    public void Tick()
    {
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
            this.StartNativeAction(request, now);
        }
    }

    public void Clear()
    {
        this.scheduler.Clear();
        this.activeNativeRequest = null;
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

    public static IReadOnlyList<uint> LimitNativeRefreshBatch(IReadOnlyList<uint> achievementIds)
        => achievementIds.Take(MaximumNativeRefreshesPerEnqueue).ToList();

    private bool WasNativeOpenedRecently(uint achievementId, DateTimeOffset now)
        => this.lastNativeOpenByAchievementId.TryGetValue(achievementId, out var lastOpen)
            && now - lastOpen >= TimeSpan.Zero
            && now - lastOpen < SameAchievementBackoff;

    private DateTimeOffset GetSameAchievementBackoffUntil(uint achievementId)
        => this.lastNativeOpenByAchievementId.TryGetValue(achievementId, out var lastOpen)
            ? lastOpen + SameAchievementBackoff
            : DateTimeOffset.MinValue;


    private void StartNativeAction(ScheduledAchievementProgressRequest request, DateTimeOffset now)
    {
        if (request.Kind == NativeAchievementActionKind.Inspection)
        {
            this.StartInspection(request, now);
            return;
        }

        this.StartNativeRefresh(request, now);
    }

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

    private void StartInspection(ScheduledAchievementProgressRequest request, DateTimeOffset now)
    {
        var achievementId = request.AchievementId;

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

        if (this.progressSource.TryGetFreshCachedObservation(request.AchievementId, request.StartedAt, out var progress))
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
        var configuredSpacing = TimeSpan.FromSeconds(Math.Max(30, Math.Clamp(this.updateSpacingSecondsProvider(), 0, 3600)));
        var cooldown = MaxTimeSpan(NativeOpenCooldown, configuredSpacing, PostProgressSettleMinimum);
        this.nextNativeOpenAllowedAt = now + cooldown;
        this.statusText = this.scheduler.HasPendingRequests ? $"Progress queue: {this.scheduler.PendingCount} pending." : string.Empty;
        this.debugLog($"AchieveEx DebugTrace NativeOpenCooldown reason={reason} nextOpenAt={this.nextNativeOpenAllowedAt:O} cooldownSeconds={cooldown.TotalSeconds:0} phase=post-progress-settle");
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

        if (now < this.nextAutoUpdateAt || this.scheduler.HasPendingRequests || this.activeNativeRequest.HasValue)
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

    private static TimeSpan MaxTimeSpan(params TimeSpan[] values)
        => values.Max();

    private readonly record struct ActiveNativeAchievementRequest(
        uint AchievementId,
        string Reason,
        DateTimeOffset StartedAt,
        DateTimeOffset MinimumCompleteAt,
        DateTimeOffset TimeoutAt);
}
