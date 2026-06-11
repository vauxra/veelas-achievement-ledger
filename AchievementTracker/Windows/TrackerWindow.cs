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

    // Section: main /val window draw loop.
    // Component: ImGui UI. Risk: low; button clicks call clearly named Plugin/service methods.
    public override void Draw()
    {
        this.plugin.AchievementProgressSource.UpdateCache();
        this.DrawTopButtons();
        this.DrawTrackedAchievementList();
    }

    // Section: top toolbar.
    // Component: user-guided native Achievement UI actions. Risk: low-to-medium; native opens are button-driven and rate-limited.
    private void DrawTopButtons()
    {
        this.DrawConfigureButton();
        ImGui.SameLine();
        this.DrawUpdateNextButton();
        ImGui.SameLine();
        this.DrawCloseAchievementsButton();
        this.DrawUpdateOpenLockoutStatus();
        ImGui.Separator();
    }

    private void DrawConfigureButton()
    {
        if (ImGui.Button("Configure"))
        {
            this.plugin.ToggleConfigUi();
        }

        AddTooltip("Open configuration.");
    }

    private void DrawUpdateNextButton()
    {
        var updateOpenLocked = !this.plugin.CanOpenAchievementForUpdate;
        if (updateOpenLocked)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button("Update Next"))
        {
            this.OpenNextTrackedAchievementForUpdate();
        }

        if (updateOpenLocked)
        {
            ImGui.EndDisabled();
        }

        AddTooltip("Open the next tracked Achievement entry.");
    }

    private void DrawCloseAchievementsButton()
    {
        if (!ImGui.Button("Close Achievements"))
        {
            AddTooltip("Close the native Achievements window.");
            return;
        }

        if (!this.plugin.NativeAchievementNavigator.CloseAchievements())
        {
            ImGui.TextDisabled("Could not close Achievements right now.");
        }

        AddTooltip("Close the native Achievements window.");
    }

    // Section: tracked achievement rows.
    // Component: plugin data + UI. Risk: low, except reload/search icons call native UI wrappers.
    private void DrawTrackedAchievementList()
    {
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
        var progressText = this.GetProgressText(achievementId);
        var updatedText = this.GetLastObservedText(achievementId);

        ImGui.PushID((int)achievementId);
        this.DrawRowUpdateButton(achievementId);
        ImGui.SameLine();
        this.DrawRowInspectButton(achievementId);
        ImGui.SameLine();
        ImGui.TextWrapped(info.Name);
        ImGui.TextDisabled(progressText);
        ImGui.SameLine();
        ImGui.TextDisabled(updatedText);
        ImGui.PopID();
    }

    private void DrawRowUpdateButton(uint achievementId)
    {
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
    }

    private void DrawRowInspectButton(uint achievementId)
    {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            this.OpenNativeAchievement(achievementId);
        }

        AddTooltip("Open in Achievements.");
    }

    private string GetProgressText(uint achievementId)
    {
        if (!this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            return "Progress unavailable";
        }

        return this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText();
    }

    private string GetLastObservedText(uint achievementId)
    {
        return this.plugin.ClientAchievementProgressSource.TryGetCachedObservation(achievementId, out var observation)
            ? $"updated {FormatAge(observation.ObservedAt)}"
            : "not updated yet";
    }

    // Section: choosing and opening achievements.
    // Component: user-guided native Achievement UI action. Risk: low-to-medium; no progress request is called directly.
    private void OpenNextTrackedAchievementForUpdate()
    {
        var nextId = this.GetNextTrackedAchievementId();
        if (nextId.HasValue)
        {
            this.OpenNativeAchievementForUpdate(nextId.Value);
        }
    }

    private uint? GetNextTrackedAchievementId()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            return null;
        }

        var unobserved = trackedIds.FirstOrDefault(id => !this.plugin.ClientAchievementProgressSource.TryGetCachedObservation(id, out _));
        if (unobserved != 0)
        {
            return unobserved;
        }

        return trackedIds
            .Select(id => new
            {
                Id = id,
                ObservedAt = this.plugin.ClientAchievementProgressSource.TryGetCachedObservation(id, out var observation)
                    ? observation.ObservedAt
                    : DateTimeOffset.MinValue,
            })
            .OrderBy(item => item.ObservedAt)
            .First().Id;
    }

    private void OpenNativeAchievementForUpdate(uint achievementId)
    {
        if (this.plugin.OpenAchievementForUpdate(achievementId))
        {
            return;
        }

        ImGui.TextDisabled(this.plugin.CanOpenAchievementForUpdate
            ? "Could not open Achievements right now."
            : "Achievement update opens are cooling down.");
    }

    private void OpenNativeAchievement(uint achievementId)
    {
        if (!this.plugin.NativeAchievementNavigator.OpenAchievement(achievementId))
        {
            ImGui.TextDisabled("Could not open Achievements right now.");
        }
    }

    // Section: small UI formatting helpers.
    // Component: pure UI. Risk: low.
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
