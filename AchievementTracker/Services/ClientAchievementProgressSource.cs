using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);

public unsafe sealed class ClientAchievementProgressSource : IAchievementProgressSource
{
    private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();
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
        var slotDebugLine = $"VAL DebugTrace ProgressSlot state={state} id={achievementId} current={current} max={max}";
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
    }

    public bool RequestProgress(uint achievementId, string reason)
    {
        // Experimental branch only: this calls the ClientStructs achievement progress request path directly.
        // FFXIVClientStructs XML documents RequestAchievementProgress as: "Requests Achievement Progress from the server."
        // This intentionally accepts the risk described in https://dalamud.dev/plugin-publishing/restrictions
        // and is not positioned for Dalamud publishing.
        var achievement = Achievement.Instance();
        if (achievement == null)
        {
            this.debugLog($"VAL DebugTrace RequestProgressFailed id={achievementId} reason={reason} achievementInstance=null");
            return false;
        }

        this.debugLog($"VAL DebugTrace RequestProgress id={achievementId} reason={reason} beforeState={achievement->ProgressRequestState} beforeSlot={achievement->ProgressAchievementId} beforeCurrent={achievement->ProgressCurrent} beforeMax={achievement->ProgressMax}");
        achievement->RequestAchievementProgress(achievementId);
        this.debugLog($"VAL DebugTrace RequestProgressSent id={achievementId} afterState={achievement->ProgressRequestState} afterSlot={achievement->ProgressAchievementId} afterCurrent={achievement->ProgressCurrent} afterMax={achievement->ProgressMax}");
        return true;
    }

    public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)
    {
        if (max == 0)
        {
            return;
        }

        this.cachedProgress[achievementId] = new ObservedAchievementProgress(current, max, DateTimeOffset.UtcNow, source);
        this.debugLog($"VAL DebugTrace RecordObservedProgress id={achievementId} current={current} max={max} source={source}");
    }

    public void RecordObservedCompletion(uint achievementId, string source)
    {
        this.cachedProgress.Remove(achievementId);
        this.debugLog($"VAL DebugTrace RecordObservedCompletion id={achievementId} source={source}");
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
