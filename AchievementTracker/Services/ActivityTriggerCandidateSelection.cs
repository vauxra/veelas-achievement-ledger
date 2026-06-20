using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public static class ActivityTriggerCandidateSelection
{
    public static List<uint> ExcludeCosmicClassAchievements(IEnumerable<uint> achievementIds, Func<uint, bool> isCosmicClassAchievement)
        => achievementIds
            .Where(id => id != 0)
            .Distinct()
            .Where(id => !isCosmicClassAchievement(id))
            .ToList();
}