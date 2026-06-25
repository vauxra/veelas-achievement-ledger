using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public readonly record struct NativeAchievementOpenEligibility(bool CanOpen, string Reason)
{
    public static NativeAchievementOpenEligibility Eligible { get; } = new(true, string.Empty);

    public static NativeAchievementOpenEligibility Ineligible(string reason)
        => new(false, reason);
}

public readonly record struct NativeUnsafeAchievementSkip(uint AchievementId, string Reason);

public sealed record UpdateEligibilityResult(
    IReadOnlyList<uint> EligibleAchievementIds,
    IReadOnlyList<uint> CompletedAchievementIds,
    IReadOnlyList<NativeUnsafeAchievementSkip> NativeUnsafeAchievementIds,
    IReadOnlyList<uint> AutoUpdateAchievementIdsToRemove)
{
    public bool ShouldRemoveAutoUpdateEntries => this.AutoUpdateAchievementIdsToRemove.Count > 0;
}

public static class UpdateEligibilityPolicy
{
    public static UpdateEligibilityResult Evaluate(
        IEnumerable<uint> achievementIds,
        Func<uint, NativeAchievementOpenEligibility> nativeEligibilityProvider,
        Func<uint, bool> completionProvider,
        IReadOnlyCollection<uint> autoUpdateAchievementIds)
    {
        ArgumentNullException.ThrowIfNull(achievementIds);
        ArgumentNullException.ThrowIfNull(nativeEligibilityProvider);
        ArgumentNullException.ThrowIfNull(completionProvider);
        ArgumentNullException.ThrowIfNull(autoUpdateAchievementIds);

        // Keep this service pure: it reports which config entries should be removed, but Plugin
        // applies config mutation/save/reset side effects because those depend on caller intent.
        var eligibleAchievementIds = new List<uint>();
        var completedAchievementIds = new List<uint>();
        var nativeUnsafeAchievementIds = new List<NativeUnsafeAchievementSkip>();
        var autoUpdateAchievementIdsToRemove = new List<uint>();
        var configuredAutoUpdateIds = autoUpdateAchievementIds.ToHashSet();

        foreach (var achievementId in achievementIds.Where(id => id != 0).Distinct())
        {
            var nativeEligibility = nativeEligibilityProvider(achievementId);
            if (!nativeEligibility.CanOpen)
            {
                nativeUnsafeAchievementIds.Add(new NativeUnsafeAchievementSkip(achievementId, nativeEligibility.Reason));
                AddAutoUpdateRemovalIfNeeded(achievementId, configuredAutoUpdateIds, autoUpdateAchievementIdsToRemove);
                continue;
            }

            if (completionProvider(achievementId))
            {
                completedAchievementIds.Add(achievementId);
                AddAutoUpdateRemovalIfNeeded(achievementId, configuredAutoUpdateIds, autoUpdateAchievementIdsToRemove);
                continue;
            }

            eligibleAchievementIds.Add(achievementId);
        }

        return new UpdateEligibilityResult(
            eligibleAchievementIds,
            completedAchievementIds,
            nativeUnsafeAchievementIds,
            autoUpdateAchievementIdsToRemove);
    }

    private static void AddAutoUpdateRemovalIfNeeded(uint achievementId, HashSet<uint> configuredAutoUpdateIds, List<uint> autoUpdateAchievementIdsToRemove)
    {
        if (configuredAutoUpdateIds.Contains(achievementId))
        {
            autoUpdateAchievementIdsToRemove.Add(achievementId);
        }
    }
}
