using Dalamud.Game.Chat;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker.Services;

public sealed class AchievementActivityUpdateObserver : IDisposable
{
    private readonly IChatGui chatGui;
    private readonly Func<IReadOnlyList<uint>> trackedIdsProvider;
    private readonly Func<uint, string> categoryNameProvider;
    private readonly Func<uint> currentClassJobIdProvider;
    private readonly Func<string, bool> triggerEnabledProvider;
    private readonly Action<IEnumerable<uint>, string> enqueueUpdate;
    private readonly Action<string> debugLog;
    private readonly Dictionary<string, DateTimeOffset> lastQueuedAtByCategory = new(StringComparer.Ordinal);
    private bool disposed;

    public AchievementActivityUpdateObserver(
        IChatGui chatGui,
        Func<IReadOnlyList<uint>> trackedIdsProvider,
        Func<uint, string> categoryNameProvider,
        Func<uint> currentClassJobIdProvider,
        Func<string, bool> triggerEnabledProvider,
        Action<IEnumerable<uint>, string> enqueueUpdate,
        Action<string> debugLog)
    {
        this.chatGui = chatGui;
        this.trackedIdsProvider = trackedIdsProvider;
        this.categoryNameProvider = categoryNameProvider;
        this.currentClassJobIdProvider = currentClassJobIdProvider;
        this.triggerEnabledProvider = triggerEnabledProvider;
        this.enqueueUpdate = enqueueUpdate;
        this.debugLog = debugLog;

        this.chatGui.LogMessage += this.OnLogMessage;
        this.chatGui.ChatMessageUnhandled += this.OnChatMessageUnhandled;
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.chatGui.LogMessage -= this.OnLogMessage;
        this.chatGui.ChatMessageUnhandled -= this.OnChatMessageUnhandled;
    }

    private void OnLogMessage(ILogMessage message)
    {
        this.TryQueueCategoryUpdate(
            message.LogMessageId,
            message.FormatLogMessageForDebugging().ToString(),
            "activity-log-message");
    }

    private void OnChatMessageUnhandled(IChatMessage message)
    {
        this.TryQueueCategoryUpdate(
            0,
            message.Message.TextValue,
            "activity-chat-message");
    }

    private void TryQueueCategoryUpdate(uint logMessageId, string messageText, string reason)
    {
        var currentClassJobId = this.currentClassJobIdProvider();
        if (!AchievementActivityUpdateClassifier.TryClassify(logMessageId, messageText, currentClassJobId, out var categoryName, out var triggerName))
        {
            return;
        }

        var logMarker = logMessageId == 0 ? "chat" : logMessageId.ToString();
        var preview = messageText.Replace('\n', ' ').Replace('\r', ' ');
        if (preview.Length > 96)
        {
            preview = preview[..96];
        }

        if (!this.triggerEnabledProvider(triggerName))
        {
            this.debugLog($"AchieveEx DebugTrace ActivityUpdateDisabled reason={reason} logId={logMarker} category={categoryName} trigger={triggerName} currentClassJob={currentClassJobId} text='{preview}'");
            return;
        }

        var matchingIds = AchievementActivityUpdateClassifier.SelectTrackedIdsForCategory(
            this.trackedIdsProvider(),
            this.categoryNameProvider,
            categoryName);

        if (matchingIds.Count == 0)
        {
            this.debugLog($"AchieveEx DebugTrace ActivityUpdateSkip reason={reason} logId={logMarker} category={categoryName} trigger={triggerName} currentClassJob={currentClassJobId} no tracked matches text='{preview}'");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (this.lastQueuedAtByCategory.TryGetValue(categoryName, out var lastQueuedAt)
            && now - lastQueuedAt < TimeSpan.FromSeconds(2))
        {
            this.debugLog($"AchieveEx DebugTrace ActivityUpdateDedup reason={reason} logId={logMarker} category={categoryName} trigger={triggerName} currentClassJob={currentClassJobId} count={matchingIds.Count} text='{preview}'");
            return;
        }

        this.lastQueuedAtByCategory[categoryName] = now;
        this.debugLog($"AchieveEx DebugTrace ActivityUpdateQueue reason={reason} logId={logMarker} category={categoryName} trigger={triggerName} currentClassJob={currentClassJobId} count={matchingIds.Count} text='{preview}'");
        this.enqueueUpdate(matchingIds, $"{reason}-{categoryName}");
    }
}
