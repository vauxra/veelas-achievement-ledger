using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);

public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();

    public void UpdateCache()
    {
        // ClientStructs stage-2 interaction is documented as a Dalamud-supported fallback:
        // https://dalamud.dev/plugin-development/interaction/
        var achievement = Achievement.Instance();
        if (achievement == null)
        {
            return;
        }

        var state = achievement->ProgressRequestState;
        var achievementId = achievement->ProgressAchievementId;
        var current = achievement->ProgressCurrent;
        var max = achievement->ProgressMax;

        if (state != Achievement.AchievementState.Loaded || max == 0)
        {
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, "Achievement state slot");
    }

    public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)
    {
        if (max == 0)
        {
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, source);
    }

    public void RecordObservedCompletion(uint achievementId, string source)
    {
        this.cachedProgress.Remove(achievementId);
    }

    public void ClearCache() => this.cachedProgress.Clear();

    public bool TryGetProgress(uint achievementId, out uint current, out uint max)
    {
        this.UpdateCache();

        if (this.cachedProgress.TryGetValue(achievementId, out var progress))
        {
            current = progress.Current;
            max = progress.Max;
            return true;
        }

        current = 0;
        max = 0;
        return false;
    }

    public bool TryGetObservation(uint achievementId, out ObservedAchievementProgress progress)
    {
        this.UpdateCache();
        return this.cachedProgress.TryGetValue(achievementId, out progress);
    }
}
