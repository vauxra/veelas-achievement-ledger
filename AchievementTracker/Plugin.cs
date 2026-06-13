using AchievementTracker.Services;
using AchievementTracker.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AchievementTracker;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/achex";

    // Dalamud service injection pattern:
    // https://dalamud.dev/plugin-development/project-layout
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    // IDataManager docs: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    // IUnlockState docs: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IUnlockState
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    // IClientState login/logout events are used to scope cached progress to the current character.
    // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    // Passive hooks observe native achievement UI progress flow; they do not issue requests.
    // https://dalamud.dev/plugin-development/interaction/
    [PluginService] internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    // Dalamud service injection pattern: https://dalamud.dev/plugin-development/project-layout
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    // Dalamud service injection pattern: https://dalamud.dev/plugin-development/project-layout
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    // IGameGui is used on this experimental branch to find and park the native Achievement addon after AgentAchievement opens it.
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    // Chat/log activity is used on this experimental branch to trigger scoped tracked-achievement refreshes.
    // https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IChatGui
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    // LocalPlayer class/job scopes activity-triggered updates to the matching Crafting & Gathering category.
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

    public Configuration Configuration { get; }
    public TrackedAchievementStore TrackedAchievements { get; }
    public AchievementCatalog AchievementCatalog { get; }
    public AchievementProgressService AchievementProgressService { get; }
    public IAchievementProgressSource AchievementProgressSource { get; }
    public ClientAchievementProgressSource ClientAchievementProgressSource { get; }
    public CosmicClassProgressProvider CosmicClassProgressProvider { get; }
    public AchievementProgressUpdater AchievementProgressUpdater { get; }
    public NativeAchievementNavigator NativeAchievementNavigator { get; }
    public WindowSystem WindowSystem { get; } = new("AchieveExPlus");

    private TrackerWindow TrackerWindow { get; }
    private ConfigWindow ConfigWindow { get; }
    private PassiveAchievementProgressObserver? passiveAchievementProgressObserver;
    private AchievementActivityUpdateObserver? activityUpdateObserver;
    private DateTimeOffset nextCosmicCacheRefreshAt = DateTimeOffset.MinValue;
    private bool pendingNativeAchievementInspectionRestore;
    private DateTimeOffset pendingNativeAchievementInspectionRestoreUntil = DateTimeOffset.MinValue;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.NormalizeAutoUpdateSettings();
        this.AchievementCatalog = new AchievementCatalog(DataManager);
        this.TrackedAchievements = new TrackedAchievementStore();
        this.TrackedAchievements.LoadFrom(this.Configuration.TrackedAchievementIds.Where(this.AchievementCatalog.IsManuallyViewable));
        this.ClientAchievementProgressSource = new ClientAchievementProgressSource(this.DebugLog);
        this.AchievementProgressSource = this.ClientAchievementProgressSource;
        this.CosmicClassProgressProvider = new CosmicClassProgressProvider(this.Configuration.CosmicClassScoreCache, this.SaveConfiguration);
        this.NativeAchievementNavigator = new NativeAchievementNavigator(GameGui);
        this.AchievementProgressUpdater = new AchievementProgressUpdater(
            this.ClientAchievementProgressSource,
            this.NativeAchievementNavigator,
            () => this.Configuration.GetAutoUpdateTrackedAchievementIds(),
            () => this.Configuration.ExperimentalAutoUpdateEnabled,
            () => this.Configuration.ExperimentalAutoUpdateIntervalSeconds,
            () => this.Configuration.ExperimentalUpdateSpacingSeconds,
            this.DebugLog);
        this.AchievementProgressService = new AchievementProgressService(UnlockState, this.AchievementProgressSource, this.CosmicClassProgressProvider);
        this.TrackerWindow = new TrackerWindow(this);
        this.ConfigWindow = new ConfigWindow(this);
        this.InstallPassiveAchievementObserver();
        this.InstallActivityUpdateObserver();
        this.WindowSystem.AddWindow(this.TrackerWindow);
        this.WindowSystem.AddWindow(this.ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Achieve Ex+. Args: config/configure/man, help/?, open/main/show, close, toggle.",
        });

        PluginInterface.UiBuilder.Draw += this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        Framework.Update += this.OnFrameworkUpdate;
        ClientState.Login += this.ResetProgressState;
        ClientState.Logout += this.ResetProgressStateOnLogout;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;
        Framework.Update -= this.OnFrameworkUpdate;
        ClientState.Login -= this.ResetProgressState;
        ClientState.Logout -= this.ResetProgressStateOnLogout;
        CommandManager.RemoveHandler(CommandName);
        this.passiveAchievementProgressObserver?.Dispose();
        this.passiveAchievementProgressObserver = null;
        this.activityUpdateObserver?.Dispose();
        this.activityUpdateObserver = null;
        this.WindowSystem.RemoveAllWindows();
    }

    public void SaveTrackedAchievements()
    {
        this.Configuration.TrackedAchievementIds = this.TrackedAchievements.ToConfigList();
        this.Configuration.AutoUpdateAchievementIds = this.Configuration.GetAutoUpdateTrackedAchievementIds();
        this.Configuration.Save();
    }

    public void SaveConfiguration()
    {
        this.Configuration.NormalizeAutoUpdateSettings();
        this.Configuration.Save();
    }

    public void EnqueueUpdateAllTracked(string reason)
        => this.AchievementProgressUpdater.EnqueueUpdateAll(this.TrackedAchievements.AchievementIds, reason);

    public void EnqueueUpdateAchievements(IEnumerable<uint> achievementIds, string reason)
        => this.AchievementProgressUpdater.EnqueueUpdateAll(achievementIds, reason);

    public void EnqueueUpdateOne(uint achievementId, string reason)
        => this.AchievementProgressUpdater.EnqueueUpdateAll([achievementId], reason);

    public void ResetAutoUpdateCountdownIfActive()
    {
        if (this.Configuration.ExperimentalAutoUpdateEnabled)
        {
            this.AchievementProgressUpdater.ResetAutoUpdateCountdown();
        }
    }

    public void StopAutoUpdateAndClearQueue()
    {
        this.Configuration.ExperimentalAutoUpdateEnabled = false;
        this.SaveConfiguration();
        this.AchievementProgressUpdater.Clear();
        this.DebugLog("AchieveEx DebugTrace AutoUpdateStopped queueCleared=true");
    }

    public void ClearUpdateQueue(string reason)
    {
        this.AchievementProgressUpdater.Clear();
        this.DebugLog($"AchieveEx DebugTrace UpdateQueueCleared reason={reason}");
    }

    public bool OpenNativeAchievementForInspection(uint achievementId)
    {
        var normalizedBeforeOpen = this.NativeAchievementNavigator.RestoreParkedAchievementWindowOrResetScale();
        var opened = this.NativeAchievementNavigator.OpenAchievement(achievementId);
        var normalizedAfterOpen = opened && !normalizedBeforeOpen && this.NativeAchievementNavigator.RestoreParkedAchievementWindowOrResetScale();
        this.pendingNativeAchievementInspectionRestore = opened && !normalizedBeforeOpen && !normalizedAfterOpen;
        this.pendingNativeAchievementInspectionRestoreUntil = DateTimeOffset.UtcNow.AddSeconds(5);
        this.DebugLog($"AchieveEx DebugTrace NativeInspectionOpen id={achievementId} opened={opened} normalizedBeforeOpen={normalizedBeforeOpen} normalizedAfterOpen={normalizedAfterOpen} pendingRestore={this.pendingNativeAchievementInspectionRestore}");
        return opened;
    }

    public void DebugLog(string message)
    {
        if (this.Configuration.ExperimentalDebugLoggingEnabled)
        {
            PluginLog.Information(message);
        }
    }

    public void ToggleMainUi()
    {
        this.TrackerWindow.ResetPanelScrollOnNextDraw();
        this.TrackerWindow.Toggle();
    }

    public void OpenMainUi()
    {
        this.TrackerWindow.ResetPanelScrollOnNextDraw();
        this.TrackerWindow.IsOpen = true;
    }

    public void CloseMainUi()
    {
        this.TrackerWindow.ResetPanelScrollOnNextDraw();
        this.TrackerWindow.IsOpen = false;
    }

    public void ToggleConfigUi() => this.ConfigWindow.Toggle();

    public bool IsConfigUiOpen => this.ConfigWindow.IsOpen;

    public void OpenConfigUi(bool help = false)
    {
        if (help)
        {
            this.ConfigWindow.OpenHelp();
        }
        else
        {
            this.ConfigWindow.OpenConfig();
        }
    }

    public void OpenTrackedAchievementsConfig()
        => this.ConfigWindow.OpenTrackedAchievements();

    private void InstallPassiveAchievementObserver()
    {
        this.passiveAchievementProgressObserver ??= new PassiveAchievementProgressObserver(
            GameInteropProvider,
            this.ClientAchievementProgressSource,
            () => this.Configuration.TriggerOnAchievementCompletion);
    }

    private void InstallActivityUpdateObserver()
    {
        this.activityUpdateObserver ??= new AchievementActivityUpdateObserver(
            ChatGui,
            this.GetActivityTriggerCandidateAchievementIds,
            this.GetAchievementCategoryName,
            this.GetCurrentClassJobId,
            this.IsActivityTriggerEnabled,
            this.EnqueueUpdateAchievements,
            this.DebugLog);
    }

    private string GetAchievementCategoryName(uint achievementId)
        => this.AchievementCatalog.TryGet(achievementId, out var info) ? info.CategoryName : string.Empty;

    private uint GetCurrentClassJobId()
        => ObjectTable.LocalPlayer?.ClassJob.RowId ?? 0;

    private IReadOnlyList<uint> GetActivityTriggerCandidateAchievementIds()
        => this.Configuration.TriggerUpdatesRespectAutoUpdateSelection
            ? this.Configuration.GetAutoUpdateTrackedAchievementIds()
            : this.TrackedAchievements.AchievementIds;

    private bool IsActivityTriggerEnabled(string triggerName)
    {
        if (!this.Configuration.TriggerAutoUpdatesEnabled)
        {
            return false;
        }

        return triggerName switch
        {
            AchievementActivityUpdateClassifier.MiningTrigger => this.Configuration.TriggerOnMinerActivities && this.Configuration.TriggerOnMiningActivities,
            AchievementActivityUpdateClassifier.QuarryingTrigger => this.Configuration.TriggerOnMinerActivities && this.Configuration.TriggerOnQuarryingActivities,
            AchievementActivityUpdateClassifier.LoggingTrigger => this.Configuration.TriggerOnBotanistActivities && this.Configuration.TriggerOnLoggingActivities,
            AchievementActivityUpdateClassifier.HarvestingTrigger => this.Configuration.TriggerOnBotanistActivities && this.Configuration.TriggerOnHarvestingActivities,
            AchievementActivityUpdateClassifier.FishingTrigger => this.Configuration.TriggerOnFisherActivities && this.Configuration.TriggerOnFishingActivities,
            AchievementActivityUpdateClassifier.SpearfishingTrigger => this.Configuration.TriggerOnFisherActivities && this.Configuration.TriggerOnSpearfishingActivities,
            AchievementActivityUpdateClassifier.CraftingTrigger => this.Configuration.TriggerOnCrafterActivities && this.Configuration.TriggerOnCraftingActivities,
            AchievementActivityUpdateClassifier.CraftingLogTrigger => this.Configuration.TriggerOnCrafterActivities && this.Configuration.TriggerOnCraftingLogActivities,
            _ => false,
        };
    }

    private void ResetProgressState()
    {
        // Login/logout only clear local progress cache. Tracked achievement IDs stay saved in config.
        this.AchievementProgressSource.ClearCache();
        this.AchievementProgressUpdater.Clear();
        this.pendingNativeAchievementInspectionRestore = false;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        this.AchievementProgressUpdater.Tick();
        this.RestoreParkedAchievementWindowIfUserOpenedIt();
        this.TryCompletePendingNativeAchievementInspectionRestore();
        this.RefreshCosmicCacheFromLiveState();
    }

    private void RestoreParkedAchievementWindowIfUserOpenedIt()
    {
        if (!this.AchievementProgressUpdater.IsUpdateInProgress
            && this.NativeAchievementNavigator.HasParkedWindow
            && this.NativeAchievementNavigator.IsOpen)
        {
            var restored = this.NativeAchievementNavigator.RestoreParkedAchievementWindow();
            if (restored)
            {
                this.DebugLog("AchieveEx DebugTrace NativeAchievementWindowUserOpenRestore restored=true");
            }
        }
    }

    private void TryCompletePendingNativeAchievementInspectionRestore()
    {
        if (!this.pendingNativeAchievementInspectionRestore)
        {
            return;
        }

        var shown = this.NativeAchievementNavigator.IsOpen || this.NativeAchievementNavigator.ShowAchievementWindow();
        var normalized = shown && this.NativeAchievementNavigator.RestoreParkedAchievementWindowOrResetScale();
        if (normalized || DateTimeOffset.UtcNow >= this.pendingNativeAchievementInspectionRestoreUntil)
        {
            this.pendingNativeAchievementInspectionRestore = false;
            this.DebugLog($"AchieveEx DebugTrace NativeInspectionRestorePendingComplete shown={shown} normalized={normalized} hasParkedWindow={this.NativeAchievementNavigator.HasParkedWindow}");
        }
    }

    private void RefreshCosmicCacheFromLiveState()
    {
        var now = DateTimeOffset.UtcNow;
        if (now < this.nextCosmicCacheRefreshAt)
        {
            return;
        }

        this.nextCosmicCacheRefreshAt = now.AddSeconds(5);
        this.CosmicClassProgressProvider.RefreshCacheFromLiveScores();
    }

    private void ResetProgressStateOnLogout(int type, int code) => this.ResetProgressState();

    private void OnCommand(string command, string args)
    {
        var normalized = args.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "":
            case "open":
            case "main":
            case "show":
                this.OpenMainUi();
                break;
            case "config":
            case "configure":
            case "settings":
            case "c":
            case "man":
                this.OpenConfigUi();
                break;
            case "?":
            case "help":
            case "h":
                this.OpenConfigUi(help: true);
                break;
            case "close":
            case "hide":
                this.CloseMainUi();
                break;
            case "toggle":
            default:
                this.ToggleMainUi();
                break;
        }
    }
}
