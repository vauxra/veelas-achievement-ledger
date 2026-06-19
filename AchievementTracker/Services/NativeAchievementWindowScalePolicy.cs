namespace AchievementTracker.Services;

public static class NativeAchievementWindowScalePolicy
{
    private const float LegacyParkedScale = 0.55f;

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

    public static bool ShouldRestoreWhenPlayerOpenedPanel(bool hasParkedWindow, bool nativeWindowIsOpen, bool nativeWindowIsStillParked, bool hasActiveOrPendingWork)
        => hasParkedWindow
            && nativeWindowIsOpen
            && (!hasActiveOrPendingWork || !nativeWindowIsStillParked);

    public static bool IsRestorableUserScale(float scale)
        // Builds before the tiny 0.1375 parking scale used 0.55 as a parked scale. If a
        // plugin reload or missed restore captured that old parked value as the "user"
        // window scale, a magnifying-glass inspect would faithfully restore the tiny window
        // instead of making the native Achievement panel readable again. Treat parked/tiny
        // values as polluted restore state and fall back to 100% scale.
        => scale > LegacyParkedScale;
}
