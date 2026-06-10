using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using AchievementTracker.Models;
using AchievementTracker.Services;

namespace AchievementTracker;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<uint> TrackedAchievementIds { get; set; } = [];

    public List<TrackedAchievementPreset> TrackedAchievementPresets { get; set; } = [];

    public bool HideCompletedInSearch { get; set; } = true;

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

    public bool TriggerOnCraftingLogActivities { get; set; } = true;

    public List<uint> AutoUpdateAchievementIds { get; set; } = [];

    public bool ExperimentalDebugLoggingEnabled { get; set; } = true;

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

        this.ExperimentalAutoUpdateIntervalSeconds = Math.Clamp(this.ExperimentalAutoUpdateIntervalSeconds, 1, 86_400);
        this.ExperimentalUpdateSpacingSeconds = Math.Clamp(this.ExperimentalUpdateSpacingSeconds, 0, 3_600);
        TrackedAchievementPresetStore.Normalize(this.TrackedAchievementPresets);
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
