using System;

namespace AchievementTracker.Services;

public readonly record struct AchievementCategoryPath(string Category, string Subcategory)
{
    public static AchievementCategoryPath Parse(string? categoryPath)
    {
        // Lumina category names can be multi-level display paths. Achieve Ex+ only treats the
        // first segment as the broad category and the final segment as the selectable subcategory.
        if (string.IsNullOrWhiteSpace(categoryPath))
        {
            return new AchievementCategoryPath(string.Empty, string.Empty);
        }

        var parts = categoryPath.Split('>', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => new AchievementCategoryPath(string.Empty, string.Empty),
            1 => new AchievementCategoryPath(parts[0], string.Empty),
            _ => new AchievementCategoryPath(parts[0], parts[^1]),
        };
    }

    public static bool MatchesCategory(string categoryPath, string categoryName)
        => Parse(categoryPath).MatchesCategory(categoryName);

    public static string BuildSubcategoryFilterKey(string category, string subcategory)
        => $"{category}>{subcategory}";

    public bool MatchesCategory(string categoryName)
    {
        // Activity triggers name broad categories like "Miner". Match exact top-level/final
        // subcategory names only so paths like "Miner Impostor" do not fire miner refreshes.
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return false;
        }

        return string.Equals(this.Category, categoryName, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(this.Subcategory)
                && string.Equals(this.Subcategory, categoryName, StringComparison.OrdinalIgnoreCase));
    }
}
