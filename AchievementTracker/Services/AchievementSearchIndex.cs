using AchievementTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public enum AchievementSearchSortMode
{
    GameOrder,
    Alphabetical,
}

public readonly record struct AchievementSearchSortKey(byte KindOrder, byte CategoryOrder, ushort AchievementOrder, uint RowId)
{
    public static AchievementSearchSortKey Fallback(uint rowId)
        => new(byte.MaxValue, byte.MaxValue, ushort.MaxValue, rowId);
}

public sealed record AchievementSearchQueryState(
    string Query,
    bool CategoryFilterAll,
    IReadOnlyCollection<string> SelectedCategoryFilters,
    IReadOnlyCollection<string> SelectedSubcategoryFilters,
    string CompletionFilter,
    AchievementSearchSortMode SortMode);

public sealed record AchievementSearchResults(
    IReadOnlyList<AchievementInfo> Results,
    int SearchableCount,
    int CategoryFilteredCount,
    int QueryFilteredCount,
    int CompletionFilteredCount);

public sealed record AchievementSearchCategoryEntry(
    AchievementInfo Info,
    string Category,
    string Subcategory,
    AchievementSearchSortKey SortKey,
    bool MatchesCompletionCountFilter)
{
    public byte KindOrder => this.SortKey.KindOrder;
}

public sealed record AchievementSearchCategoryGroup(string Category, IReadOnlyList<AchievementSearchCategoryEntry> Entries)
{
    public int DisplayCount => this.Entries.Count(entry => entry.MatchesCompletionCountFilter);

    public int CountEntriesForSubcategory(string subcategory)
        => this.Entries.Count(entry =>
            entry.MatchesCompletionCountFilter
            && string.Equals(entry.Subcategory, subcategory, StringComparison.OrdinalIgnoreCase));

    public bool ShouldShow(bool hideZeroCountEntries)
        => !hideZeroCountEntries || this.DisplayCount > 0;

    public bool ShouldShowSubcategory(string subcategory, bool hideZeroCountEntries)
        => !hideZeroCountEntries || this.CountEntriesForSubcategory(subcategory) > 0;
}

public static class AchievementSearchIndex
{
    public static IReadOnlyList<AchievementInfo> GetSearchableAchievements(IEnumerable<AchievementInfo> achievements)
        // Keep Lumina/manual-viewability in AchievementCatalog; this index only applies UI search
        // semantics that are safe to share between windows and tests.
        => achievements
            .Where(info => !string.Equals(AchievementCategoryPath.Parse(info.CategoryName).Category, "Legacy", StringComparison.OrdinalIgnoreCase))
            .ToList();

    public static AchievementSearchResults BuildResults(
        IEnumerable<AchievementInfo> achievements,
        AchievementSearchQueryState state,
        Func<uint, bool> isComplete,
        Func<AchievementInfo, AchievementSearchSortKey> getSortKey)
    {
        var searchableResults = GetSearchableAchievements(achievements);
        var selectedCategoryFilters = new HashSet<string>(state.SelectedCategoryFilters, StringComparer.OrdinalIgnoreCase);
        var selectedSubcategoryFilters = new HashSet<string>(state.SelectedSubcategoryFilters, StringComparer.OrdinalIgnoreCase);
        var categoryFilteredResults = searchableResults
            .Where(info => MatchesSelectedCategory(info, state.CategoryFilterAll, selectedCategoryFilters, selectedSubcategoryFilters))
            .ToList();
        var queryFilteredResults = categoryFilteredResults.Where(info => MatchesSearchQuery(info, state.Query)).ToList();
        var completionFilteredResults = queryFilteredResults
            .Where(info => MatchesCompletionFilter(state.CompletionFilter, info.Id, isComplete))
            .ToList();
        var matchingResults = SortSearchResults(completionFilteredResults, state.SortMode, getSortKey).ToList();

        return new AchievementSearchResults(
            matchingResults,
            searchableResults.Count,
            categoryFilteredResults.Count,
            queryFilteredResults.Count,
            matchingResults.Count);
    }

    public static IReadOnlyList<AchievementSearchCategoryGroup> BuildCategoryGroups(
        IEnumerable<AchievementInfo> achievements,
        string completionFilter,
        bool completionStateLoaded,
        Func<uint, bool> isComplete,
        Func<AchievementInfo, AchievementSearchSortKey> getSortKey)
        => GetSearchableAchievements(achievements)
            .Select(info =>
            {
                var parts = AchievementCategoryPath.Parse(info.CategoryName);
                var sortKey = getSortKey(info);
                return new AchievementSearchCategoryEntry(
                    info,
                    parts.Category,
                    parts.Subcategory,
                    sortKey,
                    MatchesCompletionCountFilter(info, completionFilter, completionStateLoaded, isComplete));
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Category))
            .GroupBy(entry => entry.Category)
            .OrderBy(group => group.Min(entry => entry.KindOrder))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AchievementSearchCategoryGroup(group.Key, group.ToList()))
            .ToList();

    public static bool MatchesCompletionCountFilter(
        AchievementInfo info,
        string completionFilter,
        bool completionStateLoaded,
        Func<uint, bool> isComplete)
        => SearchCompletionFilterPolicy.MatchesForCount(
            completionFilter,
            completionStateLoaded,
            isComplete(info.Id));

    public static bool ShouldHideZeroCountCategories(string completionFilter, bool hideZeroCountIncompleteCategories)
        => hideZeroCountIncompleteCategories
            && string.Equals(completionFilter, SearchCompletionFilterPolicy.Incomplete, StringComparison.Ordinal);

    public static bool MatchesSelectedCategory(AchievementInfo info, AchievementSearchQueryState state)
        => MatchesSelectedCategory(
            info,
            state.CategoryFilterAll,
            new HashSet<string>(state.SelectedCategoryFilters, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(state.SelectedSubcategoryFilters, StringComparer.OrdinalIgnoreCase));

    private static bool MatchesSelectedCategory(
        AchievementInfo info,
        bool categoryFilterAll,
        IReadOnlySet<string> selectedCategoryFilters,
        IReadOnlySet<string> selectedSubcategoryFilters)
    {
        if (categoryFilterAll)
        {
            return true;
        }

        var parts = AchievementCategoryPath.Parse(info.CategoryName);
        if (string.IsNullOrWhiteSpace(parts.Category))
        {
            return false;
        }

        if (selectedCategoryFilters.Contains(parts.Category))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(parts.Subcategory))
        {
            return false;
        }

        return selectedSubcategoryFilters.Contains(AchievementCategoryPath.BuildSubcategoryFilterKey(parts.Category, parts.Subcategory));
    }

    private static bool MatchesSearchQuery(AchievementInfo info, string query)
    {
        var normalizedQuery = query.Trim();
        return string.IsNullOrWhiteSpace(normalizedQuery)
            || info.Name.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)
            || info.CategoryName.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool MatchesCompletionFilter(string completionFilter, uint achievementId, Func<uint, bool> isComplete)
        => string.Equals(completionFilter, SearchCompletionFilterPolicy.All, StringComparison.Ordinal)
            || SearchCompletionFilterPolicy.Matches(completionFilter, isComplete(achievementId));

    private static IEnumerable<AchievementInfo> SortSearchResults(
        IEnumerable<AchievementInfo> results,
        AchievementSearchSortMode sortMode,
        Func<AchievementInfo, AchievementSearchSortKey> getSortKey)
        => sortMode == AchievementSearchSortMode.Alphabetical
            ? results.OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase).ThenBy(result => result.Id)
            : results.OrderBy(result => getSortKey(result).KindOrder)
                .ThenBy(result => getSortKey(result).CategoryOrder)
                .ThenBy(result => getSortKey(result).AchievementOrder)
                .ThenBy(result => getSortKey(result).RowId);
}
