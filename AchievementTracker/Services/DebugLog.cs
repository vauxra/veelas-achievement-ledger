using Dalamud.Plugin.Services;
using System;

namespace AchievementTracker.Services;

public sealed class DebugLog
{
    private readonly IPluginLog log;
    private readonly Func<bool> isEnabled;

    public DebugLog(IPluginLog log, bool enabled)
        : this(log, () => enabled)
    {
    }

    public DebugLog(IPluginLog log, Func<bool> isEnabled)
    {
        this.log = log;
        this.isEnabled = isEnabled;
    }

    public bool Enabled => this.isEnabled();

    public void Trace(string category, string message)
    {
        if (!this.Enabled)
        {
            return;
        }

        this.log.Information("[DebugTrace {Time:O}] [{Category}] {Message}", DateTimeOffset.UtcNow, category, message);
    }
}
