using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;

namespace AchievementTracker.Services;

public unsafe sealed class PassiveAchievementProgressObserver : IDisposable
{
    private readonly ClientAchievementProgressSource progressSource;
    private readonly Action<string> debugLog;
    private Hook<Achievement.Delegates.ReceiveAchievementProgress>? receiveProgressHook;
    private Hook<Achievement.Delegates.SetAchievementCompleted>? setCompletedHook;
    private bool disposed;

    public PassiveAchievementProgressObserver(
        IGameInteropProvider gameInteropProvider,
        ClientAchievementProgressSource progressSource,
        Action<string> debugLog)
    {
        this.progressSource = progressSource;
        this.debugLog = debugLog;

        try
        {
            var receiveProgressAddress = (nint)Achievement.MemberFunctionPointers.ReceiveAchievementProgress;
            var setCompletedAddress = (nint)Achievement.MemberFunctionPointers.SetAchievementCompleted;

            if (receiveProgressAddress != 0)
            {
                this.receiveProgressHook = gameInteropProvider.HookFromAddress<Achievement.Delegates.ReceiveAchievementProgress>(
                    receiveProgressAddress,
                    this.ReceiveAchievementProgressDetour);
                this.receiveProgressHook.Enable();
            }

            if (setCompletedAddress != 0)
            {
                this.setCompletedHook = gameInteropProvider.HookFromAddress<Achievement.Delegates.SetAchievementCompleted>(
                    setCompletedAddress,
                    this.SetAchievementCompletedDetour);
                this.setCompletedHook.Enable();
            }

            this.debugLog($"AchieveEx DebugTrace ProgressHookObserverInstalled receive={this.receiveProgressHook is not null} complete={this.setCompletedHook is not null}");
        }
        catch (Exception ex)
        {
            this.Dispose();
            this.debugLog($"AchieveEx DebugTrace ProgressHookObserverInstallFailed type={ex.GetType().Name} message={ex.Message}");
        }
    }

    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.disposed = true;
        this.receiveProgressHook?.Dispose();
        this.setCompletedHook?.Dispose();
        this.receiveProgressHook = null;
        this.setCompletedHook = null;
    }

    private void ReceiveAchievementProgressDetour(Achievement* thisPtr, uint achievementId, uint current, uint max)
    {
        this.receiveProgressHook!.Original(thisPtr, achievementId, current, max);
        if (thisPtr == null || max == 0)
        {
            return;
        }

        this.progressSource.RecordObservedProgress(achievementId, current, max, "ReceiveAchievementProgress hook");
        this.debugLog($"AchieveEx DebugTrace ProgressHookReceive id={achievementId} current={current} max={max}");
    }

    private void SetAchievementCompletedDetour(Achievement* thisPtr, uint achievementId)
    {
        this.setCompletedHook!.Original(thisPtr, achievementId);
        if (thisPtr == null || achievementId == 0)
        {
            return;
        }

        this.progressSource.RecordObservedCompletion(achievementId, "SetAchievementCompleted hook");
        this.debugLog($"AchieveEx DebugTrace ProgressHookComplete id={achievementId}");
    }
}
