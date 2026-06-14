using AchievementTracker.Models;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementCatalog
{
    private readonly IDataManager dataManager;
    private IReadOnlyList<AchievementInfo>? manuallyViewableAchievements;

    public AchievementCatalog(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    public IEnumerable<AchievementInfo> Search(string query, int limit = 50)
    {
        var normalizedQuery = query.Trim();
        var results = this.GetManuallyViewableAchievements().AsEnumerable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            results = results.Where(info =>
                info.Name.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase)
                || info.CategoryName.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase));
        }

        return results
            .OrderBy(info => info.Name)
            .Take(limit)
            .ToList();
    }

    public IReadOnlyList<AchievementInfo> GetManuallyViewableAchievements()
    {
        if (this.manuallyViewableAchievements is not null)
        {
            return this.manuallyViewableAchievements;
        }

        // IDataManager.GetExcelSheet<T>() docs:
        // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager
        var sheet = this.dataManager.GetExcelSheet<Achievement>();
        this.manuallyViewableAchievements = sheet
            .Where(this.IsManuallyViewable)
            .Select(this.ToInfo)
            .Where(info => !string.IsNullOrWhiteSpace(info.Name))
            .OrderBy(info => info.Name)
            .ToList();
        return this.manuallyViewableAchievements;
    }

    public bool TryGet(uint achievementId, out AchievementInfo achievementInfo)
    {
        // IDataManager.GetExcelSheet<T>() docs:
        // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager
        var sheet = this.dataManager.GetExcelSheet<Achievement>();
        if (sheet.TryGetRow(achievementId, out var achievement))
        {
            achievementInfo = this.ToInfo(achievement);
            return true;
        }

        achievementInfo = new AchievementInfo(achievementId, $"Unknown achievement #{achievementId}", string.Empty, 0, string.Empty);
        return false;
    }

    public bool TryGetRow(uint achievementId, out Achievement achievement)
    {
        // IDataManager.GetExcelSheet<T>() docs:
        // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager
        var sheet = this.dataManager.GetExcelSheet<Achievement>();
        return sheet.TryGetRow(achievementId, out achievement);
    }

    public bool IsManuallyViewable(uint achievementId)
    {
        if (!this.TryGetRow(achievementId, out var achievement))
        {
            return false;
        }

        return this.IsManuallyViewable(achievement);
    }

    private bool IsManuallyViewable(Achievement achievement)
    {
        if (!achievement.AchievementCategory.IsValid
            || achievement.AchievementCategory.Value.HideCategory)
        {
            return false;
        }

        if (achievement.AchievementHideCondition.IsValid)
        {
            var hideCondition = achievement.AchievementHideCondition.Value;
            if (hideCondition.HideAchievement || hideCondition.HideName)
            {
                return false;
            }
        }

        return !string.IsNullOrWhiteSpace(achievement.Name.ToString());
    }

    private AchievementInfo ToInfo(Achievement achievement)
    {
        var categoryName = achievement.AchievementCategory.IsValid
            ? achievement.AchievementCategory.Value.Name.ToString()
            : string.Empty;
        var categoryPath = categoryName;

        if (achievement.AchievementCategory.IsValid)
        {
            var kind = achievement.AchievementCategory.Value.AchievementKind;
            if (kind.IsValid)
            {
                var kindName = kind.Value.Name.ToString();
                if (!string.IsNullOrWhiteSpace(kindName) && !string.Equals(kindName, categoryName, StringComparison.Ordinal))
                {
                    categoryPath = string.IsNullOrWhiteSpace(categoryName)
                        ? kindName
                        : $"{kindName} > {categoryName}";
                }
            }
        }

        return new AchievementInfo(
            achievement.RowId,
            achievement.Name.ToString(),
            achievement.Description.ToString(),
            achievement.Points,
            categoryPath);
    }
}
