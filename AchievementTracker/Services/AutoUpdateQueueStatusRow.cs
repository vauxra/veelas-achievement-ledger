using System;

namespace AchievementTracker.Services;

public static class AutoUpdateQueueStatusRow
{
    public static string Format(bool isRunning, int tasksLeft, TimeSpan? elapsed)
    {
        var normalizedTasksLeft = Math.Max(0, tasksLeft);
        var normalizedElapsed = elapsed.GetValueOrDefault(TimeSpan.Zero);
        if (normalizedElapsed < TimeSpan.Zero)
        {
            normalizedElapsed = TimeSpan.Zero;
        }

        var state = isRunning ? "Running" : "Idle";
        var taskWord = normalizedTasksLeft == 1 ? "task" : "tasks";
        return $"Status: {state} — {normalizedTasksLeft} {taskWord} left — running {FormatElapsed(normalizedElapsed)}";
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        var totalSeconds = Math.Max(0, (int)Math.Floor(elapsed.TotalSeconds));
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours}h {minutes:00}m {seconds:00}s";
        }

        if (minutes > 0)
        {
            return $"{minutes}m {seconds:00}s";
        }

        return $"{seconds}s";
    }
}
