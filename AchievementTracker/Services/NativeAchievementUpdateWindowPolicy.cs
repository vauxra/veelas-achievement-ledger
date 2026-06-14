namespace AchievementTracker.Services;

public static class NativeAchievementUpdateWindowPolicy
{
    public static bool ShouldParkDuringBatch(bool batchWindowWasOpenBeforeStart, bool completedAtLeastOneRequest)
    {
        // Current stability builds keep progress refresh geometry-neutral. Native Achievement
        // crashes were occurring in the addon's refresh/update path, so automatic SetScale/
        // SetPosition parking is disabled for queued refreshes. The manual reset button remains
        // available as a recovery tool.
        return false;
    }
}
