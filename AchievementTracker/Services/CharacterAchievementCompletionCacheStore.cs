using AchievementTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public static class CharacterAchievementCompletionCacheStore
{
    public static bool TryGet(IList<CharacterAchievementCompletionCache> caches, string characterKey, out CharacterAchievementCompletionCache cache)
    {
        cache = caches.FirstOrDefault(entry => string.Equals(entry.CharacterKey, characterKey, StringComparison.Ordinal))!;
        return cache is not null;
    }

    public static bool HasCache(IList<CharacterAchievementCompletionCache> caches, string characterKey)
        => !string.IsNullOrWhiteSpace(characterKey) && TryGet(caches, characterKey, out _);

    public static bool IsComplete(IList<CharacterAchievementCompletionCache> caches, string characterKey, uint achievementId)
        => TryGet(caches, characterKey, out var cache) && cache.CompletedAchievementIds.Contains(achievementId);

    public static void ReplaceSnapshot(IList<CharacterAchievementCompletionCache> caches, string characterKey, IEnumerable<uint> completedAchievementIds)
    {
        if (string.IsNullOrWhiteSpace(characterKey))
        {
            return;
        }

        var normalizedIds = completedAchievementIds
            .Where(id => id != 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        if (!TryGet(caches, characterKey, out var cache))
        {
            cache = new CharacterAchievementCompletionCache { CharacterKey = characterKey };
            caches.Add(cache);
        }

        cache.CompletedAchievementIds = normalizedIds;
        cache.LastUpdatedAt = DateTimeOffset.UtcNow;
    }

    public static List<CharacterAchievementCompletionCache> Normalize(List<CharacterAchievementCompletionCache>? caches)
    {
        var normalized = new List<CharacterAchievementCompletionCache>();
        if (caches is null)
        {
            return normalized;
        }

        foreach (var cache in caches)
        {
            if (cache is null || string.IsNullOrWhiteSpace(cache.CharacterKey))
            {
                continue;
            }

            ReplaceSnapshot(normalized, cache.CharacterKey, cache.CompletedAchievementIds ?? []);
        }

        return normalized;
    }
}
