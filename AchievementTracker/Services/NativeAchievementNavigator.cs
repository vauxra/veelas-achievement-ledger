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

        this.parkedState ??= new ParkedAchievementWindowState(addon.X, addon.Y, addon.Scale);
        unitBase->SetScale(ParkedScale, false);
        unitBase->SetPosition(ParkedX, ParkedY);
        return true;
    }

    public bool RestoreParkedAchievementWindow()
    {
        if (!this.parkedState.HasValue)
        {
            return false;
        }

        var state = this.parkedState.Value;
        this.parkedState = null;
        var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
        if (addon.IsNull || addon.Address == IntPtr.Zero)
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
        return true;
    }

    public bool ResetAchievementWindowScale()
    {
        var addon = this.gameGui.GetAddonByName(AchievementAddonName, 1);
        if (addon.IsNull || addon.Address == IntPtr.Zero)
        {
            this.parkedState = null;
            return false;
        }

        var unitBase = (AtkUnitBase*)addon.Address;
        if (unitBase == null)
        {
            this.parkedState = null;
            return false;
        }

        unitBase->SetScale(1.0f, false);
        this.parkedState = null;
        return true;
    }

    public bool CloseAchievementWindow()
    {
        var restored = this.RestoreParkedAchievementWindow();
        var agent = AgentAchievement.Instance();
        if (agent == null || !this.IsOpen)
        {
            return restored;
        }

        agent->Hide();
        return true;
    }

    private readonly record struct ParkedAchievementWindowState(short X, short Y, float Scale);
}
