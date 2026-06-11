using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;

namespace AchievementTracker.Services;

// Component: passive native Achievement progress observation.
// Risk level: medium-high.
// Why: uses hooks around native client functions.
// Safety boundary: each hook calls the original client function first, then records what the client already received.
// It does not request progress, poll the server, or synthesize addon callbacks.
public unsafe sealed class PassiveAchievementProgressObserver : IDisposable
{
    private readonly ClientAchievementProgressSource progressSource;
    private readonly Func<bool> completionTriggerEnabledProvider;
    private readonly Hook<Achievement.Delegates.ReceiveAchievementProgress>? receiveHook;
    private readonly Hook<Achievement.Delegates.SetAchievementCompleted>? completedHook;
    private bool disposed;

    public PassiveAchievementProgressObserver(
        IGameInteropProvider interopProvider,
        ClientAchievementProgressSource progressSource,
        Func<bool> completionTriggerEnabledProvider)
    {
        this.progressSource = progressSource;
        this.completionTriggerEnabledProvider = completionTriggerEnabledProvider;

        try
        {
            this.receiveHook = interopProvider.HookFromAddress<Achievement.Delegates.ReceiveAchievementProgress>(
                Achievement.MemberFunctionPointers.ReceiveAchievementProgress,
                this.OnReceiveAchievementProgress);
            this.completedHook = interopProvider.HookFromAddress<Achievement.Delegates.SetAchievementCompleted>(
                Achievement.MemberFunctionPointers.SetAchievementCompleted,
                this.OnSetAchievementCompleted);

            this.receiveHook.Enable();
            this.completedHook.Enable();
        }
        catch
        {
            this.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.receiveHook?.Dispose();
        this.completedHook?.Dispose();
    }

    private void OnReceiveAchievementProgress(Achievement* thisPtr, uint id, uint current, uint max)
    {
        // Required safety rule: forward the original client function first.
        this.receiveHook!.Original(thisPtr, id, current, max);

        // Then cache the already-observed data for our UI.
        this.progressSource.RecordObservedProgress(id, current, max, "Achievement window");
    }

    private void OnSetAchievementCompleted(Achievement* thisPtr, uint achievementId)
    {
        // Required safety rule: forward the original client function first.
        this.completedHook!.Original(thisPtr, achievementId);

        if (this.completionTriggerEnabledProvider())
        {
            this.progressSource.RecordObservedCompletion(achievementId, "Achievement completed");
        }
    }
}
