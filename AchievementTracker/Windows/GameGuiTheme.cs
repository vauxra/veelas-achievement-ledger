using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
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
    private static readonly Vector4 Border = new(0.530f, 0.435f, 0.260f, 0.55f);
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
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(6f, 5f));
        return new StyleScope(11, 5);
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

    public static bool DrawGameIcon(uint iconId, Vector2 size)
    {
        if (iconId == 0)
        {
            ImGui.Dummy(size);
            return false;
        }

        var lookup = new GameIconLookup(iconId, false, true, null);
        if (!Plugin.TextureProvider.TryGetFromGameIcon(in lookup, out var sharedTexture))
        {
            ImGui.Dummy(size);
            return false;
        }

        if (!sharedTexture.TryGetWrap(out IDalamudTextureWrap? texture, out _))
        {
            ImGui.Dummy(size);
            return false;
        }

        ImGui.Image(texture.Handle, size);
        return true;
    }

    public static void DrawIconFrame(uint iconId, float size = 28f)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var frameSize = new Vector2(size, size);
        drawList.AddRectFilled(pos, pos + frameSize, ImGui.GetColorU32(FrameBg), 4f);
        drawList.AddRect(pos, pos + frameSize, ImGui.GetColorU32(Border), 4f);
        ImGui.SetCursorScreenPos(pos + new Vector2(2f, 2f));
        DrawGameIcon(iconId, new Vector2(size - 4f, size - 4f));
        ImGui.SetCursorScreenPos(pos + new Vector2(size, size));
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
