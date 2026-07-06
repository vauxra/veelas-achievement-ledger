using System.Collections.Generic;

namespace AchievementTracker.Services;

public static class MainPanelColumnWidthDefaults
{
    public const float Lists = 270f;
    public const float SearchCategories = 320f;
    public const float SearchResults = 550f;
    public const float TrackedAchievements = 320f;

    public static Dictionary<string, float> Create() => new()
    {
        ["Lists"] = Lists,
        ["Search Categories"] = SearchCategories,
        ["Search Results"] = SearchResults,
        ["Tracked Achievements"] = TrackedAchievements,
    };
}
