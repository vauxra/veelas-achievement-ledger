using Dalamud.Configuration;
using System;
using System.Collections.Generic;
using AchievementTracker.Services;

namespace AchievementTracker;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<uint> TrackedAchievementIds { get; set; } = [];

    public bool HideCompletedInSearch { get; set; } = true;

    public bool ExperimentalAutoUpdateEnabled { get; set; }

    public int ExperimentalAutoUpdateIntervalMinutes { get; set; } = 15;

    public List<uint> AutoUpdateAchievementIds { get; set; } = [];

    public bool ExperimentalDebugLoggingEnabled { get; set; } = true;

    public List<uint> GetAutoUpdateTrackedAchievementIds()
        => AutoUpdateSelection.SelectIncludedTrackedAchievements(this.TrackedAchievementIds, this.AutoUpdateAchievementIds);

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
