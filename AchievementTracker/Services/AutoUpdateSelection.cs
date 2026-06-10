using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public static class AutoUpdateSelection
{
    public static List<uint> SelectIncludedTrackedAchievements(IEnumerable<uint> trackedAchievementIds, IEnumerable<uint> includedAchievementIds)
    {
        var included = new HashSet<uint>(includedAchievementIds.Where(id => id != 0));
        return trackedAchievementIds
            .Where(id => id != 0 && included.Contains(id))
            .Distinct()
            .Take(TrackedAchievementStore.MaxTrackedAchievements)
            .ToList();
    }
}
