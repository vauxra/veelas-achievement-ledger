using System;
using System.Collections.Generic;

namespace AchievementTracker.Models;

[Serializable]
public sealed class CharacterAchievementCompletionCache
{
    public string CharacterKey { get; set; } = string.Empty;

    public List<uint> CompletedAchievementIds { get; set; } = [];

    public DateTimeOffset LastUpdatedAt { get; set; } = DateTimeOffset.MinValue;
}
