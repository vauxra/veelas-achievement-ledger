using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AchievementTracker.Services;

// Component: native Achievement window navigation.
// Risk level: medium.
// Why: uses ClientStructs AgentAchievement to open/close the game UI.
// Safety boundary: methods are called from user button clicks only and do not call direct progress-request methods.
public unsafe sealed class NativeAchievementNavigator
{
    private readonly IGameGui gameGui;

    public NativeAchievementNavigator(IGameGui gameGui)
    {
        this.gameGui = gameGui;
    }

    public bool IsAchievementWindowOpen()
    {
        var agent = AgentAchievement.Instance();
        return agent != null && (agent->IsAgentActive() || agent->IsAddonShown());
    }

    public bool OpenAchievement(uint achievementId)
    {
        // What this does:
        // - Gets the native Achievement UI agent.
        // - Asks the game to open the visible Achievement entry for this row.
        // What this does NOT do:
        // - It does not call direct achievement-progress request API.
        // - It does not run in a background queue.
        // - It does not send plugin network/backend data.
        // Agent/ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->OpenById(achievementId);
        return true;
    }

    public bool CloseAchievements()
    {
        // What this does:
        // - Gets the native Achievement UI agent.
        // - Hides/closes that native UI if possible.
        // Safety: this is UI-only and user-triggered from the Close Achievements button.
        // Agent/ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->Hide();
        return true;
    }

    public bool RestoreDefaultScale()
    {
        // What this does:
        // - Opens/shows the native Achievement UI if needed.
        // - Resets the native Achievement addon scale to the game's default HUD-layout scale.
        // Safety: this is UI-only and user-triggered from the config window recovery button.
        // IGameGui/GetAddonByName docs: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IGameGui
        // AtkUnitBase ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->Show();
        var addon = this.gameGui.GetAddonByName<AtkUnitBase>("Achievement", 1);
        if (addon == null)
        {
            return false;
        }

        addon->SetScaleToHudLayoutScale();
        return true;
    }
}
