using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;

namespace AchievementTracker.Services;

public unsafe sealed class AchievementProgressRequester
{
    private readonly Action<string> debugLog;

    public AchievementProgressRequester(Action<string> debugLog)
    {
        this.debugLog = debugLog;
    }

    public bool RequestProgress(uint achievementId)
    {
        var achievement = Achievement.Instance();
        if (achievement == null || achievementId == 0)
        {
            this.debugLog($"AchieveEx DebugTrace DirectProgressRequestUnavailable id={achievementId}");
            return false;
        }

        try
        {
            achievement->RequestAchievementProgress(achievementId);
            this.debugLog($"AchieveEx DebugTrace DirectProgressRequestSent id={achievementId}");
            return true;
        }
        catch (Exception ex)
        {
            this.debugLog($"AchieveEx DebugTrace DirectProgressRequestFailed id={achievementId} type={ex.GetType().Name} message={ex.Message}");
            return false;
        }
    }
}
