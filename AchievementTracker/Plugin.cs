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
    // docs/docs-cache/dalamud/plugin-development-project-layout.md
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    // IDataManager docs: docs/docs-cache/dalamud/api-IDataManager.md
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    // IUnlockState docs: docs/docs-cache/dalamud/api-IUnlockState.md
    [PluginService] internal static IUnlockState UnlockState { get; private set; } = null!;
    // IClientState login/logout events are used to scope cached progress to the current character.
    // docs/docs-cache/dalamud/api-IClientState.md
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    // Debug-only passive hooks for achievement request/receive/completion flow.
    // docs/docs-cache/dalamud/plugin-development-interaction.md
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
    public DebugLog DebugLog { get; }
    public ProgressRequestThrottler ProgressRequestThrottler { get; } = new(TimeSpan.FromSeconds(30));
    public ProgressRefreshQueue ProgressRefreshQueue { get; } = new();
    public WindowSystem WindowSystem { get; } = new("AchievementTracker");

    private TrackerWindow TrackerWindow { get; }
    private ConfigWindow ConfigWindow { get; }
    private AchievementProgressDebugHooks? achievementProgressDebugHooks;
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
        this.AchievementProgressService = new AchievementProgressService(UnlockState, this.AchievementProgressSource);
        this.TrackerWindow = new TrackerWindow(this);
        this.ConfigWindow = new ConfigWindow(this);
        this.UpdateDebugHookState();
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
        this.achievementProgressDebugHooks?.Dispose();
        this.achievementProgressDebugHooks = null;
        this.activityDebugSurfaces?.Dispose();
        this.activityDebugSurfaces = null;
        this.WindowSystem.RemoveAllWindows();
    }

    public void UpdateDebugHookState()
    {
        if (this.Configuration.EnableDebugLogging)
        {
            this.achievementProgressDebugHooks ??= new AchievementProgressDebugHooks(
                GameInteropProvider,
                AddonLifecycle,
                Framework,
                this.DebugLog,
                this.ClientAchievementProgressSource);
            this.activityDebugSurfaces ??= new ActivityDebugSurfaces(ChatGui, ClientState, Condition, this.DebugLog);
            return;
        }

        this.achievementProgressDebugHooks?.Dispose();
        this.achievementProgressDebugHooks = null;
        this.activityDebugSurfaces?.Dispose();
        this.activityDebugSurfaces = null;
    }

    private void ResetProgressState()
    {
        this.DebugLog.Trace("Plugin.ResetProgressState", "clearing progress cache, refresh queue, and throttler state");
        // Login/logout only clear local cache/queue/throttle state. Do not extend these
        // lifecycle handlers to send achievement progress requests without separate
        // Dalamud policy review; automatic request loops are prohibited by
        // docs/docs-cache/dalamud/plugin-publishing-restrictions.md.
        this.AchievementProgressSource.ClearCache();
        this.ProgressRefreshQueue.Clear();
        this.ProgressRequestThrottler.Clear();
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

    public void SaveTrackedAchievements()
    {
        this.Configuration.TrackedAchievementIds = this.TrackedAchievements.ToConfigList();
        this.Configuration.Save();
        this.DebugLog.Trace("Plugin.SaveTrackedAchievements", $"tracked=[{string.Join(", ", this.Configuration.TrackedAchievementIds)}]");
    }

    public void SaveConfiguration()
    {
        this.UpdateDebugHookState();
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
}
