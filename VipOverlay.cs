using Dalamud.Game.ClientState.Objects.SubKinds;
using ImGuiNET;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace VipNameChecker
{
    public unsafe class VipOverlay : IDisposable
    {
        private readonly VipManager _vipManager;
        public bool Enabled { get; set; } = true;

        public VipOverlay(VipManager manager)
        {
            _vipManager = manager;
            Service.PluginInterface.UiBuilder.Draw += Draw;
        }

        private void Draw()
        {
            if (!Enabled) return;

            try
            {
                if (Service.ObjectTable.Length == 0) return;

                // Move ImGui calls to separate method to prevent JIT crashes
                DrawImGuiContent();
            }
            catch (Exception ex)
            {
                // Log errors silently to the Dalamud console (/xllog) instead of chat
                Service.PluginLog.Debug($"[VIP Error] Draw loop exception: {ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawImGuiContent()
        {
            var drawList = ImGui.GetForegroundDrawList();

            foreach (var actor in Service.ObjectTable)
            {
                if (actor is IPlayerCharacter player)
                {
                    if (_vipManager.IsVip(player.Name.TextValue))
                    {
                        DrawCheckmark(player, drawList);
                    }
                }
            }
        }

        private void DrawCheckmark(IPlayerCharacter player, ImDrawListPtr drawList)
        {
            var gameObject = (GameObject*)player.Address;
            if (gameObject == null) return;

            float HeightOffset = 2.0f;
            float ScreenOffsetX = 80.0f;
            float ScreenOffsetY = -25.0f;

            var pos3d = gameObject->Position;
            pos3d.Y += HeightOffset;

            if (Service.GameGui.WorldToScreen(pos3d, out var screenPos))
            {
                screenPos.X += ScreenOffsetX;
                screenPos.Y += ScreenOffsetY;
                screenPos.X = MathF.Round(screenPos.X);
                screenPos.Y = MathF.Round(screenPos.Y);

                string text = "★ VIP";
                uint color = 0xFF00FF00;

                // Draw Shadow (Black)
                drawList.AddText(new Vector2(screenPos.X + 1, screenPos.Y + 1), 0xFF000000, text);
                // Draw Text (Green)
                drawList.AddText(screenPos, color, text);
            }
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
        }
    }
}