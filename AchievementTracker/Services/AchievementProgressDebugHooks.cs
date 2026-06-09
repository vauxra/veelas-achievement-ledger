using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Hooking;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;

namespace AchievementTracker.Services;

public unsafe sealed class AchievementProgressDebugHooks : IDisposable
{
    private static readonly AddonEvent[] AchievementAddonEvents =
    [
        AddonEvent.PostSetup,
        AddonEvent.PostRequestedUpdate,
        AddonEvent.PostRefresh,
        AddonEvent.PreReceiveEvent,
        AddonEvent.PostReceiveEvent,
        AddonEvent.PostOpen,
        AddonEvent.PostClose,
        AddonEvent.PostShow,
        AddonEvent.PostHide,
    ];

    private readonly IAddonLifecycle addonLifecycle;
    private readonly DebugLog debugLog;
    private readonly IFramework framework;
    private readonly ClientAchievementProgressSource progressSource;
    private readonly Hook<Achievement.Delegates.RequestAchievementProgress>? requestHook;
    private readonly Hook<Achievement.Delegates.ReceiveAchievementProgress>? receiveHook;
    private readonly Hook<Achievement.Delegates.SetAchievementCompleted>? completedHook;
    private readonly Hook<AgentAchievement.Delegates.OpenById>? agentOpenByIdHook;
    private Hook<AgentAchievement.Delegates.ReceiveEvent>? agentReceiveEventHook;
    private Hook<AgentAchievement.Delegates.OnGameEvent>? agentGameEventHook;
    private string? lastAgentState;
    private string? lastAchievementStateSnapshot;
    private string? lastAgentReceiveEventSignature;
    private string? lastAddonLifecycleSignature;
    private string? lastAgentGameEventSignature;
    private DateTime nextStateSampleUtc = DateTime.MinValue;
    private bool virtualHookInstallFailed;
    private bool disposed;

    public AchievementProgressDebugHooks(
        IGameInteropProvider interopProvider,
        IAddonLifecycle addonLifecycle,
        IFramework framework,
        DebugLog debugLog,
        ClientAchievementProgressSource progressSource)
    {
        this.addonLifecycle = addonLifecycle;
        this.debugLog = debugLog;
        this.framework = framework;
        this.progressSource = progressSource;

        try
        {
            // Debug-only observability for achievement progress flow. These hooks do not send requests;
            // they log the client methods that send a progress request, receive a requested progress
            // response, or mark an achievement complete. ClientStructs interaction docs:
            // docs/docs-cache/dalamud/plugin-development-interaction.md
            this.requestHook = interopProvider.HookFromAddress<Achievement.Delegates.RequestAchievementProgress>(
                Achievement.MemberFunctionPointers.RequestAchievementProgress,
                this.OnRequestAchievementProgress);
            this.receiveHook = interopProvider.HookFromAddress<Achievement.Delegates.ReceiveAchievementProgress>(
                Achievement.MemberFunctionPointers.ReceiveAchievementProgress,
                this.OnReceiveAchievementProgress);
            this.completedHook = interopProvider.HookFromAddress<Achievement.Delegates.SetAchievementCompleted>(
                Achievement.MemberFunctionPointers.SetAchievementCompleted,
                this.OnSetAchievementCompleted);
            this.agentOpenByIdHook = interopProvider.HookFromAddress<AgentAchievement.Delegates.OpenById>(
                AgentAchievement.MemberFunctionPointers.OpenById,
                this.OnAgentOpenById);

            this.requestHook.Enable();
            this.receiveHook.Enable();
            this.completedHook.Enable();
            this.agentOpenByIdHook.Enable();
            this.RegisterAddonLifecycleListeners();
            this.framework.Update += this.OnFrameworkUpdate;
            this.debugLog.Trace("ProgressHooks.Init", "enabled achievement progress hooks, AgentAchievement.OpenById hook, Achievement addon lifecycle listeners, and state sampler");
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
        this.debugLog.Trace("ProgressHooks.Dispose", "disposing achievement progress debug hooks");
        this.framework.Update -= this.OnFrameworkUpdate;
        this.UnregisterAddonLifecycleListeners();
        this.requestHook?.Dispose();
        this.receiveHook?.Dispose();
        this.completedHook?.Dispose();
        this.agentOpenByIdHook?.Dispose();
        this.agentReceiveEventHook?.Dispose();
        this.agentGameEventHook?.Dispose();
    }

    private void RegisterAddonLifecycleListeners()
    {
        foreach (var eventType in AchievementAddonEvents)
        {
            this.addonLifecycle.RegisterListener(eventType, "Achievement", this.OnAchievementAddonLifecycle);
        }
    }

    private void UnregisterAddonLifecycleListeners()
    {
        foreach (var eventType in AchievementAddonEvents)
        {
            this.addonLifecycle.UnregisterListener(eventType, "Achievement", this.OnAchievementAddonLifecycle);
        }
    }

    private void OnRequestAchievementProgress(Achievement* thisPtr, uint id)
    {
        if (thisPtr == null)
        {
            this.debugLog.Trace("ProgressHooks.OutgoingRequest", $"id={id} thisPtr=null; forwarding original without field reads");
            this.requestHook!.Original(thisPtr, id);
            return;
        }

        this.debugLog.Trace(
            "ProgressHooks.OutgoingRequest",
            $"id={id} state={thisPtr->State} progressStateBefore={thisPtr->ProgressRequestState} slotId={thisPtr->ProgressAchievementId} current={thisPtr->ProgressCurrent} max={thisPtr->ProgressMax}");

        this.requestHook!.Original(thisPtr, id);

        this.debugLog.Trace(
            "ProgressHooks.OutgoingRequest",
            $"id={id} progressStateAfter={thisPtr->ProgressRequestState} slotId={thisPtr->ProgressAchievementId} current={thisPtr->ProgressCurrent} max={thisPtr->ProgressMax}");
    }

    private void OnReceiveAchievementProgress(Achievement* thisPtr, uint id, uint current, uint max)
    {
        if (thisPtr == null)
        {
            this.debugLog.Trace("ProgressHooks.ReceiveProgress", $"id={id} current={current} max={max} thisPtr=null; forwarding original without field reads");
            this.receiveHook!.Original(thisPtr, id, current, max);
            return;
        }

        this.debugLog.Trace(
            "ProgressHooks.ReceiveProgress",
            $"id={id} current={current} max={max} stateBefore={thisPtr->State} progressStateBefore={thisPtr->ProgressRequestState} slotIdBefore={thisPtr->ProgressAchievementId} slotCurrentBefore={thisPtr->ProgressCurrent} slotMaxBefore={thisPtr->ProgressMax}");

        this.receiveHook!.Original(thisPtr, id, current, max);

        this.debugLog.Trace(
            "ProgressHooks.ReceiveProgress",
            $"id={id} stateAfter={thisPtr->State} progressStateAfter={thisPtr->ProgressRequestState} slotIdAfter={thisPtr->ProgressAchievementId} slotCurrentAfter={thisPtr->ProgressCurrent} slotMaxAfter={thisPtr->ProgressMax}");
        this.progressSource.RecordObservedProgress(id, current, max, "ReceiveAchievementProgress hook");
    }

    private void OnSetAchievementCompleted(Achievement* thisPtr, uint achievementId)
    {
        if (thisPtr == null)
        {
            this.debugLog.Trace("ProgressHooks.Complete", $"achievementId={achievementId} thisPtr=null; forwarding original without field reads");
            this.completedHook!.Original(thisPtr, achievementId);
            return;
        }

        this.debugLog.Trace(
            "ProgressHooks.Complete",
            $"achievementId={achievementId} stateBefore={thisPtr->State} isLoadedBefore={thisPtr->IsLoaded()} progressStateBefore={thisPtr->ProgressRequestState} slotIdBefore={thisPtr->ProgressAchievementId} currentBefore={thisPtr->ProgressCurrent} maxBefore={thisPtr->ProgressMax}");

        this.completedHook!.Original(thisPtr, achievementId);

        this.debugLog.Trace(
            "ProgressHooks.Complete",
            $"achievementId={achievementId} stateAfter={thisPtr->State} isLoadedAfter={thisPtr->IsLoaded()} progressStateAfter={thisPtr->ProgressRequestState} slotIdAfter={thisPtr->ProgressAchievementId} currentAfter={thisPtr->ProgressCurrent} maxAfter={thisPtr->ProgressMax}");
        this.progressSource.RecordObservedCompletion(achievementId, "SetAchievementCompleted hook");
    }

    private void OnAgentOpenById(AgentAchievement* thisPtr, uint achievementId)
    {
        if (thisPtr == null)
        {
            this.debugLog.Trace("AgentAchievement.OpenById", $"achievementId={achievementId} thisPtr=null; forwarding original without field reads");
            this.agentOpenByIdHook!.Original(thisPtr, achievementId);
            return;
        }

        this.debugLog.Trace("AgentAchievement.OpenById", $"achievementId={achievementId} addonId={thisPtr->AddonId} activeBefore={thisPtr->IsAgentActive()} shownBefore={thisPtr->IsAddonShown()} statusBefore={thisPtr->GetAddonStatus()}");
        this.agentOpenByIdHook!.Original(thisPtr, achievementId);
        this.debugLog.Trace("AgentAchievement.OpenById", $"achievementId={achievementId} activeAfter={thisPtr->IsAgentActive()} shownAfter={thisPtr->IsAddonShown()} statusAfter={thisPtr->GetAddonStatus()}");
    }

    private AtkValue* OnAgentReceiveEvent(AgentAchievement* thisPtr, AtkValue* returnValue, AtkValue* values, uint valueCount, ulong eventKind)
    {
        var signature = $"eventKind={eventKind} valueCount={valueCount} values=[{FormatAtkValues(values, valueCount)}]";
        if (this.lastAgentReceiveEventSignature != signature)
        {
            this.lastAgentReceiveEventSignature = signature;
            this.debugLog.Trace("AgentAchievement.ReceiveEvent", signature);
        }

        return this.agentReceiveEventHook!.Original(thisPtr, returnValue, values, valueCount, eventKind);
    }

    private void OnAgentGameEvent(AgentAchievement* thisPtr, AgentGameEvent gameEvent)
    {
        var signature = $"gameEvent={gameEvent}";
        if (this.lastAgentGameEventSignature != signature)
        {
            this.lastAgentGameEventSignature = signature;
            this.debugLog.Trace("AgentAchievement.OnGameEvent", signature);
        }

        this.agentGameEventHook!.Original(thisPtr, gameEvent);
    }

    private void OnAchievementAddonLifecycle(AddonEvent eventType, AddonArgs args)
    {
        var details = args switch
        {
            AddonSetupArgs setupArgs => $"atkValueCount={setupArgs.AtkValueCount} values=[{FormatAtkValues((AtkValue*)setupArgs.AtkValues, setupArgs.AtkValueCount)}]",
            AddonRefreshArgs refreshArgs => $"atkValueCount={refreshArgs.AtkValueCount} values=[{FormatAtkValues((AtkValue*)refreshArgs.AtkValues, refreshArgs.AtkValueCount)}]",
            AddonRequestedUpdateArgs requestedArgs => $"numberArray=0x{requestedArgs.NumberArrayData.ToInt64():X} stringArray=0x{requestedArgs.StringArrayData.ToInt64():X}",
            AddonReceiveEventArgs receiveArgs => $"atkEventType={receiveArgs.AtkEventType} eventParam={receiveArgs.EventParam} atkEvent=0x{receiveArgs.AtkEvent.ToInt64():X} atkEventData=0x{receiveArgs.AtkEventData.ToInt64():X}",
            _ => $"argsType={args.Type}",
        };

        var signature = $"event={eventType} addon={args.AddonName} {details}";
        if (this.lastAddonLifecycleSignature == signature)
        {
            return;
        }

        this.lastAddonLifecycleSignature = signature;
        this.debugLog.Trace("AchievementAddon.Lifecycle", signature);
    }

    private void OnFrameworkUpdate(IFramework updateFramework)
    {
        this.TryInstallAgentVirtualHooks();
        if (updateFramework.LastUpdateUTC < this.nextStateSampleUtc)
        {
            return;
        }

        this.nextStateSampleUtc = updateFramework.LastUpdateUTC.AddMilliseconds(500);
        this.SampleAgentAchievementState();
        this.SampleAchievementSystemState();
    }

    private void TryInstallAgentVirtualHooks()
    {
        if (this.virtualHookInstallFailed)
        {
            return;
        }

        if (this.agentReceiveEventHook != null && this.agentGameEventHook != null)
        {
            return;
        }

        var agent = AgentAchievement.Instance();
        if (agent == null || agent->VirtualTable == null)
        {
            return;
        }

        try
        {
            if (this.agentReceiveEventHook == null && agent->VirtualTable->ReceiveEvent != null)
            {
                this.agentReceiveEventHook = Plugin.GameInteropProvider.HookFromAddress<AgentAchievement.Delegates.ReceiveEvent>(
                    agent->VirtualTable->ReceiveEvent,
                    this.OnAgentReceiveEvent);
                this.agentReceiveEventHook.Enable();
                this.debugLog.Trace("AgentAchievement.Hook", "enabled ReceiveEvent virtual-table hook");
            }

            if (this.agentGameEventHook == null && agent->VirtualTable->OnGameEvent != null)
            {
                this.agentGameEventHook = Plugin.GameInteropProvider.HookFromAddress<AgentAchievement.Delegates.OnGameEvent>(
                    agent->VirtualTable->OnGameEvent,
                    this.OnAgentGameEvent);
                this.agentGameEventHook.Enable();
                this.debugLog.Trace("AgentAchievement.Hook", "enabled OnGameEvent virtual-table hook");
            }
        }
        catch (Exception ex)
        {
            this.agentReceiveEventHook?.Dispose();
            this.agentReceiveEventHook = null;
            this.agentGameEventHook?.Dispose();
            this.agentGameEventHook = null;
            this.virtualHookInstallFailed = true;
            this.debugLog.Trace("AgentAchievement.Hook", $"virtual-table hook install failed once; disabling virtual hook attempts error={ex.GetType().Name}: {ex.Message}");
        }
    }

    private void SampleAgentAchievementState()
    {
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            this.LogAgentStateIfChanged("instance=null");
            return;
        }

        this.LogAgentStateIfChanged($"addonId={agent->AddonId} active={agent->IsAgentActive()} shown={agent->IsAddonShown()} hidden={agent->IsAddonHidden()} ready={agent->IsAddonReady()} status={agent->GetAddonStatus()} contextMenuSelectedItemId={agent->ContextMenuSelectedItemId}");
    }

    private void LogAgentStateIfChanged(string state)
    {
        if (this.lastAgentState == state)
        {
            return;
        }

        this.lastAgentState = state;
        this.debugLog.Trace("AgentAchievement.State", state);
    }

    private void SampleAchievementSystemState()
    {
        var achievement = Achievement.Instance();
        if (achievement == null)
        {
            this.LogAchievementStateIfChanged("instance=null");
            return;
        }

        var history = string.Join(",", achievement->History.ToArray());
        this.LogAchievementStateIfChanged($"state={achievement->State} isLoaded={achievement->IsLoaded()} progressState={achievement->ProgressRequestState} slotId={achievement->ProgressAchievementId} current={achievement->ProgressCurrent} max={achievement->ProgressMax} history=[{history}]");
    }

    private void LogAchievementStateIfChanged(string state)
    {
        if (this.lastAchievementStateSnapshot == state)
        {
            return;
        }

        this.lastAchievementStateSnapshot = state;
        this.debugLog.Trace("AchievementSystem.State", state);
    }

    private static string FormatAtkValues(AtkValue* values, uint valueCount)
    {
        if (values == null || valueCount == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var count = Math.Min(valueCount, 8);
        for (var i = 0; i < count; i++)
        {
            var value = values[i];
            parts.Add(value.Type switch
            {
                AtkValueType.Bool => $"{i}:Bool={value.Bool}",
                AtkValueType.Int => $"{i}:Int={value.Int}",
                AtkValueType.UInt => $"{i}:UInt={value.UInt}",
                AtkValueType.Int64 => $"{i}:Int64={value.Int64}",
                AtkValueType.UInt64 => $"{i}:UInt64={value.UInt64}",
                AtkValueType.Float => $"{i}:Float={value.Float}",
                AtkValueType.String => $"{i}:String",
                _ => $"{i}:{value.Type}",
            });
        }

        if (valueCount > count)
        {
            parts.Add($"...+{valueCount - count}");
        }

        return string.Join(";", parts);
    }
}
