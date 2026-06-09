using System.Collections.Generic;

namespace AchievementTracker.Services;

public sealed class TrackedAchievementStore
{
    public const int MaxTrackedAchievements = 5;

    private readonly List<uint> achievementIds = [];

    public IReadOnlyList<uint> AchievementIds => this.achievementIds;

    public bool TryAdd(uint achievementId)
    {
        if (this.achievementIds.Contains(achievementId))
        {
            return false;
        }

        if (this.achievementIds.Count >= MaxTrackedAchievements)
        {
            return false;
        }

        this.achievementIds.Add(achievementId);
        return true;
    }

    public bool Remove(uint achievementId) => this.achievementIds.Remove(achievementId);

    public bool MoveUp(uint achievementId)
    {
        var index = this.achievementIds.IndexOf(achievementId);
        if (index <= 0)
        {
            return false;
        }

        (this.achievementIds[index - 1], this.achievementIds[index]) =
            (this.achievementIds[index], this.achievementIds[index - 1]);
        return true;
    }

    public bool MoveDown(uint achievementId)
    {
        var index = this.achievementIds.IndexOf(achievementId);
        if (index < 0 || index >= this.achievementIds.Count - 1)
        {
            return false;
        }

        (this.achievementIds[index + 1], this.achievementIds[index]) =
            (this.achievementIds[index], this.achievementIds[index + 1]);
        return true;
    }

    public void LoadFrom(IEnumerable<uint> achievementIds)
    {
        this.achievementIds.Clear();
        foreach (var achievementId in achievementIds)
        {
            _ = this.TryAdd(achievementId);
        }
    }

    public List<uint> ToConfigList() => [.. this.achievementIds];
}
