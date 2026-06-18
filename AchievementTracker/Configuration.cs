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
    public int Version { get; set; } = 1;

    public List<uint> TrackedAchievementIds { get; set; } = [];

    public List<TrackedAchievementPreset> TrackedAchievementPresets { get; set; } = [];

    public CosmicClassScoreCache CosmicClassScoreCache { get; set; } = new();

    public bool HideCompletedInSearch { get; set; } = true;

    public string SearchCompletionFilter { get; set; } = "All";

    public bool ExperimentalAutoUpdateEnabled { get; set; }

    public int ExperimentalAutoUpdateIntervalMinutes { get; set; }

    public int ExperimentalAutoUpdateIntervalSeconds { get; set; }

    public int ExperimentalUpdateSpacingSeconds { get; set; } = 15;

    public bool TriggerAutoUpdatesEnabled { get; set; }

    public bool TriggerUpdatesRespectAutoUpdateSelection { get; set; }

    public bool TriggerOnAchievementCompletion { get; set; } = true;

    public bool TriggerOnMinerActivities { get; set; } = true;

    public bool TriggerOnMiningActivities { get; set; } = true;

    public bool TriggerOnQuarryingActivities { get; set; } = true;

    public bool TriggerOnBotanistActivities { get; set; } = true;

    public bool TriggerOnLoggingActivities { get; set; } = true;

    public bool TriggerOnHarvestingActivities { get; set; } = true;

    public bool TriggerOnFisherActivities { get; set; } = true;

    public bool TriggerOnFishingActivities { get; set; } = true;

    public bool TriggerOnSpearfishingActivities { get; set; } = true;

    public bool TriggerOnCrafterActivities { get; set; } = true;

    public bool TriggerOnCraftingActivities { get; set; } = true;

    public List<uint> AutoUpdateAchievementIds { get; set; } = [];

    public bool ExperimentalDebugLoggingEnabled { get; set; } = true;

    public bool RestoreNativeAchievementWindowAfterUpdates { get; set; } = true;

    public List<string> MainColumnOrder { get; set; } = ["Lists", "Search Categories", "Search Results", "Tracked Achievements"];

    public List<string> MainNavigationOrder { get; set; } = ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"];

    public List<string> HiddenTrackedAchievementIcons { get; set; } = ["Auto update", "Remove", "Refresh", "Open"];

    public List<string> HiddenMainNavigationButtons { get; set; } = [];

    public List<string> ShownTrackedAchievementIcons { get; set; } = ["Auto update", "Remove", "Refresh", "Open"];

    public List<string> ShownMainNavigationButtons { get; set; } = ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"];

    public Dictionary<string, float> MainColumnWidths { get; set; } = MainPanelColumnWidthDefaults.Create();

    public List<uint> GetAutoUpdateTrackedAchievementIds()
        => AutoUpdateSelection.SelectIncludedTrackedAchievements(this.TrackedAchievementIds, this.AutoUpdateAchievementIds);

    public void NormalizeAutoUpdateSettings()
    {
        // Migrate older experimental configs that stored the cycle in minutes.
        if (this.ExperimentalAutoUpdateIntervalSeconds <= 0 && this.ExperimentalAutoUpdateIntervalMinutes > 0)
        {
            this.ExperimentalAutoUpdateIntervalSeconds = Math.Clamp(this.ExperimentalAutoUpdateIntervalMinutes, 1, 1440) * 60;
        }
        else if (this.ExperimentalAutoUpdateIntervalSeconds <= 0)
        {
            this.ExperimentalAutoUpdateIntervalSeconds = 900;
        }

        this.ExperimentalAutoUpdateIntervalSeconds = Math.Clamp(this.ExperimentalAutoUpdateIntervalSeconds, 60, 86_400);
        this.ExperimentalUpdateSpacingSeconds = Math.Clamp(this.ExperimentalUpdateSpacingSeconds < 0 ? 0 : this.ExperimentalUpdateSpacingSeconds, 0, 3_600);
        if (this.ExperimentalAutoUpdateEnabled && this.TriggerAutoUpdatesEnabled)
        {
            this.TriggerAutoUpdatesEnabled = false;
        }

        this.CosmicClassScoreCache ??= new CosmicClassScoreCache();
        this.MainColumnOrder = NormalizeStringOrder(this.MainColumnOrder, ["Lists", "Search Categories", "Search Results", "Tracked Achievements"]);
        this.MainNavigationOrder = NormalizeStringOrder(this.MainNavigationOrder, ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"]);
        this.HiddenTrackedAchievementIcons = NormalizeHiddenTrackedAchievementIcons(this.HiddenTrackedAchievementIcons, ["Auto update", "Remove", "Refresh", "Open"]);
        this.HiddenMainNavigationButtons = NormalizeStringSet(this.HiddenMainNavigationButtons, ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"]);
        this.ShownTrackedAchievementIcons = NormalizeShownStringSet(this.ShownTrackedAchievementIcons, this.HiddenTrackedAchievementIcons, ["Auto update", "Remove", "Refresh", "Open"]);
        this.ShownMainNavigationButtons = NormalizeShownStringSet(this.ShownMainNavigationButtons, this.HiddenMainNavigationButtons, ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"]);
        this.MainColumnWidths = NormalizeColumnWidths(this.MainColumnWidths, MainPanelColumnWidthDefaults.Create());
        if (this.SearchCompletionFilter is not ("All" or "Completed" or "Incomplete"))
        {
            this.SearchCompletionFilter = this.HideCompletedInSearch ? "Incomplete" : "All";
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

    private static List<string> NormalizeHiddenTrackedAchievementIcons(List<string>? values, string[] allowed)
        => NormalizeStringSet(values, allowed);

    private static List<string> NormalizeShownStringSet(List<string>? shownValues, List<string>? legacyHiddenValues, string[] allowed)
    {
        if (shownValues is not null)
        {
            return NormalizeStringSet(shownValues, allowed);
        }

        var legacyHidden = NormalizeStringSet(legacyHiddenValues, allowed);
        return allowed.Where(value => !legacyHidden.Contains(value)).ToList();
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
