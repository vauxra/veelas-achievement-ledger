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
    private bool templatesOpen;
    private bool searchPanelOpen = true;
    private SearchSortMode searchSortMode = SearchSortMode.GameOrder;
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

    private enum SearchSortMode
    {
        GameOrder,
        Alphabetical,
    }

    public override void Draw()
    {
        this.plugin.AchievementProgressSource.UpdateCache();

        this.DrawToolbar();
        this.DrawQueueStatus();
        ImGui.Separator();
        this.DrawMainPane();
    }

    private void DrawToolbar()
    {
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
        if (ImGuiComponents.IconButton("toggle-templates", this.templatesOpen ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye))
        {
            this.templatesOpen = !this.templatesOpen;
            this.EnsureSelectedPresetIsValid();
        }
        AddTooltip(this.templatesOpen ? "Hide saved templates column." : "Show saved templates column.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("toggle-main-search", FontAwesomeIcon.Book))
        {
            this.searchPanelOpen = !this.searchPanelOpen;
        }
        AddTooltip(this.searchPanelOpen ? "Hide category/search columns." : "Show category/search columns.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("open-config", FontAwesomeIcon.Cog))
        {
            this.plugin.OpenConfigUi();
        }
        AddTooltip("Open configuration.");
    }

    private void DrawMainPane()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X;
        var visibleColumns = 1 + (this.templatesOpen ? 1 : 0) + (this.searchPanelOpen ? 2 : 0);
        var availableForColumns = width - (spacing * Math.Max(0, visibleColumns - 1));
        var templateWidth = this.templatesOpen ? Math.Clamp(availableForColumns * 0.22f, 190f, 280f) : 0f;
        var categoryWidth = this.searchPanelOpen ? Math.Clamp(availableForColumns * 0.22f, 190f, 260f) : 0f;
        var searchWidth = this.searchPanelOpen ? Math.Clamp(availableForColumns * 0.32f, 290f, 440f) : 0f;
        var trackedWidth = Math.Max(280f, availableForColumns - templateWidth - categoryWidth - searchWidth);

        if (this.templatesOpen)
        {
            ImGui.BeginChild("##MainTemplateColumn", new Vector2(templateWidth, 0), true);
            this.DrawTemplateColumn();
            ImGui.EndChild();
            ImGui.SameLine();
        }

        if (this.searchPanelOpen)
        {
            ImGui.BeginChild("##MainCategoryColumn", new Vector2(categoryWidth, 0), true);
            this.DrawAchievementCategoryColumn();
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("##MainSearchColumn", new Vector2(searchWidth, 0), true);
            this.DrawAchievementSearchColumn();
            ImGui.EndChild();

            ImGui.SameLine();
        }

        ImGui.BeginChild("##MainTrackedColumn", new Vector2(trackedWidth, 0), true);
        this.DrawTrackedColumn();
        ImGui.EndChild();
    }

    private void DrawTemplateColumn()
    {
        this.EnsureSelectedPresetIsValid();
        ImGui.TextUnformatted("Saved templates");
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

    private void DrawAchievementCategoryColumn()
    {
        ImGui.TextUnformatted("Achievement Categories");
        if (ImGui.Button("All categories"))
        {
            this.selectedCategoryFilter = string.Empty;
            this.selectedSubcategoryFilter = string.Empty;
        }
        AddTooltip("Show all categories.");

        ImGui.Separator();
        var categoryEntries = this.plugin.AchievementCatalog.Search(string.Empty, 5000)
            .Select(info => (Info: info, Parts: SplitCategoryPath(info.CategoryName), Sort: this.GetGameSortKey(info)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Parts.Category))
            .GroupBy(entry => entry.Parts.Category)
            .OrderBy(group => group.Min(entry => entry.Sort.KindOrder))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var categoryGroup in categoryEntries)
        {
            var selectedCategory = string.Equals(this.selectedCategoryFilter, categoryGroup.Key, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(this.selectedSubcategoryFilter);
            if (ImGui.Selectable(categoryGroup.Key, selectedCategory))
            {
                this.selectedCategoryFilter = categoryGroup.Key;
                this.selectedSubcategoryFilter = string.Empty;
            }

            var subcategories = categoryGroup
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Parts.Subcategory))
                .GroupBy(entry => entry.Parts.Subcategory)
                .OrderBy(group => group.Min(entry => entry.Sort.CategoryOrder))
                .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (subcategories.Count == 0)
            {
                continue;
            }

            ImGui.Indent(12);
            foreach (var subcategoryGroup in subcategories)
            {
                var selected = string.Equals(this.selectedCategoryFilter, categoryGroup.Key, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(this.selectedSubcategoryFilter, subcategoryGroup.Key, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(subcategoryGroup.Key, selected))
                {
                    this.selectedCategoryFilter = categoryGroup.Key;
                    this.selectedSubcategoryFilter = subcategoryGroup.Key;
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

        var hideCompleted = this.plugin.Configuration.HideCompletedInSearch;
        if (ImGui.Checkbox("Hide completed", ref hideCompleted))
        {
            this.plugin.Configuration.HideCompletedInSearch = hideCompleted;
            this.plugin.SaveConfiguration();
        }
        AddTooltip("Hide completed achievements from search results.");

        ImGui.SameLine();
        ImGui.TextUnformatted("Sort:");
        ImGui.SameLine();
        var gameSort = this.searchSortMode == SearchSortMode.GameOrder;
        if (ImGui.RadioButton("Game", gameSort))
        {
            this.searchSortMode = SearchSortMode.GameOrder;
        }
        AddTooltip("Use the in-game achievement category/subcategory/order.");
        ImGui.SameLine();
        var alphabeticalSort = this.searchSortMode == SearchSortMode.Alphabetical;
        if (ImGui.RadioButton("A-Z", alphabeticalSort))
        {
            this.searchSortMode = SearchSortMode.Alphabetical;
        }
        AddTooltip("Sort achievement names alphabetically.");

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        var results = this.SortSearchResults(this.plugin.AchievementCatalog.Search(this.achievementSearchQuery, 5000)
                .Where(this.MatchesSelectedCategory)
                .Where(result => !this.plugin.Configuration.HideCompletedInSearch || !this.IsComplete(result.Id)))
            .Take(80)
            .ToList();

        if (results.Count == 0)
        {
            ImGui.TextDisabled("No matching manually viewable achievements found.");
            return;
        }

        ImGui.Separator();
        foreach (var result in results)
        {
            this.DrawSearchAchievementResult(result, trackedIds);
        }
    }

    private IEnumerable<AchievementInfo> SortSearchResults(IEnumerable<AchievementInfo> results)
        => this.searchSortMode == SearchSortMode.Alphabetical
            ? results.OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase).ThenBy(result => result.Id)
            : results.OrderBy(result => this.GetGameSortKey(result).KindOrder)
                .ThenBy(result => this.GetGameSortKey(result).CategoryOrder)
                .ThenBy(result => this.GetGameSortKey(result).AchievementOrder)
                .ThenBy(result => this.GetGameSortKey(result).RowId);

    private void DrawSearchAchievementResult(AchievementInfo result, IReadOnlyList<uint> trackedIds)
    {
        ImGui.PushID($"search-{result.Id}");
        var alreadyTracked = trackedIds.Contains(result.Id);
        var canAdd = !alreadyTracked && trackedIds.Count < TrackedAchievementStore.MaxTrackedAchievements;
        using (ImRaiiShim.Disabled(!canAdd))
        {
            if (ImGuiComponents.IconButton("search-add", FontAwesomeIcon.Plus))
            {
                this.AddTrackedAchievement(result.Id);
            }
        }
        AddTooltip(alreadyTracked ? "Already tracked." : canAdd ? "Add to tracked list." : "Tracked list is full.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("search-open-native", FontAwesomeIcon.Search))
        {
            this.plugin.OpenNativeAchievementForInspection(result.Id);
        }
        AddTooltip("Open in Achievements.");

        ImGui.SameLine();
        ImGui.BeginGroup();
        ImGui.TextWrapped(result.Name);
        if (this.IsComplete(result.Id))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.2f, 1f), "✓");
        }

        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            ImGui.TextDisabled(result.Description);
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
            ImGui.TextWrapped("No achievements tracked. Use the search column to add one, or show templates with the eye icon.");
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
        var progressText = "not updated yet";
        if (this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row)
            && (this.plugin.ClientAchievementProgressSource.TryGetObservation(achievementId, out _)
                || this.plugin.AchievementProgressService.IsComplete(row)))
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
        ImGui.PopID();
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

    private (byte KindOrder, byte CategoryOrder, ushort AchievementOrder, uint RowId) GetGameSortKey(AchievementInfo info)
    {
        if (!this.plugin.AchievementCatalog.TryGetRow(info.Id, out var row))
        {
            return (byte.MaxValue, byte.MaxValue, ushort.MaxValue, info.Id);
        }

        var categoryOrder = row.AchievementCategory.IsValid ? row.AchievementCategory.Value.Order : byte.MaxValue;
        var kindOrder = row.AchievementCategory.IsValid && row.AchievementCategory.Value.AchievementKind.IsValid
            ? row.AchievementCategory.Value.AchievementKind.Value.Order
            : byte.MaxValue;
        return (kindOrder, categoryOrder, row.Order, row.RowId);
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
