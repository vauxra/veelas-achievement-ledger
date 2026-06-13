using AchievementTracker.Models;
using AchievementTracker.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace AchievementTracker.Windows;

public sealed class TrackerWindow : Window
{
    private readonly Plugin plugin;
    private bool templateModalOpen;
    private string presetNameInput = string.Empty;
    private string selectedPresetName = string.Empty;
    private string achievementSearchQuery = string.Empty;
    private string selectedCategoryFilter = string.Empty;
    private string selectedSubcategoryFilter = string.Empty;

    public TrackerWindow(Plugin plugin)
        : base("Achieve Ex+##AchieveExPlusLive", ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus)
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(760, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        this.plugin.AchievementProgressSource.UpdateCache();

        this.DrawToolbar();
        this.DrawQueueStatus();
        ImGui.Separator();
        this.DrawMainPane();
        this.DrawTemplateModal();
    }

    private void DrawToolbar()
    {
        if (ImGuiComponents.IconButton("open-config", FontAwesomeIcon.Cog))
        {
            this.plugin.OpenConfigUi();
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
        if (ImGuiComponents.IconButton("open-tracked-config", FontAwesomeIcon.Search))
        {
            this.plugin.OpenTrackedAchievementsConfig();
        }
        AddTooltip("Open Config > Tracked Achievements.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("open-template-modal", FontAwesomeIcon.Eye))
        {
            this.templateModalOpen = true;
            this.EnsureSelectedPresetIsValid();
            ImGui.OpenPopup("Saved templates##AchieveExTemplateModal");
        }
        AddTooltip("Open saved achievement-list templates.");
    }

    private void DrawMainPane()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X;
        var categoryWidth = Math.Clamp(width * 0.24f, 190f, 260f);
        var searchWidth = Math.Clamp(width * 0.38f, 300f, 470f);
        var trackedWidth = Math.Max(260f, width - categoryWidth - searchWidth - (spacing * 2f));

        ImGui.BeginChild("##MainCategoryColumn", new Vector2(categoryWidth, 0), true);
        this.DrawAchievementCategoryColumn();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##MainSearchColumn", new Vector2(searchWidth, 0), true);
        this.DrawAchievementSearchColumn();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##MainTrackedColumn", new Vector2(trackedWidth, 0), true);
        this.DrawTrackedColumn();
        ImGui.EndChild();
    }

    private void DrawAchievementCategoryColumn()
    {
        ImGui.TextUnformatted("Categories");
        if (ImGui.Button("All categories"))
        {
            this.selectedCategoryFilter = string.Empty;
            this.selectedSubcategoryFilter = string.Empty;
        }
        AddTooltip("Show all categories.");

        ImGui.Separator();
        var allAchievements = this.plugin.AchievementCatalog.Search(string.Empty, 5000).ToList();
        var grouped = allAchievements
            .GroupBy(info => SplitCategoryPath(info.CategoryName).Category)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var categoryGroup in grouped)
        {
            var category = string.IsNullOrWhiteSpace(categoryGroup.Key) ? "Other" : categoryGroup.Key;
            var selectedCategory = string.Equals(this.selectedCategoryFilter, categoryGroup.Key, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(this.selectedSubcategoryFilter);
            if (ImGui.Selectable($"{category} ({categoryGroup.Count()})", selectedCategory))
            {
                this.selectedCategoryFilter = categoryGroup.Key;
                this.selectedSubcategoryFilter = string.Empty;
            }

            var subcategories = categoryGroup
                .Select(info => SplitCategoryPath(info.CategoryName).Subcategory)
                .Where(subcategory => !string.IsNullOrWhiteSpace(subcategory))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(subcategory => subcategory, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (subcategories.Count == 0)
            {
                continue;
            }

            ImGui.Indent(12);
            foreach (var subcategory in subcategories)
            {
                var count = categoryGroup.Count(info => string.Equals(SplitCategoryPath(info.CategoryName).Subcategory, subcategory, StringComparison.OrdinalIgnoreCase));
                var selected = string.Equals(this.selectedCategoryFilter, categoryGroup.Key, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(this.selectedSubcategoryFilter, subcategory, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable($"{subcategory} ({count})", selected))
                {
                    this.selectedCategoryFilter = categoryGroup.Key;
                    this.selectedSubcategoryFilter = subcategory;
                }
            }

            ImGui.Unindent(12);
        }
    }

    private void DrawAchievementSearchColumn()
    {
        ImGui.TextUnformatted("Achievement search");
        ImGui.SetNextItemWidth(-70);
        ImGui.InputTextWithHint("##MainAchievementSearch", "Search name or category", ref this.achievementSearchQuery, 128);
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            this.achievementSearchQuery = string.Empty;
            this.selectedCategoryFilter = string.Empty;
            this.selectedSubcategoryFilter = string.Empty;
        }
        AddTooltip("Clear search and category filters.");

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        var results = this.plugin.AchievementCatalog.Search(this.achievementSearchQuery, 5000)
            .Where(this.MatchesSelectedCategory)
            .Where(result => !this.plugin.Configuration.HideCompletedInSearch || !this.IsComplete(result.Id))
            .Take(80)
            .ToList();

        if (results.Count == 0)
        {
            ImGui.TextDisabled("No matching manually viewable achievements found.");
            return;
        }

        ImGui.TextDisabled($"Showing {results.Count} results.");
        ImGui.Separator();
        foreach (var result in results)
        {
            this.DrawSearchAchievementResult(result, trackedIds);
        }
    }

    private void DrawSearchAchievementResult(AchievementInfo result, IReadOnlyList<uint> trackedIds)
    {
        ImGui.PushID($"search-{result.Id}");
        var alreadyTracked = trackedIds.Contains(result.Id);
        var canAdd = !alreadyTracked && trackedIds.Count < TrackedAchievementStore.MaxTrackedAchievements;
        using (ImRaiiShim.Disabled(!canAdd))
        {
            if (ImGui.Button(alreadyTracked ? "Added" : "Add"))
            {
                this.AddTrackedAchievement(result.Id);
            }
        }
        AddTooltip(alreadyTracked ? "Already tracked." : canAdd ? "Add to tracked list." : "Tracked list is full.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            this.plugin.OpenNativeAchievementForInspection(result.Id);
        }
        AddTooltip("Open in Achievements.");

        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(result.Name);
        if (!string.IsNullOrWhiteSpace(result.CategoryName))
        {
            ImGui.TextDisabled(result.CategoryName);
        }
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void DrawTrackedColumn()
    {
        ImGui.TextUnformatted("Tracked achievements");
        ImGui.Separator();
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked. Use the search column to add one, or open templates with the eye icon.");
            return;
        }

        foreach (var achievementId in trackedIds)
        {
            this.DrawTrackedAchievement(achievementId);
        }
    }

    private void DrawTrackedAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        var progressText = "Progress unavailable";
        if (this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            progressText = this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText();
        }

        ImGui.PushID((int)achievementId);
        if (ImGuiComponents.IconButton("tracked-remove-main", FontAwesomeIcon.Times))
        {
            _ = this.RemoveTrackedAchievement(achievementId);
            ImGui.PopID();
            return;
        }
        AddTooltip("Remove from tracked.");

        ImGui.SameLine();
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

    private void DrawTemplateModal()
    {
        if (this.templateModalOpen)
        {
            ImGui.OpenPopup("Saved templates##AchieveExTemplateModal");
        }

        ImGui.SetNextWindowSize(new Vector2(520, 420), ImGuiCond.FirstUseEver);
        if (!ImGui.BeginPopupModal("Saved templates##AchieveExTemplateModal", ref this.templateModalOpen, ImGuiWindowFlags.NoSavedSettings))
        {
            return;
        }

        this.EnsureSelectedPresetIsValid();
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
        ImGui.EndPopup();
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

        ImGui.TextDisabled("Click to select. Double-click to load. Right-click for options.");
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

    private bool AddTrackedAchievement(uint achievementId)
    {
        if (!this.plugin.TrackedAchievements.TryAdd(achievementId))
        {
            return false;
        }

        this.plugin.SaveTrackedAchievements();
        if (!this.plugin.Configuration.AutoUpdateAchievementIds.Contains(achievementId))
        {
            this.plugin.Configuration.AutoUpdateAchievementIds.Add(achievementId);
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }

        return true;
    }

    private bool RemoveTrackedAchievement(uint achievementId)
    {
        if (!this.plugin.TrackedAchievements.Remove(achievementId))
        {
            return false;
        }

        var removedAutoUpdateEntry = this.plugin.Configuration.AutoUpdateAchievementIds.RemoveAll(id => id == achievementId) > 0;
        this.plugin.SaveTrackedAchievements();
        this.plugin.SaveConfiguration();
        if (removedAutoUpdateEntry)
        {
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }

        return true;
    }

    private bool MatchesSelectedCategory(AchievementInfo info)
    {
        if (string.IsNullOrWhiteSpace(this.selectedCategoryFilter))
        {
            return true;
        }

        var parts = SplitCategoryPath(info.CategoryName);
        if (!string.Equals(parts.Category, this.selectedCategoryFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(this.selectedSubcategoryFilter)
            || string.Equals(parts.Subcategory, this.selectedSubcategoryFilter, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsComplete(uint achievementId)
        => this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row)
            && this.plugin.AchievementProgressService.IsComplete(row);

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

    private static (string Category, string Subcategory) SplitCategoryPath(string categoryPath)
    {
        var parts = categoryPath.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[^1]),
        };
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
