using AchievementTracker.Services;
using AchievementTracker.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Linq;

namespace AchievementTracker;

public sealed class Plugin : IDalamudPlugin
{
    // Component: command routing and safety timing.
    // Risk: low. These constants do not touch game memory or the network.
    private const string CommandName = "/achex";
    private const ushort SinusArdorumTerritoryTypeId = 1237;
    private static readonly TimeSpan AchievementUpdateOpenWindowMinimumLockout = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AchievementUpdateClosedWindowMinimumLockout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan AchievementUpdateMaximumLockout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan AchievementObservationWindow = AchievementUpdateMaximumLockout;
    private static readonly TimeSpan CosmicCacheRefreshInterval = TimeSpan.FromSeconds(30);

    // Component: Dalamud services.
    // Risk: low-to-medium. These are framework services supplied by Dalamud. ClientStructs/interop use is isolated in Services/.
    // Dalamud service injection pattern: https://dalamud.dev/plugin-development/project-layout
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    // IDataManager docs: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IDataManager
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    // IUnlockState docs: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IUnlockState
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    // IClientState docs: https://dalamud.dev/api/Dalamud.Plugin.Services/Interfaces/IClientState
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    // IFramework runs the gated Cosmic local-cache check. It does not issue direct progress requests.
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    // Component: public app state/services used by windows.
    // Risk: mixed. Native/unsafe work is behind named service classes so UI code can stay readable.
    public Configuration Configuration { get; }
    public TrackedAchievementStore TrackedAchievements { get; }
    public AchievementCatalog AchievementCatalog { get; }
    public AchievementProgressService AchievementProgressService { get; }
    public IAchievementProgressSource AchievementProgressSource { get; }
    public ClientAchievementProgressSource ClientAchievementProgressSource { get; }
    public CosmicClassProgressProvider CosmicClassProgressProvider { get; }
    public NativeAchievementNavigator NativeAchievementNavigator { get; }
    public WindowSystem WindowSystem { get; } = new("AchieveExPlus");

    // Component: private app objects and timers.
    // Risk: low. These only control UI windows and local throttling.
    private TrackerWindow TrackerWindow { get; }
    private ConfigWindow ConfigWindow { get; }
    private DateTimeOffset nextCosmicCacheRefreshAt = DateTimeOffset.MinValue;
    private DateTimeOffset achievementUpdateMinimumOpenAt = DateTimeOffset.MinValue;
    private DateTimeOffset achievementUpdateMaximumOpenAt = DateTimeOffset.MinValue;
    private uint pendingAchievementUpdateId;
    private bool achievementWindowWasOpenForCurrentUpdate;

    public Plugin()
    {
        this.Configuration = LoadAndNormalizeConfiguration();
        this.AchievementCatalog = new AchievementCatalog(DataManager);
        this.TrackedAchievements = this.CreateTrackedAchievementStore();
        this.ClientAchievementProgressSource = new ClientAchievementProgressSource();
        this.AchievementProgressSource = this.ClientAchievementProgressSource;
        this.CosmicClassProgressProvider = new CosmicClassProgressProvider(this.Configuration.CosmicClassScoreCache, this.SaveConfiguration);
        this.NativeAchievementNavigator = new NativeAchievementNavigator();
        this.AchievementProgressService = new AchievementProgressService(UnlockState, this.AchievementProgressSource, this.CosmicClassProgressProvider);
        this.TrackerWindow = new TrackerWindow(this);
        this.ConfigWindow = new ConfigWindow(this);

        this.RegisterWindows();
        this.RegisterCommand();
        this.RegisterDalamudCallbacks();
    }

    public void Dispose()
    {
        this.UnregisterDalamudCallbacks();
        CommandManager.RemoveHandler(CommandName);
        this.WindowSystem.RemoveAllWindows();
    }

    // Section: saving configuration.
    // Component: Dalamud plugin config. Risk: low; saves only plugin-local settings.
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

    // Section: shared update-open lockout.
    // Component: user-guided native Achievement UI opening. Risk: low-to-medium; calls a native UI agent only after a button click.
    public TimeSpan AchievementUpdateOpenRemaining
    {
        get
        {
            var remaining = this.GetAchievementUpdateOpenAt(DateTimeOffset.UtcNow) - DateTimeOffset.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    public bool CanOpenAchievementForUpdate => this.AchievementUpdateOpenRemaining == TimeSpan.Zero;

    public string AchievementUpdateOpenStatusText
    {
        get
        {
            var now = DateTimeOffset.UtcNow;
            var openAt = this.GetAchievementUpdateOpenAt(now);
            if (openAt <= now)
            {
                return string.Empty;
            }

            if (now < this.achievementUpdateMinimumOpenAt)
            {
                if (!this.achievementWindowWasOpenForCurrentUpdate)
                {
                    var dataRemaining = this.achievementUpdateMaximumOpenAt - now;
                    return $"Waiting for data. ({Math.Ceiling(dataRemaining.TotalSeconds):0}s)";
                }

                var remaining = this.achievementUpdateMinimumOpenAt - now;
                return $"Request cooldown. ({Math.Ceiling(remaining.TotalSeconds):0}s)";
            }

            if (this.achievementWindowWasOpenForCurrentUpdate)
            {
                return string.Empty;
            }

            if (this.pendingAchievementUpdateId != 0
                && this.ClientAchievementProgressSource.HasActiveObservation(this.pendingAchievementUpdateId))
            {
                var remaining = this.achievementUpdateMaximumOpenAt - now;
                return $"Waiting for data. ({Math.Ceiling(remaining.TotalSeconds):0}s)";
            }

            return "Waiting for data.";
        }
    }

    public bool OpenAchievementForUpdate(uint achievementId)
    {
        if (!this.CanOpenAchievementForUpdate)
        {
            return false;
        }

        var achievementWindowWasOpen = this.NativeAchievementNavigator.IsAchievementWindowOpen();
        if (!this.NativeAchievementNavigator.OpenAchievement(achievementId))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        this.pendingAchievementUpdateId = achievementId;
        this.achievementWindowWasOpenForCurrentUpdate = achievementWindowWasOpen;
        this.achievementUpdateMinimumOpenAt = now + GetAchievementUpdateMinimumLockout(achievementWindowWasOpen);
        this.achievementUpdateMaximumOpenAt = now + AchievementUpdateMaximumLockout;
        this.ClientAchievementProgressSource.BeginObservation(achievementId, AchievementObservationWindow);
        return true;
    }

    private DateTimeOffset GetAchievementUpdateOpenAt(DateTimeOffset now)
    {
        if (this.pendingAchievementUpdateId == 0 || now >= this.achievementUpdateMaximumOpenAt)
        {
            this.ClearAchievementUpdateLockout();
            return DateTimeOffset.MinValue;
        }

        var minimumRemaining = this.achievementUpdateMinimumOpenAt - now;
        if (minimumRemaining > TimeSpan.Zero)
        {
            return this.achievementUpdateMinimumOpenAt;
        }

        if (this.achievementWindowWasOpenForCurrentUpdate)
        {
            this.ClearAchievementUpdateLockout();
            return DateTimeOffset.MinValue;
        }

        if (this.ClientAchievementProgressSource.HasActiveObservation(this.pendingAchievementUpdateId))
        {
            return this.achievementUpdateMaximumOpenAt;
        }

        this.ClearAchievementUpdateLockout();
        return DateTimeOffset.MinValue;
    }

    private void ClearAchievementUpdateLockout()
    {
        this.pendingAchievementUpdateId = 0;
        this.achievementWindowWasOpenForCurrentUpdate = false;
        this.achievementUpdateMinimumOpenAt = DateTimeOffset.MinValue;
        this.achievementUpdateMaximumOpenAt = DateTimeOffset.MinValue;
    }

    private static TimeSpan GetAchievementUpdateMinimumLockout(bool achievementWindowWasOpen)
        => achievementWindowWasOpen
            ? AchievementUpdateOpenWindowMinimumLockout
            : AchievementUpdateClosedWindowMinimumLockout;

    // Section: public window helpers.
    // Component: ImGui windows. Risk: low.
    public void ToggleMainUi() => this.TrackerWindow.Toggle();

    public void OpenMainUi() => this.TrackerWindow.IsOpen = true;

    public void ToggleConfigUi() => this.ConfigWindow.Toggle();

    public void OpenConfigUi(bool help = false)
    {
        if (help)
        {
            this.ConfigWindow.OpenHelp();
            return;
        }

        this.ConfigWindow.OpenConfig();
    }

    // Section: startup wiring helpers.
    // Component: app construction. Risk: low; keeps constructor readable.
    private static Configuration LoadAndNormalizeConfiguration()
    {
        var configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        configuration.Normalize();
        return configuration;
    }

    private TrackedAchievementStore CreateTrackedAchievementStore()
    {
        var store = new TrackedAchievementStore();
        store.LoadFrom(this.Configuration.TrackedAchievementIds.Where(this.AchievementCatalog.IsManuallyViewable));
        return store;
    }

    private void RegisterWindows()
    {
        this.WindowSystem.AddWindow(this.TrackerWindow);
        this.WindowSystem.AddWindow(this.ConfigWindow);
    }

    private void RegisterCommand()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open Achieve Ex+.",
        });
    }

    private void RegisterDalamudCallbacks()
    {
        PluginInterface.UiBuilder.Draw += this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        Framework.Update += this.OnFrameworkUpdate;
        ClientState.Login += this.ResetProgressState;
        ClientState.Logout += this.ResetProgressStateOnLogout;
    }

    private void UnregisterDalamudCallbacks()
    {
        PluginInterface.UiBuilder.Draw -= this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;
        Framework.Update -= this.OnFrameworkUpdate;
        ClientState.Login -= this.ResetProgressState;
        ClientState.Logout -= this.ResetProgressStateOnLogout;
    }

    private void ResetProgressState()
    {
        // Login/logout only clear local progress cache. Tracked achievement IDs stay saved in config.
        this.AchievementProgressSource.ClearCache();
    }

    private void ResetProgressStateOnLogout(int type, int code) => this.ResetProgressState();

    // Section: Cosmic Class local score cache.
    // Component: gated local ClientStructs read. Risk: medium because it reads client memory; no server/network request is made.
    private void OnFrameworkUpdate(IFramework framework)
    {
        this.ClientAchievementProgressSource.UpdateCache();
        this.RefreshCosmicCacheFromLiveState();
    }

    private void RefreshCosmicCacheFromLiveState()
    {
        if (!IsInSinusArdorum())
        {
            this.nextCosmicCacheRefreshAt = DateTimeOffset.MinValue;
            return;
        }

        if (!this.CosmicCacheRefreshIsDue())
        {
            return;
        }

        this.nextCosmicCacheRefreshAt = DateTimeOffset.UtcNow + CosmicCacheRefreshInterval;
        this.CosmicClassProgressProvider.RefreshCacheFromLiveScores();
    }

    private static bool IsInSinusArdorum() => ClientState.TerritoryType == SinusArdorumTerritoryTypeId;

    private bool CosmicCacheRefreshIsDue() => DateTimeOffset.UtcNow >= this.nextCosmicCacheRefreshAt;

    // Section: chat command routing.
    // Component: user commands. Risk: low.
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
