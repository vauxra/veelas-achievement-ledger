using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using AchievementTracker.Models;
using AchievementTracker.Services;

namespace AchievementTracker;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    private static readonly string[] MainColumns = ["Lists", "Search Categories", "Search Results", "Tracked Achievements"];
    private static readonly string[] MainNavigationButtons = ["Lists", "Search", "Config", "Tracked buttons"];
    private static readonly string[] TrackedAchievementButtons = ["Remove", "Refresh", "Open"];

    public int Version { get; set; } = 1;

    public List<uint> TrackedAchievementIds { get; set; } = [];

    public List<TrackedAchievementPreset> TrackedAchievementPresets { get; set; } = [];

    public CosmicClassScoreCache CosmicClassScoreCache { get; set; } = new();

    public bool HideCompletedInSearch { get; set; } = true;

    public bool HideZeroCountIncompleteSearchCategories { get; set; } = true;

    public string SearchCompletionFilter { get; set; } = SearchCompletionFilterPolicy.All;

    public List<string> MainColumnOrder { get; set; } = ["Lists", "Search Categories", "Search Results", "Tracked Achievements"];

    public List<string> MainNavigationOrder { get; set; } = ["Lists", "Search", "Config", "Tracked buttons"];

    public List<string> HiddenTrackedAchievementIcons { get; set; } = ["Remove", "Refresh", "Open"];

    public List<string> ShownMainNavigationButtons { get; set; } = ["Lists", "Search", "Config", "Tracked buttons"];

    public Dictionary<string, float> MainColumnWidths { get; set; } = MainPanelColumnWidthDefaults.Create();

    public void Normalize()
    {
        this.CosmicClassScoreCache ??= new CosmicClassScoreCache();
        this.MainColumnOrder = NormalizeStringOrder(this.MainColumnOrder, MainColumns);
        this.MainNavigationOrder = NormalizeStringOrder(this.MainNavigationOrder, MainNavigationButtons);
        this.HiddenTrackedAchievementIcons = NormalizeStringSet(this.HiddenTrackedAchievementIcons, TrackedAchievementButtons);
        this.ShownMainNavigationButtons = NormalizeShownStringSet(this.ShownMainNavigationButtons, MainNavigationButtons);
        this.MainColumnWidths = NormalizeColumnWidths(this.MainColumnWidths, MainPanelColumnWidthDefaults.Create());
        if (this.SearchCompletionFilter is not (SearchCompletionFilterPolicy.All or SearchCompletionFilterPolicy.Completed or SearchCompletionFilterPolicy.Incomplete))
        {
            this.SearchCompletionFilter = this.HideCompletedInSearch ? SearchCompletionFilterPolicy.Incomplete : SearchCompletionFilterPolicy.All;
        }

        TrackedAchievementPresetStore.Normalize(this.TrackedAchievementPresets);
    }

    private static List<string> NormalizeStringOrder(List<string>? values, string[] defaults)
    {
        var normalized = new List<string>();
        if (values is not null)
        {
            foreach (var value in values)
            {
                if (Array.Exists(defaults, item => string.Equals(item, value, StringComparison.Ordinal))
                    && !normalized.Contains(value))
                {
                    normalized.Add(value);
                }
            }
        }

        foreach (var value in defaults)
        {
            if (!normalized.Contains(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static List<string> NormalizeStringSet(List<string>? values, string[] allowed)
    {
        var normalized = new List<string>();
        if (values is null)
        {
            return normalized;
        }

        foreach (var value in values)
        {
            if (Array.Exists(allowed, item => string.Equals(item, value, StringComparison.Ordinal))
                && !normalized.Contains(value))
            {
                normalized.Add(value);
            }
        }

        return normalized;
    }

    private static List<string> NormalizeShownStringSet(List<string>? values, string[] allowed)
    {
        var normalized = NormalizeStringSet(values, allowed);
        return normalized.Count == 0 ? allowed.ToList() : normalized;
    }

    private static Dictionary<string, float> NormalizeColumnWidths(Dictionary<string, float>? values, Dictionary<string, float> defaults)
    {
        var normalized = new Dictionary<string, float>();
        foreach (var (key, defaultValue) in defaults)
        {
            var value = values is not null && values.TryGetValue(key, out var configuredValue)
                ? configuredValue
                : defaultValue;
            normalized[key] = Math.Clamp(value, 0f, 900f);
        }

        return normalized;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
