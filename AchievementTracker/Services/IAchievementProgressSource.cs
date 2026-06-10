namespace AchievementTracker.Services;

public interface IAchievementProgressSource
{
    void UpdateCache();

    void ClearCache();

    bool TryGetProgress(uint achievementId, out uint current, out uint max);

    bool IsObservedComplete(uint achievementId);
}
