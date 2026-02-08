using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;
using System.Linq;

namespace VipNameChecker
{
    public class VipGui : IDisposable
    {
        private readonly Configuration _config;
        private readonly VipManager _vipManager;
        private bool _isOpen = false;

        // Temporary input buffers for adding new columns
        private string _newColHeader = "";
        private string _newColIndex = "";
        private string _newProfileName = "";

        public bool IsOpen
        {
            get => _isOpen;
            set => _isOpen = value;
        }

        public VipGui(Configuration config, VipManager manager)
        {
            _config = config;
            _vipManager = manager;
            Service.PluginInterface.UiBuilder.Draw += Draw;
            Service.PluginInterface.UiBuilder.OpenConfigUi += OnOpenConfigUi;
        }

        private void OnOpenConfigUi() => _isOpen = true;

        private void Draw()
        {
            if (!_isOpen) return;

            ImGui.SetNextWindowSize(new Vector2(450, 500), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("VIP Name Checker Settings", ref _isOpen))
            {
                DrawProfileSelector();

                ImGui.Separator();

                var profile = _config.GetActiveProfile();

                // Profile Rename
                string pName = profile.Name;
                if (ImGui.InputText("Profile Name", ref pName, 64))
                {
                    profile.Name = pName;
                    _config.Save();
                }

                ImGui.Separator();

                bool isEnabled = profile.IsOverlayEnabled;
                if (ImGui.Checkbox("Enable Overlay", ref isEnabled))
                {
                    profile.IsOverlayEnabled = isEnabled;
                    _config.Save();
                }

                bool showRing = profile.ShowHighlightRing;
                if (ImGui.Checkbox("Show Highlight Ring", ref showRing))
                {
                    profile.ShowHighlightRing = showRing;
                    _config.Save();
                }

                bool showTag = profile.ShowVipTag;
                if (ImGui.Checkbox("Show VIP Tag", ref showTag))
                {
                    profile.ShowVipTag = showTag;
                    _config.Save();
                }

                bool showVipList = profile.ShowVipList;
                if (ImGui.Checkbox("Show VIPs In Range List", ref showVipList))
                {
                    profile.ShowVipList = showVipList;
                    _config.Save();
                }

                float vipListRange = profile.VipListRange;
                if (ImGui.SliderFloat("VIP List Range", ref vipListRange, 5.0f, 100.0f, "%.1f yalms"))
                {
                    profile.VipListRange = vipListRange;
                    _config.Save();
                }

                ImGui.Separator();

                ImGui.Text("Google Spreadsheet ID:");
                string sheetId = profile.SpreadsheetId;
                if (ImGui.InputText("##SheetID", ref sheetId, 128))
                {
                    profile.SpreadsheetId = sheetId;
                    _config.Save();
                }

                if (ImGui.Button("Reload VIP List"))
                {
                    _vipManager.LoadVipNames();
                }

                ImGui.Separator();
                ImGui.Text("Columns Configuration");
                DrawColumnEditor(profile);

                ImGui.End();
            }
        }

        private void DrawProfileSelector()
        {
            string[] profileNames = _config.Profiles.Select(p => p.Name).ToArray();
            int currentIdx = _config.ActiveProfileIndex;

            if (ImGui.Combo("Active Profile", ref currentIdx, profileNames, profileNames.Length))
            {
                _config.ActiveProfileIndex = currentIdx;
                _config.Save();
                _vipManager.LoadVipNames(); // Reload data when switching profiles
            }

            if (ImGui.Button("New Profile"))
            {
                ImGui.OpenPopup("NewProfilePopup");
                _newProfileName = "New Venue";
            }

            ImGui.SameLine();

            // Don't allow deleting the last profile
            ImGui.BeginDisabled(_config.Profiles.Count <= 1);
            if (ImGui.Button("Delete Profile"))
            {
                ImGui.OpenPopup("DeleteProfilePopup");
            }
            ImGui.EndDisabled();

            // -- Popups --
            if (ImGui.BeginPopup("NewProfilePopup"))
            {
                ImGui.InputText("Name", ref _newProfileName, 64);
                if (ImGui.Button("Create"))
                {
                    var newProfile = new VipProfile { Name = _newProfileName };
                    // Add default columns
                    newProfile.Columns.Add(new ColumnDefinition { Header = "Note", CsvColumn = "C" });

                    _config.Profiles.Add(newProfile);
                    _config.ActiveProfileIndex = _config.Profiles.Count - 1;
                    _config.Save();
                    _vipManager.LoadVipNames();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }

            if (ImGui.BeginPopup("DeleteProfilePopup"))
            {
                ImGui.Text($"Delete profile '{_config.GetActiveProfile().Name}'?");
                if (ImGui.Button("Yes, Delete"))
                {
                    _config.Profiles.RemoveAt(_config.ActiveProfileIndex);
                    _config.ActiveProfileIndex = 0;
                    _config.Save();
                    _vipManager.LoadVipNames();
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }
        }

        private void DrawColumnEditor(VipProfile profile)
        {
            if (ImGui.BeginTable("ColsEdit", 4, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Header", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("CSV Col", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Width", ImGuiTableColumnFlags.WidthFixed, 60);
                ImGui.TableSetupColumn("Act", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableHeadersRow();

                int indexToRemove = -1;

                for (int i = 0; i < profile.Columns.Count; i++)
                {
                    var col = profile.Columns[i];
                    ImGui.PushID($"col_{i}");

                    ImGui.TableNextRow();

                    ImGui.TableSetColumnIndex(0);
                    string header = col.Header;
                    if (ImGui.InputText("##h", ref header, 64))
                    {
                        col.Header = header;
                        _config.Save();
                    }

                    ImGui.TableSetColumnIndex(1);
                    string csvCol = col.CsvColumn;
                    if (ImGui.InputText("##c", ref csvCol, 4))
                    {
                        col.CsvColumn = csvCol.ToUpper();
                        _config.Save();
                    }
                    if (ImGui.IsItemHovered()) ImGui.SetTooltip("Example: 'C' or '3'");

                    ImGui.TableSetColumnIndex(2);
                    float w = col.Width;
                    if (ImGui.InputFloat("##w", ref w, 10f, 0, "%.0f"))
                    {
                        col.Width = w;
                        _config.Save();
                    }

                    ImGui.TableSetColumnIndex(3);
                    if (ImGui.Button("X"))
                    {
                        indexToRemove = i;
                    }

                    ImGui.PopID();
                }

                ImGui.EndTable();

                if (indexToRemove >= 0)
                {
                    profile.Columns.RemoveAt(indexToRemove);
                    _config.Save();
                }
            }

            ImGui.Spacing();
            ImGui.Text("Add Column:");
            ImGui.SameLine();
            ImGui.PushItemWidth(100);
            ImGui.InputTextWithHint("##newH", "Header", ref _newColHeader, 32);
            ImGui.PopItemWidth();
            ImGui.SameLine();
            ImGui.PushItemWidth(50);
            ImGui.InputTextWithHint("##newC", "Col", ref _newColIndex, 4);
            ImGui.PopItemWidth();
            ImGui.SameLine();
            if (ImGui.Button("Add"))
            {
                if (!string.IsNullOrWhiteSpace(_newColHeader))
                {
                    string colVal = string.IsNullOrWhiteSpace(_newColIndex) ? "C" : _newColIndex.ToUpper();
                    profile.Columns.Add(new ColumnDefinition
                    {
                        Header = _newColHeader,
                        CsvColumn = colVal
                    });
                    _config.Save();
                    _newColHeader = "";
                    _newColIndex = "";
                }
            }
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        }
    }
}