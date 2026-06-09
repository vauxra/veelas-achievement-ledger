using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace AchievementTracker;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<uint> TrackedAchievementIds { get; set; } = [];

    public bool EnableDebugLogging { get; set; } = false;

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
