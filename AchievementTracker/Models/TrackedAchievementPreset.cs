using System;
using System.Collections.Generic;

namespace AchievementTracker.Models;

[Serializable]
public sealed class TrackedAchievementPreset
{
    public string Name { get; set; } = string.Empty;

    public List<uint> AchievementIds { get; set; } = [];
}
