using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementProgressUpdater
{
    private static readonly TimeSpan NativeOpenInputBuffer = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan NativeOpenCooldown = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan SameAchievementBackoff = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RefreshMinimumWait = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan RefreshMaximumWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PostProgressSettleMinimum = TimeSpan.Zero;
    private static readonly TimeSpan NativeScaleOperationRetryWindow = TimeSpan.FromSeconds(8);
    private const int MaxConsecutiveNativeFailures = 3;
    private const int MaximumNativeRefreshesPerEnqueue = AchievementProgressRequestScheduler.MaxPendingRequests;

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
    private NativeUpdateJobState? activeRefreshJob;
    private DateTimeOffset nextNativeOpenAllowedAt = DateTimeOffset.MinValue;
    private bool pendingParkForActiveRefresh;
    private bool pendingRestoreForInspection;
    private bool pendingRestoreWhenIdle;
    private DateTimeOffset pendingScaleOperationUntil = DateTimeOffset.MinValue;
    private int consecutiveNativeFailures;
    private bool nativeCircuitBroken;
    private string statusText = string.Empty;
    private DateTimeOffset? queueRunStartedAt;

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

    public TimeSpan? QueueElapsed => this.queueRunStartedAt.HasValue
        ? DateTimeOffset.UtcNow - this.queueRunStartedAt.Value
        : null;

    public bool IsNativeCircuitBroken => this.nativeCircuitBroken;

    public string StatusText => this.nativeCircuitBroken
        ? "Native Achievement actions paused for this session after repeated failures."
        : this.statusText;

    public void EnqueueUpdateAll(IEnumerable<uint> achievementIds, string reason)
    {
        this.EnqueueUpdateAllCore(achievementIds, reason, activityKey: null, initialDelay: TimeSpan.Zero);
    }

    public void EnqueueActivityUpdateAll(IEnumerable<uint> achievementIds, string reason, ActivityUpdateKey activityKey, TimeSpan initialDelay)
    {
        this.EnqueueUpdateAllCore(achievementIds, reason, activityKey, initialDelay);
    }

    private void EnqueueUpdateAllCore(IEnumerable<uint> achievementIds, string reason, ActivityUpdateKey? activityKey, TimeSpan initialDelay)
    {
        // Queue entry point: normalize/filter request IDs and hand spacing/dedupe to the scheduler.
        // Actual native UI calls must stay serialized in Tick(), never in caller/UI context.
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
                this.debugLog($"AchieveEx DebugTrace QueueSkipRecentlyObserved reason={reason} skipped={skippedCount} observedThresholdSeconds={ClientAchievementProgressSource.RecentlyObservedUpdateAllSkipThreshold.TotalSeconds:0} sameIdBackoffSeconds={SameAchievementBackoff.TotalSeconds:0}");
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
            this.debugLog($"AchieveEx DebugTrace QueueBatchLimited reason={reason} candidates={candidateCount} queued={ids.Count} limit={MaximumNativeRefreshesPerEnqueue} note=queue-cap-no-direct-server-call");
        }

        var baseSpacingSeconds = Math.Clamp(this.updateSpacingSecondsProvider(), 0, 3600);
        this.StartQueueRunIfIdle(now);
        var activePendingBefore = this.scheduler.ActiveOrPendingActivityKeyCount;
        var added = activityKey.HasValue
            ? this.scheduler.EnqueueActivityUpdateAll(ids, reason, TimeSpan.FromSeconds(baseSpacingSeconds), activityKey.Value, initialDelay)
            : this.scheduler.EnqueueUpdateAllAndCount(ids, reason, TimeSpan.FromSeconds(baseSpacingSeconds));
        this.statusText = $"Progress queue: {this.PendingCount} pending.";
        if (activityKey.HasValue)
        {
            var outcome = added > 0 ? "queued" : this.scheduler.IsActivityKeyDirty(activityKey.Value) ? "marked-dirty" : "skipped";
            this.debugLog($"AchieveEx DebugTrace QueueActivityProgressRefresh outcome={outcome} reason={reason} key={activityKey.Value} count={ids.Count} added={added} candidates={candidateCount} pending={this.PendingCount} activityActivePendingBefore={activePendingBefore} activityActivePendingAfter={this.scheduler.ActiveOrPendingActivityKeyCount} delaySeconds={initialDelay.TotalSeconds:0} userSpacingSeconds={baseSpacingSeconds} immutableSpacingSeconds={AchievementProgressRequestScheduler.ImmutableActionSpacing.TotalSeconds:0} sameIdBackoffSeconds={SameAchievementBackoff.TotalSeconds:0} jitterSeconds=1-2 executor=unified-native-queue-no-direct-server-call");
        }
        else
        {
            this.debugLog($"AchieveEx DebugTrace QueueProgressRefresh reason={reason} count={ids.Count} candidates={candidateCount} pending={this.PendingCount} userSpacingSeconds={baseSpacingSeconds} immutableSpacingSeconds={AchievementProgressRequestScheduler.ImmutableActionSpacing.TotalSeconds:0} sameIdBackoffSeconds={SameAchievementBackoff.TotalSeconds:0} jitterSeconds=1-2 executor=unified-native-queue-no-direct-server-call");
        }
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

        var wasIdle = !this.IsUpdateInProgress;
        var queued = this.scheduler.EnqueueInspection(achievementId, reason);
        if (queued && wasIdle)
        {
            this.queueRunStartedAt = DateTimeOffset.UtcNow;
        }

        this.statusText = queued ? "Native Achievement open queued." : "Native Achievement open already queued or queue is full.";
        this.debugLog($"AchieveEx DebugTrace NativeInspectionQueued id={achievementId} reason={reason} queued={queued} pending={this.PendingCount} maxPending={AchievementProgressRequestScheduler.MaxPendingRequests} inputBufferMs={NativeOpenInputBuffer.TotalMilliseconds:0}");
    }

    public void Tick()
    {
        // State-machine order is intentional: finish pending window scale work, let auto-update enqueue,
        // settle an active native request, then start at most one due action. Keeping refresh and
        // inspection in this single loop prevents competing native Achievement opens.
        if (this.nativeCircuitBroken)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        this.TryApplyPendingNativeWindowScale(now);
        this.RestoreParkedWindowIfPlayerOpenedPanel(now);
        this.MaybeEnqueueAutoUpdate(now);
        this.ProcessActiveNativeRequest(now);
        this.TryApplyPendingNativeWindowScale(now);
        this.FinishQueueRunIfIdle();

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
        this.activeRefreshJob = null;
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.ClearPendingScaleOperations();
        this.statusText = string.Empty;
        this.queueRunStartedAt = null;
    }

    public void ResetAutoUpdateCountdown()
    {
        this.nextAutoUpdateAt = DateTimeOffset.MinValue;
        this.debugLog("AchieveEx DebugTrace AutoUpdateReset");
    }

    private static bool IsUpdateAllReason(string reason)
        => string.Equals(reason, "manual-update-all", StringComparison.Ordinal)
            || string.Equals(reason, "auto-update", StringComparison.Ordinal);

    private void StartQueueRunIfIdle(DateTimeOffset now)
    {
        if (!this.queueRunStartedAt.HasValue && !this.IsUpdateInProgress)
        {
            this.queueRunStartedAt = now;
        }
    }

    private void FinishQueueRunIfIdle()
    {
        if (!this.IsUpdateInProgress)
        {
            this.queueRunStartedAt = null;
        }
    }

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
        // Refresh action: open the native Achievement entry, optionally park a window opened by us,
        // then wait for ClientAchievementProgressSource to observe fresh progress data.
        var dueAt = Max(now, this.nextNativeOpenAllowedAt, this.GetSameAchievementBackoffUntil(request.AchievementId));
        if (dueAt > now)
        {
            this.scheduler.Requeue(request, dueAt);
            this.debugLog($"AchieveEx DebugTrace NativeRefreshDeferred id={request.AchievementId} reason={request.Reason} jobId={request.JobId} dueAt={dueAt:O}");
            return;
        }

        var job = this.GetOrStartRefreshJob(request, now);
        if (!this.nativeAchievementNavigator.OpenAchievement(request.AchievementId))
        {
            this.RegisterNativeFailure($"open-failed-refresh-{request.AchievementId}");
            this.scheduler.MarkActivityJobSettled(request.JobId, now);
            this.debugLog($"AchieveEx DebugTrace NativeRefreshOpenFailed id={request.AchievementId} reason={request.Reason} pending={this.scheduler.PendingCount}");
            return;
        }

        this.lastNativeOpenByAchievementId[request.AchievementId] = now;
        var shouldPark = job.ShouldPark && !job.ParkRequested;
        if (shouldPark)
        {
            this.RequestScaleOperation(now, parkForRefresh: true);
            job = job with { ParkRequested = true };
            this.activeRefreshJob = job;
        }
        else if (job.NativeWindowWasAlreadyOpen && this.nativeAchievementNavigator.HasParkedWindow)
        {
            this.RequestScaleOperation(now, restoreForInspection: true);
        }

        this.activeNativeRequest = new ActiveNativeAchievementRequest(
            request.AchievementId,
            request.Reason,
            request.JobId,
            request.JobKind,
            now,
            now + RefreshMinimumWait,
            now + RefreshMaximumWait,
            job.NativeWindowWasAlreadyOpen);
        this.statusText = "Waiting for data.";
        var hasPendingSameJob = this.scheduler.HasPendingRequestsForJob(request.JobId);
        var closeAtJobEnd = NativeAchievementWindowScalePolicy.ShouldCloseAfterRefreshJobItem(request.JobKind, job.NativeWindowWasAlreadyOpen, hasPendingSameJob);
        this.debugLog($"AchieveEx DebugTrace NativeRefreshOpenSent id={request.AchievementId} reason={request.Reason} jobId={request.JobId} jobKind={request.JobKind} minWaitSeconds={RefreshMinimumWait.TotalSeconds:0.0} maxWaitSeconds={RefreshMaximumWait.TotalSeconds:0} pending={this.scheduler.PendingCount} pendingSameJob={this.scheduler.PendingCountForJob(request.JobId)} nativeWindowWasOpen={job.NativeWindowWasAlreadyOpen} scaleIntent={(shouldPark ? "park" : "none")} closeAtJobEnd={closeAtJobEnd}");
    }

    private void StartInspection(ScheduledAchievementProgressRequest request, DateTimeOffset now)
    {
        // Inspection is user-visible and shares the native queue with refreshes so row-open clicks
        // cannot race an active refresh batch. It restores any parked window before opening.
        var achievementId = request.AchievementId;
        this.ReclaimNativeWindowForInspection(now, achievementId);

        if (!this.nativeAchievementNavigator.OpenAchievement(achievementId))
        {
            this.RegisterNativeFailure($"open-failed-inspect-{achievementId}");
            this.debugLog($"AchieveEx DebugTrace NativeInspectionOpenFailed id={achievementId}");
            return;
        }

        this.RegisterNativeSuccess();
        this.lastNativeOpenByAchievementId[achievementId] = now;
        if (NativeAchievementWindowScalePolicy.ShouldRestoreForAction(request.Kind))
        {
            this.RequestScaleOperation(now, restoreForInspection: true);
        }

        this.nextNativeOpenAllowedAt = now + NativeOpenCooldown;
        this.statusText = "Native Achievement opened.";
        this.debugLog($"AchieveEx DebugTrace NativeInspectionOpen id={achievementId} opened=true cooldownSeconds={NativeOpenCooldown.TotalSeconds:0} scaleIntent=restore");
    }

    private void ProcessActiveNativeRequest(DateTimeOffset now)
    {
        // Active refresh settle point: success and timeout both clear active state, notify the
        // scheduler/job, and apply the same native-window lifecycle cleanup.
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
            this.CompleteRefreshWindowLifecycle(now, request);
            this.scheduler.MarkActivityJobSettled(request.JobId, now);
            this.MarkNativeRequestSettled(now, request.Reason);
            return;
        }

        if (now >= request.TimeoutAt)
        {
            this.debugLog($"AchieveEx DebugTrace NativeRefreshTimeout id={request.AchievementId} reason={request.Reason} elapsedMs={(now - request.StartedAt).TotalMilliseconds:0}");
            this.activeNativeRequest = null;
            this.RegisterNativeFailure($"timeout-{request.AchievementId}");
            this.CompleteRefreshWindowLifecycle(now, request);
            this.scheduler.MarkActivityJobSettled(request.JobId, now);
            this.MarkNativeRequestSettled(now, request.Reason);
        }
    }

    private void MarkNativeRequestSettled(DateTimeOffset now, string reason)
    {
        var cooldown = MaxTimeSpan(NativeOpenCooldown, PostProgressSettleMinimum);
        this.nextNativeOpenAllowedAt = now + cooldown;
        this.statusText = this.scheduler.HasPendingRequests ? $"Progress queue: {this.scheduler.PendingCount} pending." : string.Empty;
        this.debugLog($"AchieveEx DebugTrace NativeOpenCooldown reason={reason} nextOpenAt={this.nextNativeOpenAllowedAt:O} cooldownSeconds={cooldown.TotalSeconds:0} phase=queue-owned-spacing");
    }




    private NativeUpdateJobState GetOrStartRefreshJob(ScheduledAchievementProgressRequest request, DateTimeOffset now)
    {
        // Job state spans a batch of refresh items so the native window closes/restores only once
        // at the end of the job instead of flickering between achievements.
        if (this.activeRefreshJob is { } existing && existing.JobId == request.JobId)
        {
            return existing;
        }

        var nativeWindowWasAlreadyOpen = this.nativeAchievementNavigator.IsVisible;
        var job = new NativeUpdateJobState(
            request.JobId,
            request.JobKind,
            nativeWindowWasAlreadyOpen,
            ShouldPark: NativeAchievementWindowScalePolicy.ShouldParkForAction(request.Kind, nativeWindowWasAlreadyOpen),
            ParkRequested: false);
        this.activeRefreshJob = job;
        this.debugLog($"AchieveEx DebugTrace NativeJobStarted jobId={job.JobId} jobKind={job.JobKind} nativeWindowWasOpen={job.NativeWindowWasAlreadyOpen} closeAtJobEnd={!job.NativeWindowWasAlreadyOpen}");
        return job;
    }

    private void ClearActiveRefreshJob(Guid jobId)
    {
        if (this.activeRefreshJob is { } job && job.JobId == jobId)
        {
            this.activeRefreshJob = null;
        }
    }

    private void ReclaimNativeWindowForInspection(DateTimeOffset now, uint achievementId)
    {
        if (this.activeRefreshJob is not { } job || !this.nativeAchievementNavigator.HasParkedWindow)
        {
            return;
        }

        this.RequestScaleOperation(now, restoreForInspection: true);
        this.activeRefreshJob = job with { NativeWindowWasAlreadyOpen = true, ShouldPark = false, ParkRequested = true };
        this.debugLog($"AchieveEx DebugTrace NativeJobReclaimedByInspection jobId={job.JobId} id={achievementId}");
    }

    private void RestoreParkedWindowIfPlayerOpenedPanel(DateTimeOffset now)
    {
        var hasActiveOrPendingWork = this.activeNativeRequest.HasValue || this.scheduler.HasPendingRequests;
        if (!NativeAchievementWindowScalePolicy.ShouldRestoreWhenPlayerOpenedPanel(
                this.nativeAchievementNavigator.HasParkedWindow,
                this.nativeAchievementNavigator.IsVisible,
                this.nativeAchievementNavigator.IsAchievementWindowParked(),
                hasActiveOrPendingWork))
        {
            return;
        }

        this.RequestScaleOperation(now, restoreForInspection: true);
        if (this.activeRefreshJob is { } job)
        {
            this.activeRefreshJob = job with { NativeWindowWasAlreadyOpen = true, ShouldPark = false, ParkRequested = true };
        }

        if (this.activeNativeRequest is { } request)
        {
            this.activeNativeRequest = request with { NativeWindowWasAlreadyOpen = true };
        }

        this.debugLog("AchieveEx DebugTrace NativeWindowReclaimedByManualOpen");
    }

    private void CompleteRefreshWindowLifecycle(DateTimeOffset now, ActiveNativeAchievementRequest request)
    {
        // Window cleanup is coupled to the job, not the individual row, to preserve native UI state
        // across a batch and avoid fighting the player's already-open Achievement window.
        var hasPendingSameJob = this.scheduler.HasPendingRequestsForJob(request.JobId);
        var shouldClose = NativeAchievementWindowScalePolicy.ShouldCloseAfterRefreshJobItem(
            request.JobKind,
            request.NativeWindowWasAlreadyOpen,
            hasPendingSameJob);

        this.debugLog($"AchieveEx DebugTrace NativeJobItemSettled id={request.AchievementId} jobId={request.JobId} jobKind={request.JobKind} pendingSameJob={this.scheduler.PendingCountForJob(request.JobId)} closeNow={shouldClose}");

        if (hasPendingSameJob)
        {
            return;
        }

        if (shouldClose)
        {
            var closed = this.nativeAchievementNavigator.CloseAchievementWindow(restoreParkedWindow: false);
            this.ClearPendingScaleOperations();
            this.debugLog($"AchieveEx DebugTrace NativeWindowClosedAfterRefreshJob id={request.AchievementId} jobId={request.JobId} closed={closed} nativeWindowWasOpen={request.NativeWindowWasAlreadyOpen}");
            this.ClearActiveRefreshJob(request.JobId);
            return;
        }

        this.ClearActiveRefreshJob(request.JobId);
    }

    private void RequestScaleOperation(DateTimeOffset now, bool parkForRefresh = false, bool restoreForInspection = false, bool restoreWhenIdle = false)
    {
        this.pendingParkForActiveRefresh |= parkForRefresh;
        this.pendingRestoreForInspection |= restoreForInspection;
        this.pendingRestoreWhenIdle |= restoreWhenIdle;
        this.pendingScaleOperationUntil = now + NativeScaleOperationRetryWindow;
        this.TryApplyPendingNativeWindowScale(now);
    }

    private void TryApplyPendingNativeWindowScale(DateTimeOffset now)
    {
        // Scale operations can need multiple frames because the native addon may not exist yet.
        // Keep retry state here instead of storing raw addon pointers across frames.
        if (this.pendingScaleOperationUntil != DateTimeOffset.MinValue && now > this.pendingScaleOperationUntil)
        {
            this.ClearPendingScaleOperations();
            return;
        }

        if (this.pendingRestoreForInspection)
        {
            if (this.nativeAchievementNavigator.RestoreParkedAchievementWindowOrResetScale())
            {
                this.pendingRestoreForInspection = false;
                this.pendingRestoreWhenIdle = false;
                this.debugLog("AchieveEx DebugTrace NativeWindowRestored reason=inspection");
            }
        }

        if (this.pendingParkForActiveRefresh)
        {
            if (this.nativeAchievementNavigator.TryParkAchievementWindow())
            {
                this.pendingParkForActiveRefresh = false;
                this.debugLog("AchieveEx DebugTrace NativeWindowParked reason=refresh");
            }
        }

        if (this.pendingRestoreWhenIdle
            && NativeAchievementWindowScalePolicy.ShouldRestoreWhenIdle(
                this.activeNativeRequest.HasValue,
                this.scheduler.HasPendingRequests,
                this.nativeAchievementNavigator.HasParkedWindow))
        {
            if (this.nativeAchievementNavigator.RestoreParkedAchievementWindow())
            {
                this.pendingRestoreWhenIdle = false;
                this.debugLog("AchieveEx DebugTrace NativeWindowRestored reason=idle");
            }
        }

        if (!this.pendingParkForActiveRefresh && !this.pendingRestoreForInspection && !this.pendingRestoreWhenIdle)
        {
            this.pendingScaleOperationUntil = DateTimeOffset.MinValue;
        }
    }

    private void ClearPendingScaleOperations()
    {
        this.pendingParkForActiveRefresh = false;
        this.pendingRestoreForInspection = false;
        this.pendingRestoreWhenIdle = false;
        this.pendingScaleOperationUntil = DateTimeOffset.MinValue;
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
            this.activeRefreshJob = null;
            _ = this.nativeAchievementNavigator.RestoreParkedAchievementWindow();
            this.ClearPendingScaleOperations();
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

    private readonly record struct NativeUpdateJobState(
        Guid JobId,
        NativeAchievementJobKind JobKind,
        bool NativeWindowWasAlreadyOpen,
        bool ShouldPark,
        bool ParkRequested);

    private readonly record struct ActiveNativeAchievementRequest(
        uint AchievementId,
        string Reason,
        Guid JobId,
        NativeAchievementJobKind JobKind,
        DateTimeOffset StartedAt,
        DateTimeOffset MinimumCompleteAt,
        DateTimeOffset TimeoutAt,
        bool NativeWindowWasAlreadyOpen);
}
