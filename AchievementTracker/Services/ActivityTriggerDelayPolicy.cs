using System;

namespace AchievementTracker.Services;

public static class ActivityTriggerDelayPolicy
{
    public static readonly TimeSpan CraftingInitialDelay = TimeSpan.FromSeconds(6);

    public static TimeSpan GetInitialDelay(string triggerName)
        => string.Equals(triggerName, AchievementActivityUpdateClassifier.CraftingTrigger, StringComparison.Ordinal)
            ? CraftingInitialDelay
            : TimeSpan.Zero;
}
