namespace AchievementTracker.Services;

public static class SearchCompletionFilterPolicy
{
    public const string All = "All";
    public const string Completed = "Completed";
    public const string Incomplete = "Incomplete";

    public static bool RequiresCompletionState(string filter)
        => filter is Completed or Incomplete;

    public static bool CanEvaluate(string filter, bool completionStateLoaded, bool updateInProgress)
        => !RequiresCompletionState(filter) || completionStateLoaded;

    public static bool Matches(string filter, bool isComplete)
        => filter switch
        {
            Completed => isComplete,
            Incomplete => !isComplete,
            _ => true,
        };

    public static bool MatchesForCount(string filter, bool completionStateLoaded, bool isComplete)
        => RequiresCompletionState(filter) && !completionStateLoaded
            ? true
            : Matches(filter, isComplete);
}
