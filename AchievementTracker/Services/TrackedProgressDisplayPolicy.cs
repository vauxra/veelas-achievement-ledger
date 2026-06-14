namespace AchievementTracker.Services;

public static class TrackedProgressDisplayPolicy
{
    public static bool ShouldEvaluateProgress(bool hasObservedProgress, bool isComplete, bool hasCosmicProgressOverride)
        => hasObservedProgress || isComplete || hasCosmicProgressOverride;
}
