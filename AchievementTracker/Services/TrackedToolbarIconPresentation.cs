namespace AchievementTracker.Services;

public sealed record TrackedToolbarIconPresentation(string IconName, string ColorName, string Tooltip)
{
    public static TrackedToolbarIconPresentation ForHiddenState(bool hidden)
        => hidden
            ? new TrackedToolbarIconPresentation("Eye", "Default", "Show tracked achievement icons.")
            : new TrackedToolbarIconPresentation("Eye", "Red", "Hide tracked achievement icons.");
}
