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
    private string appliedAchievementSearchQuery = string.Empty;
    private DateTime searchQueryChangedAt = DateTime.MinValue;
    private SearchResultsCache? cachedSearchResults;
    private bool searchResultsDirty = true;
    private IReadOnlyList<AchievementInfo>? cachedSearchableAchievements;
    private IReadOnlyList<SearchCategoryGroup>? cachedSearchCategoryGroups;
    private bool categoryFilterAll = true;
    private readonly HashSet<string> selectedCategoryFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> selectedSubcategoryFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> collapsedSearchCategories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, (byte KindOrder, byte CategoryOrder, ushort AchievementOrder, uint RowId)> gameSortKeyCache = new();

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

    private sealed record SearchResultsCache(
        IReadOnlyList<AchievementInfo> Results,
        int SearchableCount,
        int CategoryFilteredCount,
        int QueryFilteredCount,
        int CompletionFilteredCount,
        DateTime BuiltAtUtc);

    private sealed record SearchCategoryEntry(AchievementInfo Info, string Category, string Subcategory, byte KindOrder);

    private sealed record SearchCategoryGroup(string Category, IReadOnlyList<SearchCategoryEntry> Entries);

    public override void Draw()
    {
        this.DrawToolbar();
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
                AddTooltip("Crash-guarded: queues one eligible tracked achievement per click/cycle through the native Achievement UI. No direct progress request.");
                break;
            case "Auto update":
                var autoUpdateEnabled = this.plugin.Configuration.ExperimentalAutoUpdateEnabled;
                if (ImGui.Checkbox("Auto update", ref autoUpdateEnabled))
                {
                    this.plugin.Configuration.ExperimentalAutoUpdateEnabled = autoUpdateEnabled;
                    this.plugin.SaveConfiguration();
                    this.plugin.ResetAutoUpdateCountdownIfActive();
                }
                AddTooltip("Crash-guarded: refreshes one eligible tracked achievement per timed cycle through the native Achievement UI. No direct progress request.");
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
        var presentation = TrackedToolbarIconPresentation.ForHiddenState(this.hideTrackedIcons);
        var hasCustomColor = string.Equals(presentation.ColorName, "Red", StringComparison.Ordinal);
        if (hasCustomColor)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonHovered]);
        }

        var clicked = ImGuiComponents.IconButton("toggle-tracked-buttons", FontAwesomeIcon.Eye);
        if (hasCustomColor)
        {
            ImGui.PopStyleColor();
        }

        AddTooltip(presentation.Tooltip);
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
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var orderedColumns = this.plugin.Configuration.MainColumnOrder.Where(this.ShouldDrawColumn).ToList();
        var widths = this.GetFittedColumnWidths(orderedColumns, availableWidth, spacing);

        for (var i = 0; i < orderedColumns.Count; i++)
        {
            var column = orderedColumns[i];
            ImGui.BeginChild($"##MainColumn-{column}", new Vector2(widths[i], 0), true, ImGuiWindowFlags.NoScrollbar);
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

    private List<float> GetFittedColumnWidths(IReadOnlyList<string> orderedColumns, float availableWidth, float spacing)
    {
        var widths = orderedColumns.Select(this.GetConfiguredColumnWidth).ToList();
        if (widths.Count == 0)
        {
            return widths;
        }

        var spacingTotal = spacing * Math.Max(0, widths.Count - 1);
        var targetWidth = Math.Max(120f * widths.Count, availableWidth - spacingTotal);
        var totalWidth = widths.Sum();
        if (totalWidth <= targetWidth)
        {
            return widths;
        }

        var scale = targetWidth / totalWidth;
        for (var i = 0; i < widths.Count; i++)
        {
            widths[i] = Math.Max(this.GetMinimumColumnWidth(orderedColumns[i]), widths[i] * scale);
        }

        return widths;
    }

    private float GetConfiguredColumnWidth(string column)
    {
        var configured = this.plugin.Configuration.MainColumnWidths.TryGetValue(column, out var width) ? width : 260f;
        return Math.Max(this.GetMinimumColumnWidth(column), configured);
    }

    private float GetMinimumColumnWidth(string column)
    {
        return column switch
        {
            "Lists" => MainPanelColumnWidthDefaults.Lists,
            "Search Categories" => MainPanelColumnWidthDefaults.SearchCategories,
            "Search Results" => MainPanelColumnWidthDefaults.SearchResults,
            "Tracked Achievements" => MainPanelColumnWidthDefaults.TrackedAchievements,
            _ => 180f,
        };
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


    private void DrawDisabledWrapped(string text)
    {
        var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        ImGui.PushStyleColor(ImGuiCol.Text, disabledColor);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private void DrawAchievementCategoryColumn()
    {
        var searchableAchievements = this.GetSearchableAchievements();
        var completionFilteredCategoryCount = searchableAchievements.Count(this.MatchesCompletionCountFilter);
        ImGui.TextUnformatted($"Achievement categories ({completionFilteredCategoryCount})");
        this.DrawDisabledWrapped("Ctrl-click to select multiple categories or subcategories.");
        ImGui.Separator();

        foreach (var categoryGroup in this.GetSearchCategoryGroups())
        {
            var categoryKey = categoryGroup.Category;
            var selectedCategory = this.selectedCategoryFilters.Contains(categoryKey);
            var categoryCount = categoryGroup.Entries.Count(entry => this.MatchesCompletionCountFilter(entry.Info));
            var categoryLabel = $"{categoryKey} ({categoryCount})";
            var collapsed = this.collapsedSearchCategories.Contains(categoryKey);

            ImGui.PushID($"category-{categoryKey}");
            if (ImGui.SmallButton(collapsed ? "▶" : "▼"))
            {
                if (!this.collapsedSearchCategories.Add(categoryKey))
                {
                    this.collapsedSearchCategories.Remove(categoryKey);
                }
            }
            AddTooltip(collapsed ? "Expand category." : "Collapse category.");
            ImGui.SameLine();
            if (ImGui.Selectable(categoryLabel, selectedCategory))
            {
                this.ToggleCategoryFilter(categoryKey);
            }
            ImGui.PopID();

            var subcategories = categoryGroup.Entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Subcategory))
                .GroupBy(entry => entry.Subcategory)
                .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (collapsed || subcategories.Count == 0)
            {
                continue;
            }

            ImGui.Indent(24);
            foreach (var subcategoryGroup in subcategories)
            {
                var subcategoryKey = AchievementCategoryPath.BuildSubcategoryFilterKey(categoryKey, subcategoryGroup.Key);
                var selected = this.selectedSubcategoryFilters.Contains(subcategoryKey);
                var subcategoryCount = subcategoryGroup.Count(entry => this.MatchesCompletionCountFilter(entry.Info));
                var subcategoryLabel = $"{subcategoryGroup.Key} ({subcategoryCount})";
                ImGui.PushID(subcategoryKey);
                if (ImGui.Selectable(subcategoryLabel, selected))
                {
                    this.ToggleSubcategoryFilter(categoryKey, subcategoryGroup.Key);
                }

                ImGui.PopID();
            }

            ImGui.Unindent(24);
        }
    }

    private void DrawAchievementSearchColumn()
    {
        ImGui.TextUnformatted("Achievement search");
        ImGui.SetNextItemWidth(Math.Max(120f, ImGui.GetContentRegionAvail().X - 58f));
        var previousSearchQuery = this.achievementSearchQuery;
        if (ImGui.InputTextWithHint("##MainAchievementSearch", "Search name or category", ref this.achievementSearchQuery, 128)
            && !string.Equals(previousSearchQuery, this.achievementSearchQuery, StringComparison.Ordinal))
        {
            this.searchQueryChangedAt = DateTime.UtcNow;
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            this.achievementSearchQuery = string.Empty;
            this.appliedAchievementSearchQuery = string.Empty;
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        AddTooltip("Clear search text.");

        ImGui.TextUnformatted("Category:");
        ImGui.SameLine();
        if (ImGui.RadioButton("All##CategoryFilter", this.categoryFilterAll))
        {
            this.categoryFilterAll = true;
            this.selectedCategoryFilters.Clear();
            this.selectedSubcategoryFilters.Clear();
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        AddTooltip("Ignore selected categories.");
        ImGui.SameLine();
        if (ImGui.RadioButton("Selected##CategoryFilter", !this.categoryFilterAll))
        {
            this.categoryFilterAll = false;
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        AddTooltip("Filter by selected categories/subcategories from the Achievement categories column. Ctrl-click there to select multiple.");

        ImGui.TextUnformatted("Completion:");
        ImGui.SameLine();
        if (ImGui.RadioButton("All##CompletionFilter", this.plugin.Configuration.SearchCompletionFilter == "All"))
        {
            this.plugin.Configuration.SearchCompletionFilter = "All";
            this.plugin.Configuration.HideCompletedInSearch = false;
            this.plugin.SaveConfiguration();
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Completed##CompletionFilter", this.plugin.Configuration.SearchCompletionFilter == "Completed"))
        {
            this.plugin.Configuration.SearchCompletionFilter = "Completed";
            this.plugin.Configuration.HideCompletedInSearch = false;
            this.plugin.SaveConfiguration();
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        ImGui.SameLine();
        if (ImGui.RadioButton("Incomplete##CompletionFilter", this.plugin.Configuration.SearchCompletionFilter == "Incomplete"))
        {
            this.plugin.Configuration.SearchCompletionFilter = "Incomplete";
            this.plugin.Configuration.HideCompletedInSearch = true;
            this.plugin.SaveConfiguration();
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }

        ImGui.TextUnformatted("Sort:");
        ImGui.SameLine();
        var gameSort = this.searchSortMode == SearchSortMode.GameOrder;
        if (ImGui.RadioButton("Game", gameSort))
        {
            this.searchSortMode = SearchSortMode.GameOrder;
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        AddTooltip("Use the in-game achievement category/subcategory/order.");
        ImGui.SameLine();
        var alphabeticalSort = this.searchSortMode == SearchSortMode.Alphabetical;
        if (ImGui.RadioButton("A-Z", alphabeticalSort))
        {
            this.searchSortMode = SearchSortMode.Alphabetical;
            this.MarkSearchResultsDirty(resetVisibleResults: true);
        }
        AddTooltip("Sort achievement names alphabetically.");

        this.ApplyPendingSearchQueryIfReady();

        if (!SearchCompletionFilterPolicy.CanEvaluate(
                this.plugin.Configuration.SearchCompletionFilter,
                this.plugin.AchievementProgressService.AreCompletionStatesLoaded,
                this.plugin.AchievementProgressUpdater.IsUpdateInProgress))
        {
            ImGui.Separator();
            this.DrawDisabledWrapped("Completed/Incomplete search needs the in-game achievement completion list loaded first.");
            this.DrawDisabledWrapped("Search text and categories remain Lumina-only; no native Achievement window is opened automatically.");
            return;
        }

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        var searchResults = this.GetCachedSearchResults();
        var matchingResults = searchResults.Results;

        ImGui.Separator();
        var shownCount = matchingResults.Count;
        var completionFilter = this.plugin.Configuration.SearchCompletionFilter;
        var countLabel = string.Equals(completionFilter, SearchCompletionFilterPolicy.All, StringComparison.Ordinal)
            ? $"Results: {shownCount}"
            : $"Results: {shownCount} {completionFilter.ToLowerInvariant()} / {searchResults.QueryFilteredCount} matching search/category";
        this.DrawDisabledWrapped(countLabel);

        if (matchingResults.Count == 0)
        {
            this.DrawDisabledWrapped("No matching manually viewable achievements found.");
            return;
        }

        foreach (var result in matchingResults)
        {
            this.DrawSearchAchievementResult(result, trackedIds);
        }

    }


    private void ApplyPendingSearchQueryIfReady()
    {
        if (string.Equals(this.appliedAchievementSearchQuery, this.achievementSearchQuery, StringComparison.Ordinal))
        {
            return;
        }

        if ((DateTime.UtcNow - this.searchQueryChangedAt).TotalMilliseconds < 350)
        {
            return;
        }

        this.appliedAchievementSearchQuery = this.achievementSearchQuery;
        this.MarkSearchResultsDirty(resetVisibleResults: true);
    }

    private SearchResultsCache GetCachedSearchResults()
    {
        if (this.cachedSearchResults is not null
            && !this.searchResultsDirty
            && (this.plugin.Configuration.SearchCompletionFilter == "All"
                || (DateTime.UtcNow - this.cachedSearchResults.BuiltAtUtc).TotalSeconds < 2))
        {
            return this.cachedSearchResults;
        }

        var searchableResults = this.GetSearchableAchievements();
        var categoryFilteredResults = searchableResults.Where(this.MatchesSelectedCategory).ToList();
        var queryFilteredResults = categoryFilteredResults.Where(this.MatchesSearchQuery).ToList();
        var matchingResults = this.SortSearchResults(queryFilteredResults.Where(this.MatchesCompletionFilter)).ToList();
        this.cachedSearchResults = new SearchResultsCache(
            matchingResults,
            searchableResults.Count,
            categoryFilteredResults.Count,
            queryFilteredResults.Count,
            matchingResults.Count,
            DateTime.UtcNow);
        this.searchResultsDirty = false;
        return this.cachedSearchResults;
    }

    private void MarkSearchResultsDirty(bool resetVisibleResults)
    {
        this.searchResultsDirty = true;
        _ = resetVisibleResults;
    }

    private IReadOnlyList<AchievementInfo> GetSearchableAchievements()
    {
        if (this.cachedSearchableAchievements is not null)
        {
            return this.cachedSearchableAchievements;
        }

        this.cachedSearchableAchievements = this.plugin.AchievementCatalog.GetManuallyViewableAchievements()
            .Where(result => !string.Equals(AchievementCategoryPath.Parse(result.CategoryName).Category, "Legacy", StringComparison.OrdinalIgnoreCase))
            .ToList();
        return this.cachedSearchableAchievements;
    }

    private IReadOnlyList<SearchCategoryGroup> GetSearchCategoryGroups()
    {
        if (this.cachedSearchCategoryGroups is not null)
        {
            return this.cachedSearchCategoryGroups;
        }

        this.cachedSearchCategoryGroups = this.GetSearchableAchievements()
            .Select(info =>
            {
                var parts = AchievementCategoryPath.Parse(info.CategoryName);
                var sort = this.GetGameSortKey(info);
                return new SearchCategoryEntry(info, parts.Category, parts.Subcategory, sort.KindOrder);
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Category))
            .GroupBy(entry => entry.Category)
            .OrderBy(group => group.Min(entry => entry.KindOrder))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new SearchCategoryGroup(group.Key, group.ToList()))
            .ToList();
        return this.cachedSearchCategoryGroups;
    }

    private bool MatchesSearchQuery(AchievementInfo info)
    {
        var query = this.appliedAchievementSearchQuery.Trim();
        return string.IsNullOrWhiteSpace(query)
            || info.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || info.CategoryName.Contains(query, StringComparison.CurrentCultureIgnoreCase);
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
        ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
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
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + Math.Max(80f, ImGui.GetContentRegionAvail().X));
        ImGui.TextWrapped(result.Name);
        if (this.IsComplete(result.Id))
        {
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.2f, 1f), "✓");
        }

        if (!string.IsNullOrWhiteSpace(result.Description))
        {
            this.DrawDisabledWrapped(result.Description);
        }
        ImGui.PopTextWrapPos();
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private void DrawTrackedColumn()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        var staleTrackedCount = trackedIds.Count(this.IsTrackedAchievementStale);
        var indicatorState = TrackedUpdateIndicatorPolicy.GetState(
            this.plugin.AchievementProgressUpdater.PendingCount,
            this.plugin.AchievementProgressUpdater.IsUpdateInProgress,
            staleTrackedCount);

        ImGui.TextUnformatted("Tracked achievements");
        this.DrawTrackedUpdateIndicator(indicatorState, staleTrackedCount);
        ImGui.Separator();
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

    private void DrawTrackedUpdateIndicator(TrackedUpdateIndicatorState state, int staleTrackedCount)
    {
        var (icon, color, tooltip) = state switch
        {
            TrackedUpdateIndicatorState.Working => (FontAwesomeIcon.SyncAlt, new Vector4(0.4f, 0.7f, 1f, 1f), "Updating tracked achievements."),
            TrackedUpdateIndicatorState.NeedsUpdate => (FontAwesomeIcon.Times, new Vector4(1f, 0.8f, 0.2f, 1f), $"{staleTrackedCount} tracked achievement{(staleTrackedCount == 1 ? string.Empty : "s")} need{(staleTrackedCount == 1 ? "s" : string.Empty)} an update."),
            _ => (FontAwesomeIcon.Check, new Vector4(0.2f, 0.9f, 0.2f, 1f), "All tracked achievements are updated or complete."),
        };
        var glyph = IconString(icon);

        ImGui.PushFont(UiBuilder.IconFontFixedWidth);
        var currentX = ImGui.GetCursorPosX();
        var width = ImGui.CalcTextSize(glyph).X;
        var rightX = ImGui.GetWindowContentRegionMax().X - width;
        ImGui.SameLine();
        ImGui.SetCursorPosX(Math.Max(currentX, rightX));
        ImGui.TextColored(color, glyph);
        ImGui.PopFont();
        AddTooltip(tooltip);
    }

    private static string IconString(FontAwesomeIcon icon)
        => char.ConvertFromUtf32((int)icon);

    private void DrawTrackedAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        var progressText = this.GetTrackedProgressText(achievementId);
        var removeRequested = false;

        ImGui.PushID((int)achievementId);
        ImGui.BeginGroup();
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
                removeRequested = true;
            }
            AddTooltip("Remove from tracked.");
            ImGui.SameLine();
        }

        var nativeSafe = this.plugin.AchievementCatalog.CanOpenInNativeAchievementUi(achievementId, out var nativeUnsafeReason);

        if (this.ShouldShowTrackedIcon("Refresh"))
        {
            using (ImRaiiShim.Disabled(!nativeSafe))
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
                {
                    this.plugin.EnqueueUpdateOne(achievementId, "manual-row-update");
                }
            }

            AddTooltip(nativeSafe ? "Update this achievement." : nativeUnsafeReason);
            ImGui.SameLine();
        }

        if (this.ShouldShowTrackedIcon("Open"))
        {
            using (ImRaiiShim.Disabled(!nativeSafe))
            {
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    this.plugin.OpenNativeAchievementForInspection(achievementId);
                }
            }

            AddTooltip(nativeSafe ? "Open in Achievements." : nativeUnsafeReason);
            ImGui.SameLine();
        }

        ImGui.TextWrapped(info.Name);
        ImGui.TextDisabled(progressText);
        ImGui.EndGroup();

        if (ImGui.BeginPopupContextItem("tracked-achievement-context"))
        {
            ImGui.TextUnformatted(info.Name);
            ImGui.Separator();
            if (ImGui.MenuItem("Remove from tracked list"))
            {
                removeRequested = true;
            }

            ImGui.EndPopup();
        }

        if (removeRequested)
        {
            _ = this.RemoveTrackedAchievement(achievementId);
        }

        ImGui.PopID();
    }

    private string GetTrackedProgressText(uint achievementId)
    {
        if (!this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            return "not updated yet";
        }

        var hasObservedProgress = this.plugin.ClientAchievementProgressSource.TryGetObservation(achievementId, out _);
        var isComplete = this.plugin.AchievementProgressService.IsComplete(row);
        var hasCosmicProgressOverride = this.plugin.CosmicClassProgressProvider.Handles(row);
        if (!TrackedProgressDisplayPolicy.ShouldEvaluateProgress(hasObservedProgress, isComplete, hasCosmicProgressOverride))
        {
            return "not updated yet";
        }

        return this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText();
    }

    private bool IsTrackedAchievementStale(uint achievementId)
        => string.Equals(this.GetTrackedProgressText(achievementId), "not updated yet", StringComparison.Ordinal);

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
    {
        if (string.Equals(this.plugin.Configuration.SearchCompletionFilter, "All", StringComparison.Ordinal))
        {
            return true;
        }

        return SearchCompletionFilterPolicy.Matches(this.plugin.Configuration.SearchCompletionFilter, this.IsComplete(info.Id));
    }

    private bool MatchesCompletionCountFilter(AchievementInfo info)
        => SearchCompletionFilterPolicy.MatchesForCount(
            this.plugin.Configuration.SearchCompletionFilter,
            this.plugin.AchievementProgressService.AreCompletionStatesLoaded,
            this.IsComplete(info.Id));

    private bool ShouldShowTrackedIcon(string iconName)
        => !this.hideTrackedIcons || !this.plugin.Configuration.HiddenTrackedAchievementIcons.Contains(iconName);

    private bool MatchesSelectedCategory(AchievementInfo info)
    {
        if (this.categoryFilterAll)
        {
            return true;
        }

        var parts = AchievementCategoryPath.Parse(info.CategoryName);
        if (string.IsNullOrWhiteSpace(parts.Category))
        {
            return false;
        }

        if (this.selectedCategoryFilters.Contains(parts.Category))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(parts.Subcategory)
            && this.selectedSubcategoryFilters.Contains(AchievementCategoryPath.BuildSubcategoryFilterKey(parts.Category, parts.Subcategory));
    }

    private void ToggleCategoryFilter(string category)
    {
        var ctrl = ImGui.GetIO().KeyCtrl;
        this.categoryFilterAll = false;
        if (!ctrl)
        {
            this.selectedCategoryFilters.Clear();
            this.selectedSubcategoryFilters.Clear();
            this.selectedCategoryFilters.Add(category);
            this.MarkSearchResultsDirty(resetVisibleResults: true);
            return;
        }

        if (!this.selectedCategoryFilters.Add(category))
        {
            this.selectedCategoryFilters.Remove(category);
        }

        this.MarkSearchResultsDirty(resetVisibleResults: true);
    }

    private void ToggleSubcategoryFilter(string category, string subcategory)
    {
        var ctrl = ImGui.GetIO().KeyCtrl;
        this.categoryFilterAll = false;
        var key = AchievementCategoryPath.BuildSubcategoryFilterKey(category, subcategory);
        if (!ctrl)
        {
            this.selectedCategoryFilters.Clear();
            this.selectedSubcategoryFilters.Clear();
            this.selectedSubcategoryFilters.Add(key);
            this.MarkSearchResultsDirty(resetVisibleResults: true);
            return;
        }

        if (!this.selectedSubcategoryFilters.Add(key))
        {
            this.selectedSubcategoryFilters.Remove(key);
        }

        this.MarkSearchResultsDirty(resetVisibleResults: true);
    }

    private bool IsComplete(uint achievementId)
        => this.plugin.IsAchievementCompleteForSearch(achievementId);

    private (byte KindOrder, byte CategoryOrder, ushort AchievementOrder, uint RowId) GetGameSortKey(AchievementInfo info)
    {
        if (this.gameSortKeyCache.TryGetValue(info.Id, out var cached))
        {
            return cached;
        }

        if (!this.plugin.AchievementCatalog.TryGetRow(info.Id, out var row))
        {
            cached = (byte.MaxValue, byte.MaxValue, ushort.MaxValue, info.Id);
            this.gameSortKeyCache[info.Id] = cached;
            return cached;
        }

        var categoryOrder = row.AchievementCategory.IsValid ? row.AchievementCategory.Value.Order : byte.MaxValue;
        var kindOrder = row.AchievementCategory.IsValid && row.AchievementCategory.Value.AchievementKind.IsValid
            ? row.AchievementCategory.Value.AchievementKind.Value.Order
            : byte.MaxValue;
        cached = (kindOrder, categoryOrder, row.Order, row.RowId);
        this.gameSortKeyCache[info.Id] = cached;
        return cached;
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
