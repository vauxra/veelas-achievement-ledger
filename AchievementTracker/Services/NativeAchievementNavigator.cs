using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;

namespace AchievementTracker.Services;

public unsafe sealed class NativeAchievementNavigator
{
    private const string AchievementAddonName = "Achievement";
    public const float ParkedScale = 0.1375f;
    private const short ParkedX = 20;
    private const short ParkedY = 20;

    private readonly IGameGui gameGui;
    private ParkedAchievementWindowState? parkedState;
    private ParkedAchievementWindowState? lastUserWindowState;

    public NativeAchievementNavigator(IGameGui gameGui)
    {
        this.gameGui = gameGui;
    }

    public bool IsOpen
    {
        get
        {
            var agent = AgentAchievement.Instance();
            return agent != null && (agent->IsAgentActive() || agent->IsAddonShown());
        }
    }

    public bool HasParkedWindow => this.parkedState.HasValue;

    public bool IsVisible
    {
        get
        {
            var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
            return !addon.IsNull && addon.IsReady && addon.IsVisible && addon.Address != IntPtr.Zero;
        }
    }

    public bool IsAchievementWindowParked()
    {
        var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
        if (addon.IsNull || !addon.IsReady || !addon.IsVisible || addon.Address == IntPtr.Zero)
        {
            return false;
        }

        return IsParkedState(new ParkedAchievementWindowState(addon.X, addon.Y, addon.Scale));
    }

    public bool OpenAchievement(uint achievementId)
    {
        // This asks the native Achievement agent to open the same game UI a player would inspect
        // manually. It uses the native Achievement agent instead of direct progress request APIs.
        // Agent/ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->OpenById(achievementId);
        return true;
    }

    public bool ShowAchievementWindow()
    {
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->Show();
        return true;
    }

    public bool TryParkAchievementWindow()
    {
        var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
        if (addon.IsNull || !addon.IsReady || !addon.IsVisible || addon.Address == IntPtr.Zero)
        {
            return false;
        }

        var unitBase = (AtkUnitBase*)addon.Address;
        if (unitBase == null)
        {
            return false;
        }

        var currentState = new ParkedAchievementWindowState(addon.X, addon.Y, addon.Scale);
        if (!IsParkedState(currentState) && NativeAchievementWindowScalePolicy.IsRestorableUserScale(currentState.Scale))
        {
            this.lastUserWindowState = currentState;
            this.parkedState ??= currentState;
        }
        else
        {
            this.parkedState ??= this.lastUserWindowState;
        }

        unitBase->SetScale(ParkedScale, false);
        unitBase->SetPosition(ParkedX, ParkedY);
        return true;
    }

    public bool RestoreParkedAchievementWindow()
    {
        var stateToRestore = this.parkedState ?? this.lastUserWindowState;
        if (!stateToRestore.HasValue)
        {
            return false;
        }

        var state = stateToRestore.Value;
        if (!NativeAchievementWindowScalePolicy.IsRestorableUserScale(state.Scale))
        {
            this.parkedState = null;
            return this.ResetAchievementWindowScale();
        }

        var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
        if (addon.IsNull || !addon.IsReady || !addon.IsVisible || addon.Address == IntPtr.Zero)
        {
            return false;
        }

        var unitBase = (AtkUnitBase*)addon.Address;
        if (unitBase == null)
        {
            return false;
        }

        unitBase->SetScale(state.Scale, false);
        unitBase->SetPosition(state.X, state.Y);
        this.lastUserWindowState = state;
        this.parkedState = null;
        return true;
    }

    private static bool IsParkedState(ParkedAchievementWindowState state)
        => Math.Abs(state.Scale - ParkedScale) < 0.001f && state.X == ParkedX && state.Y == ParkedY;

    public bool ResetAchievementWindowScale()
    {
        var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
        if (addon.IsNull || !addon.IsReady || !addon.IsVisible || addon.Address == IntPtr.Zero)
        {
            return false;
        }

        var unitBase = (AtkUnitBase*)addon.Address;
        if (unitBase == null)
        {
            return false;
        }

        unitBase->SetScale(1.0f, false);
        this.lastUserWindowState = new ParkedAchievementWindowState(addon.X, addon.Y, 1.0f);
        this.parkedState = null;
        return true;
    }

    public bool RestoreParkedAchievementWindowOrResetScale()
        => this.RestoreParkedAchievementWindow() || this.ResetAchievementWindowScale();

    public bool CloseAchievementWindow(bool restoreParkedWindow = true)
    {
        var restored = restoreParkedWindow && this.RestoreParkedAchievementWindow();
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return restored;
        }

        agent->Hide();
        return true;
    }

    private readonly record struct ParkedAchievementWindowState(short X, short Y, float Scale);
}
