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
    private ConfigSection selectedSection = ConfigSection.Ui;

    public ConfigWindow(Plugin plugin)
        : base("Achieve Ex Config##AchieveExConfig", ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoBringToFrontOnFocus)
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(720, 420),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        this.Size = new Vector2(900, 560);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    private enum ConfigSection
    {
        Ui,
        Help,
    }

    public void OpenConfig()
    {
        this.selectedSection = ConfigSection.Ui;
        this.IsOpen = true;
    }

    public void OpenHelp()
    {
        this.selectedSection = ConfigSection.Help;
        this.IsOpen = true;
    }

    public override void Draw()
    {
        if (ImGui.Button("Open Achieve Ex"))
        {
            this.plugin.OpenMainUi();
        }
        this.AddTooltip("Open tracker window.");

        if (ImGui.Button("Restore Achievement window default scale"))
        {
            _ = this.plugin.RestoreNativeAchievementWindowDefaultScale();
        }
        this.AddTooltip("Open/show the native Achievement window if needed, then restore its scale to the default 100%.");

        this.DrawDisabledWrapped("Tracked items and Lists are saved between logouts.");
        this.DrawDisabledWrapped("Mainline safety: each reload or magnifying-glass click opens at most one native Achievement entry and can update cached status when progress data loads.");
        ImGui.Separator();

        this.DrawLeftNavigation();
        ImGui.SameLine();
        ImGui.BeginChild("##ConfigContent", Vector2.Zero, false);
        switch (this.selectedSection)
        {
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

    private void DrawUiPage()
    {
        ImGui.TextUnformatted("Search category display");
        var hideZeroCountIncompleteCategories = this.plugin.Configuration.HideZeroCountIncompleteSearchCategories;
        if (ImGui.Checkbox("Hide zero-count categories when Incomplete is selected", ref hideZeroCountIncompleteCategories))
        {
            this.plugin.Configuration.HideZeroCountIncompleteSearchCategories = hideZeroCountIncompleteCategories;
            this.plugin.SaveConfiguration();
        }
        this.AddTooltip("When the main search completion filter is Incomplete, hide categories and subcategories whose filtered count is 0.");

        ImGui.Separator();
        ImGui.TextUnformatted("Main panel column order");
        ImGui.TextDisabled("Top to bottom here means left to right in the main panel.");
        this.DrawOrderEditor(this.plugin.Configuration.MainColumnOrder, ["Lists", "Search Categories", "Search Results", "Tracked Achievements"], "columns");

        ImGui.Separator();
        ImGui.TextUnformatted("Main panel navigation order");
        ImGui.TextDisabled("Top to bottom here means left to right in the main panel navigation.");
        this.DrawOrderEditor(this.plugin.Configuration.MainNavigationOrder, ["Lists", "Search", "Config", "Tracked buttons"], "nav");

        ImGui.Separator();
        ImGui.TextUnformatted("Show main panel navigation buttons");
        this.DrawShownToggleGroup(
            "All main panel buttons",
            this.plugin.Configuration.ShownMainNavigationButtons,
            ["Lists", "Search", "Config", "Tracked buttons"]);

        ImGui.Separator();
        ImGui.TextUnformatted("Tracked achievement buttons hidden by the red eye button");
        ImGui.TextDisabled("White eye = normal/default and shows all tracked achievement buttons. Red eye = hide only the checked buttons below.");
        this.DrawHiddenToggleGroup(
            "All tracked achievement buttons",
            this.plugin.Configuration.HiddenTrackedAchievementIcons,
            ["Remove", "Refresh", "Open"]);

        ImGui.Separator();
        ImGui.TextUnformatted("Main panel column widths");
        ImGui.TextDisabled("Effective minimums: Lists 270, Search Categories 320, Search Results 550, Tracked Achievements 320.");
        this.DrawColumnWidthEditor("Lists", MainPanelColumnWidthDefaults.Lists);
        this.DrawColumnWidthEditor("Search Categories", MainPanelColumnWidthDefaults.SearchCategories);
        this.DrawColumnWidthEditor("Search Results", MainPanelColumnWidthDefaults.SearchResults);
        this.DrawColumnWidthEditor("Tracked Achievements", MainPanelColumnWidthDefaults.TrackedAchievements);
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

        order.RemoveAll(item => !defaults.Contains(item));

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
        ImGui.PushID(parentLabel);
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
            ImGui.PushID(child);
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

            ImGui.PopID();
        }

        ImGui.Unindent(18);
        ImGui.PopID();
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
        ImGui.PushID(parentLabel);
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
            ImGui.PushID(child);
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

            ImGui.PopID();
        }

        ImGui.Unindent(18);
        ImGui.PopID();
    }

    private void DrawHelp()
    {
        ImGui.TextUnformatted("Help");
        ImGui.TextWrapped("Achieve Ex uses a user-guided native Achievement UI flow. Reload buttons open exactly one game's Achievement entry per click; the plugin watches briefly for matching local progress data.");
        ImGui.Separator();

        ImGui.TextUnformatted("Main Achieve Ex window");
        this.DrawWrappedBullet("Lists: save, load, copy, rename, and delete reusable tracked-achievement lists.");
        this.DrawWrappedBullet("Search Categories and Search Results are Lumina/search UI only; they do not open the native Achievement window automatically.");
        this.DrawWrappedBullet("Tracked Achievements shows your tracked rows. Row reload and magnifying-glass buttons each open one native Achievement entry and can update cached status when progress data loads.");
        this.DrawWrappedBullet("The toolbar eye hides or shows configured tracked-row buttons. The disk toggles Lists, the book toggles search, and the gear toggles this config window.");
        this.DrawWrappedBullet("Cosmic Class achievements show cached score progress when local WKS/Cosmic scores have been observed. The cache is read-only local state.");
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
