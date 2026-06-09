using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AchievementTracker.Windows;

public sealed class TrackerWindow : Window
{
    private readonly Plugin plugin;
    private readonly Dictionary<uint, string> lastLiveProgressText = new();
    private bool lastInFlightLogged;
    private uint? lastWaitingAchievementId;
    private DateTimeOffset? lastWaitingNotBefore;

    public TrackerWindow(Plugin plugin)
        : base("Achievement Tracker##AchievementTrackerLive")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 120),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        this.ProcessQueuedProgressRequests();

        if (ImGui.Button("Configure"))
        {
            this.plugin.DebugLog.Trace("Tracker.Button", "Configure pressed");
            this.plugin.ToggleConfigUi();
        }

        ImGui.SameLine();
        if (ImGui.Button("Refresh tracked progress"))
        {
            this.plugin.DebugLog.Trace("Tracker.Button", "Refresh tracked progress pressed");
            this.RequestTrackedProgress();
        }

        ImGui.SameLine();
        ImGui.TextDisabled("/achtrack");
        ImGui.Separator();

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked. Open Configure to add up to 5.");
            return;
        }

        foreach (var achievementId in trackedIds)
        {
            this.DrawLiveAchievement(achievementId);
        }
    }

    private void DrawLiveAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        var progressText = "Progress unavailable";

        if (this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            progressText = this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText();
        }

        if (!this.lastLiveProgressText.TryGetValue(achievementId, out var previousText) || previousText != progressText)
        {
            this.lastLiveProgressText[achievementId] = progressText;
            this.plugin.DebugLog.Trace("Tracker.ProgressValue", $"achievementId={achievementId} name='{info.Name}' progress='{progressText}'");
        }

        ImGui.BulletText(info.Name);
        ImGui.SameLine();
        ImGui.TextDisabled(progressText);
    }

    private void RequestTrackedProgress()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        var queue = this.plugin.ProgressRefreshQueue;
        this.plugin.DebugLog.Trace("Tracker.RequestTrackedProgress", $"tracked=[{string.Join(", ", trackedIds)}] queueBefore={queue.Count}");
        queue.Enqueue(trackedIds);
        this.plugin.DebugLog.Trace("Tracker.RequestTrackedProgress", $"queueAfter={queue.Count}");
    }

    private void ProcessQueuedProgressRequests()
    {
        this.plugin.AchievementProgressSource.UpdateCache();
        if (this.plugin.AchievementProgressSource.IsRequestInFlight)
        {
            if (!this.lastInFlightLogged)
            {
                this.plugin.DebugLog.Trace("Tracker.ProcessQueue", $"requestInFlight queueCount={this.plugin.ProgressRefreshQueue.Count}");
                this.lastInFlightLogged = true;
            }

            return;
        }

        this.lastInFlightLogged = false;

        var now = DateTimeOffset.UtcNow;
        if (!this.plugin.ProgressRefreshQueue.TryPeekReady(now, out var achievementId))
        {
            if (this.plugin.ProgressRefreshQueue.TryPeekNotBefore(out var waitingId, out var notBefore)
                && (this.lastWaitingAchievementId != waitingId || this.lastWaitingNotBefore != notBefore))
            {
                this.lastWaitingAchievementId = waitingId;
                this.lastWaitingNotBefore = notBefore;
                this.plugin.DebugLog.Trace("Tracker.ProcessQueue", $"waiting achievementId={waitingId} notBefore={notBefore:O} now={now:O} queueCount={this.plugin.ProgressRefreshQueue.Count}");
            }

            return;
        }

        this.lastWaitingAchievementId = null;
        this.lastWaitingNotBefore = null;

        this.plugin.DebugLog.Trace("Tracker.ProcessQueue", $"ready achievementId={achievementId} now={now:O} queueCount={this.plugin.ProgressRefreshQueue.Count}");

        if (!this.plugin.ProgressRequestThrottler.CanRequest(achievementId, now))
        {
            this.plugin.DebugLog.Trace("Tracker.ProcessQueue", $"throttled achievementId={achievementId} now={now:O} dequeuing without request");
            this.plugin.ProgressRefreshQueue.Dequeue();
            return;
        }

        if (this.plugin.AchievementProgressSource.RequestProgress(achievementId))
        {
            this.plugin.ProgressRequestThrottler.MarkRequest(achievementId, now);
            this.plugin.ProgressRefreshQueue.Dequeue();
            this.plugin.DebugLog.Trace("Tracker.ProcessQueue", $"requested achievementId={achievementId} queueAfter={this.plugin.ProgressRefreshQueue.Count}");
        }
        else
        {
            this.plugin.DebugLog.Trace("Tracker.ProcessQueue", $"requestFailed achievementId={achievementId} queueCount={this.plugin.ProgressRefreshQueue.Count}");
        }
    }
}
