using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AchievementTracker.Services;

public unsafe sealed class NativeAchievementNavigator
{
    private readonly DebugLog debugLog;

    public NativeAchievementNavigator(DebugLog debugLog)
    {
        this.debugLog = debugLog;
    }

    public bool OpenAchievement(uint achievementId)
    {
        // Direct user action only: this asks the native Achievement agent to open the same game UI
        // a player would inspect manually. It does not call achievement progress request methods.
        // Agent/ClientStructs interaction docs: https://dalamud.dev/plugin-development/interaction/
        var agent = AgentAchievement.Instance();
        if (agent == null)
        {
            this.debugLog.Trace("NativeAchievementNavigator.Open", $"achievementId={achievementId} rejected=AgentAchievement.Instance null");
            return false;
        }

        this.debugLog.Trace("NativeAchievementNavigator.Open", $"achievementId={achievementId} activeBefore={agent->IsAgentActive()} shownBefore={agent->IsAddonShown()} statusBefore={agent->GetAddonStatus()}");
        agent->OpenById(achievementId);
        this.debugLog.Trace("NativeAchievementNavigator.Open", $"achievementId={achievementId} activeAfter={agent->IsAgentActive()} shownAfter={agent->IsAddonShown()} statusAfter={agent->GetAddonStatus()}");
        return true;
    }
}
