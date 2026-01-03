using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Bindings.ImGui; // Requires successful 'dotnet restore' to resolve
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
                // CORRECTED: Use .Length for API 14 compatibility
                if (Service.ObjectTable.Length == 0) return;

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
            var pos3d = gameObject->Position;
            pos3d.Y += HeightOffset;

            if (Service.GameGui.WorldToScreen(pos3d, out var screenPos))
            {
                screenPos.X += 80.0f;
                screenPos.Y += -25.0f;
                screenPos.X = MathF.Round(screenPos.X);
                screenPos.Y = MathF.Round(screenPos.Y);

                string text = "★ VIP";
                drawList.AddText(new Vector2(screenPos.X + 1, screenPos.Y + 1), 0xFF000000, text);
                drawList.AddText(screenPos, 0xFF00FF00, text);
            }
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
        }
    }
}