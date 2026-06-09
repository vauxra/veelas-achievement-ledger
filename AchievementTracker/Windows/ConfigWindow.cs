using AchievementTracker.Services;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System.Linq;
using System.Numerics;

namespace AchievementTracker.Windows;

public sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private string searchQuery = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base("Veela's Ledger Config##AchievementLedgerConfig")
    {
        this.plugin = plugin;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public override void Draw()
    {
        ImGui.TextUnformatted("Tracked achievements");
        ImGui.TextDisabled("Tracked items are saved between logouts.");
        ImGui.TextDisabled("Use ↻ to open an achievement in the game UI and update progress.");
        ImGui.Separator();

        this.DrawTrackedManagement();
        ImGui.Separator();
        this.DrawSearchAndAdd();
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
            if (ImGui.Button("Up") && this.plugin.TrackedAchievements.MoveUp(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
            }

            ImGui.SameLine();
            if (ImGui.Button("Down") && this.plugin.TrackedAchievements.MoveDown(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
            }

            ImGui.SameLine();
            if (ImGui.Button("Remove") && this.plugin.TrackedAchievements.Remove(achievementId))
            {
                this.plugin.SaveTrackedAchievements();
                ImGui.PopID();
                continue;
            }

            ImGui.SameLine();
            if (ImGui.Button("↻"))
            {
                this.plugin.NativeAchievementNavigator.OpenAchievement(achievementId);
            }

            ImGui.SameLine();
            this.DrawManagedAchievement(achievementId);
            ImGui.PopID();
        }
    }

    private void DrawManagedAchievement(uint achievementId)
    {
        _ = this.plugin.AchievementCatalog.TryGet(achievementId, out var info);
        var progressText = "Progress unavailable";
        if (this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row))
        {
            progressText = this.plugin.AchievementProgressService.GetProgress(row).ToDisplayText();
        }

        ImGui.TextWrapped($"{info.Name} — {progressText}");
        if (!string.IsNullOrWhiteSpace(info.CategoryName) || info.Points > 0)
        {
            ImGui.TextDisabled($"{info.CategoryName}  {info.Points} pts");
        }
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

        ImGui.SetNextItemWidth(-1);
        ImGui.InputText("##AchievementSearch", ref this.searchQuery, 128);

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
            ImGui.TextDisabled("No matching achievements found.");
            return;
        }

        foreach (var result in results)
        {
            ImGui.PushID((int)result.Id);
            var alreadyTracked = trackedIds.Contains(result.Id);
            var canAdd = !isFull && !alreadyTracked;

            if (canAdd && ImGui.Button("Add"))
            {
                if (this.plugin.TrackedAchievements.TryAdd(result.Id))
                {
                    this.plugin.SaveTrackedAchievements();
                }
            }
            else if (!canAdd)
            {
                ImGui.TextDisabled(alreadyTracked ? "Added" : "Full");
            }

            ImGui.SameLine();
            ImGui.TextWrapped($"{result.Name} ({result.Points} pts)");
            if (!string.IsNullOrWhiteSpace(result.CategoryName))
            {
                ImGui.TextDisabled(result.CategoryName);
            }

            ImGui.PopID();
        }
    }

    private bool IsComplete(uint achievementId)
        => this.plugin.AchievementCatalog.TryGetRow(achievementId, out var row)
            && this.plugin.AchievementProgressService.IsComplete(row);
}
