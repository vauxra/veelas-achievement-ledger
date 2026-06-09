using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, (uint Current, uint Max)> cachedProgress = new();
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

    public bool IsRequestInFlight
    {
        get
        {
            var achievement = Achievement.Instance();
            return achievement != null && achievement->ProgressRequestState == Achievement.AchievementState.Requested;
        }
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

        this.cachedProgress[achievementId] = (current, max);
    }

    public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)
    {
        if (max == 0)
        {
            this.debugLog.Trace("ProgressSource.RecordObservedProgress", $"ignored source={source} achievementId={achievementId} current={current} max={max}");
            return;
        }

        this.cachedProgress[achievementId] = (current, max);
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

    public bool RequestProgress(uint achievementId)
    {
        this.UpdateCache();

        // This calls the same client path used for achievement progress and must remain user-triggered.
        // Server interaction restriction docs: https://dalamud.dev/plugin-publishing/restrictions
        var achievement = Achievement.Instance();
        if (achievement == null)
        {
            this.debugLog.Trace("ProgressSource.RequestProgress", $"achievementId={achievementId} rejected=Achievement.Instance null");
            return false;
        }

        if (achievement->ProgressRequestState == Achievement.AchievementState.Requested)
        {
            this.debugLog.Trace("ProgressSource.RequestProgress", $"achievementId={achievementId} rejected=request already in flight currentSlotId={achievement->ProgressAchievementId} current={achievement->ProgressCurrent} max={achievement->ProgressMax}");
            return false;
        }

        this.debugLog.Trace("ProgressSource.RequestProgress", $"achievementId={achievementId} beforeState={achievement->ProgressRequestState} slotId={achievement->ProgressAchievementId} current={achievement->ProgressCurrent} max={achievement->ProgressMax}");
        achievement->RequestAchievementProgress(achievementId);
        this.debugLog.Trace("ProgressSource.RequestProgress", $"achievementId={achievementId} submitted afterState={achievement->ProgressRequestState} slotId={achievement->ProgressAchievementId} current={achievement->ProgressCurrent} max={achievement->ProgressMax}");
        return true;
    }
}
