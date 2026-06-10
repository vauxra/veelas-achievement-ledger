using AchievementTracker.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public static class TrackedAchievementPresetStore
{
    public const int MaxPresetNameLength = 40;
    public const int MaxPresets = 50;

    public static string SanitizeName(string? rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return string.Empty;
        }

        var sanitized = new string(rawName
            .Where(c => !char.IsControl(c))
            .ToArray())
            .Trim();

        if (sanitized.Length > MaxPresetNameLength)
        {
            sanitized = sanitized[..MaxPresetNameLength].Trim();
        }

        return sanitized;
    }

    public static void Normalize(List<TrackedAchievementPreset> presets)
    {
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = presets.Count - 1; index >= 0; index--)
        {
            var preset = presets[index];
            preset.Name = SanitizeName(preset.Name);
            preset.AchievementIds = SanitizeAchievementIds(preset.AchievementIds);

            if (preset.Name.Length == 0 || preset.AchievementIds.Count == 0 || !seenNames.Add(preset.Name))
            {
                presets.RemoveAt(index);
            }
        }

        if (presets.Count > MaxPresets)
        {
            presets.RemoveRange(MaxPresets, presets.Count - MaxPresets);
        }
    }

    public static bool SavePreset(List<TrackedAchievementPreset> presets, string rawName, IEnumerable<uint> achievementIds, out string savedName)
    {
        savedName = SanitizeName(rawName);
        if (savedName.Length == 0)
        {
            return false;
        }

        var sanitizedIds = SanitizeAchievementIds(achievementIds);
        if (sanitizedIds.Count == 0)
        {
            return false;
        }

        var existing = FindPreset(presets, savedName);
        if (existing is not null)
        {
            existing.Name = savedName;
            existing.AchievementIds = sanitizedIds;
            return true;
        }

        if (presets.Count >= MaxPresets)
        {
            return false;
        }

        presets.Add(new TrackedAchievementPreset
        {
            Name = savedName,
            AchievementIds = sanitizedIds,
        });
        return true;
    }

    public static bool RenamePreset(List<TrackedAchievementPreset> presets, string currentName, string rawNewName, out string renamedTo)
    {
        renamedTo = SanitizeName(rawNewName);
        if (renamedTo.Length == 0)
        {
            return false;
        }

        var preset = FindPreset(presets, currentName);
        if (preset is null)
        {
            return false;
        }

        var newName = renamedTo;
        var nameConflict = presets.Any(existing =>
            !ReferenceEquals(existing, preset) && string.Equals(existing.Name, newName, StringComparison.OrdinalIgnoreCase));
        if (nameConflict)
        {
            return false;
        }

        preset.Name = renamedTo;
        return true;
    }

    public static bool DeletePreset(List<TrackedAchievementPreset> presets, string name)
    {
        var preset = FindPreset(presets, name);
        return preset is not null && presets.Remove(preset);
    }

    public static TrackedAchievementPreset? FindPreset(List<TrackedAchievementPreset> presets, string name)
        => presets.FirstOrDefault(preset => string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));

    private static List<uint> SanitizeAchievementIds(IEnumerable<uint> achievementIds)
    {
        var ids = new List<uint>();
        foreach (var achievementId in achievementIds)
        {
            if (achievementId == 0 || ids.Contains(achievementId))
            {
                continue;
            }

            ids.Add(achievementId);
            if (ids.Count >= TrackedAchievementStore.MaxTrackedAchievements)
            {
                break;
            }
        }

        return ids;
    }
}
