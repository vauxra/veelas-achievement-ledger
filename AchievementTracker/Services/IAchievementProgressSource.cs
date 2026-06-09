namespace AchievementTracker.Services;

public interface IAchievementProgressSource
{
    bool IsRequestInFlight { get; }

    void UpdateCache();

    void ClearCache();

    bool TryGetProgress(uint achievementId, out uint current, out uint max);

    bool RequestProgress(uint achievementId);
}
