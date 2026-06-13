using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace AchievementTracker.Windows;

internal static class GameGuiTheme
{
    private static readonly Vector4 WindowBg = new(0.055f, 0.055f, 0.070f, 0.94f);
    private static readonly Vector4 ChildBg = new(0.075f, 0.070f, 0.085f, 0.90f);
    private static readonly Vector4 FrameBg = new(0.115f, 0.105f, 0.125f, 0.96f);
    private static readonly Vector4 FrameBgHovered = new(0.170f, 0.145f, 0.165f, 0.98f);
    private static readonly Vector4 Header = new(0.180f, 0.145f, 0.095f, 0.88f);
    private static readonly Vector4 HeaderHovered = new(0.250f, 0.190f, 0.115f, 0.96f);
    private static readonly Vector4 Button = new(0.135f, 0.115f, 0.130f, 0.96f);
    private static readonly Vector4 ButtonHovered = new(0.245f, 0.185f, 0.105f, 0.96f);
    private static readonly Vector4 ButtonActive = new(0.315f, 0.225f, 0.115f, 1.00f);
    private static readonly Vector4 Border = new(0.650f, 0.535f, 0.315f, 0.95f);
    private static readonly Vector4 CheckMark = new(0.980f, 0.800f, 0.430f, 1.00f);
    private static readonly Vector4 TextDisabled = new(0.680f, 0.630f, 0.530f, 1.00f);
    private static readonly Vector4 Gold = new(0.880f, 0.715f, 0.385f, 1.00f);
    private static readonly Vector4 MutedGold = new(0.640f, 0.520f, 0.300f, 1.00f);

    public static StyleScope PushStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, WindowBg);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, ChildBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, FrameBg);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, FrameBgHovered);
        ImGui.PushStyleColor(ImGuiCol.Header, Header);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, HeaderHovered);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Button, Button);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ButtonHovered);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ButtonActive);
        ImGui.PushStyleColor(ImGuiCol.Border, Border);
        ImGui.PushStyleColor(ImGuiCol.CheckMark, CheckMark);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, TextDisabled);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1.35f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1.15f);
        ImGui.PushStyleVar(ImGuiStyleVar.DisabledAlpha, 0.72f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 5f));
        return new StyleScope(13, 8);
    }

    public static void DrawSectionHeader(string label)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, Gold);
        ImGui.TextUnformatted(label);
        ImGui.PopStyleColor();
        var drawList = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var endX = start.X + ImGui.GetContentRegionAvail().X;
        drawList.AddLine(start, new Vector2(endX, start.Y), ImGui.GetColorU32(MutedGold), 1f);
        ImGui.Dummy(new Vector2(0, 4));
    }

    public sealed class StyleScope : IDisposable
    {
        private readonly int colorCount;
        private readonly int styleCount;
        private bool disposed;

        public StyleScope(int colorCount, int styleCount)
        {
            this.colorCount = colorCount;
            this.styleCount = styleCount;
        }

        public void Dispose()
        {
            if (this.disposed)
            {
                return;
            }

            this.disposed = true;
            ImGui.PopStyleVar(this.styleCount);
            ImGui.PopStyleColor(this.colorCount);
        }
    }
}
