using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);

// Component: in-memory observed progress cache.
// Risk level: medium.
// Why: reads local ClientStructs Achievement state slots.
// Safety boundary: reads already-loaded local state only during a bounded user-action observation window;
// does not call direct achievement-progress request APIs, hooks, signatures, or raw-memory address scans.
public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();
    private readonly Dictionary<uint, DateTimeOffset> observationDeadlines = new();
    private readonly HashSet<uint> observedCompletions = [];
    private readonly Func<DateTimeOffset> nowProvider;

    public ClientAchievementProgressSource()
        : this(() => DateTimeOffset.UtcNow)
    {
    }

    public ClientAchievementProgressSource(Func<DateTimeOffset> nowProvider)
    {
        this.nowProvider = nowProvider;
    }

    public int ActiveObservationCount => this.observationDeadlines.Count;

    public void BeginObservation(uint achievementId, TimeSpan duration)
    {
        if (achievementId == 0 || duration <= TimeSpan.Zero)
        {
            return;
        }

        this.PruneExpiredObservations();
        this.observationDeadlines[achievementId] = this.nowProvider() + duration;
    }

    public void UpdateCache()
    {
        this.PruneExpiredObservations();
        if (this.observationDeadlines.Count == 0)
        {
            return;
        }

        // What this does:
        // - Reads the client's current Achievement progress slot after a user-guided native Achievement open.
        // - Stores it only if the slot matches an active observation window.
        // What this does NOT do:
        // - It does not ask the server for new progress.
        // - It does not hook native functions or bind native function pointers.
        // - It does not open UI or trigger gameplay actions.
        // ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var achievement = Achievement.Instance();
        if (achievement == null)
        {
            return;
        }

        this.TryRecordObservedSlot(
            achievement->ProgressRequestState == Achievement.AchievementState.Loaded,
            achievement->ProgressAchievementId,
            achievement->ProgressCurrent,
            achievement->ProgressMax,
            "Achievement state slot");
    }

    public bool TryRecordObservedSlot(bool isLoaded, uint achievementId, uint current, uint max, string source)
    {
        this.PruneExpiredObservations();
        if (!isLoaded || achievementId == 0 || max == 0)
        {
            return false;
        }

        if (!this.observationDeadlines.ContainsKey(achievementId))
        {
            return false;
        }

        this.RecordObservedProgress(achievementId, current, max, source);
        this.observationDeadlines.Remove(achievementId);
        return true;
    }

    public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)
    {
        if (achievementId == 0 || max == 0)
        {
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, this.nowProvider(), source);
        if (current >= max)
        {
            this.observedCompletions.Add(achievementId);
        }
    }

    public void ClearCache()
    {
        this.cachedProgress.Clear();
        this.observedCompletions.Clear();
        this.observationDeadlines.Clear();
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

    public bool TryGetCachedObservation(uint achievementId, out ObservedAchievementProgress progress)
        => this.cachedProgress.TryGetValue(achievementId, out progress);

    public bool IsObservedComplete(uint achievementId) => this.observedCompletions.Contains(achievementId);

    private void PruneExpiredObservations()
    {
        if (this.observationDeadlines.Count == 0)
        {
            return;
        }

        var now = this.nowProvider();
        foreach (var expiredId in this.observationDeadlines
            .Where(item => item.Value <= now)
            .Select(item => item.Key)
            .ToList())
        {
            this.observationDeadlines.Remove(expiredId);
        }
    }
}
