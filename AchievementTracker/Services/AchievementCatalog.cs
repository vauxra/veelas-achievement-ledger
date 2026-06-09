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

    public AchievementCatalog(IDataManager dataManager)
    {
        this.dataManager = dataManager;
    }

    public IEnumerable<AchievementInfo> Search(string query, int limit = 50)
    {
        var normalizedQuery = query.Trim();
        // IDataManager.GetExcelSheet<T>() docs:
        // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager
        var sheet = this.dataManager.GetExcelSheet<Achievement>();

        var results = sheet
            .Select(this.ToInfo)
            .Where(info => !string.IsNullOrWhiteSpace(info.Name));

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            results = results.Where(info => info.Name.Contains(normalizedQuery, StringComparison.CurrentCultureIgnoreCase));
        }

        return results
            .OrderBy(info => info.Name)
            .Take(limit)
            .ToList();
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

    private AchievementInfo ToInfo(Achievement achievement)
    {
        var categoryName = achievement.AchievementCategory.IsValid
            ? achievement.AchievementCategory.Value.Name.ToString()
            : string.Empty;

        return new AchievementInfo(
            achievement.RowId,
            achievement.Name.ToString(),
            achievement.Description.ToString(),
            achievement.Points,
            categoryName);
    }
}
