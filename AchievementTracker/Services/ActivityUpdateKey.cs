namespace AchievementTracker.Services;

public readonly record struct ActivityUpdateKey(string TriggerName, string CategoryName)
{
    public override string ToString() => $"{this.TriggerName}:{this.CategoryName}";
}
