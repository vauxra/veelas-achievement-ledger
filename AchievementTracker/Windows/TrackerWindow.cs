using AchievementTracker.Services;
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

    public TrackerWindow(Plugin plugin)
        : base("Veela's Achievement Ledger##AchievementLedgerLive")
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
            this.plugin.DebugLog.Trace("Tracker.Button", "Configure pressed");
            this.plugin.ToggleConfigUi();
        }

        ImGui.SameLine();
        if (ImGui.Button("Open next"))
        {
            var nextId = this.GetNextTrackedAchievementId();
            if (nextId.HasValue)
            {
                this.OpenNativeAchievement(nextId.Value, "open-next");
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("/val");
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

        if (!this.lastLiveProgressText.TryGetValue(achievementId, out var previousText) || previousText != progressText)
        {
            this.lastLiveProgressText[achievementId] = progressText;
            this.plugin.DebugLog.Trace("Tracker.ProgressValue", $"achievementId={achievementId} name='{info.Name}' progress='{progressText}'");
        }

        ImGui.PushID((int)achievementId);
        if (ImGui.Button("Open"))
        {
            this.OpenNativeAchievement(achievementId, "row-button");
        }

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

    private uint? GetNextTrackedAchievementId()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            return null;
        }

        var unobserved = trackedIds.FirstOrDefault(id => !this.plugin.ClientAchievementProgressSource.TryGetObservation(id, out _));
        if (unobserved != 0)
        {
            return unobserved;
        }

        return trackedIds
            .Select(id => new
            {
                Id = id,
                ObservedAt = this.plugin.ClientAchievementProgressSource.TryGetObservation(id, out var observation)
                    ? observation.ObservedAt
                    : DateTimeOffset.MinValue,
            })
            .OrderBy(item => item.ObservedAt)
            .First().Id;
    }

    private void OpenNativeAchievement(uint achievementId, string source)
    {
        this.plugin.DebugLog.Trace("Tracker.OpenNativeAchievement", $"source={source} achievementId={achievementId}");
        if (!this.plugin.NativeAchievementNavigator.OpenAchievement(achievementId))
        {
            ImGui.TextDisabled("Could not open Achievements right now.");
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
