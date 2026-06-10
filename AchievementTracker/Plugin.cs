using AchievementTracker.Services;
using AchievementTracker.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;

namespace AchievementTracker;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/val";
    private const ushort SinusArdorumTerritoryTypeId = 1237;
    private static readonly TimeSpan AchievementUpdateOpenLockout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CosmicCacheRefreshInterval = TimeSpan.FromSeconds(30);

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
    // Framework update is used only to passively refresh local Cosmic score cache from loaded client state.
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    public Configuration Configuration { get; }
    public TrackedAchievementStore TrackedAchievements { get; }
    public AchievementCatalog AchievementCatalog { get; }
    public AchievementProgressService AchievementProgressService { get; }
    public IAchievementProgressSource AchievementProgressSource { get; }
    public ClientAchievementProgressSource ClientAchievementProgressSource { get; }
    public CosmicClassProgressProvider CosmicClassProgressProvider { get; }
    public NativeAchievementNavigator NativeAchievementNavigator { get; }
    public WindowSystem WindowSystem { get; } = new("VeelasAchievementLedger");

    private TrackerWindow TrackerWindow { get; }
    private ConfigWindow ConfigWindow { get; }
    private PassiveAchievementProgressObserver? passiveAchievementProgressObserver;
    private DateTimeOffset nextCosmicCacheRefreshAt = DateTimeOffset.MinValue;
    private DateTimeOffset nextAchievementUpdateOpenAt = DateTimeOffset.MinValue;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.Configuration.Normalize();
        this.TrackedAchievements = new TrackedAchievementStore();
        this.TrackedAchievements.LoadFrom(this.Configuration.TrackedAchievementIds);
        this.AchievementCatalog = new AchievementCatalog(DataManager);
        this.ClientAchievementProgressSource = new ClientAchievementProgressSource();
        this.AchievementProgressSource = this.ClientAchievementProgressSource;
        this.CosmicClassProgressProvider = new CosmicClassProgressProvider(this.Configuration.CosmicClassScoreCache, this.SaveConfiguration);
        this.NativeAchievementNavigator = new NativeAchievementNavigator();
        this.AchievementProgressService = new AchievementProgressService(UnlockState, this.AchievementProgressSource, this.CosmicClassProgressProvider);
        this.TrackerWindow = new TrackerWindow(this);
        this.ConfigWindow = new ConfigWindow(this);
        this.InstallPassiveAchievementObserver();
        this.WindowSystem.AddWindow(this.TrackerWindow);
        this.WindowSystem.AddWindow(this.ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Veela's Achievement Ledger.",
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
        this.WindowSystem.RemoveAllWindows();
    }

    public void SaveTrackedAchievements()
    {
        this.Configuration.TrackedAchievementIds = this.TrackedAchievements.ToConfigList();
        this.Configuration.Save();
    }

    public void SaveConfiguration()
    {
        this.Configuration.Normalize();
        this.Configuration.Save();
    }


    public TimeSpan AchievementUpdateOpenRemaining
    {
        get
        {
            var remaining = this.nextAchievementUpdateOpenAt - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool CanOpenAchievementForUpdate => this.AchievementUpdateOpenRemaining == TimeSpan.Zero;

    public bool OpenAchievementForUpdate(uint achievementId)
    {
        if (!this.CanOpenAchievementForUpdate)
        {
            return false;
        }

        if (!this.NativeAchievementNavigator.OpenAchievement(achievementId))
        {
            return false;
        }

        this.nextAchievementUpdateOpenAt = DateTimeOffset.UtcNow + AchievementUpdateOpenLockout;
        return true;
    }

    public void ToggleMainUi() => this.TrackerWindow.Toggle();

    public void OpenMainUi() => this.TrackerWindow.IsOpen = true;

    public void ToggleConfigUi() => this.ConfigWindow.Toggle();

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

    private void InstallPassiveAchievementObserver()
    {
        this.passiveAchievementProgressObserver ??= new PassiveAchievementProgressObserver(
            GameInteropProvider,
            this.ClientAchievementProgressSource,
            () => true);
    }

    private void ResetProgressState()
    {
        // Login/logout only clear local progress cache. Tracked achievement IDs stay saved in config.
        this.AchievementProgressSource.ClearCache();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        this.RefreshCosmicCacheFromLiveState();
    }

    private void RefreshCosmicCacheFromLiveState()
    {
        if (ClientState.TerritoryType != SinusArdorumTerritoryTypeId)
        {
            this.nextCosmicCacheRefreshAt = DateTimeOffset.MinValue;
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (now < this.nextCosmicCacheRefreshAt)
        {
            return;
        }

        this.nextCosmicCacheRefreshAt = now + CosmicCacheRefreshInterval;
        this.CosmicClassProgressProvider.RefreshCacheFromLiveScores();
    }

    private void ResetProgressStateOnLogout(int type, int code) => this.ResetProgressState();

    private void OnCommand(string command, string args)
    {
        var normalized = args.Trim().ToLowerInvariant();
        switch (normalized)
        {
            case "config":
            case "configure":
            case "man":
                this.OpenConfigUi();
                break;
            case "?":
            case "help":
                this.OpenConfigUi(help: true);
                break;
            default:
                this.ToggleMainUi();
                break;
        }
    }
}
