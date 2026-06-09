using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public sealed class ProgressRefreshQueue
{
    private readonly Queue<ProgressRefreshQueueItem> queue = new();
    private readonly HashSet<uint> queuedIds = new();
    private readonly Func<TimeSpan> nextJitter;
    private DateTimeOffset lastQueuedNotBefore = DateTimeOffset.MinValue;

    public ProgressRefreshQueue(Func<TimeSpan>? nextJitter = null)
    {
        this.nextJitter = nextJitter ?? DefaultJitter;
    }

    public int Count => this.queue.Count;

    public void Enqueue(IEnumerable<uint> achievementIds)
    {
        this.Enqueue(achievementIds, DateTimeOffset.UtcNow);
    }

    public void Enqueue(IEnumerable<uint> achievementIds, DateTimeOffset now)
    {
        var scheduledAfter = this.queue.Count == 0 || this.lastQueuedNotBefore < now
            ? now
            : this.lastQueuedNotBefore;

        foreach (var achievementId in achievementIds)
        {
            if (!this.queuedIds.Add(achievementId))
            {
                continue;
            }

            var jitter = this.nextJitter();
            if (jitter < TimeSpan.Zero)
            {
                jitter = TimeSpan.Zero;
            }

            var notBefore = scheduledAfter + jitter;
            this.queue.Enqueue(new ProgressRefreshQueueItem(achievementId, notBefore));
            this.lastQueuedNotBefore = notBefore;
            scheduledAfter = notBefore;
        }
    }

    public bool TryPeek(out uint achievementId)
    {
        if (this.queue.TryPeek(out var item))
        {
            achievementId = item.AchievementId;
            return true;
        }

        achievementId = 0;
        return false;
    }

    public bool TryPeekReady(DateTimeOffset now, out uint achievementId)
    {
        if (this.queue.TryPeek(out var item) && item.NotBefore <= now)
        {
            achievementId = item.AchievementId;
            return true;
        }

        achievementId = 0;
        return false;
    }

    public bool TryPeekNotBefore(out uint achievementId, out DateTimeOffset notBefore)
    {
        if (this.queue.TryPeek(out var item))
        {
            achievementId = item.AchievementId;
            notBefore = item.NotBefore;
            return true;
        }

        achievementId = 0;
        notBefore = DateTimeOffset.MinValue;
        return false;
    }

    public void Dequeue()
    {
        var item = this.queue.Dequeue();
        this.queuedIds.Remove(item.AchievementId);
        if (this.queue.Count == 0)
        {
            this.lastQueuedNotBefore = DateTimeOffset.MinValue;
        }
    }

    public void Clear()
    {
        this.queue.Clear();
        this.queuedIds.Clear();
        this.lastQueuedNotBefore = DateTimeOffset.MinValue;
    }

    private static TimeSpan DefaultJitter()
    {
        // Small random spacing avoids sending a whole tracked batch on consecutive frames.
        // This is still user-triggered; it only adds politeness/jitter to the queued requests.
        return TimeSpan.FromMilliseconds(Random.Shared.Next(250, 1251));
    }

    private readonly record struct ProgressRefreshQueueItem(uint AchievementId, DateTimeOffset NotBefore);
}
