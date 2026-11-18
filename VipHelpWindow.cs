using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace VipNameChecker;

internal sealed class VipHelpWindow : IDisposable
{
    private bool isOpen;
    public VipHelpWindow() => Service.PluginInterface.UiBuilder.Draw += Draw;
    public void Open() => isOpen = true;
    private void Draw()
    {
        if (!isOpen) return;
        ImGui.SetNextWindowSize(new Vector2(650, 600), ImGuiCond.FirstUseEver);
        if (ImGui.Begin("VIP Name Checker - Help", ref isOpen))
        {
            ImGui.TextWrapped("Commands and quick-start guide. All display settings apply to the active venue profile.");
            ImGui.Separator();
            if (ImGui.CollapsingHeader("Commands", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var entry in VipHelpContent.Commands)
                {
                    ImGui.TextUnformatted(entry.Command);
                    ImGui.Indent(); ImGui.TextWrapped(entry.Description); ImGui.Unindent();
                    ImGui.Spacing();
                }
            }
            if (ImGui.CollapsingHeader("Setup and features", ImGuiTreeNodeFlags.DefaultOpen))
            {
                foreach (var entry in VipHelpContent.Basics)
                {
                    ImGui.Separator(); ImGui.TextUnformatted(entry.Title);
                    ImGui.TextWrapped(entry.Text); ImGui.Spacing();
                }
            }
        }
        ImGui.End();
    }
    public void Dispose() => Service.PluginInterface.UiBuilder.Draw -= Draw;
}
