using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System;

namespace AchievementTracker.Services;

public sealed class ActivityDebugSurfaces : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly DebugLog debugLog;
    private bool disposed;
    private string? lastChatSignature;
    private string? lastLogMessageSignature;

    public ActivityDebugSurfaces(
        IChatGui chatGui,
        IClientState clientState,
        ICondition condition,
        DebugLog debugLog)
    {
        this.chatGui = chatGui;
        this.clientState = clientState;
        this.condition = condition;
        this.debugLog = debugLog;

        this.chatGui.ChatMessageUnhandled += this.OnChatMessageUnhandled;
        this.chatGui.LogMessage += this.OnLogMessage;
        this.clientState.ClassJobChanged += this.OnClassJobChanged;
        this.clientState.LevelChanged += this.OnLevelChanged;
        this.clientState.TerritoryChanged += this.OnTerritoryChanged;
        this.clientState.MapIdChanged += this.OnMapIdChanged;
        this.clientState.InstanceChanged += this.OnInstanceChanged;
        this.condition.ConditionChange += this.OnConditionChange;

        this.debugLog.Trace(
            "ActivitySurfaces.Init",
            $"enabled chat/log-message/client-state/condition debug surfaces territory={this.clientState.TerritoryType} map={this.clientState.MapId} instance={this.clientState.Instance}");
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.chatGui.ChatMessageUnhandled -= this.OnChatMessageUnhandled;
        this.chatGui.LogMessage -= this.OnLogMessage;
        this.clientState.ClassJobChanged -= this.OnClassJobChanged;
        this.clientState.LevelChanged -= this.OnLevelChanged;
        this.clientState.TerritoryChanged -= this.OnTerritoryChanged;
        this.clientState.MapIdChanged -= this.OnMapIdChanged;
        this.clientState.InstanceChanged -= this.OnInstanceChanged;
        this.condition.ConditionChange -= this.OnConditionChange;
        this.debugLog.Trace("ActivitySurfaces.Dispose", "disposed chat/log-message/client-state/condition debug surfaces");
    }

    private void OnChatMessageUnhandled(IChatMessage message)
    {
        var signature = $"kind={message.LogKind} sourceKind={message.SourceKind} targetKind={message.TargetKind} handled={message.IsHandled} sender='{Sanitize(message.Sender.TextValue)}' message='{Sanitize(message.Message.TextValue)}'";
        if (this.lastChatSignature == signature)
        {
            return;
        }

        this.lastChatSignature = signature;
        this.debugLog.Trace("Activity.Chat", signature);
    }

    private void OnLogMessage(ILogMessage message)
    {
        var formatted = SafeFormatLogMessage(message);
        var signature = $"logMessageId={message.LogMessageId} parameterCount={message.ParameterCount} handled={message.IsHandled} formatted='{formatted}'";
        if (this.lastLogMessageSignature == signature)
        {
            return;
        }

        this.lastLogMessageSignature = signature;
        this.debugLog.Trace("Activity.LogMessage", signature);
    }

    private void OnClassJobChanged(uint classJobId)
    {
        this.debugLog.Trace("Activity.ClassJobChanged", $"classJobId={classJobId}");
    }

    private void OnLevelChanged(uint classJobId, uint level)
    {
        this.debugLog.Trace("Activity.LevelChanged", $"classJobId={classJobId} level={level}");
    }

    private void OnTerritoryChanged(uint territoryType)
    {
        this.debugLog.Trace("Activity.TerritoryChanged", $"territoryType={territoryType}");
    }

    private void OnMapIdChanged(uint mapId)
    {
        this.debugLog.Trace("Activity.MapIdChanged", $"mapId={mapId}");
    }

    private void OnInstanceChanged(uint instance)
    {
        this.debugLog.Trace("Activity.InstanceChanged", $"instance={instance}");
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        this.debugLog.Trace("Activity.Condition", $"flag={flag} value={value}");
    }

    private static string SafeFormatLogMessage(ILogMessage message)
    {
        try
        {
            return Sanitize(message.FormatLogMessageForDebugging().ToString());
        }
        catch (Exception ex)
        {
            return $"<format failed {ex.GetType().Name}: {Sanitize(ex.Message)}>";
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var sanitized = value.Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ').Trim();
        return sanitized.Length > 220 ? sanitized[..220] + "…" : sanitized;
    }
}
