namespace AchievementTracker.Models;

public sealed record AchievementInfo(
    uint Id,
    string Name,
    string Description,
    byte Points,
    string CategoryName,
    uint IconId);
