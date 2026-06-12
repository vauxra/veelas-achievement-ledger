using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AchievementTracker.Services;

public unsafe sealed class NativeAchievementNavigator
{
    public bool IsOpen
    {
        get
        {
            var agent = AgentAchievement.Instance();
            return agent != null && (agent->IsAgentActive() || agent->IsAddonShown());
        }
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

    public bool CloseAchievementWindow()
    {
        var agent = AgentAchievement.Instance();
        if (agent == null || !this.IsOpen)
        {
            return false;
        }

        agent->Hide();
        return true;
    }
}
