using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace VipNameChecker
{
    public unsafe class VipOverlay : IDisposable
    {
        private readonly VipManager _vipManager;
        private readonly Configuration _config;

        public VipOverlay(VipManager manager, Configuration config)
        {
            _vipManager = manager;
            _config = config;
            Service.PluginInterface.UiBuilder.Draw += Draw;
        }

        private void Draw()
        {
            if (!_config.IsOverlayEnabled || Service.ObjectTable.Length == 0) return;

            try
            {
                DrawImGuiContent();
            }
            catch (Exception ex)
            {
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
                        if (_config.ShowHighlightRing)
                        {
                            DrawHighlightRing(player, drawList);
                        }

                        // NEW: Check setting before drawing overhead text
                        if (_config.ShowVipTag)
                        {
                            DrawCenteredVipText(player, drawList);
                        }
                    }
                }
            }
        }

        private void DrawHighlightRing(IPlayerCharacter player, ImDrawListPtr drawList)
        {
            var gameObject = (GameObject*)player.Address;
            if (gameObject == null) return;

            Vector3 pos = gameObject->Position;
            float radius = 0.8f;
            int segments = 32;
            uint color = 0xFF00FF00;

            var points = new Vector2[segments];
            int visiblePoints = 0;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * MathF.PI * 2;
                Vector3 worldPoint = new Vector3(
                    pos.X + MathF.Cos(angle) * radius,
                    pos.Y + 0.1f,
                    pos.Z + MathF.Sin(angle) * radius
                );

                if (Service.GameGui.WorldToScreen(worldPoint, out var screenPoint))
                {
                    points[visiblePoints++] = screenPoint;
                }
            }

            if (visiblePoints >= 3)
            {
                for (int i = 0; i < visiblePoints; i++)
                {
                    drawList.AddLine(points[i], points[(i + 1) % visiblePoints], color, 3.0f);
                }
            }
        }

        private void DrawCenteredVipText(IPlayerCharacter player, ImDrawListPtr drawList)
        {
            var gameObject = (GameObject*)player.Address;
            if (gameObject == null) return;

            float characterHeight = 1.9f;
            var pos3d = gameObject->Position;
            pos3d.Y += characterHeight + 0.5f;

            if (Service.GameGui.WorldToScreen(pos3d, out var screenPos))
            {
                string text = "★ VIP ★";
                var textSize = ImGui.CalcTextSize(text);

                Vector2 textPos = new Vector2(
                    screenPos.X - (textSize.X / 2.0f),
                    screenPos.Y - textSize.Y
                );

                drawList.AddText(new Vector2(textPos.X + 1, textPos.Y + 1), 0xFF000000, text);
                drawList.AddText(textPos, 0xFF00FF00, text);
            }
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
        }
    }
}