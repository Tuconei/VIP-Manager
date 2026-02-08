using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;

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
            var profile = _config.GetActiveProfile();
            if (!profile.IsOverlayEnabled || Service.ObjectTable.Length == 0) return;

            try
            {
                DrawImGuiContent(profile);
            }
            catch (Exception ex)
            {
                Service.PluginLog.Debug($"[VIP Error] Draw loop exception: {ex.Message}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DrawImGuiContent(VipProfile profile)
        {
            var drawList = ImGui.GetForegroundDrawList();

            // Optimization: Only calculate list if window is actually needed? 
            // For now, we calculate anyway to keep logic simple, or we can check if we are drawing the list.
            List<(IPlayerCharacter player, float distance, List<string> data)>? vipsInRange = null;
            if (profile.ShowVipList)
            {
                vipsInRange = GetVipsInRange(profile);
            }

            foreach (var actor in Service.ObjectTable)
            {
                if (actor is IPlayerCharacter player)
                {
                    if (_vipManager.IsVip(player.Name.TextValue))
                    {
                        if (profile.ShowHighlightRing)
                        {
                            DrawHighlightRing(player, drawList);
                        }

                        if (profile.ShowVipTag)
                        {
                            DrawCenteredVipText(player, drawList);
                        }
                    }
                }
            }

            if (profile.ShowVipList && vipsInRange != null)
            {
                DrawVipListWindow(vipsInRange, profile);
            }
        }

        private List<(IPlayerCharacter player, float distance, List<string> data)> GetVipsInRange(VipProfile profile)
        {
            var results = new List<(IPlayerCharacter player, float distance, List<string> data)>();
            var localPlayer = Service.ClientState.LocalPlayer;
            if (localPlayer == null)
            {
                return results;
            }

            float maxDistance = MathF.Max(0.0f, profile.VipListRange);

            foreach (var actor in Service.ObjectTable)
            {
                if (actor is IPlayerCharacter player)
                {
                    if (!_vipManager.IsVip(player.Name.TextValue))
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(localPlayer.Position, player.Position);
                    if (distance <= maxDistance && player != localPlayer)
                    {
                        var vipData = _vipManager.GetVipData(player.Name.TextValue);
                        // If data is null (shouldn't be if IsVip is true), use empty list
                        results.Add((player, distance, vipData ?? new List<string>()));
                    }
                }
            }

            results.Sort((left, right) => left.distance.CompareTo(right.distance));
            return results;
        }

        private void DrawVipListWindow(List<(IPlayerCharacter player, float distance, List<string> data)> vipsInRange, VipProfile profile)
        {
            ImGui.SetNextWindowSize(new Vector2(520, 300), ImGuiCond.FirstUseEver);

            // Create a local bool initialized to the profile setting to track state
            bool isOpen = profile.ShowVipList;

            // Pass ref isOpen to enable the close button (X) in the window header
            if (ImGui.Begin("VIPs In Range", ref isOpen))
            {
                if (vipsInRange.Count == 0)
                {
                    ImGui.Text("No VIPs within range.");
                }
                else
                {
                    // 1 column for Name + N columns from profile
                    int colCount = 1 + profile.Columns.Count;

                    if (ImGui.BeginTable("VipListTable", colCount, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable))
                    {
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);

                        foreach (var colDef in profile.Columns)
                        {
                            ImGui.TableSetupColumn(colDef.Header, ImGuiTableColumnFlags.WidthFixed, colDef.Width);
                        }

                        ImGui.TableHeadersRow();

                        foreach (var (player, distance, data) in vipsInRange)
                        {
                            ImGui.TableNextRow();
                            ImGui.TableSetColumnIndex(0);

                            string label = $"{player.Name.TextValue} ({distance:F1}y)";
                            if (ImGui.Selectable(label, false, ImGuiSelectableFlags.SpanAllColumns))
                            {
                                Service.TargetManager.Target = player;
                            }

                            for (int i = 0; i < profile.Columns.Count; i++)
                            {
                                ImGui.TableSetColumnIndex(i + 1);
                                if (i < data.Count)
                                {
                                    ImGui.TextUnformatted(data[i]);
                                }
                                else
                                {
                                    ImGui.TextUnformatted("-");
                                }
                            }
                        }

                        ImGui.EndTable();

                    }
                }

                ImGui.End();
            }

            // Check if the user closed the window using the X button
            if (isOpen != profile.ShowVipList)
            {
                profile.ShowVipList = isOpen;
                _config.Save();
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