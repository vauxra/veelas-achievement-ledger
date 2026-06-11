using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);

// Component: in-memory observed progress cache.
// Risk level: medium.
// Why: reads local ClientStructs Achievement state slots.
// Safety boundary: reads already-loaded local state only; does not call direct achievement-progress request API.
public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();
    private readonly HashSet<uint> observedCompletions = [];

    public void UpdateCache()
    {
        // What this does:
        // - Reads the client's current Achievement progress slot.
        // - Stores it if the slot says progress is loaded.
        // What this does NOT do:
        // - It does not ask the server for new progress.
        // - It does not open UI or trigger gameplay actions.
        // ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
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

        this.RecordObservedProgress(achievementId, current, max, "Achievement state slot");
    }

    public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)
    {
        if (max == 0)
        {
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, source);
        if (current >= max)
        {
            this.observedCompletions.Add(achievementId);
        }
    }

    public void RecordObservedCompletion(uint achievementId, string source)
    {
        this.cachedProgress.Remove(achievementId);
        this.observedCompletions.Add(achievementId);
    }

    public void ClearCache()
    {
        this.cachedProgress.Clear();
        this.observedCompletions.Clear();
    }

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

    public bool IsObservedComplete(uint achievementId) => this.observedCompletions.Contains(achievementId);
}
