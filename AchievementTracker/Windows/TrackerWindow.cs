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
    private uint? guidedAchievementId;
    private GuidedStep guidedStep = GuidedStep.NotStarted;

    public TrackerWindow(Plugin plugin)
        : base("Achievement Tracker##AchievementTrackerLive")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(460, 220),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    private enum GuidedStep
    {
        NotStarted,
        OpenAchievementsMenu,
        ClickCategory,
        ClickSubcategory,
        ClickAchievement,
        WaitForObservation,
        Complete,
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
        if (ImGui.Button("Open next in Achievements"))
        {
            var nextId = this.GetNextTrackedAchievementId();
            if (nextId.HasValue)
            {
                this.OpenNativeAchievement(nextId.Value, "open-next");
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("Start guided check"))
        {
            var nextId = this.GetNextTrackedAchievementId();
            if (nextId.HasValue)
            {
                this.StartGuide(nextId.Value, "top-button");
            }
        }

        ImGui.SameLine();
        ImGui.TextDisabled("/achtrack");
        ImGui.Separator();

        ImGui.TextWrapped("A/B test options: direct buttons open the native Achievement entry; guided check walks you through the manual menu > category > subcategory > achievement flow without synthetic clicks.");
        ImGui.TextDisabled("Passive observer records progress only when the native client receives progress data.");
        ImGui.Separator();

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked. Open Configure to add up to 5.");
            return;
        }

        this.DrawGuidedCheck();
        ImGui.Separator();

        foreach (var achievementId in trackedIds)
        {
            this.DrawLiveAchievement(achievementId);
        }
    }

    private void DrawGuidedCheck()
    {
        ImGui.TextUnformatted("Guided manual check");
        if (!this.guidedAchievementId.HasValue || !this.plugin.AchievementCatalog.TryGet(this.guidedAchievementId.Value, out var info))
        {
            ImGui.TextDisabled("No guided check active. Use Start guided check or Guide on a row.");
            return;
        }

        var observed = this.plugin.ClientAchievementProgressSource.TryGetObservation(info.Id, out var observation);
        if (observed && this.guidedStep == GuidedStep.WaitForObservation)
        {
            this.guidedStep = GuidedStep.Complete;
        }

        ImGui.TextWrapped($"Target: {info.Name}");
        ImGui.TextDisabled($"Category: {DisplayCategory(info.CategoryName)} | ID: {info.Id} | {info.Points} pts");
        ImGui.TextWrapped(this.GetGuidedInstruction(info));

        var buttonLabel = this.GetGuidedButtonLabel();
        if (ImGui.Button(buttonLabel))
        {
            this.AdvanceGuide(info.Id, buttonLabel);
        }

        ImGui.SameLine();
        if (ImGui.Button("Restart guide"))
        {
            this.StartGuide(info.Id, "restart");
        }

        ImGui.SameLine();
        if (ImGui.Button("Stop guide"))
        {
            this.plugin.DebugLog.Trace("Tracker.Guide", $"stop achievementId={info.Id} step={this.guidedStep}");
            this.guidedAchievementId = null;
            this.guidedStep = GuidedStep.NotStarted;
        }

        if (observed)
        {
            ImGui.TextDisabled($"Observed: {observation.Current:n0} / {observation.Max:n0} {FormatAge(observation.ObservedAt)} via {observation.Source}");
        }
        else
        {
            ImGui.TextDisabled("Not observed this session yet.");
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

        ImGui.PushID((int)achievementId);
        if (ImGui.Button("Open in Achievements"))
        {
            this.OpenNativeAchievement(achievementId, "row-button");
        }

        ImGui.SameLine();
        if (ImGui.Button("Guide"))
        {
            this.StartGuide(achievementId, "row-button");
        }

        ImGui.SameLine();
        ImGui.TextWrapped(info.Name);
        ImGui.TextDisabled(progressText);
        ImGui.SameLine();
        ImGui.TextDisabled($"category: {DisplayCategory(info.CategoryName)}");

        if (this.plugin.ClientAchievementProgressSource.TryGetObservation(achievementId, out var observation))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"observed {FormatAge(observation.ObservedAt)} via {observation.Source}");
        }
        else
        {
            ImGui.SameLine();
            ImGui.TextDisabled("not observed this session");
        }

        ImGui.PopID();
    }

    private void StartGuide(uint achievementId, string source)
    {
        this.guidedAchievementId = achievementId;
        this.guidedStep = GuidedStep.OpenAchievementsMenu;
        this.plugin.DebugLog.Trace("Tracker.Guide", $"start source={source} achievementId={achievementId}");
    }

    private void AdvanceGuide(uint achievementId, string buttonLabel)
    {
        this.plugin.DebugLog.Trace("Tracker.Guide", $"advance achievementId={achievementId} from={this.guidedStep} button='{buttonLabel}'");
        this.guidedStep = this.guidedStep switch
        {
            GuidedStep.NotStarted => GuidedStep.OpenAchievementsMenu,
            GuidedStep.OpenAchievementsMenu => GuidedStep.ClickCategory,
            GuidedStep.ClickCategory => GuidedStep.ClickSubcategory,
            GuidedStep.ClickSubcategory => GuidedStep.ClickAchievement,
            GuidedStep.ClickAchievement => GuidedStep.WaitForObservation,
            GuidedStep.WaitForObservation => GuidedStep.Complete,
            GuidedStep.Complete => GuidedStep.OpenAchievementsMenu,
            _ => GuidedStep.OpenAchievementsMenu,
        };
    }

    private string GetGuidedButtonLabel() => this.guidedStep switch
    {
        GuidedStep.OpenAchievementsMenu => "I opened Achievements",
        GuidedStep.ClickCategory => "I clicked category",
        GuidedStep.ClickSubcategory => "I clicked subcategory",
        GuidedStep.ClickAchievement => "I clicked achievement",
        GuidedStep.WaitForObservation => "Check observation",
        GuidedStep.Complete => "Check again",
        _ => "Next step",
    };

    private string GetGuidedInstruction(AchievementTracker.Models.AchievementInfo info) => this.guidedStep switch
    {
        GuidedStep.OpenAchievementsMenu => "Step 1: Manually open the game's Achievement menu. Do not use the direct A/B open button for this guided run.",
        GuidedStep.ClickCategory => $"Step 2: Click the category that contains this achievement. Known category from Lumina: {DisplayCategory(info.CategoryName)}.",
        GuidedStep.ClickSubcategory => "Step 3: Click the matching subcategory in the native Achievement window. If the native UI has no separate subcategory for this achievement, continue after selecting the closest visible grouping.",
        GuidedStep.ClickAchievement => $"Step 4: Click the achievement named '{info.Name}' in the native Achievement window.",
        GuidedStep.WaitForObservation => "Step 5: Waiting for the passive observer to see the native progress response. If nothing appears, leave the Achievement entry selected briefly or re-click it manually.",
        GuidedStep.Complete => "Observed. Use Check again to restart this target, Guide on another row, or Start guided check for the oldest/unobserved tracked achievement.",
        _ => "Start a guided check from a tracked row or the top button.",
    };

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
            ImGui.TextDisabled("Native Achievement window could not be opened right now.");
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

    private static string DisplayCategory(string categoryName)
        => string.IsNullOrWhiteSpace(categoryName) ? "unknown" : categoryName;
}
