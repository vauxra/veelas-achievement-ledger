using AchievementTracker.Models;
using AchievementTracker.Services;

var tests = new List<(string Name, Action Body)>
{
    ("Add allows up to twenty unique achievement ids", AddAllowsUpToTwentyUniqueAchievementIds),
    ("Add rejects duplicate achievement ids", AddRejectsDuplicateAchievementIds),
    ("Remove deletes the selected achievement id", RemoveDeletesSelectedAchievementId),
    ("MoveToTop reorders an item to the start", MoveToTopReordersItemToStart),
    ("MoveUp reorders an item toward the start", MoveUpReordersItemTowardStart),
    ("MoveDown reorders an item toward the end", MoveDownReordersItemTowardEnd),
    ("MoveToBottom reorders an item to the end", MoveToBottomReordersItemToEnd),
    ("LoadFrom sanitizes duplicates and trims to twenty", LoadFromSanitizesDuplicatesAndTrimsToTwenty),
    ("Preset save sanitizes names and achievement ids", PresetSaveSanitizesNamesAndAchievementIds),
    ("Preset copy name avoids collisions", PresetCopyNameAvoidsCollisions),
    ("Preset rename and delete cover CRUD", PresetRenameAndDeleteCoverCrud),
    ("Progress display formats all safe states", ProgressDisplayFormatsAllSafeStates),
    ("Configuration defaults use requested main column widths", ConfigurationDefaultsUseRequestedMainColumnWidths),
    ("Tracked display evaluates cosmic progress overrides", TrackedDisplayEvaluatesCosmicProgressOverrides),
    ("Cosmic progress override parses achievement details", CosmicProgressOverrideParsesAchievementDetails),
    ("Completion filters wait for loaded achievement state", CompletionFiltersWaitForLoadedAchievementState),
    ("Completion-filtered counts fall back to all while unloaded", CompletionFilteredCountsFallBackToAllWhileUnloaded),
    ("Lumina search all does not wait for loaded achievement state", LuminaSearchAllDoesNotWaitForLoadedAchievementState),
    ("Achievement search index filters category query and completion", AchievementSearchIndexFiltersCategoryQueryAndCompletion),
    ("Achievement search index counts categories with unloaded completion fallback", AchievementSearchIndexCountsCategoriesWithUnloadedCompletionFallback),
    ("Achievement search index hides zero-count incomplete categories only when configured", AchievementSearchIndexHidesZeroCountIncompleteCategoriesOnlyWhenConfigured),
    ("Achievement search index keeps game order stable", AchievementSearchIndexKeepsGameOrderStable),
    ("Achievement category path splits top-level and subcategory", AchievementCategoryPathSplitsTopLevelAndSubcategory),
    ("Achievement category path matches exact category or final subcategory", AchievementCategoryPathMatchesExactCategoryOrFinalSubcategory),
    ("Tracked toolbar hidden state shows default eye", TrackedToolbarHiddenStateShowsDefaultEye),
    ("Tracked toolbar shown state shows red eye", TrackedToolbarShownStateShowsRedEye),
};

foreach (var test in tests)
{
    test.Body();
    Console.WriteLine($"PASS {test.Name}");
}

static void AddAllowsUpToTwentyUniqueAchievementIds()
{
    var store = new TrackedAchievementStore();

    for (uint id = 1; id <= 20; id++)
    {
        AssertTrue(store.TryAdd(id), $"{id} should be added");
    }

    AssertFalse(store.TryAdd(21), "21 should be rejected because max tracked achievements is twenty");
    AssertSequence(store.AchievementIds, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
}

static void AddRejectsDuplicateAchievementIds()
{
    var store = new TrackedAchievementStore();

    AssertTrue(store.TryAdd(42), "first add should pass");
    AssertFalse(store.TryAdd(42), "duplicate add should fail");

    AssertSequence(store.AchievementIds, [42]);
}

static void RemoveDeletesSelectedAchievementId()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30]);

    AssertTrue(store.Remove(20), "existing id should be removed");
    AssertFalse(store.Remove(99), "missing id should not be removed");

    AssertSequence(store.AchievementIds, [10, 30]);
}

static void MoveToTopReordersItemToStart()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30, 40]);

    AssertTrue(store.MoveToTop(30), "30 should move to top");
    AssertFalse(store.MoveToTop(30), "top item cannot move to top again");

    AssertSequence(store.AchievementIds, [30, 10, 20, 40]);
}

static void MoveUpReordersItemTowardStart()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30]);

    AssertTrue(store.MoveUp(30), "30 should move up");
    AssertFalse(store.MoveUp(10), "first item cannot move up");

    AssertSequence(store.AchievementIds, [10, 30, 20]);
}

static void MoveDownReordersItemTowardEnd()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30]);

    AssertTrue(store.MoveDown(10), "10 should move down");
    AssertFalse(store.MoveDown(30), "last item cannot move down");

    AssertSequence(store.AchievementIds, [20, 10, 30]);
}

static void MoveToBottomReordersItemToEnd()
{
    var store = new TrackedAchievementStore();
    store.LoadFrom([10, 20, 30, 40]);

    AssertTrue(store.MoveToBottom(20), "20 should move to bottom");
    AssertFalse(store.MoveToBottom(20), "bottom item cannot move to bottom again");

    AssertSequence(store.AchievementIds, [10, 30, 40, 20]);
}

static void LoadFromSanitizesDuplicatesAndTrimsToTwenty()
{
    var store = new TrackedAchievementStore();
    var ids = new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 2, 3 };

    store.LoadFrom(ids);

    AssertSequence(store.AchievementIds, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
}

static void PresetSaveSanitizesNamesAndAchievementIds()
{
    var presets = new List<TrackedAchievementPreset>();
    var dirtyName = "  Miner\nSet  ";
    var ids = new uint[] { 1, 2, 2, 0, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21 };

    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, dirtyName, ids, out var savedName), "preset should save");
    AssertEqual("MinerSet", savedName);
    AssertEqualInt(1, presets.Count);
    AssertEqual("MinerSet", presets[0].Name);
    AssertSequence(presets[0].AchievementIds, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20]);
}

static void PresetCopyNameAvoidsCollisions()
{
    var presets = new List<TrackedAchievementPreset>();
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "Gather", [1], out _), "initial save should pass");
    AssertEqual("Copy_Gather", TrackedAchievementPresetStore.BuildCopyName(presets, "Gather"));
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "Copy_Gather", [2], out _), "copy save should pass");
    AssertEqual("Copy_2_Gather", TrackedAchievementPresetStore.BuildCopyName(presets, "Gather"));
}

static void PresetRenameAndDeleteCoverCrud()
{
    var presets = new List<TrackedAchievementPreset>();
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "List A", [10, 20], out _), "initial save should pass");
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "List B", [30, 40], out _), "second save should pass");
    AssertTrue(TrackedAchievementPresetStore.SavePreset(presets, "List A", [50], out _), "same name should update");
    AssertEqualInt(2, presets.Count);
    AssertSequence(TrackedAchievementPresetStore.FindPreset(presets, "List A")!.AchievementIds, [50]);

    AssertFalse(TrackedAchievementPresetStore.RenamePreset(presets, "List A", "List B", out _), "rename should reject duplicate names");
    AssertTrue(TrackedAchievementPresetStore.RenamePreset(presets, "List A", "Renamed", out var renamedTo), "rename should pass");
    AssertEqual("Renamed", renamedTo);
    AssertTrue(TrackedAchievementPresetStore.DeletePreset(presets, "Renamed"), "delete should pass");
    AssertFalse(TrackedAchievementPresetStore.DeletePreset(presets, "Renamed"), "delete should reject missing preset");
}

static void ProgressDisplayFormatsAllSafeStates()
{
    AssertEqual("Open Achievements to load status", AchievementProgress.CompletionListNotLoaded().ToDisplayText());
    AssertEqual("Complete", AchievementProgress.Complete().ToDisplayText());
    AssertEqual("Incomplete", AchievementProgress.Incomplete().ToDisplayText());
    AssertEqual("437 / 1,000", AchievementProgress.Numeric(437, 1000).ToDisplayText());
    AssertEqual("Current unavailable / 1,500", AchievementProgress.TargetKnown(1500).ToDisplayText());
    AssertEqual("Progress unavailable", AchievementProgress.Unavailable().ToDisplayText());
    AssertEqual("Data not available", AchievementProgress.DataNotAvailable().ToDisplayText());
}

static void ConfigurationDefaultsUseRequestedMainColumnWidths()
{
    var defaults = MainPanelColumnWidthDefaults.Create();
    AssertTrue(Math.Abs(defaults["Lists"] - 270f) < 0.001f, "Lists width should default to 270");
    AssertTrue(Math.Abs(defaults["Search Categories"] - 320f) < 0.001f, "Search Categories width should default to 320");
    AssertTrue(Math.Abs(defaults["Search Results"] - 550f) < 0.001f, "Search Results width should default to 550");
    AssertTrue(Math.Abs(defaults["Tracked Achievements"] - 320f) < 0.001f, "Tracked Achievements width should default to 320");
}

static void TrackedDisplayEvaluatesCosmicProgressOverrides()
{
    AssertTrue(TrackedProgressDisplayPolicy.ShouldEvaluateProgress(hasObservedProgress: false, isComplete: false, hasCosmicProgressOverride: true), "cosmic override should display without a normal observation");
    AssertFalse(TrackedProgressDisplayPolicy.ShouldEvaluateProgress(hasObservedProgress: false, isComplete: false, hasCosmicProgressOverride: false), "ordinary rows remain not updated until observed/complete");
}

static void CosmicProgressOverrideParsesAchievementDetails()
{
    var scores = new[] { 123456, 0, 0, 0, 0, 0, 0, 0, 222222, 0, 0 };
    AssertTrue(CosmicClassProgressProvider.TryCreateProgressOverride("Carpenter", "Earn a cosmic class score of 500,000 points as a carpenter.", scores, out var carpenter), "carpenter cosmic description should parse");
    AssertEqual("123,456 / 500,000", carpenter.ToDisplayText());
    AssertTrue(CosmicClassProgressProvider.TryCreateProgressOverride("Miner", "Earn a cosmic class score of 150,000 points as a miner.", scores, out var miner), "miner cosmic description should parse");
    AssertEqual("222,222 / 150,000", miner.ToDisplayText());
    AssertTrue(CosmicClassProgressProvider.TryCreateProgressOverride("Carpenter", "Earn 500,000 tool mastery points as a carpenter.", scores, out var mastery), "tool mastery descriptions should use cosmic class scores");
    AssertEqual("123,456 / 500,000", mastery.ToDisplayText());
    AssertFalse(CosmicClassProgressProvider.TryCreateProgressOverride("Carpenter", "Synthesize 1,000 items.", scores, out _), "non-cosmic descriptions should not override");
}

static void CompletionFiltersWaitForLoadedAchievementState()
{
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: false, updateInProgress: false), "All should not need live completion state");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: false, updateInProgress: false), "Completed should wait for completion state");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Incomplete", completionStateLoaded: false, updateInProgress: false), "Incomplete should wait for completion state");
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: true, updateInProgress: false), "Completed should work after load");
}

static void CompletionFilteredCountsFallBackToAllWhileUnloaded()
{
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Completed", completionStateLoaded: false, isComplete: false), "category/search counts should stay all-counts until completion state is loaded");
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Incomplete", completionStateLoaded: false, isComplete: true), "category/search counts should stay all-counts until completion state is loaded");
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Completed", completionStateLoaded: true, isComplete: true), "completed counts include complete rows after load");
    AssertFalse(SearchCompletionFilterPolicy.MatchesForCount("Completed", completionStateLoaded: true, isComplete: false), "completed counts exclude incomplete rows after load");
    AssertTrue(SearchCompletionFilterPolicy.MatchesForCount("Incomplete", completionStateLoaded: true, isComplete: false), "incomplete counts include incomplete rows after load");
    AssertFalse(SearchCompletionFilterPolicy.MatchesForCount("Incomplete", completionStateLoaded: true, isComplete: true), "incomplete counts exclude complete rows after load");
}

static void LuminaSearchAllDoesNotWaitForLoadedAchievementState()
{
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: false, updateInProgress: false), "Lumina-only All search should not wait for the session's initial achievement load");
    AssertTrue(SearchCompletionFilterPolicy.CanEvaluate("All", completionStateLoaded: true, updateInProgress: true), "Lumina-only All search should not pause during unrelated work");
    AssertFalse(SearchCompletionFilterPolicy.CanEvaluate("Completed", completionStateLoaded: false, updateInProgress: false), "Completed search still needs loaded completion state");
}

static void AchievementSearchIndexFiltersCategoryQueryAndCompletion()
{
    var achievements = new[]
    {
        SearchInfo(1, "Mine 10 items", "Crafting & Gathering > Miner"),
        SearchInfo(2, "Harvest 20 items", "Crafting & Gathering > Botanist"),
        SearchInfo(3, "Mine impostor", "Battle > Miner Impostor"),
        SearchInfo(4, "Legacy mine", "Legacy > Events"),
    };
    var sortKeys = SearchSortKeys(
        (1u, new AchievementSearchSortKey(0, 2, 2, 1)),
        (2u, new AchievementSearchSortKey(0, 1, 1, 2)),
        (3u, new AchievementSearchSortKey(1, 1, 1, 3)),
        (4u, new AchievementSearchSortKey(0, 0, 0, 4)));

    var results = AchievementSearchIndex.BuildResults(
        AchievementSearchIndex.GetSearchableAchievements(achievements),
        new AchievementSearchQueryState(
            "mine",
            CategoryFilterAll: false,
            SelectedCategoryFilters: [],
            SelectedSubcategoryFilters: [AchievementCategoryPath.BuildSubcategoryFilterKey("Crafting & Gathering", "Miner")],
            SearchCompletionFilterPolicy.Incomplete,
            AchievementSearchSortMode.GameOrder),
        isComplete: id => id != 1,
        getSortKey: info => sortKeys[info.Id]);

    AssertEqualInt(3, results.SearchableCount);
    AssertEqualInt(1, results.CategoryFilteredCount);
    AssertEqualInt(1, results.QueryFilteredCount);
    AssertEqualInt(1, results.CompletionFilteredCount);
    AssertSequence(results.Results.Select(info => info.Id).ToList(), [1]);
}

static void AchievementSearchIndexCountsCategoriesWithUnloadedCompletionFallback()
{
    var achievements = AchievementSearchIndex.GetSearchableAchievements(
    [
        SearchInfo(1, "Mine 10 items", "Crafting & Gathering > Miner"),
        SearchInfo(2, "Harvest 20 items", "Crafting & Gathering > Botanist"),
        SearchInfo(3, "Win battles", "Battle > Field Operations"),
    ]);
    var sortKeys = SearchSortKeys(
        (1u, new AchievementSearchSortKey(0, 2, 2, 1)),
        (2u, new AchievementSearchSortKey(0, 1, 1, 2)),
        (3u, new AchievementSearchSortKey(1, 1, 1, 3)));

    var unloadedGroups = AchievementSearchIndex.BuildCategoryGroups(
        achievements,
        SearchCompletionFilterPolicy.Completed,
        completionStateLoaded: false,
        isComplete: id => id == 2,
        getSortKey: info => sortKeys[info.Id]);

    var craftingUnloaded = unloadedGroups.Single(group => group.Category == "Crafting & Gathering");
    AssertEqualInt(2, craftingUnloaded.DisplayCount);
    AssertEqualInt(1, craftingUnloaded.CountEntriesForSubcategory("Miner"));
    AssertEqualInt(1, craftingUnloaded.CountEntriesForSubcategory("Botanist"));

    var loadedGroups = AchievementSearchIndex.BuildCategoryGroups(
        achievements,
        SearchCompletionFilterPolicy.Completed,
        completionStateLoaded: true,
        isComplete: id => id == 2,
        getSortKey: info => sortKeys[info.Id]);

    var craftingLoaded = loadedGroups.Single(group => group.Category == "Crafting & Gathering");
    AssertEqualInt(1, craftingLoaded.DisplayCount);
    AssertEqualInt(0, craftingLoaded.CountEntriesForSubcategory("Miner"));
    AssertEqualInt(1, craftingLoaded.CountEntriesForSubcategory("Botanist"));
}

static void AchievementSearchIndexHidesZeroCountIncompleteCategoriesOnlyWhenConfigured()
{
    var achievements = AchievementSearchIndex.GetSearchableAchievements(
    [
        SearchInfo(1, "Mine 10 items", "Crafting & Gathering > Miner"),
        SearchInfo(2, "Harvest 20 items", "Crafting & Gathering > Botanist"),
        SearchInfo(3, "Win battles", "Battle > Field Operations"),
    ]);
    var sortKeys = SearchSortKeys(
        (1u, new AchievementSearchSortKey(0, 1, 1, 1)),
        (2u, new AchievementSearchSortKey(0, 1, 2, 2)),
        (3u, new AchievementSearchSortKey(1, 1, 1, 3)));
    var groups = AchievementSearchIndex.BuildCategoryGroups(
        achievements,
        SearchCompletionFilterPolicy.Incomplete,
        completionStateLoaded: true,
        isComplete: id => id is 1 or 2,
        getSortKey: info => sortKeys[info.Id]);
    var hideZeroCountEntries = AchievementSearchIndex.ShouldHideZeroCountCategories(
        SearchCompletionFilterPolicy.Incomplete,
        hideZeroCountIncompleteCategories: true);

    AssertTrue(hideZeroCountEntries, "incomplete filter plus enabled config should hide zero-count entries");
    AssertSequence(groups.Where(group => group.ShouldShow(hideZeroCountEntries)).Select(group => group.Entries[0].Info.Id).ToList(), [3]);

    var craftingGroup = groups.Single(group => group.Category == "Crafting & Gathering");
    AssertFalse(craftingGroup.ShouldShowSubcategory("Miner", hideZeroCountEntries), "zero-count subcategories should hide when configured");
    AssertFalse(AchievementSearchIndex.ShouldHideZeroCountCategories(SearchCompletionFilterPolicy.Completed, hideZeroCountIncompleteCategories: true), "completed filter should not use the incomplete zero-count hiding toggle");
    AssertFalse(AchievementSearchIndex.ShouldHideZeroCountCategories(SearchCompletionFilterPolicy.Incomplete, hideZeroCountIncompleteCategories: false), "disabled config should leave zero-count categories visible");
}

static void AchievementSearchIndexKeepsGameOrderStable()
{
    var achievements = AchievementSearchIndex.GetSearchableAchievements(
    [
        SearchInfo(10, "Second", "Battle"),
        SearchInfo(20, "First", "Crafting & Gathering > Miner"),
        SearchInfo(30, "Third by row", "Crafting & Gathering > Miner"),
    ]);
    var sortKeys = SearchSortKeys(
        (10u, new AchievementSearchSortKey(1, 1, 1, 10)),
        (20u, new AchievementSearchSortKey(0, 1, 1, 20)),
        (30u, new AchievementSearchSortKey(0, 1, 1, 30)));

    var results = AchievementSearchIndex.BuildResults(
        achievements,
        new AchievementSearchQueryState(
            string.Empty,
            CategoryFilterAll: true,
            SelectedCategoryFilters: [],
            SelectedSubcategoryFilters: [],
            SearchCompletionFilterPolicy.All,
            AchievementSearchSortMode.GameOrder),
        isComplete: _ => false,
        getSortKey: info => sortKeys[info.Id]);

    AssertSequence(results.Results.Select(info => info.Id).ToList(), [20, 30, 10]);
}

static void AchievementCategoryPathSplitsTopLevelAndSubcategory()
{
    var simple = AchievementCategoryPath.Parse("Miner");
    AssertEqual("Miner", simple.Category);
    AssertEqual(string.Empty, simple.Subcategory);

    var nested = AchievementCategoryPath.Parse("Crafting & Gathering > Botanist > Harvesting");
    AssertEqual("Crafting & Gathering", nested.Category);
    AssertEqual("Harvesting", nested.Subcategory);

    var empty = AchievementCategoryPath.Parse("  >  ");
    AssertEqual(string.Empty, empty.Category);
    AssertEqual(string.Empty, empty.Subcategory);
}

static void AchievementCategoryPathMatchesExactCategoryOrFinalSubcategory()
{
    AssertTrue(AchievementCategoryPath.MatchesCategory("Miner", "Miner"), "exact category should match");
    AssertTrue(AchievementCategoryPath.MatchesCategory("Crafting & Gathering > Miner", "Miner"), "final subcategory should match");
    AssertFalse(AchievementCategoryPath.MatchesCategory("Battle > Miner Impostor", "Miner"), "partial subcategory text should not match");
    AssertFalse(AchievementCategoryPath.MatchesCategory("", "Miner"), "blank path should not match");
}

static void TrackedToolbarHiddenStateShowsDefaultEye()
{
    var presentation = TrackedToolbarIconPresentation.ForHiddenState(hidden: true);

    AssertEqual("Eye", presentation.IconName);
    AssertEqual("Default", presentation.ColorName);
    AssertEqual("Show tracked achievement icons.", presentation.Tooltip);
}

static void TrackedToolbarShownStateShowsRedEye()
{
    var presentation = TrackedToolbarIconPresentation.ForHiddenState(hidden: false);

    AssertEqual("Eye", presentation.IconName);
    AssertEqual("Red", presentation.ColorName);
    AssertEqual("Hide tracked achievement icons.", presentation.Tooltip);
}

static AchievementInfo SearchInfo(uint id, string name, string category)
    => new(id, name, $"Description for {name}", Points: 0, category);

static Dictionary<uint, AchievementSearchSortKey> SearchSortKeys(params (uint Id, AchievementSearchSortKey Key)[] entries)
    => entries.ToDictionary(entry => entry.Id, entry => entry.Key);

static void AssertEqual(string expected, string actual)
{
    if (!string.Equals(expected, actual, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'");
    }
}

static void AssertEqualInt(int expected, int actual)
{
    if (expected != actual)
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}");
    }
}

static void AssertTrue(bool value, string message)
{
    if (!value)
    {
        throw new InvalidOperationException($"Expected true: {message}");
    }
}

static void AssertFalse(bool value, string message)
{
    if (value)
    {
        throw new InvalidOperationException($"Expected false: {message}");
    }
}

static void AssertSequence(IReadOnlyList<uint> actual, IReadOnlyList<uint> expected)
{
    if (!actual.SequenceEqual(expected))
    {
        throw new InvalidOperationException($"Expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
    }
}
