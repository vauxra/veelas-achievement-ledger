using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AchievementTracker.Services;

public unsafe sealed class NativeAchievementNavigator
{
    public bool OpenAchievement(uint achievementId)
    {
        // Direct user action only: this asks the native Achievement agent to open the same game UI
        // a player would inspect manually. It does not call achievement progress request methods.
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
        // Direct user action only: hide the native Achievement agent/addon if it is open.
        // Agent/ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->Hide();
        return true;
    }
}
