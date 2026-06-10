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
    public const string CraftingLogTrigger = "CraftingLog";

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
    private static readonly HashSet<uint> CraftingCompletionLogMessageIds = [1158, 1159];
    private static readonly HashSet<uint> CraftingLogCompletionLogMessageIds = [1178];

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

        if (CraftingLogCompletionLogMessageIds.Contains(logMessageId)
            && TryGetCategoryForClassJob(currentClassJobId, out categoryName)
            && IsCrafterCategory(categoryName))
        {
            triggerName = CraftingLogTrigger;
            return true;
        }

        return TryClassifyByText(messageText, currentClassJobId, out categoryName, out triggerName);
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

    private static bool TryClassifyByText(string messageText, uint currentClassJobId, out string categoryName, out string triggerName)
    {
        var normalized = messageText.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            categoryName = string.Empty;
            triggerName = string.Empty;
            return false;
        }

        if (normalized.Contains("finish mining", StringComparison.Ordinal))
        {
            categoryName = "Miner";
            triggerName = MiningTrigger;
            return true;
        }

        if (normalized.Contains("finish quarrying", StringComparison.Ordinal))
        {
            categoryName = "Miner";
            triggerName = QuarryingTrigger;
            return true;
        }

        if (normalized.Contains("finish logging", StringComparison.Ordinal))
        {
            categoryName = "Botanist";
            triggerName = LoggingTrigger;
            return true;
        }

        if (normalized.Contains("finish harvesting", StringComparison.Ordinal))
        {
            categoryName = "Botanist";
            triggerName = HarvestingTrigger;
            return true;
        }

        if (normalized.Contains("finish gathering", StringComparison.Ordinal)
            && TryGetCategoryForClassJob(currentClassJobId, out categoryName)
            && (string.Equals(categoryName, "Miner", StringComparison.Ordinal)
                || string.Equals(categoryName, "Botanist", StringComparison.Ordinal)))
        {
            triggerName = categoryName == "Miner" ? MiningTrigger : HarvestingTrigger;
            return true;
        }

        if ((normalized.Contains("finish fishing", StringComparison.Ordinal)
                || normalized.Contains("reel in", StringComparison.Ordinal)
                || normalized.Contains("catch", StringComparison.Ordinal))
            && TryGetCategoryForClassJob(currentClassJobId, out categoryName)
            && string.Equals(categoryName, "Fisher", StringComparison.Ordinal))
        {
            triggerName = FishingTrigger;
            return true;
        }

        if ((normalized.Contains("synthesis", StringComparison.Ordinal)
                || normalized.Contains("synthesize", StringComparison.Ordinal)
                || normalized.Contains("craft", StringComparison.Ordinal))
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

    private static bool IsCategoryMatch(string categoryPath, string categoryName, string expectedSuffix)
        => string.Equals(categoryPath, categoryName, StringComparison.OrdinalIgnoreCase)
            || categoryPath.EndsWith(expectedSuffix, StringComparison.OrdinalIgnoreCase);

    private static bool IsCrafterCategory(string categoryName)
        => !string.Equals(categoryName, "Miner", StringComparison.Ordinal)
            && !string.Equals(categoryName, "Botanist", StringComparison.Ordinal)
            && !string.Equals(categoryName, "Fisher", StringComparison.Ordinal);
}
