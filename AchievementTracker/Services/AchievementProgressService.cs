using AchievementTracker.Models;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementProgressService
{
    private readonly IUnlockState unlockState;
    private readonly IAchievementProgressSource? progressSource;

    public AchievementProgressService(IUnlockState unlockState, IAchievementProgressSource? progressSource = null)
    {
        this.unlockState = unlockState;
        this.progressSource = progressSource;
    }

    public AchievementProgress GetProgress(Achievement achievement)
    {
        var requiredTarget = GetRequiredTarget(achievement);

        // IUnlockState achievement docs:
        // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IUnlockState
        if (this.IsComplete(achievement) || this.progressSource?.IsObservedComplete(achievement.RowId) == true)
        {
            return requiredTarget.HasValue
                ? AchievementProgress.Numeric(requiredTarget.Value, requiredTarget.Value)
                : AchievementProgress.Complete();
        }

        if (this.progressSource?.TryGetProgress(achievement.RowId, out var current, out var max) == true)
        {
            return AchievementProgress.Numeric((int)current, (int)max);
        }

        if (!this.unlockState.IsAchievementListLoaded)
        {
            return requiredTarget.HasValue
                ? AchievementProgress.TargetKnown(requiredTarget.Value)
                : AchievementProgress.CompletionListNotLoaded();
        }

        return requiredTarget.HasValue
            ? AchievementProgress.TargetKnown(requiredTarget.Value)
            : AchievementProgress.Incomplete();
    }

    public bool IsComplete(Achievement achievement)
        => this.unlockState.IsAchievementListLoaded && this.unlockState.IsAchievementComplete(achievement);

    private static int? GetRequiredTarget(Achievement achievement)
    {
        var firstDataRow = achievement.Data.FirstOrDefault();
        if (firstDataRow.RowId > 1 && firstDataRow.RowId <= int.MaxValue)
        {
            return (int)firstDataRow.RowId;
        }

        return null;
    }
}
