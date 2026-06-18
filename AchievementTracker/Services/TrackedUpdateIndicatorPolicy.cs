namespace AchievementTracker.Services;

public enum TrackedUpdateIndicatorState
{
    Working,
    NeedsUpdate,
    AllUpdated,
}

public static class TrackedUpdateIndicatorPolicy
{
    public static TrackedUpdateIndicatorState GetState(int pendingCount, bool isUpdateInProgress, int staleTrackedCount)
    {
        if (pendingCount > 0 || isUpdateInProgress)
        {
            return TrackedUpdateIndicatorState.Working;
        }

        return staleTrackedCount > 0
            ? TrackedUpdateIndicatorState.NeedsUpdate
            : TrackedUpdateIndicatorState.AllUpdated;
    }
}
