using Dalamud.Game.ClientState.Objects.SubKinds;
using ImGuiNET;
using System;
using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace VipNameChecker
{
    public unsafe class VipOverlay : IDisposable
    {
        private readonly VipManager _vipManager;

        // NEW: Controls whether we draw anything or not. Default is ON.
        public bool Enabled { get; set; } = true;

        public VipOverlay(VipManager manager)
        {
            _vipManager = manager;
            Service.PluginInterface.UiBuilder.Draw += Draw;
        }

        private void Draw()
        {
            // 1. Check if the plugin is manually disabled via command
            if (!Enabled) return;

            // 2. Standard safety checks
            if (Service.ObjectTable.Length == 0 || Service.ObjectTable[0] == null) return;

            foreach (var actor in Service.ObjectTable)
            {
                if (actor is IPlayerCharacter player)
                {
                    if (_vipManager.IsVip(player.Name.TextValue))
                    {
                        DrawCheckmark(player);
                    }
                }
            }
        }

        private void DrawCheckmark(IPlayerCharacter player)
        {
            var gameObject = (GameObject*)player.Address;
            if (gameObject == null) return;

            // --- POSITION SETTINGS ---
            float HeightOffset = 2.0f;
            float ScreenOffsetX = 80.0f;
            float ScreenOffsetY = -25.0f;
            // -------------------------

            var pos3d = gameObject->Position;
            pos3d.Y += HeightOffset;

            if (Service.GameGui.WorldToScreen(pos3d, out var screenPos))
            {
                screenPos.X += ScreenOffsetX;
                screenPos.Y += ScreenOffsetY;

                // Jitter Fix
                screenPos.X = MathF.Round(screenPos.X);
                screenPos.Y = MathF.Round(screenPos.Y);

                var drawList = ImGui.GetForegroundDrawList();
                string text = "★ VIP";
                uint color = 0xFF00FF00;

                drawList.AddText(new Vector2(screenPos.X + 1, screenPos.Y + 1), 0xFF000000, text);
                drawList.AddText(screenPos, color, text);
            }
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
        }
    }
}