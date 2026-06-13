using AchievementTracker.Models;
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
    private bool presetPanelOpen;
    private string presetNameInput = string.Empty;
    private string selectedPresetName = string.Empty;

    public TrackerWindow(Plugin plugin)
        : base("Achieve Ex+##AchieveExPlusLive", ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus)
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

        this.DrawToolbar();
        this.DrawQueueStatus();
        ImGui.Separator();

        if (this.presetPanelOpen)
        {
            var spacing = ImGui.GetStyle().ItemSpacing.X;
            var availableWidth = ImGui.GetContentRegionAvail().X;
            var presetWidth = Math.Clamp(availableWidth * 0.34f, 260f, 360f);
            var listWidth = Math.Max(300f, availableWidth - presetWidth - spacing);

            ImGui.BeginChild("##ValTrackedList", new Vector2(listWidth, 0), false);
            this.DrawTrackedList();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("##ValPresetPopout", new Vector2(presetWidth, 0), true);
            this.DrawPresetPopout();
            ImGui.EndChild();
            return;
        }

        this.DrawTrackedList();
    }

    private void DrawToolbar()
    {
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

        this.SameLineOrWrap(110f);
        var autoUpdateEnabled = this.plugin.Configuration.ExperimentalAutoUpdateEnabled;
        if (ImGui.Checkbox("Auto update", ref autoUpdateEnabled))
        {
            this.plugin.Configuration.ExperimentalAutoUpdateEnabled = autoUpdateEnabled;
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        AddTooltip("Run timed updates.");

        this.SameLineOrWrap(42f);
        if (ImGuiComponents.IconButton(this.presetPanelOpen ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye))
        {
            this.presetPanelOpen = !this.presetPanelOpen;
            this.EnsureSelectedPresetIsValid();
        }
        AddTooltip(this.presetPanelOpen ? "Hide saved achievement-list templates." : "Show saved achievement-list templates.");
    }

    private void DrawTrackedList()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked. Use Configure to add one or open templates with the eye icon.");
            return;
        }

        foreach (var achievementId in trackedIds)
        {
            this.DrawAchievement(achievementId);
        }
    }

    private void DrawPresetPopout()
    {
        this.EnsureSelectedPresetIsValid();

        ImGui.TextUnformatted("Saved templates");
        ImGui.SameLine();
        if (ImGuiComponents.IconButton("hide-template-popout", FontAwesomeIcon.EyeSlash))
        {
            this.presetPanelOpen = false;
            return;
        }
        AddTooltip("Hide templates.");

        ImGui.Separator();
        this.DrawPresetButtons();

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##PresetName", "Template name", ref this.presetNameInput, TrackedAchievementPresetStore.MaxPresetNameLength))
        {
            this.presetNameInput = TrackedAchievementPresetStore.SanitizeName(this.presetNameInput);
        }
        AddTooltip("Name used by Save and Rename.");

        ImGui.Separator();
        this.DrawPresetList();
        this.DrawPresetContextPopups();
    }

    private void DrawPresetButtons()
    {
        if (ImGuiComponents.IconButton("template-save", FontAwesomeIcon.Save))
        {
            this.SaveCurrentTemplate();
        }
        AddTooltip("Save current tracked list as this template name.");

        ImGui.SameLine(0, 4);
        if (ImGuiComponents.IconButton("template-load", FontAwesomeIcon.FolderOpen))
        {
            this.LoadSelectedPreset();
        }
        AddTooltip("Load selected template.");

        ImGui.SameLine(0, 4);
        if (ImGuiComponents.IconButton("template-rename", FontAwesomeIcon.Edit))
        {
            this.RenameSelectedPreset();
        }
        AddTooltip("Rename selected template to the typed name.");

        ImGui.SameLine(0, 4);
        if (ImGuiComponents.IconButton("template-copy", FontAwesomeIcon.Copy))
        {
            this.CopySelectedPreset();
        }
        AddTooltip("Make a copy of the selected template.");

        ImGui.SameLine(0, 4);
        using (ImRaiiShim.Disabled(!ImGui.GetIO().KeyShift))
        {
            if (ImGuiComponents.IconButton("template-delete", FontAwesomeIcon.Trash))
            {
                this.DeleteSelectedPreset();
            }
        }
        AddTooltip("Hold Shift to delete selected template.");
    }

    private void DrawPresetList()
    {
        if (this.plugin.Configuration.TrackedAchievementPresets.Count == 0)
        {
            ImGui.TextDisabled("No saved templates yet.");
            ImGui.TextWrapped("Type a name, then press save to capture the current tracked list.");
            return;
        }

        ImGui.TextDisabled("Click to preview/select. Double-click to load. Right-click for options.");
        ImGui.BeginChild("##TemplateList", Vector2.Zero, true);
        foreach (var preset in this.plugin.Configuration.TrackedAchievementPresets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase))
        {
            this.DrawPresetListItem(preset);
        }

        ImGui.EndChild();
    }

    private void DrawPresetListItem(TrackedAchievementPreset preset)
    {
        var selected = string.Equals(preset.Name, this.selectedPresetName, StringComparison.OrdinalIgnoreCase);
        var label = $"{(selected ? "> " : string.Empty)}{preset.Name} ({preset.AchievementIds.Count})";
        ImGui.PushID($"template-{preset.Name}");
        if (ImGui.Selectable(label, selected, ImGuiSelectableFlags.AllowDoubleClick))
        {
            this.selectedPresetName = preset.Name;
            this.presetNameInput = preset.Name;
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                this.LoadSelectedPreset();
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Right-click for template options. Double-click to load.");
        }

        this.DrawPresetContextMenu(preset);
        ImGui.PopID();
    }

    private void DrawPresetContextMenu(TrackedAchievementPreset preset)
    {
        if (!ImGui.BeginPopupContextItem($"template-context-{preset.Name}"))
        {
            return;
        }

        this.selectedPresetName = preset.Name;
        if (string.IsNullOrWhiteSpace(this.presetNameInput))
        {
            this.presetNameInput = preset.Name;
        }

        if (ImGui.Selectable("Load"))
        {
            this.LoadSelectedPreset();
        }

        if (ImGui.Selectable("Rename to typed name", false, ImGuiSelectableFlags.DontClosePopups))
        {
            this.RenameSelectedPreset();
        }

        if (ImGui.Selectable("Make a copy"))
        {
            this.CopySelectedPreset();
        }

        ImGui.Separator();
        using (ImRaiiShim.Disabled(!ImGui.GetIO().KeyShift))
        {
            if (ImGui.Selectable("Delete", false, ImGuiSelectableFlags.DontClosePopups))
            {
                this.DeleteSelectedPreset();
            }
        }
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            ImGui.SetTooltip("Hold Shift to delete.");
        }

        ImGui.EndPopup();
    }

    private void DrawPresetContextPopups()
    {
        // Reserved for modal-style template actions; keeps the template surface structured like AutoHook's list/context area.
    }

    private void SaveCurrentTemplate()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        if (TrackedAchievementPresetStore.SavePreset(this.plugin.Configuration.TrackedAchievementPresets, this.presetNameInput, trackedIds, out var savedName))
        {
            this.selectedPresetName = savedName;
            this.presetNameInput = savedName;
            this.plugin.SaveConfiguration();
        }
    }

    private void LoadSelectedPreset()
    {
        var preset = TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName);
        if (preset is null)
        {
            return;
        }

        this.plugin.TrackedAchievements.LoadFrom(preset.AchievementIds.Where(this.plugin.AchievementCatalog.IsManuallyViewable));
        this.plugin.SaveTrackedAchievements();
        this.plugin.ResetAutoUpdateCountdownIfActive();
    }

    private void RenameSelectedPreset()
    {
        if (TrackedAchievementPresetStore.RenamePreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName, this.presetNameInput, out var renamedTo))
        {
            this.selectedPresetName = renamedTo;
            this.presetNameInput = renamedTo;
            this.plugin.SaveConfiguration();
        }
    }

    private void CopySelectedPreset()
    {
        var preset = TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName);
        if (preset is null)
        {
            return;
        }

        var copyName = TrackedAchievementPresetStore.BuildCopyName(this.plugin.Configuration.TrackedAchievementPresets, preset.Name);
        if (TrackedAchievementPresetStore.SavePreset(this.plugin.Configuration.TrackedAchievementPresets, copyName, preset.AchievementIds, out var savedName))
        {
            this.selectedPresetName = savedName;
            this.presetNameInput = savedName;
            this.plugin.SaveConfiguration();
        }
    }

    private void DeleteSelectedPreset()
    {
        if (TrackedAchievementPresetStore.DeletePreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName))
        {
            this.selectedPresetName = string.Empty;
            this.presetNameInput = string.Empty;
            this.plugin.SaveConfiguration();
            this.EnsureSelectedPresetIsValid();
        }
    }

    private void EnsureSelectedPresetIsValid()
    {
        TrackedAchievementPresetStore.Normalize(this.plugin.Configuration.TrackedAchievementPresets);
        if (!string.IsNullOrWhiteSpace(this.selectedPresetName)
            && TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName) is not null)
        {
            return;
        }

        this.selectedPresetName = this.plugin.Configuration.TrackedAchievementPresets
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(this.presetNameInput))
        {
            this.presetNameInput = this.selectedPresetName;
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
            this.plugin.OpenNativeAchievementForInspection(achievementId);
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
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
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

    private sealed class ImRaiiShim : IDisposable
    {
        private readonly bool disabled;

        private ImRaiiShim(bool disabled)
        {
            this.disabled = disabled;
            if (disabled)
            {
                ImGui.BeginDisabled();
            }
        }

        public static ImRaiiShim Disabled(bool disabled) => new(disabled);

        public void Dispose()
        {
            if (this.disabled)
            {
                ImGui.EndDisabled();
            }
        }
    }
}
