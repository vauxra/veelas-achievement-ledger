using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public static class AchievementActivityUpdateClassifier
{
    public const string MiningTrigger = "Mining";
    public const string QuarryingTrigger = "Quarrying";
    public const string LoggingTrigger = "Logging";
    public const string HarvestingTrigger = "Harvesting";
    public const string FishingTrigger = "Fishing";
    public const string SpearfishingTrigger = "Spearfishing";
    public const string CraftingTrigger = "Crafting";

    private static readonly IReadOnlyDictionary<uint, string> ClassJobCategories = new Dictionary<uint, string>
    {
        [8] = "Carpenter",
        [9] = "Blacksmith",
        [10] = "Armorer",
        [11] = "Goldsmith",
        [12] = "Leatherworker",
        [13] = "Weaver",
        [14] = "Alchemist",
        [15] = "Culinarian",
        [16] = "Miner",
        [17] = "Botanist",
        [18] = "Fisher",
    };

    private static readonly HashSet<uint> FishingCompletionLogMessageIds = [1114, 1115, 3512, 3559];
    private static readonly HashSet<uint> SpearfishingCompletionLogMessageIds = [3576, 3577, 3579];
    private static readonly HashSet<uint> CraftingCompletionLogMessageIds = [1156, 1158];

    public static bool TryClassify(uint logMessageId, string messageText, uint currentClassJobId, out string categoryName)
        => TryClassify(logMessageId, messageText, currentClassJobId, out categoryName, out _);

    public static bool TryClassify(
        uint logMessageId,
        string messageText,
        uint currentClassJobId,
        out string categoryName,
        out string triggerName)
    {
        switch (logMessageId)
        {
            case 1067:
                categoryName = "Miner";
                triggerName = MiningTrigger;
                return true;
            case 1068:
                categoryName = "Miner";
                triggerName = QuarryingTrigger;
                return true;
            case 1069:
                categoryName = "Botanist";
                triggerName = LoggingTrigger;
                return true;
            case 1070:
                categoryName = "Botanist";
                triggerName = HarvestingTrigger;
                return true;
        }

        if (FishingCompletionLogMessageIds.Contains(logMessageId))
        {
            categoryName = "Fisher";
            triggerName = FishingTrigger;
            return true;
        }

        if (SpearfishingCompletionLogMessageIds.Contains(logMessageId))
        {
            categoryName = "Fisher";
            triggerName = SpearfishingTrigger;
            return true;
        }

        if (CraftingCompletionLogMessageIds.Contains(logMessageId)
            && TryGetCategoryForClassJob(currentClassJobId, out categoryName)
            && IsCrafterCategory(categoryName))
        {
            triggerName = CraftingTrigger;
            return true;
        }

        categoryName = string.Empty;
        triggerName = string.Empty;
        return false;
    }

    public static IReadOnlyList<uint> SelectTrackedIdsForCategory(
        IEnumerable<uint> trackedAchievementIds,
        Func<uint, string> categoryNameProvider,
        string categoryName)
    {
        var expectedSuffix = $" > {categoryName}";
        return trackedAchievementIds
            .Where(id => IsCategoryMatch(categoryNameProvider(id), categoryName, expectedSuffix))
            .Distinct()
            .ToList();
    }

    public static bool TryGetCategoryForClassJob(uint classJobId, out string categoryName)
        => ClassJobCategories.TryGetValue(classJobId, out categoryName!);


    private static bool IsCategoryMatch(string categoryPath, string categoryName, string expectedSuffix)
        => string.Equals(categoryPath, categoryName, StringComparison.OrdinalIgnoreCase)
            || categoryPath.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsCrafterCategory(string categoryName)
        => !string.Equals(categoryName, "Miner", StringComparison.Ordinal)
            && !string.Equals(categoryName, "Botanist", StringComparison.Ordinal)
            && !string.Equals(categoryName, "Fisher", StringComparison.Ordinal);
}
