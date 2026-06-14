namespace AchievementTracker.Services;

public static class NativeAchievementUpdateWindowPolicy
{
    public static bool ShouldParkDuringBatch(bool batchWindowWasOpenBeforeStart)
    {
        // Crash packs from repeated Update All native opens showed the game crashing in the
        // native Achievement addon's refresh path after the window had been shrunk/parked.
        // Keep batch OpenById refreshes geometry-neutral; manual recovery/reset controls can
        // still fix a previously parked window, but update batches should not mutate it.
        return false;
    }
}
