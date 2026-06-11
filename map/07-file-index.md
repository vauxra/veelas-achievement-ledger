# File index

Version: `v0.2.0.22`

## What this page is for

This is a source locator, not a duplicate of the function-call map.

Use it when you already know a class or method name from another wiki page and want to jump to the file that owns it. The [Function call map](./02-function-call-map.md) explains runtime behavior; this page answers “where is that code?”

## How to use

- Start with [Big picture](./01-big-picture.md) or [Function call map](./02-function-call-map.md) to understand the flow.
- Come here to find the file/class/method owner.
- Then open the listed C# source file in the repo.
## `AchievementTracker/Configuration.cs`

Types: `Configuration`

Members found:
- line 12: `public int Version { get; set; } = 1;`
- line 14: `public List<uint> TrackedAchievementIds { get; set; } = [];`
- line 16: `public List<TrackedAchievementPreset> TrackedAchievementPresets { get; set; } = [];`
- line 18: `public CosmicClassScoreCache CosmicClassScoreCache { get; set; } = new();`
- line 20: `public bool HideCompletedInSearch { get; set; } = true;`
- line 22: `public void Normalize()`
- line 28: `public void Save()`

## `AchievementTracker/Models/AchievementInfo.cs`

Types: `AchievementInfo`

Members found:
- line 3: `public sealed record AchievementInfo(`

## `AchievementTracker/Models/AchievementProgress.cs`

Types: `AchievementProgressKind`, `AchievementProgress`

Members found:
- line 14: `public sealed record AchievementProgress(AchievementProgressKind Kind, int? Current = null, int? Required = null)`
- line 16: `public static AchievementProgress CompletionListNotLoaded() => new(AchievementProgressKind.CompletionListNotLoaded);`
- line 18: `public static AchievementProgress Complete() => new(AchievementProgressKind.Complete);`
- line 20: `public static AchievementProgress Incomplete() => new(AchievementProgressKind.Incomplete);`
- line 22: `public static AchievementProgress Numeric(int current, int required) => new(AchievementProgressKind.Numeric, current, required);`
- line 24: `public static AchievementProgress TargetKnown(int required) => new(AchievementProgressKind.TargetKnown, null, required);`
- line 26: `public static AchievementProgress Unavailable() => new(AchievementProgressKind.Unavailable);`
- line 28: `public static AchievementProgress DataNotAvailable() => new(AchievementProgressKind.DataNotAvailable);`
- line 30: `public string ToDisplayText()`

## `AchievementTracker/Models/CosmicClassScoreCache.cs`

Types: `CosmicClassScoreCache`

Members found:
- line 9: `public List<int> Scores { get; set; } = [];`
- line 11: `public DateTimeOffset? UpdatedAtUtc { get; set; }`

## `AchievementTracker/Models/TrackedAchievement.cs`

Types: `TrackedAchievement`

Members found:
- line 3: `public sealed record TrackedAchievement(uint AchievementId);`

## `AchievementTracker/Models/TrackedAchievementPreset.cs`

Types: `TrackedAchievementPreset`

Members found:
- line 9: `public string Name { get; set; } = string.Empty;`
- line 11: `public List<uint> AchievementIds { get; set; } = [];`

## `AchievementTracker/Plugin.cs`

Types: `Plugin`

Members found:
- line 18: `private static readonly TimeSpan AchievementUpdateOpenLockout = TimeSpan.FromSeconds(1);`
- line 19: `private static readonly TimeSpan AchievementObservationWindow = TimeSpan.FromSeconds(8);`
- line 20: `private static readonly TimeSpan CosmicCacheRefreshInterval = TimeSpan.FromSeconds(30);`
- line 38: `public Configuration Configuration { get; }`
- line 39: `public TrackedAchievementStore TrackedAchievements { get; }`
- line 40: `public AchievementCatalog AchievementCatalog { get; }`
- line 41: `public AchievementProgressService AchievementProgressService { get; }`
- line 42: `public IAchievementProgressSource AchievementProgressSource { get; }`
- line 43: `public ClientAchievementProgressSource ClientAchievementProgressSource { get; }`
- line 44: `public CosmicClassProgressProvider CosmicClassProgressProvider { get; }`
- line 45: `public NativeAchievementNavigator NativeAchievementNavigator { get; }`
- line 46: `public WindowSystem WindowSystem { get; } = new("VeelasAchievementLedger");`
- line 50: `private TrackerWindow TrackerWindow { get; }`
- line 51: `private ConfigWindow ConfigWindow { get; }`
- line 55: `public Plugin()`
- line 73: `public void Dispose()`
- line 82: `public void SaveTrackedAchievements()`
- line 88: `public void SaveConfiguration()`
- line 105: `public bool CanOpenAchievementForUpdate => this.AchievementUpdateOpenRemaining == TimeSpan.Zero;`
- line 107: `public bool OpenAchievementForUpdate(uint achievementId)`
- line 126: `public void ToggleMainUi() => this.TrackerWindow.Toggle();`
- line 128: `public void OpenMainUi() => this.TrackerWindow.IsOpen = true;`
- line 130: `public void ToggleConfigUi() => this.ConfigWindow.Toggle();`
- line 132: `public void OpenConfigUi(bool help = false)`
- line 145: `private static Configuration LoadAndNormalizeConfiguration()`
- line 152: `private TrackedAchievementStore CreateTrackedAchievementStore()`
- line 159: `private void RegisterWindows()`
- line 165: `private void RegisterCommand()`
- line 173: `private void RegisterDalamudCallbacks()`
- line 183: `private void UnregisterDalamudCallbacks()`
- line 193: `private void ResetProgressState()`
- line 199: `private void ResetProgressStateOnLogout(int type, int code) => this.ResetProgressState();`
- line 203: `private void OnFrameworkUpdate(IFramework framework)`
- line 209: `private void RefreshCosmicCacheFromLiveState()`
- line 226: `private static bool IsInSinusArdorum() => ClientState.TerritoryType == SinusArdorumTerritoryTypeId;`
- line 228: `private bool CosmicCacheRefreshIsDue() => DateTimeOffset.UtcNow >= this.nextCosmicCacheRefreshAt;`
- line 232: `private void OnCommand(string command, string args)`

## `AchievementTracker/Services/AchievementCatalog.cs`

Types: `AchievementCatalog`

Members found:
- line 14: `public AchievementCatalog(IDataManager dataManager)`
- line 19: `public IEnumerable<AchievementInfo> Search(string query, int limit = 50)`
- line 43: `public bool TryGet(uint achievementId, out AchievementInfo achievementInfo)`
- line 58: `public bool TryGetRow(uint achievementId, out Achievement achievement)`
- line 66: `private AchievementInfo ToInfo(Achievement achievement)`

## `AchievementTracker/Services/AchievementProgressService.cs`

Types: `AchievementProgressService`

Members found:
- line 14: `public AchievementProgressService(IUnlockState unlockState, IAchievementProgressSource? progressSource = null, CosmicClassProgressProvider? cosmicClassProgressProvider = null)`
- line 21: `public AchievementProgress GetProgress(Achievement achievement)`
- line 56: `public bool IsComplete(Achievement achievement)`
- line 59: `private static int? GetRequiredTarget(Achievement achievement)`

## `AchievementTracker/Services/ClientAchievementProgressSource.cs`

Types: `struct`, `ClientAchievementProgressSource`

Members found:
- line 8: `public readonly record struct ObservedAchievementProgress(uint Current, uint Max, DateTimeOffset ObservedAt, string Source);`
- line 17: `private readonly Dictionary<uint, ObservedAchievementProgress> cachedProgress = new();`
- line 18: `private readonly Dictionary<uint, DateTimeOffset> observationDeadlines = new();`
- line 22: `public ClientAchievementProgressSource()`
- line 27: `public ClientAchievementProgressSource(Func<DateTimeOffset> nowProvider)`
- line 32: `public int ActiveObservationCount => this.observationDeadlines.Count;`
- line 34: `public void BeginObservation(uint achievementId, TimeSpan duration)`
- line 45: `public void UpdateCache()`
- line 75: `public bool TryRecordObservedSlot(bool isLoaded, uint achievementId, uint current, uint max, string source)`
- line 93: `public void RecordObservedProgress(uint achievementId, uint current, uint max, string source)`
- line 107: `public void ClearCache()`
- line 114: `public bool TryGetProgress(uint achievementId, out uint current, out uint max)`
- line 129: `public bool TryGetObservation(uint achievementId, out ObservedAchievementProgress progress)`
- line 135: `public bool TryGetCachedObservation(uint achievementId, out ObservedAchievementProgress progress)`
- line 138: `public bool IsObservedComplete(uint achievementId) => this.observedCompletions.Contains(achievementId);`
- line 140: `private void PruneExpiredObservations()`

## `AchievementTracker/Services/CosmicClassProgressProvider.cs`

Types: `CosmicClassProgressProvider`, `CosmicAchievementRule`, `CosmicScoreAggregation`

Members found:
- line 35: `public CosmicClassProgressProvider(CosmicClassScoreCache cache, Action saveCache)`
- line 44: `public bool Handles(uint achievementId) => GetRule(achievementId) is not null;`
- line 46: `public void RefreshCacheFromLiveScores() => _ = this.TryReadLiveScores();`
- line 48: `public AchievementProgress GetProgress(uint achievementId)`
- line 66: `public string GetDiagnostics()`
- line 80: `public static bool IsCosmicClassAchievement(uint achievementId) => GetRule(achievementId) is not null;`
- line 84: `private static CosmicAchievementRule? GetRule(uint achievementId)`
- line 130: `private static CosmicAchievementRule Single(int index, int target) => new([index], target, CosmicScoreAggregation.Maximum);`
- line 132: `private static CosmicAchievementRule Any(int[] indexes, int target) => new(indexes, target, CosmicScoreAggregation.Maximum);`
- line 134: `private static CosmicAchievementRule Every(int[] indexes, int target) => new(indexes, target, CosmicScoreAggregation.Minimum);`
- line 136: `private static int CalculateCurrentScore(IReadOnlyList<int> scores, CosmicAchievementRule rule)`
- line 145: `private static void NormalizeCache(CosmicClassScoreCache scoreCache)`
- line 155: `private int[]? TryReadCachedScores()`
- line 169: `private unsafe int[]? TryReadLiveScores(bool saveWhenAvailable = true)`
- line 192: `private void SaveScoresToCache(int[] liveScores)`
- line 199: `private unsafe string GetLiveStateSummary()`
- line 210: `private bool ScoresEqualCache(IReadOnlyList<int> liveScores)`
- line 228: `private sealed record CosmicAchievementRule(int[] ScoreIndexes, int TargetScore, CosmicScoreAggregation Aggregation);`

## `AchievementTracker/Services/IAchievementProgressSource.cs`

Types: `IAchievementProgressSource`

No public/private member signatures found by the simple indexer.

## `AchievementTracker/Services/NativeAchievementNavigator.cs`

Types: `NativeAchievementNavigator`

Members found:
- line 11: `public bool OpenAchievement(uint achievementId)`
- line 31: `public bool CloseAchievements()`

## `AchievementTracker/Services/TrackedAchievementPresetStore.cs`

Types: `TrackedAchievementPresetStore`

Members found:
- line 13: `public static string SanitizeName(string? rawName)`
- line 33: `public static void Normalize(List<TrackedAchievementPreset> presets)`
- line 54: `public static bool SavePreset(List<TrackedAchievementPreset> presets, string rawName, IEnumerable<uint> achievementIds, out string savedName)`
- line 89: `public static bool RenamePreset(List<TrackedAchievementPreset> presets, string currentName, string rawNewName, out string renamedTo)`
- line 115: `public static bool DeletePreset(List<TrackedAchievementPreset> presets, string name)`
- line 121: `public static TrackedAchievementPreset? FindPreset(List<TrackedAchievementPreset> presets, string name)`
- line 124: `private static List<uint> SanitizeAchievementIds(IEnumerable<uint> achievementIds)`

## `AchievementTracker/Services/TrackedAchievementStore.cs`

Types: `TrackedAchievementStore`

Members found:
- line 11: `public IReadOnlyList<uint> AchievementIds => this.achievementIds;`
- line 13: `public bool TryAdd(uint achievementId)`
- line 29: `public bool Remove(uint achievementId) => this.achievementIds.Remove(achievementId);`
- line 31: `public bool MoveToTop(uint achievementId)`
- line 44: `public bool MoveUp(uint achievementId)`
- line 57: `public bool MoveDown(uint achievementId)`
- line 70: `public bool MoveToBottom(uint achievementId)`
- line 83: `public void LoadFrom(IEnumerable<uint> achievementIds)`
- line 92: `public List<uint> ToConfigList() => [.. this.achievementIds];`

## `AchievementTracker/Windows/ConfigWindow.cs`

Types: `ConfigWindow`, `ConfigSection`

Members found:
- line 20: `public ConfigWindow(Plugin plugin)`
- line 41: `public void OpenConfig()`
- line 47: `public void OpenHelp()`
- line 55: `public override void Draw()`
- line 63: `private void DrawHeader()`
- line 75: `private void DrawLeftNavigation()`
- line 83: `private void DrawNavItem(string label, ConfigSection section)`
- line 91: `private void DrawSelectedPage()`
- line 109: `private void DrawPresetControls()`
- line 127: `private void DrawPresetNameInput()`
- line 138: `private void DrawPresetSaveButton()`
- line 157: `private void DrawPresetPicker()`
- line 176: `private void DrawPresetPickerItem(string presetName)`
- line 192: `private void DrawPresetReadButton()`
- line 202: `private void DrawPresetRenameButton()`
- line 220: `private void DrawPresetDeleteButton()`
- line 238: `private void EnsureSelectedPresetIsValid()`
- line 255: `private bool SelectedPresetExists()`
- line 261: `private void LoadSelectedPreset()`
- line 275: `private void DrawTrackedAchievementsPage()`
- line 297: `private void DrawTrackedManagement()`
- line 315: `private void DrawTrackedAchievementRow(uint achievementId)`
- line 342: `private void DrawMoveButton(string label, string tooltip, Func<bool> moveAction)`
- line 352: `private bool DrawTrackedRemoveButton(uint achievementId)`
- line 359: `private void DrawTrackedUpdateButton(uint achievementId)`
- line 380: `private void DrawInspectButton(uint achievementId)`
- line 390: `private void DrawManagedAchievement(uint achievementId)`
- line 398: `private bool RemoveTrackedAchievement(uint achievementId)`
- line 411: `private void DrawSearchAndAdd()`
- line 436: `private void DrawHideCompletedCheckbox()`
- line 448: `private void DrawSearchInput()`
- line 461: `private System.Collections.Generic.List<AchievementTracker.Models.AchievementInfo> GetVisibleSearchResults()`
- line 469: `private void DrawSearchResultRow(AchievementTracker.Models.AchievementInfo result)`
- line 482: `private void DrawSearchResultAction(uint achievementId, bool canAdd, bool alreadyTracked)`
- line 499: `private void DrawSearchAddButton(uint achievementId)`
- line 511: `private void DrawSearchRemoveButton(uint achievementId)`
- line 524: `private void DrawSearchFullLabel(uint achievementId)`
- line 531: `private void DrawSearchResultDetails(AchievementTracker.Models.AchievementInfo result)`
- line 542: `private void DrawCategoryPath(string categoryPath)`
- line 550: `private void DrawCosmicProgressIfAvailable(uint achievementId)`
- line 561: `private bool IsComplete(uint achievementId)`
- line 567: `private void DrawHelp()`
- line 594: `private void DrawWrappedBullet(string text)`
- line 601: `private void OpenAchievementForUpdate(uint achievementId)`
- line 613: `private void DrawUpdateOpenLockoutStatus()`
- line 622: `private void AddTooltip(string text)`

## `AchievementTracker/Windows/TrackerWindow.cs`

Types: `TrackerWindow`

Members found:
- line 16: `public TrackerWindow(Plugin plugin)`
- line 29: `public override void Draw()`
- line 38: `private void DrawTopButtons()`
- line 49: `private void DrawConfigureButton()`
- line 59: `private void DrawUpdateNextButton()`
- line 80: `private void DrawCloseAchievementsButton()`
- line 98: `private void DrawTrackedAchievementList()`
- line 113: `private void DrawAchievement(uint achievementId)`
- line 131: `private void DrawRowUpdateButton(uint achievementId)`
- line 152: `private void DrawRowInspectButton(uint achievementId)`
- line 162: `private string GetProgressText(uint achievementId)`
- line 172: `private string GetLastObservedText(uint achievementId)`
- line 181: `private void OpenNextTrackedAchievementForUpdate()`
- line 190: `private uint? GetNextTrackedAchievementId()`
- line 216: `private void OpenNativeAchievementForUpdate(uint achievementId)`
- line 228: `private void OpenNativeAchievement(uint achievementId)`
- line 238: `private void DrawUpdateOpenLockoutStatus()`
- line 247: `private static void AddTooltip(string text)`
- line 255: `private static string FormatAge(DateTimeOffset observedAt)`
