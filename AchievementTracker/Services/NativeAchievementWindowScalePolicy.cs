namespace AchievementTracker.Services;

public static class NativeAchievementWindowScalePolicy
{
    public static bool ShouldParkForAction(NativeAchievementActionKind kind, bool nativeWindowWasAlreadyOpen)
        => kind == NativeAchievementActionKind.Refresh && !nativeWindowWasAlreadyOpen;

    public static bool ShouldCloseAfterRefresh(bool nativeWindowWasAlreadyOpen)
        => !nativeWindowWasAlreadyOpen;

    public static bool ShouldCloseAfterRefreshJobItem(NativeAchievementJobKind jobKind, bool nativeWindowWasAlreadyOpen, bool hasPendingSameJob)
        => jobKind != NativeAchievementJobKind.Inspection && !nativeWindowWasAlreadyOpen && !hasPendingSameJob;

    public static bool ShouldRestoreForAction(NativeAchievementActionKind kind)
        => kind == NativeAchievementActionKind.Inspection;

    public static bool ShouldRestoreWhenIdle(bool hasActiveRequest, bool hasPendingRequests, bool hasParkedWindow)
        => hasParkedWindow && !hasActiveRequest && !hasPendingRequests;
}
