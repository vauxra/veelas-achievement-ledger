namespace AchievementTracker.Services;

public static class NativeAchievementUpdateWindowPolicy
{
    public static bool ShouldParkDuringBatch(bool batchWindowWasOpenBeforeStart, bool completedAtLeastOneRequest)
    {
        // Crash packs from repeated Update All native opens showed the game crashing in the
        // native Achievement addon's refresh path after the window had been shrunk/parked
        // immediately during the first cold OpenById. Keep the first refresh geometry-neutral,
        // and only allow parking during the idle gap after at least one row has loaded or timed out.
        return !batchWindowWasOpenBeforeStart && completedAtLeastOneRequest;
    }
}
