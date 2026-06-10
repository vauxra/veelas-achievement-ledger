using System;
using System.Collections.Generic;

namespace AchievementTracker.Models;

[Serializable]
public sealed class CosmicClassScoreCache
{
    public List<int> Scores { get; set; } = [];

    public DateTimeOffset? UpdatedAtUtc { get; set; }
}
