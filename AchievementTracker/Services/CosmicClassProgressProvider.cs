using AchievementTracker.Models;
using FFXIVClientStructs.FFXIV.Client.Game.WKS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AchievementTracker.Services;

public sealed partial class CosmicClassProgressProvider
{
    public const int ScoreCount = 11;
    private const int Carpenter = 0;
    private const int Blacksmith = 1;
    private const int Armorer = 2;
    private const int Goldsmith = 3;
    private const int Leatherworker = 4;
    private const int Weaver = 5;
    private const int Alchemist = 6;
    private const int Culinarian = 7;
    private const int Miner = 8;
    private const int Botanist = 9;
    private const int Fisher = 10;

    private static readonly int[] DiscipleOfHand = [Carpenter, Blacksmith, Armorer, Goldsmith, Leatherworker, Weaver, Alchemist, Culinarian];
    private static readonly int[] DiscipleOfLand = [Miner, Botanist, Fisher];
    private static readonly int[] AllClasses = [Carpenter, Blacksmith, Armorer, Goldsmith, Leatherworker, Weaver, Alchemist, Culinarian, Miner, Botanist, Fisher];
    private static readonly Dictionary<string, int> JobNameToIndex = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Carpenter"] = Carpenter,
        ["Blacksmith"] = Blacksmith,
        ["Armorer"] = Armorer,
        ["Goldsmith"] = Goldsmith,
        ["Leatherworker"] = Leatherworker,
        ["Weaver"] = Weaver,
        ["Alchemist"] = Alchemist,
        ["Culinarian"] = Culinarian,
        ["Miner"] = Miner,
        ["Botanist"] = Botanist,
        ["Fisher"] = Fisher,
    };

    private readonly CosmicClassScoreCache cache;
    private readonly Action saveCache;

    public CosmicClassProgressProvider(CosmicClassScoreCache cache, Action saveCache)
    {
        this.cache = cache;
        this.saveCache = saveCache;
        NormalizeCache(this.cache);
    }

    public bool Handles(uint achievementId) => GetRule(achievementId) is not null;

    public bool Handles(Lumina.Excel.Sheets.Achievement achievement)
        => TryCreateRuleFromAchievementDetails(achievement, out _) || this.Handles(achievement.RowId);

    public void RefreshCacheFromLiveScores() => _ = this.TryReadLiveScores();

    public AchievementProgress GetProgress(uint achievementId)
    {
        var rule = GetRule(achievementId);
        return rule is null ? AchievementProgress.DataNotAvailable() : this.GetProgress(rule);
    }

    public AchievementProgress GetProgress(Lumina.Excel.Sheets.Achievement achievement)
    {
        var rule = TryCreateRuleFromAchievementDetails(achievement, out var detailRule)
            ? detailRule
            : GetRule(achievement.RowId);
        return rule is null ? AchievementProgress.DataNotAvailable() : this.GetProgress(rule);
    }

    public static bool TryCreateProgressOverride(string categoryName, string description, IReadOnlyList<int> scores, out AchievementProgress progress)
    {
        progress = AchievementProgress.DataNotAvailable();
        if (!TryCreateRuleFromDetails(categoryName, description, out var rule) || scores.Count < ScoreCount)
        {
            return false;
        }

        progress = BuildProgress(rule, scores);
        return true;
    }

    private AchievementProgress GetProgress(CosmicAchievementRule rule)
    {
        var scores = this.TryReadLiveScores() ?? this.TryReadCachedScores();
        return scores is null ? AchievementProgress.DataNotAvailable() : BuildProgress(rule, scores);
    }

    private static AchievementProgress BuildProgress(CosmicAchievementRule rule, IReadOnlyList<int> scores)
    {
        var current = rule.Aggregation == CosmicScoreAggregation.Minimum
            ? rule.ScoreIndexes.Min(index => scores[index])
            : rule.ScoreIndexes.Max(index => scores[index]);
        return AchievementProgress.Numeric(Math.Max(0, current), rule.TargetScore);
    }

    public string GetDiagnostics()
    {
        var liveScores = this.TryReadLiveScores();
        var cachedScores = this.TryReadCachedScores();
        var liveState = this.GetLiveStateSummary();
        var cachedState = cachedScores is null
            ? "cache=empty"
            : $"cache={string.Join(", ", cachedScores)} updated={this.cache.UpdatedAtUtc?.ToString("u") ?? "unknown"}";
        var source = liveScores is not null ? "live" : cachedScores is not null ? "cache" : "unavailable";
        var scores = liveScores ?? cachedScores;
        var scoreText = scores is null ? "scores=Data not available" : $"scores={string.Join(", ", scores)}";
        return $"source={source}; {liveState}; {cachedState}; {scoreText}";
    }


    private static bool TryCreateRuleFromAchievementDetails(Lumina.Excel.Sheets.Achievement achievement, out CosmicAchievementRule rule)
    {
        var categoryName = achievement.AchievementCategory.IsValid
            ? achievement.AchievementCategory.Value.Name.ToString()
            : string.Empty;
        return TryCreateRuleFromDetails(categoryName, achievement.Description.ToString(), out rule);
    }

    private static bool TryCreateRuleFromDetails(string categoryName, string description, out CosmicAchievementRule rule)
    {
        rule = default!;
        if (string.IsNullOrWhiteSpace(description) || !IsCosmicScoreDescription(description))
        {
            return false;
        }

        var targetMatch = CosmicTargetRegex().Match(description);
        if (!targetMatch.Success || !int.TryParse(targetMatch.Groups[1].Value.Replace(",", string.Empty), out var target))
        {
            return false;
        }

        var indexes = GetScoreIndexesForCategory(categoryName);
        if (indexes.Length == 0)
        {
            indexes = GetScoreIndexesForDescription(description);
        }

        if (indexes.Length == 0)
        {
            return false;
        }

        var aggregation = description.IndexOf("all ", StringComparison.OrdinalIgnoreCase) >= 0
            || description.IndexOf("each ", StringComparison.OrdinalIgnoreCase) >= 0
                ? CosmicScoreAggregation.Minimum
                : CosmicScoreAggregation.Maximum;
        rule = new CosmicAchievementRule(indexes, target, aggregation);
        return true;
    }

    private static int[] GetScoreIndexesForDescription(string description)
        => JobNameToIndex.FirstOrDefault(pair => description.Contains(pair.Key, StringComparison.OrdinalIgnoreCase)).Value is var index && index >= 0
            ? [index]
            : description.Contains("Disciple of the Hand", StringComparison.OrdinalIgnoreCase) || description.Contains("hand", StringComparison.OrdinalIgnoreCase)
                ? DiscipleOfHand
                : description.Contains("Disciple of the Land", StringComparison.OrdinalIgnoreCase) || description.Contains("land", StringComparison.OrdinalIgnoreCase)
                    ? DiscipleOfLand
                    : [];

    private static bool IsCosmicScoreDescription(string description)
        => description.Contains("cosmic class score", StringComparison.OrdinalIgnoreCase)
            || description.Contains("tool mastery points", StringComparison.OrdinalIgnoreCase);

    private static int[] GetScoreIndexesForCategory(string categoryName)
        => JobNameToIndex.TryGetValue(categoryName, out var index)
            ? [index]
            : categoryName.Contains("Hand", StringComparison.OrdinalIgnoreCase)
                ? DiscipleOfHand
                : categoryName.Contains("Land", StringComparison.OrdinalIgnoreCase)
                    ? DiscipleOfLand
                    : [];

    [GeneratedRegex(@"([0-9][0-9,]*).*points", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CosmicTargetRegex();

    public static bool IsCosmicClassAchievement(uint achievementId) => GetRule(achievementId) is not null;

    private static CosmicAchievementRule? GetRule(uint achievementId)
    {
        return achievementId switch
        {
            3702 => Single(Carpenter, 50_000),
            3703 => Single(Carpenter, 150_000),
            3704 => Single(Carpenter, 500_000),
            3705 => Single(Blacksmith, 50_000),
            3706 => Single(Blacksmith, 150_000),
            3707 => Single(Blacksmith, 500_000),
            3708 => Single(Armorer, 50_000),
            3709 => Single(Armorer, 150_000),
            3710 => Single(Armorer, 500_000),
            3711 => Single(Goldsmith, 50_000),
            3712 => Single(Goldsmith, 150_000),
            3713 => Single(Goldsmith, 500_000),
            3714 => Single(Leatherworker, 50_000),
            3715 => Single(Leatherworker, 150_000),
            3716 => Single(Leatherworker, 500_000),
            3717 => Single(Weaver, 50_000),
            3718 => Single(Weaver, 150_000),
            3719 => Single(Weaver, 500_000),
            3720 => Single(Alchemist, 50_000),
            3721 => Single(Alchemist, 150_000),
            3722 => Single(Alchemist, 500_000),
            3723 => Single(Culinarian, 50_000),
            3724 => Single(Culinarian, 150_000),
            3725 => Single(Culinarian, 500_000),
            3726 => Single(Miner, 50_000),
            3727 => Single(Miner, 150_000),
            3728 => Single(Miner, 500_000),
            3729 => Single(Botanist, 50_000),
            3730 => Single(Botanist, 150_000),
            3731 => Single(Botanist, 500_000),
            3732 => Single(Fisher, 50_000),
            3733 => Single(Fisher, 150_000),
            3734 => Single(Fisher, 500_000),
            3735 => Any(DiscipleOfHand, 50_000),
            3736 => Any(DiscipleOfLand, 50_000),
            3737 => Every(DiscipleOfHand, 500_000),
            3738 => Every(DiscipleOfLand, 500_000),
            3739 => Every(AllClasses, 500_000),
            _ => null,
        };
    }

    private static CosmicAchievementRule Single(int index, int target) => new([index], target, CosmicScoreAggregation.Maximum);

    private static CosmicAchievementRule Any(int[] indexes, int target) => new(indexes, target, CosmicScoreAggregation.Maximum);

    private static CosmicAchievementRule Every(int[] indexes, int target) => new(indexes, target, CosmicScoreAggregation.Minimum);

    private static void NormalizeCache(CosmicClassScoreCache scoreCache)
    {
        scoreCache.Scores ??= [];
        scoreCache.Scores.RemoveAll(score => score < 0);
        while (scoreCache.Scores.Count > ScoreCount)
        {
            scoreCache.Scores.RemoveAt(scoreCache.Scores.Count - 1);
        }
    }

    private int[]? TryReadCachedScores()
    {
        NormalizeCache(this.cache);
        if (this.cache.UpdatedAtUtc is null || this.cache.Scores.Count != ScoreCount)
        {
            return null;
        }

        return this.cache.Scores.ToArray();
    }

    private unsafe int[]? TryReadLiveScores(bool saveWhenAvailable = true)
    {
        var manager = WKSManager.Instance();
        if (manager is null || !manager->IsLoaded)
        {
            return null;
        }

        var liveScores = manager->State.Scores.ToArray();
        if (liveScores.Length < ScoreCount)
        {
            return null;
        }

        liveScores = liveScores.Take(ScoreCount).Select(score => Math.Max(0, score)).ToArray();
        if (saveWhenAvailable && !this.ScoresEqualCache(liveScores))
        {
            this.cache.Scores = liveScores.ToList();
            this.cache.UpdatedAtUtc = DateTimeOffset.UtcNow;
            this.saveCache();
        }

        return liveScores;
    }

    private unsafe string GetLiveStateSummary()
    {
        var manager = WKSManager.Instance();
        if (manager is null)
        {
            return "live=manager-null";
        }

        return $"live=isLoaded={manager->IsLoaded} territory={manager->TerritoryId} devGrade={manager->State.DevGrade}";
    }

    private bool ScoresEqualCache(IReadOnlyList<int> liveScores)
    {
        if (this.cache.Scores.Count != ScoreCount)
        {
            return false;
        }

        for (var i = 0; i < ScoreCount; i++)
        {
            if (this.cache.Scores[i] != liveScores[i])
            {
                return false;
            }
        }

        return true;
    }

    private sealed record CosmicAchievementRule(int[] ScoreIndexes, int TargetScore, CosmicScoreAggregation Aggregation);

    private enum CosmicScoreAggregation
    {
        Maximum,
        Minimum,
    }
}
