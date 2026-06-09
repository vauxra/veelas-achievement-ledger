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
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public TrackedAchievementStore TrackedAchievements { get; }
    public AchievementCatalog AchievementCatalog { get; }
    public AchievementProgressService AchievementProgressService { get; }
    public IAchievementProgressSource AchievementProgressSource { get; }
    public ClientAchievementProgressSource ClientAchievementProgressSource { get; }
    public NativeAchievementNavigator NativeAchievementNavigator { get; }
    public DebugLog DebugLog { get; }
    public WindowSystem WindowSystem { get; } = new("VeelasAchievementLedger");

    private TrackerWindow TrackerWindow { get; }
    private ConfigWindow ConfigWindow { get; }
    private PassiveAchievementProgressObserver? passiveAchievementProgressObserver;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.DebugLog = new DebugLog(Log, false);
        this.TrackedAchievements = new TrackedAchievementStore();
        this.TrackedAchievements.LoadFrom(this.Configuration.TrackedAchievementIds);
        this.AchievementCatalog = new AchievementCatalog(DataManager);
        this.ClientAchievementProgressSource = new ClientAchievementProgressSource(this.DebugLog);
        this.AchievementProgressSource = this.ClientAchievementProgressSource;
        this.NativeAchievementNavigator = new NativeAchievementNavigator(this.DebugLog);
        this.AchievementProgressService = new AchievementProgressService(UnlockState, this.AchievementProgressSource);
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
        ClientState.Login += this.ResetProgressState;
        ClientState.Logout += this.ResetProgressStateOnLogout;

        Log.Information("Veela's Achievement Ledger loaded.");
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;
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
        this.Configuration.Save();
    }

    public void ToggleMainUi() => this.TrackerWindow.Toggle();

    public void ToggleConfigUi() => this.ConfigWindow.Toggle();

    private void InstallPassiveAchievementObserver()
    {
        this.passiveAchievementProgressObserver ??= new PassiveAchievementProgressObserver(
            GameInteropProvider,
            this.ClientAchievementProgressSource);
    }

    private void ResetProgressState()
    {
        // Login/logout only clear local progress cache. Tracked achievement IDs stay saved in config.
        this.AchievementProgressSource.ClearCache();
    }

    private void ResetProgressStateOnLogout(int type, int code) => this.ResetProgressState();

    private void OnCommand(string command, string args) => this.ToggleMainUi();
}
