using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public sealed class ProgressRequestThrottler
{
    private readonly TimeSpan cooldown;
    private readonly Dictionary<uint, DateTimeOffset> lastRequests = new();

    public ProgressRequestThrottler(TimeSpan cooldown)
    {
        this.cooldown = cooldown;
    }

    public bool CanRequest(uint achievementId, DateTimeOffset now)
    {
        return !this.lastRequests.TryGetValue(achievementId, out var lastRequest) || now - lastRequest >= this.cooldown;
    }

    public void MarkRequest(uint achievementId, DateTimeOffset now)
    {
        this.lastRequests[achievementId] = now;
    }

    public bool TryMarkRequest(uint achievementId, DateTimeOffset now)
    {
        if (!this.CanRequest(achievementId, now))
        {
            return false;
        }

        this.MarkRequest(achievementId, now);
        return true;
    }

    public void Clear()
    {
        this.lastRequests.Clear();
    }
}
