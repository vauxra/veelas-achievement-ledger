using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;

namespace AchievementTracker.Services;

public unsafe sealed class PassiveAchievementProgressObserver : IDisposable
{
    private readonly ClientAchievementProgressSource progressSource;
    private readonly Hook<Achievement.Delegates.ReceiveAchievementProgress>? receiveHook;
    private readonly Hook<Achievement.Delegates.SetAchievementCompleted>? completedHook;
    private bool disposed;

    public PassiveAchievementProgressObserver(
        IGameInteropProvider interopProvider,
        ClientAchievementProgressSource progressSource)
    {
        this.progressSource = progressSource;

        try
        {
            // Passive observation only. These hooks forward the original client call and cache
            // progress already returned to the native Achievement UI.
            // https://dalamud.dev/plugin-development/interaction/
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
        this.receiveHook!.Original(thisPtr, id, current, max);
        this.progressSource.RecordObservedProgress(id, current, max, "Achievement window");
    }

    private void OnSetAchievementCompleted(Achievement* thisPtr, uint achievementId)
    {
        this.completedHook!.Original(thisPtr, achievementId);
        this.progressSource.RecordObservedCompletion(achievementId, "Achievement completed");
    }
}
