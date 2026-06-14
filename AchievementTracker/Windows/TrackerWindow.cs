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
    private bool hideTrackedIcons;
    private bool resetMainPanelScrollNextDraw;
    private bool searchPanelOpen = true;
    private SearchSortMode searchSortMode = SearchSortMode.GameOrder;
    private string presetNameInput = string.Empty;
    private string selectedPresetName = string.Empty;
    private string achievementSearchQuery = string.Empty;
    private bool categoryFilterAll = true;
    private readonly HashSet<string> selectedCategoryFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> selectedSubcategoryFilters = new(StringComparer.OrdinalIgnoreCase);

    public TrackerWindow(Plugin plugin)
        : base("Achieve Ex+##AchieveExPlusLive", ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus)
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 260),
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

    public void ResetPanelScrollOnNextDraw()
        => this.resetMainPanelScrollNextDraw = true;

    private void DrawToolbar()
    {
        foreach (var item in this.plugin.Configuration.MainNavigationOrder)
        {
            if (!this.plugin.Configuration.ShownMainNavigationButtons.Contains(item))
            {
                continue;
            }

            this.DrawToolbarItem(item);
            ImGui.SameLine();
        }

        ImGui.NewLine();
    }

    private void DrawToolbarItem(string item)
    {
        switch (item)
        {
            case "Update All":
                if (ImGui.Button("Update All"))
                {
                    this.plugin.EnqueueUpdateAllTracked("manual-update-all");
                }
                AddTooltip("Queue tracked achievements through the native refresh coordinator with spacing, backoff, and a circuit breaker.");
                break;
            case "Auto update":
                var autoUpdateEnabled = this.plugin.Configuration.ExperimentalAutoUpdateEnabled;
                if (ImGui.Checkbox("Auto update", ref autoUpdateEnabled))
                {
                    this.plugin.Configuration.ExperimentalAutoUpdateEnabled = autoUpdateEnabled;
                    this.plugin.SaveConfiguration();
                    this.plugin.ResetAutoUpdateCountdownIfActive();
                }
                AddTooltip("Run conservative timed refreshes through the same native refresh coordinator.");
                break;
            case "Lists":
                if (this.DrawActiveIconButton("toggle-lists", FontAwesomeIcon.Save, this.templatesOpen))
                {
                    this.templatesOpen = !this.templatesOpen;
                    this.resetMainPanelScrollNextDraw = true;
                    this.EnsureSelectedPresetIsValid();
                }
                AddTooltip(this.templatesOpen ? "Hide Lists column." : "Show Lists column.");
                break;
            case "Search":
                if (this.DrawActiveIconButton("toggle-main-search", FontAwesomeIcon.Book, this.searchPanelOpen))
                {
                    this.searchPanelOpen = !this.searchPanelOpen;
                    this.resetMainPanelScrollNextDraw = true;
                }
                AddTooltip(this.searchPanelOpen ? "Hide category/search columns." : "Show category/search columns.");
                break;
            case "Config":
                if (this.DrawActiveIconButton("toggle-config", FontAwesomeIcon.Cog, this.plugin.IsConfigUiOpen))
                {
                    this.plugin.ToggleConfigUi();
                }
                AddTooltip("Toggle configuration.");
                break;
            case "Tracked buttons":
                if (this.DrawTrackedButtonsToggle())
                {
                    this.hideTrackedIcons = !this.hideTrackedIcons;
                    this.resetMainPanelScrollNextDraw = true;
                }
                AddTooltip(this.hideTrackedIcons ? "Show tracked achievement icons." : "Hide tracked achievement icons.");
                break;
        }
    }

    private bool DrawActiveIconButton(string id, FontAwesomeIcon icon, bool active)
    {
        if (active)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered]);
        }

        var clicked = ImGuiComponents.IconButton(id, icon);
        if (active)
        {
            ImGui.PopStyleColor();
        }

        return clicked;
    }

    private bool DrawTrackedButtonsToggle()
    {
        if (this.hideTrackedIcons)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered]);
        }

        var clicked = ImGuiComponents.IconButton("toggle-tracked-buttons", this.hideTrackedIcons ? FontAwesomeIcon.EyeSlash : FontAwesomeIcon.Eye);
        if (this.hideTrackedIcons)
        {
            ImGui.PopStyleColor();
        }

        return clicked;
    }

    private bool ShouldDrawColumn(string column)
        => column switch
        {
            "Lists" => this.templatesOpen,
            "Search Categories" => this.searchPanelOpen,
            "Search Results" => this.searchPanelOpen,
            "Tracked Achievements" => true,
            _ => false,
        };

    private void DrawMainPane()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var width = ImGui.GetContentRegionAvail().X;
        var orderedColumns = this.plugin.Configuration.MainColumnOrder.Where(this.ShouldDrawColumn).ToList();
        var visibleColumns = Math.Max(1, orderedColumns.Count);
        _ = width;
        _ = spacing;
        _ = visibleColumns;

        for (var i = 0; i < orderedColumns.Count; i++)
        {
            var column = orderedColumns[i];
            var columnWidth = this.GetConfiguredColumnWidth(column);
            ImGui.BeginChild($"##MainColumn-{column}", new Vector2(columnWidth, 0), true);
            if (this.resetMainPanelScrollNextDraw)
            {
                ImGui.SetScrollY(0);
            }

            this.DrawColumn(column);
            ImGui.EndChild();
            if (i < orderedColumns.Count - 1)
            {
                ImGui.SameLine();
            }
        }

        this.resetMainPanelScrollNextDraw = false;
    }

    private float GetConfiguredColumnWidth(string column)
    {
        var configured = this.plugin.Configuration.MainColumnWidths.TryGetValue(column, out var width) ? width : 260f;
        var minimum = column switch
        {
            "Search Categories" => 320f,
            "Search Results" => 420f,
            _ => 0f,
        };

        return Math.Max(minimum, configured);
    }

    private void DrawColumn(string column)
    {
        switch (column)
        {
            case "Lists":
                this.DrawTemplateColumn();
                break;
            case "Search Categories":
                this.DrawAchievementCategoryColumn();
                break;
            case "Search Results":
                this.DrawAchievementSearchColumn();
                break;
            case "Tracked Achievements":
                this.DrawTrackedColumn();
                break;
        }
    }

    private void DrawTemplateColumn()
    {
        this.EnsureSelectedPresetIsValid();
        ImGui.TextUnformatted("Lists");
        ImGui.Separator();
        this.DrawPresetButtons();

        ImGui.SetNextItemWidth(-1);
        if (ImGui.InputTextWithHint("##PresetName", "List name", ref this.presetNameInput, TrackedAchievementPresetStore.MaxPresetNameLength))
        {
            this.presetNameInput = TrackedAchievementPresetStore.SanitizeName(this.presetNameInput);
        }
        AddTooltip("List name used by Save and Rename.");

        ImGui.Separator();
        this.DrawPresetList();
        this.DrawPresetContextPopups();
    }

    private void DrawAchievementCategoryColumn()
    {
        var searchableAchievements = this.GetSearchableAchievements().ToList();
        ImGui.TextUnformatted($"Achievement categories ({searchableAchievements.Count})");
        ImGui.TextDisabled("Counts include searchable, manually viewable achievements only.");
        ImGui.TextDisabled("Click to select one category. Ctrl-click to add/remove multiple categories or subcategories.");
        ImGui.Separator();
        var categoryEntries = searchableAchievements
            .Select(info => (Info: info, Parts: SplitCategoryPath(info.CategoryName), Sort: this.GetGameSortKey(info)))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Parts.Category))
            .GroupBy(entry => entry.Parts.Category)
            .OrderBy(group => group.Min(entry => entry.Sort.KindOrder))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var categoryGroup in categoryEntries)
        {
            var selectedCategory = this.selectedCategoryFilters.Contains(categoryGroup.Key);
            var categoryLabel = $"{categoryGroup.Key} ({categoryGroup.Count()})";
            if (ImGui.Selectable(categoryLabel, selectedCategory))
            {
                this.ToggleCategoryFilter(categoryGroup.Key);
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
                var subcategoryKey = BuildSubcategoryFilterKey(categoryGroup.Key, subcategoryGroup.Key);
                var selected = this.selectedSubcategoryFilters.Contains(subcategoryKey);
                var subcategoryLabel = $"{subcategoryGroup.Key} ({subcategoryGroup.Count()})";
                ImGui.PushID(subcategoryKey);
                if (ImGui.Selectable(subcategoryLabel, selected))
                {
                    this.ToggleSubcategoryFilter(categoryGroup.Key, subcategoryGroup.Key);
                }

                ImGui.PopID();
            }

            ImGui.Unindent(12);
        }
    }

    private void DrawAchievementSearchColumn()
    {
        ImGui.TextUnformatted("Achievement search");
        ImGui.SetNextItemWidth(-70);
        _ = ImGui.InputTextWithHint("##MainAchievementSearch", "Search name or category", ref this.achievementSearchQuery, 128);

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            this.achievementSearchQuery = string.Empty;
        }
        AddTooltip("Clear search text.");

        ImGui.TextUnformatted("Category:");
        ImGui.SameLine();
        if (ImGui.RadioButton("All", this.categoryFilterAll))
        {
            this.categoryFilterAll = true;
        }
        AddTooltip("Ignore selected categories.");
        ImGui.SameLine();
        if (ImGui.RadioButton("Selected", !this.categoryFilterAll))
        {
            this.categoryFilterAll = false;
        }
        AddTooltip("Filter by selected categories/subcategories from the Achievement categories column. Ctrl-click there to select multiple.");

        ImGui.TextUnformatted("Completion:");
        ImGui.SameLine();
        if (ImGui.RadioButton("All", this.plugin.Configuration.SearchCompletionFilter == "All"))
        {
            this.plugin.Configuration.SearchCompletionFilter = "All";
            this.plugin.Configuration.HideCompletedInSearch = false;
            this.plugin.SaveConfiguration();
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Completed", this.plugin.Configuration.SearchCompletionFilter == "Completed"))
        {
            this.plugin.Configuration.SearchCompletionFilter = "Completed";
            this.plugin.Configuration.HideCompletedInSearch = false;
            this.plugin.SaveConfiguration();
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Incomplete", this.plugin.Configuration.SearchCompletionFilter == "Incomplete"))
        {
            this.plugin.Configuration.SearchCompletionFilter = "Incomplete";
            this.plugin.Configuration.HideCompletedInSearch = true;
            this.plugin.SaveConfiguration();
        }

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

        if (!SearchCompletionFilterPolicy.CanEvaluate(
                this.plugin.Configuration.SearchCompletionFilter,
                this.plugin.AchievementProgressService.AreCompletionStatesLoaded,
                this.plugin.AchievementProgressUpdater.IsUpdateInProgress))
        {
            ImGui.Separator();
            ImGui.TextDisabled("Completed/Incomplete search needs the in-game achievement completion list loaded first.");
            ImGui.TextDisabled("Search text and categories remain Lumina-only; no native Achievement window is opened automatically.");
            return;
        }

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        var searchableResults = this.GetSearchableAchievements().ToList();
        var categoryFilteredResults = searchableResults
            .Where(this.MatchesSelectedCategory)
            .ToList();
        var queryFilteredResults = categoryFilteredResults
            .Where(this.MatchesSearchQuery)
            .ToList();
        var matchingResults = this.SortSearchResults(queryFilteredResults
                .Where(this.MatchesCompletionFilter))
            .ToList();

        ImGui.Separator();
        ImGui.TextDisabled($"Results: {matchingResults.Count} shown / {queryFilteredResults.Count} matching text / {categoryFilteredResults.Count} in category filter / {searchableResults.Count} searchable");

        if (matchingResults.Count == 0)
        {
            ImGui.TextDisabled("No matching manually viewable achievements found.");
            return;
        }

        foreach (var result in matchingResults)
        {
            this.DrawSearchAchievementResult(result, trackedIds);
        }
    }

    private IEnumerable<AchievementInfo> GetSearchableAchievements()
        => this.plugin.AchievementCatalog.Search(string.Empty, 5000)
            .Where(result => !string.Equals(SplitCategoryPath(result.CategoryName).Category, "Legacy", StringComparison.OrdinalIgnoreCase));

    private bool MatchesSearchQuery(AchievementInfo info)
        => string.IsNullOrWhiteSpace(this.achievementSearchQuery)
            || info.Name.Contains(this.achievementSearchQuery.Trim(), StringComparison.CurrentCultureIgnoreCase)
            || info.CategoryName.Contains(this.achievementSearchQuery.Trim(), StringComparison.CurrentCultureIgnoreCase);

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
        var canAdd = trackedIds.Count < TrackedAchievementStore.MaxTrackedAchievements;
        using (ImRaiiShim.Disabled(!alreadyTracked && !canAdd))
        {
            if (ImGuiComponents.IconButton("search-add-remove", alreadyTracked ? FontAwesomeIcon.Times : FontAwesomeIcon.Plus))
            {
                if (alreadyTracked)
                {
                    _ = this.RemoveTrackedAchievement(result.Id);
                }
                else
                {
                    this.AddTrackedAchievement(result.Id);
                }
            }
        }
        AddTooltip(alreadyTracked ? "Remove from tracked list." : canAdd ? "Add to tracked list." : "Tracked list is full.");

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
            ImGui.TextWrapped("No achievements tracked. Use the search column to add one, or show Lists with the disk icon.");
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
        if (this.ShouldShowTrackedIcon("Auto update"))
        {
            var included = this.plugin.Configuration.AutoUpdateAchievementIds.Contains(achievementId);
            if (ImGui.Checkbox("Auto", ref included))
            {
                if (included && !this.plugin.Configuration.AutoUpdateAchievementIds.Contains(achievementId))
                {
                    this.plugin.Configuration.AutoUpdateAchievementIds.Add(achievementId);
                }
                else if (!included)
                {
                    this.plugin.Configuration.AutoUpdateAchievementIds.RemoveAll(id => id == achievementId);
                }

                this.plugin.SaveConfiguration();
                this.plugin.ResetAutoUpdateCountdownIfActive();
            }
            AddTooltip("Include in timed auto updates.");
            ImGui.SameLine();
        }

        if (this.ShouldShowTrackedIcon("Remove"))
        {
            if (ImGuiComponents.IconButton("tracked-remove-main", FontAwesomeIcon.Times))
            {
                _ = this.RemoveTrackedAchievement(achievementId);
                ImGui.PopID();
                return;
            }
            AddTooltip("Remove from tracked.");
            ImGui.SameLine();
        }

        if (this.ShouldShowTrackedIcon("Refresh"))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
            {
                this.plugin.EnqueueUpdateOne(achievementId, "manual-row-update");
            }
            AddTooltip("Update this achievement.");
            ImGui.SameLine();
        }

        if (this.ShouldShowTrackedIcon("Open"))
        {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
            {
                this.plugin.OpenNativeAchievementForInspection(achievementId);
            }
            AddTooltip("Open in Achievements.");
            ImGui.SameLine();
        }

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
        AddTooltip("Save current tracked list as this list name.");

        ImGui.SameLine(0, 4);
        if (ImGuiComponents.IconButton("template-load", FontAwesomeIcon.FolderOpen))
        {
            this.LoadSelectedPreset();
        }
        AddTooltip("Load selected list.");

        ImGui.SameLine(0, 4);
        if (ImGuiComponents.IconButton("template-rename", FontAwesomeIcon.Edit))
        {
            this.RenameSelectedPreset();
        }
        AddTooltip("Rename selected list to the typed name.");

        ImGui.SameLine(0, 4);
        if (ImGuiComponents.IconButton("template-copy", FontAwesomeIcon.Copy))
        {
            this.CopySelectedPreset();
        }
        AddTooltip("Make a copy of the selected list.");

        ImGui.SameLine(0, 4);
        using (ImRaiiShim.Disabled(!ImGui.GetIO().KeyShift))
        {
            if (ImGuiComponents.IconButton("template-delete", FontAwesomeIcon.Trash))
            {
                this.DeleteSelectedPreset();
            }
        }
        AddTooltip("Hold Shift to delete selected list.");
    }

    private void DrawPresetList()
    {
        if (this.plugin.Configuration.TrackedAchievementPresets.Count == 0)
        {
            ImGui.TextDisabled("No saved lists yet.");
            ImGui.TextWrapped("Type a name, then press save to capture the current tracked achievements.");
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
            ImGui.SetTooltip("Right-click for list options. Double-click to load.");
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

    private bool MatchesCompletionFilter(AchievementInfo info)
        => SearchCompletionFilterPolicy.Matches(this.plugin.Configuration.SearchCompletionFilter, this.IsComplete(info.Id));

    private bool ShouldShowTrackedIcon(string iconName)
        => !this.hideTrackedIcons || !this.plugin.Configuration.HiddenTrackedAchievementIcons.Contains(iconName);

    private bool MatchesSelectedCategory(AchievementInfo info)
    {
        if (this.categoryFilterAll)
        {
            return true;
        }

        var parts = SplitCategoryPath(info.CategoryName);
        if (string.IsNullOrWhiteSpace(parts.Category))
        {
            return false;
        }

        if (this.selectedCategoryFilters.Contains(parts.Category))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(parts.Subcategory)
            && this.selectedSubcategoryFilters.Contains(BuildSubcategoryFilterKey(parts.Category, parts.Subcategory));
    }

    private void ToggleCategoryFilter(string category)
    {
        var ctrl = ImGui.GetIO().KeyCtrl;
        if (!ctrl)
        {
            this.selectedCategoryFilters.Clear();
            this.selectedSubcategoryFilters.Clear();
            this.selectedCategoryFilters.Add(category);
            return;
        }

        if (!this.selectedCategoryFilters.Add(category))
        {
            this.selectedCategoryFilters.Remove(category);
        }
    }

    private void ToggleSubcategoryFilter(string category, string subcategory)
    {
        var ctrl = ImGui.GetIO().KeyCtrl;
        var key = BuildSubcategoryFilterKey(category, subcategory);
        if (!ctrl)
        {
            this.selectedCategoryFilters.Clear();
            this.selectedSubcategoryFilters.Clear();
            this.selectedSubcategoryFilters.Add(key);
            return;
        }

        if (!this.selectedSubcategoryFilters.Add(key))
        {
            this.selectedSubcategoryFilters.Remove(key);
        }
    }

    private static string BuildSubcategoryFilterKey(string category, string subcategory)
        => $"{category}>{subcategory}";

    private bool IsComplete(uint achievementId)
        => this.plugin.IsAchievementCompleteForSearch(achievementId);

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
        var statusText = this.plugin.AchievementProgressUpdater.StatusText;
        if (!string.IsNullOrWhiteSpace(statusText))
        {
            ImGui.TextDisabled(statusText);
        }

        var pending = this.plugin.AchievementProgressUpdater.PendingCount;
        var nextDue = this.plugin.AchievementProgressUpdater.NextDueAt;
        if (pending > 0 && nextDue.HasValue)
        {
            var seconds = Math.Max(0, (nextDue.Value - DateTimeOffset.UtcNow).TotalSeconds);
            ImGui.TextDisabled($"Progress queue: {pending} pending, next native action in {seconds:0}s");
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
