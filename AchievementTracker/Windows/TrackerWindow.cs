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
            this.plugin.ToggleConfigUi();
        }
        AddTooltip("Open configuration.");

        ImGui.SameLine();
        var updateOpenLocked = !this.plugin.CanOpenAchievementForUpdate;
        if (updateOpenLocked)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Update Next"))
        {
            var nextId = this.GetNextTrackedAchievementId();
            if (nextId.HasValue)
            {
                this.OpenNativeAchievementForUpdate(nextId.Value);
            }
        }

        if (updateOpenLocked)
        {
            ImGui.EndDisabled();
        }

        AddTooltip("Open the next tracked Achievement entry.");

        ImGui.SameLine();
        if (ImGui.Button("Close Achievements"))
        {
            if (!this.plugin.NativeAchievementNavigator.CloseAchievements())
            {
                ImGui.TextDisabled("Could not close Achievements right now.");
            }
        }
        AddTooltip("Close the native Achievements window.");

        this.DrawUpdateOpenLockoutStatus();

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
        var updateOpenLocked = !this.plugin.CanOpenAchievementForUpdate;
        if (updateOpenLocked)
        {
            ImGui.BeginDisabled();
        }

        if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
        {
            this.OpenNativeAchievementForUpdate(achievementId);
        }

        if (updateOpenLocked)
        {
            ImGui.EndDisabled();
        }

        AddTooltip("Open native Achievement entry to update.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            this.OpenNativeAchievement(achievementId);
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

    private void OpenNativeAchievementForUpdate(uint achievementId)
    {
        if (!this.plugin.OpenAchievementForUpdate(achievementId))
        {
            ImGui.TextDisabled(this.plugin.CanOpenAchievementForUpdate
                ? "Could not open Achievements right now."
                : "Achievement update opens are cooling down.");
        }
    }

    private void OpenNativeAchievement(uint achievementId)
    {
        if (!this.plugin.NativeAchievementNavigator.OpenAchievement(achievementId))
        {
            ImGui.TextDisabled("Could not open Achievements right now.");
        }
    }

    private void DrawUpdateOpenLockoutStatus()
    {
        var remaining = this.plugin.AchievementUpdateOpenRemaining;
        if (remaining > TimeSpan.Zero)
        {
            ImGui.TextDisabled($"Achievement update opens available in {Math.Ceiling(remaining.TotalSeconds):0}s");
        }
    }

    private static void AddTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
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
