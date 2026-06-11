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
    private ConfigSection selectedSection = ConfigSection.TrackedAchievements;
    private string searchQuery = string.Empty;
    private string presetNameInput = string.Empty;
    private string selectedPresetName = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("Veela's Ledger Config##AchievementLedgerConfig")
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
        TrackedAchievements,
        Help,
    }

    // Section: external entry points.
    // Component: config window navigation. Risk: low.
    public void OpenConfig()
    {
        this.selectedSection = ConfigSection.TrackedAchievements;
        this.IsOpen = true;
    }

    public void OpenHelp()
    {
        this.selectedSection = ConfigSection.Help;
        this.IsOpen = true;
    }

    // Section: top-level config layout.
    // Component: ImGui UI. Risk: low; this only chooses what page to draw.
    public override void Draw()
    {
        this.DrawHeader();
        this.DrawLeftNavigation();
        ImGui.SameLine();
        this.DrawSelectedPage();
    }

    private void DrawHeader()
    {
        if (ImGui.Button("Open VAL"))
        {
            this.plugin.OpenMainUi();
        }

        this.AddTooltip("Open tracker window.");
        ImGui.TextDisabled("Tracked items are saved between logouts.");
        ImGui.Separator();
    }

    private void DrawLeftNavigation()
    {
        ImGui.BeginChild("##ConfigNavigation", new Vector2(180, 0), true);
        this.DrawNavItem("Tracked Achievements", ConfigSection.TrackedAchievements);
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

    private void DrawSelectedPage()
    {
        ImGui.BeginChild("##ConfigContent", Vector2.Zero, false);
        switch (this.selectedSection)
        {
            case ConfigSection.TrackedAchievements:
                this.DrawTrackedAchievementsPage();
                break;
            case ConfigSection.Help:
                this.DrawHelp();
                break;
        }

        ImGui.EndChild();
    }

    // Section: presets.
    // Component: saved plugin configuration. Risk: low; writes only plugin config.
    private void DrawPresetControls()
    {
        this.EnsureSelectedPresetIsValid();
        ImGui.TextUnformatted("Presets");
        ImGui.SameLine();
        this.DrawPresetNameInput();
        ImGui.SameLine();
        this.DrawPresetSaveButton();
        ImGui.SameLine();
        this.DrawPresetPicker();
        ImGui.SameLine();
        this.DrawPresetReadButton();
        ImGui.SameLine();
        this.DrawPresetRenameButton();
        ImGui.SameLine();
        this.DrawPresetDeleteButton();
    }

    private void DrawPresetNameInput()
    {
        ImGui.SetNextItemWidth(190);
        if (ImGui.InputTextWithHint("##PresetName", "Preset name", ref this.presetNameInput, TrackedAchievementPresetStore.MaxPresetNameLength))
        {
            this.presetNameInput = TrackedAchievementPresetStore.SanitizeName(this.presetNameInput);
        }

        this.AddTooltip("Preset name.");
    }

    private void DrawPresetSaveButton()
    {
        if (!ImGuiComponents.IconButton("preset-save", FontAwesomeIcon.Save))
        {
            this.AddTooltip("Save current list.");
            return;
        }

        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        if (TrackedAchievementPresetStore.SavePreset(this.plugin.Configuration.TrackedAchievementPresets, this.presetNameInput, trackedIds, out var savedName))
        {
            this.selectedPresetName = savedName;
            this.presetNameInput = savedName;
            this.plugin.SaveConfiguration();
        }

        this.AddTooltip("Save current list.");
    }

    private void DrawPresetPicker()
    {
        ImGui.SetNextItemWidth(220);
        var comboLabel = string.IsNullOrWhiteSpace(this.selectedPresetName) ? "Select preset" : this.selectedPresetName;
        if (!ImGui.BeginCombo("##PresetPicker", comboLabel))
        {
            this.AddTooltip("Selecting a preset loads it immediately.");
            return;
        }

        foreach (var preset in this.plugin.Configuration.TrackedAchievementPresets.OrderBy(preset => preset.Name, StringComparer.OrdinalIgnoreCase))
        {
            this.DrawPresetPickerItem(preset.Name);
        }

        ImGui.EndCombo();
        this.AddTooltip("Selecting a preset loads it immediately.");
    }

    private void DrawPresetPickerItem(string presetName)
    {
        var selected = string.Equals(presetName, this.selectedPresetName, StringComparison.OrdinalIgnoreCase);
        if (ImGui.Selectable(presetName, selected))
        {
            this.selectedPresetName = presetName;
            this.presetNameInput = presetName;
            this.LoadSelectedPreset();
        }

        if (selected)
        {
            ImGui.SetItemDefaultFocus();
        }
    }

    private void DrawPresetReadButton()
    {
        if (ImGuiComponents.IconButton("preset-read", FontAwesomeIcon.FolderOpen))
        {
            this.LoadSelectedPreset();
        }

        this.AddTooltip("Read selected list.");
    }

    private void DrawPresetRenameButton()
    {
        if (!ImGuiComponents.IconButton("preset-rename", FontAwesomeIcon.Edit))
        {
            this.AddTooltip("Rename selected list.");
            return;
        }

        if (TrackedAchievementPresetStore.RenamePreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName, this.presetNameInput, out var renamedTo))
        {
            this.selectedPresetName = renamedTo;
            this.presetNameInput = renamedTo;
            this.plugin.SaveConfiguration();
        }

        this.AddTooltip("Rename selected list.");
    }

    private void DrawPresetDeleteButton()
    {
        if (!ImGuiComponents.IconButton("preset-delete", FontAwesomeIcon.Trash))
        {
            this.AddTooltip("Delete selected list.");
            return;
        }

        if (TrackedAchievementPresetStore.DeletePreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName))
        {
            this.selectedPresetName = string.Empty;
            this.plugin.SaveConfiguration();
            this.EnsureSelectedPresetIsValid();
        }

        this.AddTooltip("Delete selected list.");
    }

    private void EnsureSelectedPresetIsValid()
    {
        TrackedAchievementPresetStore.Normalize(this.plugin.Configuration.TrackedAchievementPresets);
        if (this.SelectedPresetExists())
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

    private bool SelectedPresetExists()
    {
        return !string.IsNullOrWhiteSpace(this.selectedPresetName)
            && TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName) is not null;
    }

    private void LoadSelectedPreset()
    {
        var preset = TrackedAchievementPresetStore.FindPreset(this.plugin.Configuration.TrackedAchievementPresets, this.selectedPresetName);
        if (preset is null)
        {
            return;
        }

        this.plugin.TrackedAchievements.LoadFrom(preset.AchievementIds);
        this.plugin.SaveTrackedAchievements();
    }

    // Section: tracked achievements page layout.
    // Component: ImGui UI. Risk: low except reload/open icons call native UI wrappers.
    private void DrawTrackedAchievementsPage()
    {
        this.DrawPresetControls();
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

    // Section: tracked list management.
    // Component: saved tracked IDs + user-guided native Achievement UI action. Risk: low-to-medium for native open buttons.
    private void DrawTrackedManagement()
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds.ToList();
        ImGui.TextUnformatted($"Tracked: {trackedIds.Count} / {TrackedAchievementStore.MaxTrackedAchievements}");
        this.DrawUpdateOpenLockoutStatus();

        if (trackedIds.Count == 0)
        {
            ImGui.TextWrapped("No achievements tracked yet. Search below and add one.");
            return;
        }

        foreach (var achievementId in trackedIds)
        {
            this.DrawTrackedAchievementRow(achievementId);
        }
    }

    private void DrawTrackedAchievementRow(uint achievementId)
    {
        ImGui.PushID((int)achievementId);
        this.DrawMoveButton("Top", "Move to top.", () => this.plugin.TrackedAchievements.MoveToTop(achievementId));
        ImGui.SameLine();
        this.DrawMoveButton("Up", "Move up one slot.", () => this.plugin.TrackedAchievements.MoveUp(achievementId));
        ImGui.SameLine();
        this.DrawMoveButton("Down", "Move down one slot.", () => this.plugin.TrackedAchievements.MoveDown(achievementId));
        ImGui.SameLine();
        this.DrawMoveButton("Bottom", "Move to bottom.", () => this.plugin.TrackedAchievements.MoveToBottom(achievementId));
        ImGui.SameLine();

        if (this.DrawTrackedRemoveButton(achievementId))
        {
            ImGui.PopID();
            return;
        }

        ImGui.SameLine();
        this.DrawTrackedUpdateButton(achievementId);
        ImGui.SameLine();
        this.DrawInspectButton(achievementId);
        ImGui.SameLine();
        this.DrawManagedAchievement(achievementId);
        ImGui.PopID();
    }

    private void DrawMoveButton(string label, string tooltip, Func<bool> moveAction)
    {
        if (ImGui.Button(label) && moveAction())
        {
            this.plugin.SaveTrackedAchievements();
        }

        this.AddTooltip(tooltip);
    }

    private bool DrawTrackedRemoveButton(uint achievementId)
    {
        var clicked = ImGuiComponents.IconButton("tracked-remove", FontAwesomeIcon.Times);
        this.AddTooltip("Remove from tracked.");
        return clicked && this.RemoveTrackedAchievement(achievementId);
    }

    private void DrawTrackedUpdateButton(uint achievementId)
    {
        var updateOpenLocked = !this.plugin.CanOpenAchievementForUpdate;
        if (updateOpenLocked)
        {
            ImGui.BeginDisabled();
        }

        if (ImGuiComponents.IconButton(FontAwesomeIcon.SyncAlt))
        {
            this.OpenAchievementForUpdate(achievementId);
        }

        if (updateOpenLocked)
        {
            ImGui.EndDisabled();
        }

        this.AddTooltip("Open native Achievement entry to update.");
    }

    private void DrawInspectButton(uint achievementId)
    {
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
        {
            this.plugin.NativeAchievementNavigator.OpenAchievement(achievementId);
        }

        this.AddTooltip("Open in Achievements.");
    }

    private void DrawManagedAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        ImGui.TextWrapped(info.Name);
        this.DrawCosmicProgressIfAvailable(achievementId);
        this.DrawCategoryPath(info.CategoryName);
    }

    private bool RemoveTrackedAchievement(uint achievementId)
    {
        if (!this.plugin.TrackedAchievements.Remove(achievementId))
        {
            return false;
        }

        this.plugin.SaveTrackedAchievements();
        return true;
    }

    // Section: search and add.
    // Component: Lumina search + saved tracked IDs. Risk: low; inspect button opens native Achievement UI by user click.
    private void DrawSearchAndAdd()
    {
        ImGui.TextUnformatted("Search achievements to track");
        this.DrawHideCompletedCheckbox();
        this.DrawSearchInput();

        if (this.searchQuery.Trim().Length < 2)
        {
            ImGui.TextDisabled("Type 2+ characters from a name or category.");
            return;
        }

        var results = this.GetVisibleSearchResults();
        if (results.Count == 0)
        {
            ImGui.TextDisabled("No matching achievements found.");
            return;
        }

        foreach (var result in results)
        {
            this.DrawSearchResultRow(result);
        }
    }

    private void DrawHideCompletedCheckbox()
    {
        var hideCompleted = this.plugin.Configuration.HideCompletedInSearch;
        if (ImGui.Checkbox("Hide completed", ref hideCompleted))
        {
            this.plugin.Configuration.HideCompletedInSearch = hideCompleted;
            this.plugin.SaveConfiguration();
        }

        this.AddTooltip("Hide completed search results.");
    }

    private void DrawSearchInput()
    {
        ImGui.SetNextItemWidth(-70);
        ImGui.InputText("##AchievementSearch", ref this.searchQuery, 128);
        ImGui.SameLine();
        if (ImGui.Button("Clear"))
        {
            this.searchQuery = string.Empty;
        }

        this.AddTooltip("Clear search.");
    }

    private System.Collections.Generic.List<AchievementTracker.Models.AchievementInfo> GetVisibleSearchResults()
    {
        return this.plugin.AchievementCatalog.Search(this.searchQuery, 200)
            .Where(result => !this.plugin.Configuration.HideCompletedInSearch || !this.IsComplete(result.Id))
            .Take(25)
            .ToList();
    }

    private void DrawSearchResultRow(AchievementTracker.Models.AchievementInfo result)
    {
        var trackedIds = this.plugin.TrackedAchievements.AchievementIds;
        var alreadyTracked = trackedIds.Contains(result.Id);
        var canAdd = trackedIds.Count < TrackedAchievementStore.MaxTrackedAchievements && !alreadyTracked;

        ImGui.PushID((int)result.Id);
        this.DrawSearchResultAction(result.Id, canAdd, alreadyTracked);
        ImGui.SameLine();
        this.DrawSearchResultDetails(result);
        ImGui.PopID();
    }

    private void DrawSearchResultAction(uint achievementId, bool canAdd, bool alreadyTracked)
    {
        if (canAdd)
        {
            this.DrawSearchAddButton(achievementId);
            return;
        }

        if (alreadyTracked)
        {
            this.DrawSearchRemoveButton(achievementId);
            return;
        }

        this.DrawSearchFullLabel(achievementId);
    }

    private void DrawSearchAddButton(uint achievementId)
    {
        if (ImGui.Button("Add") && this.plugin.TrackedAchievements.TryAdd(achievementId))
        {
            this.plugin.SaveTrackedAchievements();
        }

        this.AddTooltip("Track this achievement.");
        ImGui.SameLine();
        this.DrawInspectButton(achievementId);
    }

    private void DrawSearchRemoveButton(uint achievementId)
    {
        var removed = ImGuiComponents.IconButton("search-remove", FontAwesomeIcon.Times);
        this.AddTooltip("Remove from tracked.");
        if (removed)
        {
            this.RemoveTrackedAchievement(achievementId);
        }

        ImGui.SameLine();
        this.DrawInspectButton(achievementId);
    }

    private void DrawSearchFullLabel(uint achievementId)
    {
        ImGui.TextDisabled("Full");
        ImGui.SameLine();
        this.DrawInspectButton(achievementId);
    }

    private void DrawSearchResultDetails(AchievementTracker.Models.AchievementInfo result)
    {
        ImGui.BeginGroup();
        ImGui.TextWrapped(result.Name);
        this.DrawCosmicProgressIfAvailable(result.Id);
        this.DrawCategoryPath(result.CategoryName);
        ImGui.EndGroup();
    }

    // Section: display helpers.
    // Component: progress/category UI. Risk: low; progress may call local Cosmic score provider.
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

    private bool IsComplete(uint achievementId)
        => this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row)
            && this.plugin.AchievementProgressService.IsComplete(row);

    // Section: help page.
    // Component: player-facing text. Risk: low.
    private void DrawHelp()
    {
        ImGui.TextUnformatted("Help");
        ImGui.TextWrapped("Veela's Achievement Ledger uses a user-guided native Achievement UI flow. Reload buttons open the game's Achievement entry; the plugin passively caches progress the client returns.");
        ImGui.Separator();

        ImGui.TextUnformatted("Main VAL window");
        this.DrawWrappedBullet("Shows tracked achievements, progress, last observed update time, and row actions.");
        this.DrawWrappedBullet("Update Next opens the native Achievement entry for the next tracked item needing a refresh.");
        this.DrawWrappedBullet("Use the row reload icon to open that achievement entry directly.");
        this.DrawWrappedBullet("Use the magnifying glass to open that achievement in the native Achievements window.");

        ImGui.Separator();
        ImGui.TextUnformatted("Config sections");
        this.DrawWrappedBullet("Tracked Achievements manages tracked rows, presets, ordering, search, add/remove, Cosmic score planning, and native Achievement opens.");
        this.DrawWrappedBullet("Help explains the windows and controls.");

        ImGui.Separator();
        ImGui.TextUnformatted("Tracked Achievements notes");
        this.DrawWrappedBullet("Presets save, read, rename, and delete reusable tracked-achievement lists. Selecting a preset loads it immediately; Read reloads the selected preset on demand.");
        this.DrawWrappedBullet("Search adds achievements to the tracked list; Clear resets the search bar.");
        this.DrawWrappedBullet("Cosmic Class achievements show cached score progress in tracked and search rows when scores have been observed in Sinus Ardorum.");
        this.DrawWrappedBullet("Cosmic score cache is read-only local state. It does not request achievement progress from the server.");
    }

    // Section: small shared UI helpers.
    // Component: pure UI + user-guided native open wrapper. Risk: low-to-medium for native open wrapper.
    private void DrawWrappedBullet(string text)
    {
        ImGui.Bullet();
        ImGui.SameLine();
        ImGui.TextWrapped(text);
    }

    private void OpenAchievementForUpdate(uint achievementId)
    {
        if (this.plugin.OpenAchievementForUpdate(achievementId))
        {
            return;
        }

        ImGui.TextDisabled(this.plugin.CanOpenAchievementForUpdate
            ? "Could not open Achievements right now."
            : "Achievement update opens are cooling down.");
    }

    private void DrawUpdateOpenLockoutStatus()
    {
        var remaining = this.plugin.AchievementUpdateOpenRemaining;
        if (remaining > TimeSpan.Zero)
        {
            ImGui.TextDisabled($"Achievement update opens available in {Math.Ceiling(remaining.TotalSeconds):0}s");
        }
    }

    private void AddTooltip(string text)
    {
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(text);
        }
    }
}
