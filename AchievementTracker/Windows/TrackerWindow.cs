using AchievementTracker.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace AchievementTracker.Windows;

public sealed class TrackerWindow : Window
{
    private readonly Plugin plugin;
    public TrackerWindow(Plugin plugin)
        : base("Veela's Achievement Ledger Ex Mid##AchievementLedgerLive")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 180),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        this.plugin.AchievementProgressSource.UpdateCache();

        if (ImGui.Button("Configure"))
        {
            this.plugin.ToggleConfigUi();
        }
        AddTooltip("Open configuration.");

        ImGui.SameLine();
        if (ImGui.Button("Update All"))
        {
            this.plugin.EnqueueUpdateAllTracked("manual-update-all");
        }
        AddTooltip("Update tracked achievements.");

        this.SameLineOrWrap(250f);
        if (ImGui.Button("Stop Update Tasks"))
        {
            this.plugin.StopAutoUpdateAndClearQueue();
        }
        AddTooltip("Disable auto update and clear queue.");

        this.SameLineOrWrap(110f);
        var autoUpdateEnabled = this.plugin.Configuration.ExperimentalAutoUpdateEnabled;
        if (ImGui.Checkbox("Auto update", ref autoUpdateEnabled))
        {
            this.plugin.Configuration.ExperimentalAutoUpdateEnabled = autoUpdateEnabled;
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        AddTooltip("Run timed updates.");

        this.DrawQueueStatus();
        ImGui.Separator();

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked. Use Configure to add one.");
            return;
        }

        foreach (var achievementId in trackedIds)
        {
            this.DrawAchievement(achievementId);
        }
    }

    private void DrawAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        var progressText = "Progress unavailable";
        if (this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            progressText = this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText();
        }

        ImGui.PushID((int)achievementId);
        if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
        {
            this.plugin.EnqueueUpdateOne(achievementId, "manual-row-update");
        }
        AddTooltip("Update this achievement.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            this.plugin.NativeAchievementNavigator.OpenAchievement(achievementId);
        }
        AddTooltip("Open in Achievements.");

        ImGui.SameLine();
        ImGui.TextWrapped(info.Name);
        ImGui.TextDisabled(progressText);

        var updatedText = this.plugin.ClientAchievementProgressSource.TryGetObservation(achievementId, out var observation)
            ? $"updated {FormatAge(observation.ObservedAt)}"
            : "not updated yet";

        ImGui.SameLine();
        ImGui.TextDisabled(updatedText);
        ImGui.PopID();
    }

    private void DrawQueueStatus()
    {
        var pending = this.plugin.AchievementProgressUpdater.PendingCount;
        var nextDue = this.plugin.AchievementProgressUpdater.NextDueAt;
        if (pending > 0 && nextDue.HasValue)
        {
            var seconds = Math.Max(0, (nextDue.Value - DateTimeOffset.UtcNow).TotalSeconds);
            ImGui.TextDisabled($"Progress queue: {pending} pending, next request in {seconds:0}s");
        }

        var nextAuto = this.plugin.AchievementProgressUpdater.NextAutoUpdateAt;
        if (nextAuto.HasValue)
        {
            var seconds = Math.Max(0, (nextAuto.Value - DateTimeOffset.UtcNow).TotalSeconds);
            ImGui.TextDisabled($"Auto update next cycle in {seconds:0}s");
        }
    }

    private static void AddTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
        }
    }

    private void SameLineOrWrap(float estimatedNextItemWidth)
    {
        if (ImGui.GetContentRegionAvail().X >= estimatedNextItemWidth)
        {
            ImGui.SameLine();
        }
    }

    private static string FormatAge(DateTimeOffset observedAt)
    {
        var age = DateTimeOffset.UtcNow - observedAt;
        if (age.TotalSeconds < 60)
        {
            return "just now";
        }

        if (age.TotalMinutes < 60)
        {
            return $"{(int)age.TotalMinutes}m ago";
        }

        return $"{(int)age.TotalHours}h ago";
    }
}
