namespace AchievementTracker.Services;

public sealed class CompletionCacheRefreshPolicy
{
    private bool hasRefreshedLiveSnapshotThisSession;
    private bool wasUpdateInProgress;

    public bool ShouldRefresh(bool liveCompletionStateLoaded, bool updateInProgress)
    {
        if (!liveCompletionStateLoaded)
        {
            this.wasUpdateInProgress = updateInProgress;
            return false;
        }

        if (!this.hasRefreshedLiveSnapshotThisSession && !updateInProgress)
        {
            this.hasRefreshedLiveSnapshotThisSession = true;
            this.wasUpdateInProgress = false;
            return true;
        }

        var updateJustFinished = this.wasUpdateInProgress && !updateInProgress;
        this.wasUpdateInProgress = updateInProgress;
        return updateJustFinished;
    }

    public void Reset()
    {
        this.hasRefreshedLiveSnapshotThisSession = false;
        this.wasUpdateInProgress = false;
    }
}
