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

    public CosmicClassScoreCache CosmicClassScoreCache { get; set; } = new();

    public bool HideCompletedInSearch { get; set; } = true;

    public void Normalize()
    {
        this.CosmicClassScoreCache ??= new CosmicClassScoreCache();
        TrackedAchievementPresetStore.Normalize(this.TrackedAchievementPresets);
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
