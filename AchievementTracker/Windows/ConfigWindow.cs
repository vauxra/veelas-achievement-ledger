using AchievementTracker.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using System;
using System.Linq;
using System.Numerics;

namespace AchievementTracker.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private ConfigSection selectedSection = ConfigSection.AutoUpdate;
    private string searchQuery = string.Empty;
    private string presetNameInput = string.Empty;
    private string selectedPresetName = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("Achieve Ex+ Config##AchieveExPlusConfig", ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus)
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(980, 520),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.Size = new Vector2(1080, 620);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    private enum ConfigSection
    {
        AutoUpdate,
        TrackedAchievements,
        Ui,
        Help,
    }

    public void OpenConfig()
    {
        this.selectedSection = ConfigSection.AutoUpdate;
        this.IsOpen = true;
    }

    public void OpenTrackedAchievements()
    {
        this.selectedSection = ConfigSection.TrackedAchievements;
        this.IsOpen = true;
    }

    public void OpenHelp()
    {
        this.selectedSection = ConfigSection.Help;
        this.IsOpen = true;
    }

    public override void Draw()
    {
        if (ImGui.Button("Open Achieve Ex+"))
        {
            this.plugin.OpenMainUi();
        }
        this.AddTooltip("Open tracker window.");
        this.DrawDisabledWrapped("Tracked items are saved between logouts.");
        this.DrawDisabledWrapped("⚠ FLASH / UI motion warning: queued updates can briefly open, shrink, move, restore, and close the native Achievement window.");
        this.DrawDisabledWrapped("Experimental branch: reload buttons open native Achievement entries, shrink/park the Achievement window, read the progress slot, then auto-close if Achieve Ex+ opened it.");
        ImGui.Separator();

        this.DrawLeftNavigation();
        ImGui.SameLine();
        ImGui.BeginChild("##ConfigContent", Vector2.Zero, false);
        switch (this.selectedSection)
        {
            case ConfigSection.AutoUpdate:
                this.DrawAutoUpdatePage();
                break;
            case ConfigSection.TrackedAchievements:
                this.DrawTrackedAchievementsPage();
                break;
            case ConfigSection.Ui:
                this.DrawUiPage();
                break;
            case ConfigSection.Help:
                this.DrawHelp();
                break;
        }

        ImGui.EndChild();
    }

    private void DrawLeftNavigation()
    {
        ImGui.BeginChild("##ConfigNavigation", new Vector2(180, 0), true);
        this.DrawNavItem("Auto update", ConfigSection.AutoUpdate);
        this.DrawNavItem("Tracked Achievements", ConfigSection.TrackedAchievements);
        this.DrawNavItem("UI", ConfigSection.Ui);
        this.DrawNavItem("Help", ConfigSection.Help);
        ImGui.EndChild();
    }

    private void DrawNavItem(string label, ConfigSection section)
    {
        if (ImGui.Selectable(label, this.selectedSection == section))
        {
            this.selectedSection = section;
        }
    }

    private void DrawPresetControls()
    {
        this.EnsureSelectedPresetIsValid();
        ImGui.TextUnformatted("Presets");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(190);
        if (ImGui.InputTextWithHint("##PresetName", "Preset name", ref this.presetNameInput, TrackedAchievementPresetStore.MaxPresetNameLength))
        {
            this.presetNameInput = TrackedAchievementPresetStore.SanitizeName(this.presetNameInput);
        }
        this.AddTooltip("Preset name.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("preset-save", FontAwesomeIcon.Save))
        {
            var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
            if (TrackedAchievementPresetStore.SavePreset(this.plugin.Configuration.TrackedAchievementPresets, this.presetNameInput, trackedIds, out var savedName))
            {
                this.selectedPresetName = savedName;
                this.presetNameInput = savedName;
                this.plugin.SaveConfiguration();
            }
        }
        this.AddTooltip("Save current list.");

        ImGui.SameLine();
        ImGui.SetNextItemWidth(220);
        var comboLabel = string.IsNullOrWhiteSpace(this.selectedPresetName) ? "Select preset" : this.selectedPresetName;
        if (ImGui.BeginCombo("##PresetPicker", comboLabel))
        {
            foreach (var preset in this.plugin.Configuration.TrackedAchievementPresets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase))
            {
                var selected = string.Equals(preset.Name, this.selectedPresetName, StringComparison.OrdinalIgnoreCase);
                if (ImGui.Selectable(preset.Name, selected))
                {
                    this.selectedPresetName = preset.Name;
                    this.presetNameInput = preset.Name;
                    this.LoadSelectedPreset();
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }
        this.AddTooltip("Selecting a preset loads it immediately.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("preset-read", FontAwesomeIcon.FolderOpen))
        {
            this.LoadSelectedPreset();
        }
        this.AddTooltip("Read selected list.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("preset-rename", FontAwesomeIcon.Edit))
        {
            if (TrackedAchievementPresetStore.RenamePreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName, this.presetNameInput, out var renamedTo))
            {
                this.selectedPresetName = renamedTo;
                this.presetNameInput = renamedTo;
                this.plugin.SaveConfiguration();
            }
        }
        this.AddTooltip("Rename selected list.");

        ImGui.SameLine();
        if (ImGuiComponents.IconButton("preset-delete", FontAwesomeIcon.Trash))
        {
            if (TrackedAchievementPresetStore.DeletePreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName))
            {
                this.selectedPresetName = string.Empty;
                this.plugin.SaveConfiguration();
                this.EnsureSelectedPresetIsValid();
            }
        }
        this.AddTooltip("Delete selected list.");
    }

    private void DrawAutoUpdateBulkControls()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        if (ImGui.Button("Include all tracked in auto update"))
        {
            this.plugin.Configuration.AutoUpdateAchievementIds = trackedIds.ToList();
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        this.AddTooltip("Check Auto for all tracked achievements in the current tracked list.");

        ImGui.SameLine();
        if (ImGui.Button("Include none"))
        {
            this.plugin.Configuration.AutoUpdateAchievementIds.Clear();
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        this.AddTooltip("Uncheck Auto for all tracked achievements.");
    }

    private void EnsureSelectedPresetIsValid()
    {
        TrackedAchievementPresetStore.Normalize(this.plugin.Configuration.TrackedAchievementPresets);
        if (!string.IsNullOrWhiteSpace(this.selectedPresetName)
            && TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName) is not null)
        {
            return;
        }

        this.selectedPresetName = this.plugin.Configuration.TrackedAchievementPresets
            .OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()?.Name ?? string.Empty;
        if (string.IsNullOrWhiteSpace(this.presetNameInput))
        {
            this.presetNameInput = this.selectedPresetName;
        }
    }

    private void LoadSelectedPreset()
    {
        var preset = TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName);
        if (preset is null)
        {
            return;
        }

        this.plugin.TrackedAchievements.LoadFrom(preset.AchievementIds.Where(this.plugin.AchievementCatalog.IsManuallyViewable));
        this.plugin.SaveTrackedAchievements();
        this.plugin.ResetAutoUpdateCountdownIfActive();
    }

    private void DrawTrackedAchievementsPage()
    {
        this.DrawAutoUpdateBulkControls();
        ImGui.Separator();

        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var leftWidth = Math.Max(430f, (availableWidth - spacing) * 0.52f);
        var rightWidth = Math.Max(360f, availableWidth - leftWidth - spacing);

        ImGui.BeginChild("##TrackedAchievementsColumn", new Vector2(leftWidth, 0), true);
        this.DrawTrackedManagement();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##SearchAchievementsColumn", new Vector2(rightWidth, 0), true);
        this.DrawSearchAndAdd();
        ImGui.EndChild();
    }

    private void DrawTrackedManagement()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        ImGui.TextUnformatted($"Tracked: {trackedIds.Count} / {TrackedAchievementStore.MaxTrackedAchievements}");

        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked yet. Search below and add one.");
            return;
        }

        foreach (var achievementId in trackedIds)
        {
            ImGui.PushID((int)achievementId);
            if (ImGui.Button("Top") && this.plugin.TrackedAchievements.MoveToTop(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
            }
            this.AddTooltip("Move to top.");

            ImGui.SameLine();
            if (ImGui.Button("Up") && this.plugin.TrackedAchievements.MoveUp(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
            }
            this.AddTooltip("Move up one slot.");

            ImGui.SameLine();
            if (ImGui.Button("Down") && this.plugin.TrackedAchievements.MoveDown(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
            }
            this.AddTooltip("Move down one slot.");

            ImGui.SameLine();
            if (ImGui.Button("Bottom") && this.plugin.TrackedAchievements.MoveToBottom(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
            }
            this.AddTooltip("Move to bottom.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton("tracked-remove", FontAwesomeIcon.Times) && this.RemoveTrackedAchievement(achievementId))
            {
                ImGui.PopID();
                continue;
            }
            this.AddTooltip("Remove from tracked.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
            {
                this.plugin.EnqueueUpdateOne(achievementId, "config-row-update");
            }
            this.AddTooltip("Update this achievement.");

            ImGui.SameLine();
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
            {
                this.plugin.OpenNativeAchievementForInspection(achievementId);
            }
            this.AddTooltip("Open in Achievements.");

            ImGui.SameLine();
            this.DrawAutoUpdateIncludeCheckbox(achievementId);

            ImGui.SameLine();
            this.DrawManagedAchievement(achievementId);
            ImGui.PopID();
        }
    }

    private void DrawManagedAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        ImGui.TextWrapped(info.Name);
        this.DrawCosmicProgressIfAvailable(achievementId);
        this.DrawCategoryPath(info.CategoryName);
    }

    private void DrawCategoryPath(string categoryPath)
    {
        if (!string.IsNullOrWhiteSpace(categoryPath))
        {
            ImGui.TextDisabled(categoryPath);
        }
    }

    private void DrawCosmicProgressIfAvailable(uint achievementId)
    {
        if (!this.plugin.CosmicClassProgressProvider.Handles(achievementId)
            || !this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            return;
        }

        ImGui.TextDisabled(this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText());
    }

    private void DrawSearchAndAdd()
    {
        ImGui.TextUnformatted("Search achievements to track");
        var hideCompleted = this.plugin.Configuration.HideCompletedInSearch;
        if (ImGui.Checkbox("Hide completed", ref hideCompleted))
        {
            this.plugin.Configuration.HideCompletedInSearch = hideCompleted;
            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("Hide completed search results.");

        ImGui.SetNextItemWidth(-70);
        ImGui.InputText("##AchievementSearch", ref this.searchQuery, 128);
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            this.searchQuery = string.Empty;
        }
        this.AddTooltip("Clear search.");

        if (this.searchQuery.Trim().Length < 2)
        {
            ImGui.TextDisabled("Type 2+ characters from a name or category.");
            return;
        }

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        var isFull = trackedIds.Count >= TrackedAchievementStore.MaxTrackedAchievements;
        var results = this.plugin.AchievementCatalog.Search(this.searchQuery, 200)
            .Where(result => !this.plugin.Configuration.HideCompletedInSearch || !this.IsComplete(result.Id))
            .Take(25)
            .ToList();

        if (results.Count == 0)
        {
            ImGui.TextDisabled("No matching manually viewable achievements found.");
            return;
        }

        foreach (var result in results)
        {
            ImGui.PushID((int)result.Id);
            var alreadyTracked = trackedIds.Contains(result.Id);
            var canAdd = !isFull && !alreadyTracked;

            if (canAdd)
            {
                if (ImGui.Button("Add") && this.plugin.AchievementCatalog.IsManuallyViewable(result.Id))
                {
                    if (this.plugin.TrackedAchievements.TryAdd(result.Id))
                    {
                        this.plugin.SaveTrackedAchievements();
                        if (!this.plugin.Configuration.AutoUpdateAchievementIds.Contains(result.Id))
                        {
                            this.plugin.Configuration.AutoUpdateAchievementIds.Add(result.Id);
                            this.plugin.SaveConfiguration();
                            this.plugin.ResetAutoUpdateCountdownIfActive();
                        }
                    }
                }
                this.AddTooltip("Track this achievement.");

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    this.plugin.OpenNativeAchievementForInspection(result.Id);
                }
                this.AddTooltip("Open in Achievements.");
            }
            else if (alreadyTracked)
            {
                var removed = ImGuiComponents.IconButton("search-remove", FontAwesomeIcon.Times);
                this.AddTooltip("Remove from tracked.");
                if (removed && this.RemoveTrackedAchievement(result.Id))
                {
                    ImGui.PopID();
                    continue;
                }

                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    this.plugin.OpenNativeAchievementForInspection(result.Id);
                }
                this.AddTooltip("Open in Achievements.");
            }
            else
            {
                ImGui.TextDisabled("Full");
                ImGui.SameLine();
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    this.plugin.OpenNativeAchievementForInspection(result.Id);
                }
                this.AddTooltip("Open in Achievements.");
            }

            ImGui.SameLine();
            ImGui.BeginGroup();
            ImGui.TextWrapped(result.Name);
            this.DrawCosmicProgressIfAvailable(result.Id);
            this.DrawCategoryPath(result.CategoryName);
            ImGui.EndGroup();

            ImGui.PopID();
        }
    }

    private bool RemoveTrackedAchievement(uint achievementId)
    {
        if (!this.plugin.TrackedAchievements.Remove(achievementId))
        {
            return false;
        }

        var removedAutoUpdateEntry = this.plugin.Configuration.AutoUpdateAchievementIds.RemoveAll(id => id == achievementId) > 0;
        this.plugin.SaveTrackedAchievements();
        this.plugin.SaveConfiguration();
        if (removedAutoUpdateEntry)
        {
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }

        return true;
    }

    private bool IsComplete(uint achievementId)
        => this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row)
            && this.plugin.AchievementProgressService.IsComplete(row);

    private void DrawAutoUpdatePage()
    {
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var availableWidth = ImGui.GetContentRegionAvail().X;
        var leftWidth = Math.Max(360f, (availableWidth - spacing) * 0.50f);
        var rightWidth = Math.Max(360f, availableWidth - leftWidth - spacing);

        ImGui.BeginChild("##AutoUpdateSettingsColumn", new Vector2(leftWidth, 0), true);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        this.DrawExperimentalAutoUpdateSettings();
        ImGui.PopTextWrapPos();
        ImGui.EndChild();

        ImGui.SameLine();
        ImGui.BeginChild("##EventTriggerSettingsColumn", new Vector2(rightWidth, 0), true);
        ImGui.PushTextWrapPos(ImGui.GetContentRegionAvail().X);
        this.DrawTriggerAutoUpdateSettings();
        ImGui.PopTextWrapPos();
        ImGui.EndChild();
    }

    private void DrawExperimentalAutoUpdateSettings()
    {
        ImGui.TextUnformatted("Experimental auto update");
        this.DrawDisabledWrapped("Native Achievement opens. Countdown runs before the first cycle.");
        this.DrawDisabledWrapped("Timed auto update and event-triggered updates cannot both be enabled.");

        var enabled = this.plugin.Configuration.ExperimentalAutoUpdateEnabled;
        if (ImGui.Checkbox("Enable auto update", ref enabled))
        {
            this.plugin.Configuration.ExperimentalAutoUpdateEnabled = enabled;
            if (enabled)
            {
                this.plugin.Configuration.TriggerAutoUpdatesEnabled = false;
                this.plugin.ClearUpdateQueue("auto-update-enabled");
            }

            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        this.AddTooltip("Run timed updates. Mutually exclusive with event-triggered updates.");

        ImGui.SameLine();
        if (ImGui.Button("Stop Update Tasks"))
        {
            this.plugin.StopAutoUpdateAndClearQueue();
        }
        this.AddTooltip("Disable auto update and clear queue.");

        var interval = Math.Clamp(this.plugin.Configuration.ExperimentalAutoUpdateIntervalSeconds, 1, 86_400);
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Seconds between auto update cycles", ref interval))
        {
            this.plugin.Configuration.ExperimentalAutoUpdateIntervalSeconds = Math.Clamp(interval, 1, 86_400);
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        this.AddTooltip("Time between auto cycles.");

        var spacing = Math.Clamp(this.plugin.Configuration.ExperimentalUpdateSpacingSeconds, 0, 3_600);
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputInt("Base seconds between update calls", ref spacing))
        {
            this.plugin.Configuration.ExperimentalUpdateSpacingSeconds = Math.Clamp(spacing, 0, 3_600);
            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        this.AddTooltip("Delay between queued requests.");

        ImGui.Separator();
        var debug = this.plugin.Configuration.ExperimentalDebugLoggingEnabled;
        if (ImGui.Checkbox("Debug prints (AchieveEx DebugTrace)", ref debug))
        {
            this.plugin.Configuration.ExperimentalDebugLoggingEnabled = debug;
            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("Write AchieveEx DebugTrace logs.");

        if (ImGui.Button("Restore default Achievement window scale"))
        {
            this.plugin.ResetNativeAchievementWindowScale();
        }
        this.AddTooltip("Open/show the native Achievement window if needed, then restore its scale to the default 100% while keeping its current position.");

        ImGui.Separator();
    }



    private void DrawTriggerAutoUpdateSettings()
    {
        ImGui.TextUnformatted("Trigger auto updates");
        this.DrawDisabledWrapped("When enabled, matching game events queue updates for tracked achievements in that category.");
        this.DrawDisabledWrapped("Event-triggered updates and timed auto update cannot both be enabled.");

        var triggerAutoUpdates = this.plugin.Configuration.TriggerAutoUpdatesEnabled;
        if (ImGui.Checkbox("Enable event-triggered updates", ref triggerAutoUpdates))
        {
            this.plugin.Configuration.TriggerAutoUpdatesEnabled = triggerAutoUpdates;
            if (triggerAutoUpdates)
            {
                this.plugin.Configuration.ExperimentalAutoUpdateEnabled = false;
                this.plugin.ClearUpdateQueue("event-trigger-enabled");
            }

            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("Allow event-based updates. Mutually exclusive with timed auto update.");

        var respectAutoSelection = this.plugin.Configuration.TriggerUpdatesRespectAutoUpdateSelection;
        if (ImGui.Checkbox("Event triggers only update achievements checked Auto", ref respectAutoSelection))
        {
            this.plugin.Configuration.TriggerUpdatesRespectAutoUpdateSelection = respectAutoSelection;
            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("Limit triggers to Auto rows.");

        var triggerCompletion = this.plugin.Configuration.TriggerOnAchievementCompletion;
        if (ImGui.Checkbox("Achievement completion events mark tracked achievements complete", ref triggerCompletion))
        {
            this.plugin.Configuration.TriggerOnAchievementCompletion = triggerCompletion;
            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("Mark completions when observed.");

        ImGui.Separator();
        this.DrawDisabledWrapped("Choose exactly which event types can trigger updates:");
        var allEventTypesEnabled = this.plugin.Configuration.TriggerOnMinerActivities
            && this.plugin.Configuration.TriggerOnMiningActivities
            && this.plugin.Configuration.TriggerOnQuarryingActivities
            && this.plugin.Configuration.TriggerOnBotanistActivities
            && this.plugin.Configuration.TriggerOnLoggingActivities
            && this.plugin.Configuration.TriggerOnHarvestingActivities
            && this.plugin.Configuration.TriggerOnFisherActivities
            && this.plugin.Configuration.TriggerOnFishingActivities
            && this.plugin.Configuration.TriggerOnSpearfishingActivities
            && this.plugin.Configuration.TriggerOnCrafterActivities
            && this.plugin.Configuration.TriggerOnCraftingActivities
            && this.plugin.Configuration.TriggerOnCraftingLogActivities;
        this.DrawTriggerCheckbox("All event types", allEventTypesEnabled, value =>
        {
            this.plugin.Configuration.TriggerOnMinerActivities = value;
            this.plugin.Configuration.TriggerOnMiningActivities = value;
            this.plugin.Configuration.TriggerOnQuarryingActivities = value;
            this.plugin.Configuration.TriggerOnBotanistActivities = value;
            this.plugin.Configuration.TriggerOnLoggingActivities = value;
            this.plugin.Configuration.TriggerOnHarvestingActivities = value;
            this.plugin.Configuration.TriggerOnFisherActivities = value;
            this.plugin.Configuration.TriggerOnFishingActivities = value;
            this.plugin.Configuration.TriggerOnSpearfishingActivities = value;
            this.plugin.Configuration.TriggerOnCrafterActivities = value;
            this.plugin.Configuration.TriggerOnCraftingActivities = value;
            this.plugin.Configuration.TriggerOnCraftingLogActivities = value;
        });

        this.DrawTriggerCheckbox("All Miner", this.plugin.Configuration.TriggerOnMinerActivities, value =>
        {
            this.plugin.Configuration.TriggerOnMinerActivities = value;
            this.plugin.Configuration.TriggerOnMiningActivities = value;
            this.plugin.Configuration.TriggerOnQuarryingActivities = value;
        });
        this.DrawIndentedTriggerCheckbox("Mining", this.plugin.Configuration.TriggerOnMiningActivities, value => this.plugin.Configuration.TriggerOnMiningActivities = value);
        this.DrawIndentedTriggerCheckbox("Quarrying", this.plugin.Configuration.TriggerOnQuarryingActivities, value => this.plugin.Configuration.TriggerOnQuarryingActivities = value);

        this.DrawTriggerCheckbox("All Botanist", this.plugin.Configuration.TriggerOnBotanistActivities, value =>
        {
            this.plugin.Configuration.TriggerOnBotanistActivities = value;
            this.plugin.Configuration.TriggerOnLoggingActivities = value;
            this.plugin.Configuration.TriggerOnHarvestingActivities = value;
        });
        this.DrawIndentedTriggerCheckbox("Logging", this.plugin.Configuration.TriggerOnLoggingActivities, value => this.plugin.Configuration.TriggerOnLoggingActivities = value);
        this.DrawIndentedTriggerCheckbox("Harvesting", this.plugin.Configuration.TriggerOnHarvestingActivities, value => this.plugin.Configuration.TriggerOnHarvestingActivities = value);

        this.DrawTriggerCheckbox("All Fisher", this.plugin.Configuration.TriggerOnFisherActivities, value =>
        {
            this.plugin.Configuration.TriggerOnFisherActivities = value;
            this.plugin.Configuration.TriggerOnFishingActivities = value;
            this.plugin.Configuration.TriggerOnSpearfishingActivities = value;
        });
        this.DrawIndentedTriggerCheckbox("Fishing", this.plugin.Configuration.TriggerOnFishingActivities, value => this.plugin.Configuration.TriggerOnFishingActivities = value);
        this.DrawIndentedTriggerCheckbox("Spearfishing", this.plugin.Configuration.TriggerOnSpearfishingActivities, value => this.plugin.Configuration.TriggerOnSpearfishingActivities = value);

        this.DrawTriggerCheckbox("All Crafters", this.plugin.Configuration.TriggerOnCrafterActivities, value =>
        {
            this.plugin.Configuration.TriggerOnCrafterActivities = value;
            this.plugin.Configuration.TriggerOnCraftingActivities = value;
            this.plugin.Configuration.TriggerOnCraftingLogActivities = value;
        });
        this.DrawIndentedTriggerCheckbox("Successful synthesis", this.plugin.Configuration.TriggerOnCraftingActivities, value => this.plugin.Configuration.TriggerOnCraftingActivities = value);
        this.DrawIndentedTriggerCheckbox("Crafting log completion", this.plugin.Configuration.TriggerOnCraftingLogActivities, value => this.plugin.Configuration.TriggerOnCraftingLogActivities = value);
    }

    private void DrawTriggerCheckbox(string label, bool value, Action<bool> applyValue)
    {
        var editableValue = value;
        if (ImGui.Checkbox(label, ref editableValue))
        {
            applyValue(editableValue);
            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("Toggle this trigger.");
    }

    private void DrawIndentedTriggerCheckbox(string label, bool value, Action<bool> applyValue)
    {
        ImGui.Indent(18);
        this.DrawTriggerCheckbox(label, value, applyValue);
        ImGui.Unindent(18);
    }

    private void DrawUiPage()
    {
        ImGui.TextUnformatted("Main panel column order");
        ImGui.TextDisabled("Top to bottom here means left to right in the main panel.");
        this.DrawOrderEditor(this.plugin.Configuration.MainColumnOrder, ["Lists", "Search Categories", "Search Results", "Tracked Achievements"], "columns");


        ImGui.Separator();
        ImGui.TextUnformatted("Main panel navigation order");
        ImGui.TextDisabled("Top to bottom here means left to right in the main panel navigation.");
        this.DrawOrderEditor(this.plugin.Configuration.MainNavigationOrder, ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"], "nav");

        ImGui.Separator();
        ImGui.TextUnformatted("Show main panel navigation buttons");
        this.DrawShownToggleGroup(
            "All main panel buttons",
            this.plugin.Configuration.ShownMainNavigationButtons,
            ["Update All", "Auto update", "Lists", "Search", "Config", "Tracked buttons"]);

        ImGui.Separator();
        ImGui.TextUnformatted("Tracked achievement buttons hidden by the eye-slash button");
        ImGui.TextDisabled("White eye = normal/default and shows all tracked achievement buttons. Red slash-eye = hide only the checked buttons below.");
        this.DrawHiddenToggleGroup(
            "All tracked achievement buttons",
            this.plugin.Configuration.HiddenTrackedAchievementIcons,
            ["Auto update", "Remove", "Refresh", "Open"]);

        ImGui.Separator();
        ImGui.TextUnformatted("Main panel column widths");
        ImGui.TextDisabled("Set 0 for automatic/remaining width. Search Categories and Search Results still enforce minimums so labels/options do not truncate.");
        this.DrawColumnWidthEditor("Lists", 0f);
        this.DrawColumnWidthEditor("Search Categories", 320f);
        this.DrawColumnWidthEditor("Search Results", 420f);
        this.DrawColumnWidthEditor("Tracked Achievements", 0f);
    }

    private void DrawOrderEditor(System.Collections.Generic.List<string> order, string[] defaults, string idPrefix)
    {
        foreach (var item in defaults)
        {
            if (!order.Contains(item))
            {
                order.Add(item);
            }
        }

        for (var i = 0; i < order.Count; i++)
        {
            ImGui.PushID($"{idPrefix}-{order[i]}");
            ImGui.TextUnformatted(order[i]);
            ImGui.SameLine();
            using (ImRaiiShim.Disabled(i == 0))
            {
                if (ImGui.Button("Top"))
                {
                    var item = order[i];
                    order.RemoveAt(i);
                    order.Insert(0, item);
                    this.plugin.SaveConfiguration();
                }
            }

            ImGui.SameLine();
            using (ImRaiiShim.Disabled(i == 0))
            {
                if (ImGuiComponents.IconButton("up", FontAwesomeIcon.ArrowUp))
                {
                    (order[i - 1], order[i]) = (order[i], order[i - 1]);
                    this.plugin.SaveConfiguration();
                }
            }

            ImGui.SameLine();
            using (ImRaiiShim.Disabled(i == order.Count - 1))
            {
                if (ImGuiComponents.IconButton("down", FontAwesomeIcon.ArrowDown))
                {
                    (order[i + 1], order[i]) = (order[i], order[i + 1]);
                    this.plugin.SaveConfiguration();
                }
            }

            ImGui.SameLine();
            using (ImRaiiShim.Disabled(i == order.Count - 1))
            {
                if (ImGui.Button("Bottom"))
                {
                    var item = order[i];
                    order.RemoveAt(i);
                    order.Add(item);
                    this.plugin.SaveConfiguration();
                }
            }

            ImGui.PopID();
        }
    }

    private void DrawHiddenToggleGroup(string parentLabel, System.Collections.Generic.List<string> hiddenValues, string[] children)
    {
        var allHidden = children.All(hiddenValues.Contains);
        if (ImGui.Checkbox(parentLabel, ref allHidden))
        {
            if (allHidden)
            {
                foreach (var child in children)
                {
                    if (!hiddenValues.Contains(child))
                    {
                        hiddenValues.Add(child);
                    }
                }
            }
            else
            {
                hiddenValues.RemoveAll(children.Contains);
            }

            this.plugin.SaveConfiguration();
        }

        ImGui.Indent(18);
        foreach (var child in children)
        {
            var hidden = hiddenValues.Contains(child);
            if (ImGui.Checkbox(child, ref hidden))
            {
                if (hidden && !hiddenValues.Contains(child))
                {
                    hiddenValues.Add(child);
                }
                else if (!hidden)
                {
                    hiddenValues.RemoveAll(value => value == child);
                }

                this.plugin.SaveConfiguration();
            }
        }

        ImGui.Unindent(18);
    }

    private void DrawColumnWidthEditor(string columnName, float minimum)
    {
        var width = this.plugin.Configuration.MainColumnWidths.TryGetValue(columnName, out var configuredWidth)
            ? configuredWidth
            : minimum;
        width = Math.Max(minimum, width);
        ImGui.PushID($"column-width-{columnName}");
        ImGui.SetNextItemWidth(120);
        if (ImGui.InputFloat(columnName, ref width, 10f, 50f, "%.0f"))
        {
            this.plugin.Configuration.MainColumnWidths[columnName] = Math.Clamp(width, 0f, 900f);
            this.plugin.SaveConfiguration();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(minimum > 0 ? $"Minimum effective width: {minimum:0}px" : "0 uses automatic/remaining width.");
        }

        ImGui.PopID();
    }

    private void DrawShownToggleGroup(string parentLabel, System.Collections.Generic.List<string> shownValues, string[] children)
    {
        var allShown = children.All(shownValues.Contains);
        if (ImGui.Checkbox(parentLabel, ref allShown))
        {
            if (allShown)
            {
                foreach (var child in children)
                {
                    if (!shownValues.Contains(child))
                    {
                        shownValues.Add(child);
                    }
                }
            }
            else
            {
                shownValues.RemoveAll(children.Contains);
            }

            this.plugin.SaveConfiguration();
        }

        ImGui.Indent(18);
        foreach (var child in children)
        {
            var shown = shownValues.Contains(child);
            if (ImGui.Checkbox(child, ref shown))
            {
                if (shown && !shownValues.Contains(child))
                {
                    shownValues.Add(child);
                }
                else if (!shown)
                {
                    shownValues.RemoveAll(value => value == child);
                }

                this.plugin.SaveConfiguration();
            }
        }

        ImGui.Unindent(18);
    }

    private void DrawHelp()
    {
        ImGui.TextUnformatted("Help");
        this.DrawDisabledWrapped("⚠ FLASH / UI motion warning: queued updates can briefly open, shrink, move, restore, and close the native Achievement window.");
        ImGui.TextWrapped("Disclaimer: This experimental addon uses native Achievement UI opens, timers, window parking/rescaling, and local ClientStructs reads that are discouraged for normal Dalamud submissions when automated. Use may have consequences for your account, including a ban.");
        ImGui.Separator();

        ImGui.TextUnformatted("Main Achieve Ex+ window");
        this.DrawWrappedBullet("Shows your tracked achievements, progress, last update time, and row actions.");
        this.DrawWrappedBullet("Reload buttons and Update All open native Achievement entries, then Achieve Ex+ reads the already-populated progress slot.");
        this.DrawWrappedBullet("Update All queues native Achievement UI assisted progress updates for tracked achievements. Items updated in the last 30 seconds are skipped by Update All.");
        this.DrawWrappedBullet("Timed auto update and event-triggered updates cannot both be enabled at the same time.");
        this.DrawWrappedBullet("Queued update tasks no longer change the native Achievement window scale or position.");
        this.DrawWrappedBullet("Magnifying-glass Open in Achievements buttons restore the parked Achievement window scale/position before showing the selected entry.");
        this.DrawWrappedBullet("Magnifying-glass inspection opens restore any parked Achievement window state before showing the entry.");
        this.DrawWrappedBullet("Stop Update Tasks disables auto update and clears queued update tasks.");
        this.DrawWrappedBullet("Use the magnifying glass to open that achievement in the native Achievements window.");

        ImGui.Separator();
        ImGui.TextUnformatted("Config sections");
        this.DrawWrappedBullet("Auto update: controls timed update cycles, request spacing, debug logs, included tracked rows, and event-triggered updates.");
        this.DrawWrappedBullet("Tracked Achievements: manages tracked rows, ordering, search, add/remove, Cosmic score planning, and native Achievement opens.");
        this.DrawWrappedBullet("Help: explains the windows, controls, and risk notes.");

        ImGui.Separator();
        ImGui.TextUnformatted("Tracked Achievements notes");
        this.DrawWrappedBullet("The Auto checkbox on each tracked row controls whether timed auto update includes that achievement.");
        this.DrawWrappedBullet("Lists live in the main Achieve Ex+ pane. Click the disk icon to show or hide the Lists column, then right-click list names for options.");
        this.DrawWrappedBullet("Search adds achievements to the tracked list; Clear resets the search bar.");
        this.DrawWrappedBullet("Cosmic Class achievements show cached score progress in tracked and search rows when scores have been observed in Cosmic content.");
        this.DrawWrappedBullet("Cosmic score cache refreshes passively while WKS/Cosmic data is loaded and remains available outside the zone.");
    }

    private void DrawDisabledWrapped(string text)
    {
        var disabledColor = ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled];
        ImGui.PushStyleColor(ImGuiCol.Text, disabledColor);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private void DrawWrappedBullet(string text)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    private void DrawAutoUpdateIncludeCheckbox(uint achievementId)
    {
        var included = this.plugin.Configuration.AutoUpdateAchievementIds.Contains(achievementId);
        if (ImGui.Checkbox("Auto", ref included))
        {
            if (included)
            {
                if (!this.plugin.Configuration.AutoUpdateAchievementIds.Contains(achievementId))
                {
                    this.plugin.Configuration.AutoUpdateAchievementIds.Add(achievementId);
                }
            }
            else
            {
                this.plugin.Configuration.AutoUpdateAchievementIds.RemoveAll(id => id == achievementId);
            }

            this.plugin.SaveConfiguration();
            this.plugin.ResetAutoUpdateCountdownIfActive();
        }
        this.AddTooltip("Include in timed updates.");
    }

    private void AddTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
        }
    }

    private sealed class ImRaiiShim : IDisposable
    {
        private readonly bool disabled;

        private ImRaiiShim(bool disabled)
        {
            this.disabled = disabled;
            if (disabled)
            {
                ImGui.BeginDisabled();
            }
        }

        public static ImRaiiShim Disabled(bool disabled) => new(disabled);

        public void Dispose()
        {
            if (this.disabled)
            {
                ImGui.EndDisabled();
            }
        }
    }
}
