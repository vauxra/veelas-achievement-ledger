using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);

public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();
    private readonly DebugLog debugLog;
    private Achievement.AchievementState? lastSeenAchievementState;
    private Achievement.AchievementState? lastSeenState;
    private bool? lastIsLoaded;
    private string? lastHistory;
    private uint? lastLoadedAchievementId;
    private uint? lastLoadedCurrent;
    private uint? lastLoadedMax;

    public ClientAchievementProgressSource(DebugLog debugLog)
    {
        this.debugLog = debugLog;
    }

    public void UpdateCache()
    {
        // ClientStructs stage-2 interaction is documented as a Dalamud-supported fallback:
        // https://dalamud.dev/plugin-development/interaction/
        var achievement = Achievement.Instance();
        if (achievement == null)
        {
            if (this.lastSeenState.HasValue)
            {
                this.debugLog.Trace("ProgressSource.UpdateCache", "Achievement.Instance() is null");
                this.lastSeenState = null;
            }

            return;
        }

        var state = achievement->ProgressRequestState;
        var achievementState = achievement->State;
        var isLoaded = achievement->IsLoaded();
        var achievementId = achievement->ProgressAchievementId;
        var current = achievement->ProgressCurrent;
        var max = achievement->ProgressMax;
        if (this.debugLog.Enabled)
        {
            var history = string.Join(",", achievement->History.ToArray());
            if (this.lastSeenAchievementState != achievementState || this.lastIsLoaded != isLoaded || this.lastHistory != history)
            {
                this.debugLog.Trace("ProgressSource.AchievementState", $"state={achievementState} isLoaded={isLoaded} history=[{history}]");
                this.lastSeenAchievementState = achievementState;
                this.lastIsLoaded = isLoaded;
                this.lastHistory = history;
            }
        }

        if (this.lastSeenState != state)
        {
            this.debugLog.Trace("ProgressSource.State", $"state={state} achievementId={achievementId} current={current} max={max}");
            this.lastSeenState = state;
        }

        if (state != Achievement.AchievementState.Loaded || max == 0)
        {
            return;
        }

        if (this.lastLoadedAchievementId != achievementId || this.lastLoadedCurrent != current || this.lastLoadedMax != max)
        {
            this.debugLog.Trace("ProgressSource.UpdateCache", $"cache achievementId={achievementId} current={current} max={max}");
            this.lastLoadedAchievementId = achievementId;
            this.lastLoadedCurrent = current;
            this.lastLoadedMax = max;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, "Achievement state slot");
    }

    public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)
    {
        if (max == 0)
        {
            this.debugLog.Trace("ProgressSource.RecordObservedProgress", $"ignored source={source} achievementId={achievementId} current={current} max={max}");
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, source);
        this.lastLoadedAchievementId = achievementId;
        this.lastLoadedCurrent = current;
        this.lastLoadedMax = max;
        this.debugLog.Trace("ProgressSource.RecordObservedProgress", $"source={source} achievementId={achievementId} current={current} max={max}");
    }

    public void RecordObservedCompletion(uint achievementId, string source)
    {
        if (this.cachedProgress.Remove(achievementId))
        {
            this.debugLog.Trace("ProgressSource.RecordObservedCompletion", $"source={source} achievementId={achievementId} removed stale numeric cache");
            return;
        }

        this.debugLog.Trace("ProgressSource.RecordObservedCompletion", $"source={source} achievementId={achievementId}");
    }

    public void ClearCache()
    {
        this.debugLog.Trace("ProgressSource.ClearCache", $"entries={this.cachedProgress.Count}");
        this.cachedProgress.Clear();
        this.lastLoadedAchievementId = null;
        this.lastLoadedCurrent = null;
        this.lastLoadedMax = null;
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
}
