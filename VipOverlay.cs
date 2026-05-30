using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.UI;
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
        private readonly Dictionary<ulong, IntPtr> _vipNamePlateObjects = new();

        public VipOverlay(VipManager manager, Configuration config)
        {
            _vipManager = manager;
            _config = config;
            Service.PluginInterface.UiBuilder.Draw += Draw;
            Service.NamePlateGui.OnPostNamePlateUpdate += OnNamePlateUpdate;
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
                            DrawHighlightRing(player, drawList, profile);
                        }

                    }
                }
            }

            if (profile.ShowVipTag)
            {
                DrawVipNamePlateTags(drawList, profile);
            }

            if (profile.ShowVipList && vipsInRange != null)
            {
                DrawVipListWindow(vipsInRange, profile);
            }
        }

        private List<(IPlayerCharacter player, float distance, List<string> data)> GetVipsInRange(VipProfile profile)
        {
            var results = new List<(IPlayerCharacter player, float distance, List<string> data)>();
            var localPlayer = Service.ObjectTable.LocalPlayer;
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

        private void DrawHighlightRing(IPlayerCharacter player, ImDrawListPtr drawList, VipProfile profile)
        {
            var gameObject = (GameObject*)player.Address;
            if (gameObject == null) return;

            Vector3 pos = gameObject->Position;
            float radius = profile.RingRadius;
            int segments = 32;
            uint color = ColorToUint(profile.RingColor);

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
                if (profile.RingSolid)
                {
                    drawList.AddConvexPolyFilled(ref points[0], visiblePoints, color);
                }
                else
                {
                    for (int i = 0; i < visiblePoints; i++)
                    {
                        drawList.AddLine(points[i], points[(i + 1) % visiblePoints], color, 3.0f);
                    }
                }
            }
        }

        private void OnNamePlateUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
        {
            var profile = _config.GetActiveProfile();
            if (!profile.IsOverlayEnabled || !profile.ShowVipTag)
            {
                _vipNamePlateObjects.Clear();
                return;
            }

            if (context.IsFullUpdate)
            {
                _vipNamePlateObjects.Clear();
            }

            foreach (var handler in handlers)
            {
                var player = handler.PlayerCharacter;
                if (player == null || !_vipManager.IsVip(player.Name.TextValue))
                {
                    _vipNamePlateObjects.Remove(handler.GameObjectId);
                    continue;
                }

                var namePlateObjectAddress = handler.NamePlateObjectAddress;
                if (namePlateObjectAddress != IntPtr.Zero)
                {
                    _vipNamePlateObjects[handler.GameObjectId] = namePlateObjectAddress;
                }
            }
        }

        private void DrawVipNamePlateTags(ImDrawListPtr drawList, VipProfile profile)
        {
            if (_vipNamePlateObjects.Count == 0)
            {
                return;
            }

            var staleIds = new List<ulong>();

            foreach (var (gameObjectId, namePlateObjectAddress) in _vipNamePlateObjects)
            {
                if (!IsCurrentVipPlayer(gameObjectId) || !DrawVipNamePlateTag(drawList, namePlateObjectAddress, profile))
                {
                    staleIds.Add(gameObjectId);
                }
            }

            foreach (ulong gameObjectId in staleIds)
            {
                _vipNamePlateObjects.Remove(gameObjectId);
            }
        }

        private bool IsCurrentVipPlayer(ulong gameObjectId)
        {
            foreach (var actor in Service.ObjectTable)
            {
                if (actor is IPlayerCharacter player &&
                    player.GameObjectId == gameObjectId &&
                    _vipManager.IsVip(player.Name.TextValue))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool DrawVipNamePlateTag(ImDrawListPtr drawList, IntPtr namePlateObjectAddress, VipProfile profile)
        {
            var namePlateObject = (AddonNamePlate.NamePlateObject*)namePlateObjectAddress;
            if (namePlateObject == null || !namePlateObject->IsVisible || namePlateObject->NameText == null)
            {
                return false;
            }

            var nameText = namePlateObject->NameText;
            if (!nameText->IsVisible())
            {
                return false;
            }

            const string tagText = "VIP";
            const float gap = 6.0f;
            const float paddingX = 5.0f;
            const float paddingY = 2.0f;
            const float rounding = 3.0f;

            Vector2 textSize = ImGui.CalcTextSize(tagText);
            Vector2 tagSize = textSize + new Vector2(paddingX * 2.0f, paddingY * 2.0f);

            float scaleX = MathF.Max(0.1f, nameText->ScaleX);
            float scaleY = MathF.Max(0.1f, nameText->ScaleY);
            float nameWidth = namePlateObject->TextW > 0
                ? namePlateObject->TextW * scaleX
                : nameText->Width * scaleX;
            float nameHeight = namePlateObject->TextH > 0
                ? namePlateObject->TextH * scaleY
                : nameText->Height * scaleY;

            var tagPos = new Vector2(
                nameText->ScreenX - tagSize.X - gap + profile.VipTagOffsetX,
                nameText->ScreenY + (nameHeight - tagSize.Y) * 0.5f + profile.VipTagOffsetY);
            var tagMax = tagPos + tagSize;
            var textPos = tagPos + new Vector2(paddingX, paddingY);
            uint tagColor = ColorToUint(profile.VipTagColor);

            drawList.AddRectFilled(tagPos, tagMax, 0xB0000000, rounding);
            drawList.AddText(textPos + Vector2.One, 0xFF000000, tagText);
            drawList.AddText(textPos, tagColor, tagText);

            return true;
        }

        private static uint ColorToUint(System.Numerics.Vector4 c)
        {
            byte r = (byte)(Math.Clamp(c.X, 0f, 1f) * 255);
            byte g = (byte)(Math.Clamp(c.Y, 0f, 1f) * 255);
            byte b = (byte)(Math.Clamp(c.Z, 0f, 1f) * 255);
            byte a = (byte)(Math.Clamp(c.W, 0f, 1f) * 255);
            return (uint)((a << 24) | (b << 16) | (g << 8) | r);
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
            Service.NamePlateGui.OnPostNamePlateUpdate -= OnNamePlateUpdate;
        }
    }
}
