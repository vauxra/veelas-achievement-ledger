using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);

public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();
    private readonly HashSet<uint> observedCompletions = [];
    private readonly Action<string> debugLog;
    private string lastSlotDebugLine = string.Empty;

    public ClientAchievementProgressSource(Action<string>? debugLog = null)
    {
        this.debugLog = debugLog ?? (_ => { });
    }

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
        var slotDebugLine = $"AchieveEx DebugTrace ProgressSlot state={state} id={achievementId} current={current} max={max}";
        if (!string.Equals(slotDebugLine, this.lastSlotDebugLine, StringComparison.Ordinal))
        {
            this.lastSlotDebugLine = slotDebugLine;
            this.debugLog(slotDebugLine);
        }

        if (state != Achievement.AchievementState.Loaded || max == 0)
        {
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, "Achievement state slot");
        if (current >= max)
        {
            this.observedCompletions.Add(achievementId);
        }
    }

    public bool TryGetFreshObservation(uint achievementId, DateTimeOffset notBefore, out ObservedAchievementProgress progress)
    {
        this.UpdateCache();
        return this.TryGetFreshCachedObservation(achievementId, notBefore, out progress);
    }

    public bool TryGetFreshCachedObservation(uint achievementId, DateTimeOffset notBefore, out ObservedAchievementProgress progress)
    {
        return this.cachedProgress.TryGetValue(achievementId, out progress)
            && progress.ObservedAt >= notBefore;
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

        this.debugLog($"AchieveEx DebugTrace RecordObservedProgress id={achievementId} current={current} max={max} source={source}");
    }

    public void RecordObservedCompletion(uint achievementId, string source)
    {
        this.cachedProgress.Remove(achievementId);
        this.observedCompletions.Add(achievementId);
        this.debugLog($"AchieveEx DebugTrace RecordObservedCompletion id={achievementId} source={source}");
    }

    public static readonly TimeSpan RecentlyObservedUpdateAllSkipThreshold = TimeSpan.FromSeconds(30);

    public void ClearCache()
    {
        this.cachedProgress.Clear();
        this.observedCompletions.Clear();
    }

    public bool IsRecentlyObserved(uint achievementId, DateTimeOffset now, TimeSpan threshold)
    {
        this.UpdateCache();
        return this.cachedProgress.TryGetValue(achievementId, out var progress)
            && now - progress.ObservedAt >= TimeSpan.Zero
            && now - progress.ObservedAt <= threshold;
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
