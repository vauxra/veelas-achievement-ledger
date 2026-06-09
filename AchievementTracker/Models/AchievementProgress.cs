namespace AchievementTracker.Models;

public enum AchievementProgressKind
{
    CompletionListNotLoaded,
    Complete,
    Incomplete,
    Numeric,
    TargetKnown,
    Unavailable,
}

public sealed record AchievementProgress(AchievementProgressKind Kind, int? Current = null, int? Required = null)
{
    public static AchievementProgress CompletionListNotLoaded() => new(AchievementProgressKind.CompletionListNotLoaded);

    public static AchievementProgress Complete() => new(AchievementProgressKind.Complete);

    public static AchievementProgress Incomplete() => new(AchievementProgressKind.Incomplete);

    public static AchievementProgress Numeric(int current, int required) => new(AchievementProgressKind.Numeric, current, required);

    public static AchievementProgress TargetKnown(int required) => new(AchievementProgressKind.TargetKnown, null, required);

    public static AchievementProgress Unavailable() => new(AchievementProgressKind.Unavailable);

    public string ToDisplayText()
    {
        return this.Kind switch
        {
            AchievementProgressKind.CompletionListNotLoaded => "Open Achievements to load status",
            AchievementProgressKind.Complete => "Complete",
            AchievementProgressKind.Incomplete => "Incomplete",
            AchievementProgressKind.Numeric when this.Current.HasValue && this.Required.HasValue => $"{this.Current.Value:N0} / {this.Required.Value:N0}",
            AchievementProgressKind.TargetKnown when this.Required.HasValue => $"Current unavailable / {this.Required.Value:N0}",
            _ => "Progress unavailable",
        };
    }
}
