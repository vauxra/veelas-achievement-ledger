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
    private const string CommandName = "/achtrack";

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
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public Configuration Configuration { get; }
    public TrackedAchievementStore TrackedAchievements { get; }
    public AchievementCatalog AchievementCatalog { get; }
    public AchievementProgressService AchievementProgressService { get; }
    public IAchievementProgressSource AchievementProgressSource { get; }
    public ClientAchievementProgressSource ClientAchievementProgressSource { get; }
    public NativeAchievementNavigator NativeAchievementNavigator { get; }
    public DebugLog DebugLog { get; }
    public WindowSystem WindowSystem { get; } = new("AchievementTracker");

    private TrackerWindow TrackerWindow { get; }
    private ConfigWindow ConfigWindow { get; }
    private AchievementProgressDebugHooks? achievementProgressObserver;
    private ActivityDebugSurfaces? activityDebugSurfaces;

    public Plugin()
    {
        this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        this.DebugLog = new DebugLog(Log, () => this.Configuration.EnableDebugLogging);
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
        this.UpdateDebugSurfaceState();
        this.WindowSystem.AddWindow(this.TrackerWindow);
        this.WindowSystem.AddWindow(this.ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "Open the Achievement Tracker window.",
        });

        PluginInterface.UiBuilder.Draw += this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += this.ToggleConfigUi;
        ClientState.Login += this.ResetProgressState;
        ClientState.Logout += this.ResetProgressStateOnLogout;

        Log.Information("Achievement Tracker loaded.");
        this.DebugLog.Trace("Plugin.Load", $"tracked=[{string.Join(", ", this.TrackedAchievements.AchievementIds)}] debugLogging={this.Configuration.EnableDebugLogging}");
    }

    public void Dispose()
    {
        this.DebugLog.Trace("Plugin.Dispose", "disposing plugin and removing event handlers/windows");
        PluginInterface.UiBuilder.Draw -= this.WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= this.ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= this.ToggleConfigUi;
        ClientState.Login -= this.ResetProgressState;
        ClientState.Logout -= this.ResetProgressStateOnLogout;
        CommandManager.RemoveHandler(CommandName);
        this.achievementProgressObserver?.Dispose();
        this.achievementProgressObserver = null;
        this.activityDebugSurfaces?.Dispose();
        this.activityDebugSurfaces = null;
        this.WindowSystem.RemoveAllWindows();
    }

    public void SaveTrackedAchievements()
    {
        this.Configuration.TrackedAchievementIds = this.TrackedAchievements.ToConfigList();
        this.Configuration.Save();
        this.DebugLog.Trace("Plugin.SaveTrackedAchievements", $"tracked=[{string.Join(", ", this.Configuration.TrackedAchievementIds)}]");
    }

    public void SaveConfiguration()
    {
        this.UpdateDebugSurfaceState();
        this.Configuration.Save();
        this.DebugLog.Trace("Plugin.SaveConfiguration", $"debugLogging={this.Configuration.EnableDebugLogging} tracked=[{string.Join(", ", this.Configuration.TrackedAchievementIds)}]");
    }

    public void ToggleMainUi()
    {
        this.DebugLog.Trace("Plugin.ToggleMainUi", $"beforeVisible={this.TrackerWindow.IsOpen}");
        this.TrackerWindow.Toggle();
        this.DebugLog.Trace("Plugin.ToggleMainUi", $"afterVisible={this.TrackerWindow.IsOpen}");
    }

    public void ToggleConfigUi()
    {
        this.DebugLog.Trace("Plugin.ToggleConfigUi", $"beforeVisible={this.ConfigWindow.IsOpen}");
        this.ConfigWindow.Toggle();
        this.DebugLog.Trace("Plugin.ToggleConfigUi", $"afterVisible={this.ConfigWindow.IsOpen}");
    }

    private void InstallPassiveAchievementObserver()
    {
        this.achievementProgressObserver ??= new AchievementProgressDebugHooks(
            GameInteropProvider,
            AddonLifecycle,
            Framework,
            this.DebugLog,
            this.ClientAchievementProgressSource);
    }

    private void UpdateDebugSurfaceState()
    {
        if (this.Configuration.EnableDebugLogging)
        {
            this.activityDebugSurfaces ??= new ActivityDebugSurfaces(ChatGui, ClientState, Condition, this.DebugLog);
            return;
        }

        this.activityDebugSurfaces?.Dispose();
        this.activityDebugSurfaces = null;
    }

    private void ResetProgressState()
    {
        this.DebugLog.Trace("Plugin.ResetProgressState", "clearing observed progress cache");
        // Login/logout only clear local observed cache state. Do not extend these lifecycle handlers
        // to send achievement progress requests without separate Dalamud policy review:
        // https://dalamud.dev/plugin-publishing/restrictions.
        this.AchievementProgressSource.ClearCache();
    }

    private void ResetProgressStateOnLogout(int type, int code)
    {
        this.DebugLog.Trace("Plugin.Logout", $"logout event type={type} code={code}");
        this.ResetProgressState();
    }

    private void OnCommand(string command, string args)
    {
        this.DebugLog.Trace("Plugin.Command", $"command={command} args='{args}' toggling main UI");
        this.ToggleMainUi();
    }
}
