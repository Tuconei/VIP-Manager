using Dalamud.Bindings.ImGui;
using System;
using System.Numerics;

namespace VipNameChecker
{
    public class VipGui : IDisposable
    {
        private readonly Configuration _config;
        private readonly VipManager _vipManager;
        private bool _isOpen = false;

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

            ImGui.SetNextWindowSize(new Vector2(350, 250), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("VIP Name Checker Settings", ref _isOpen))
            {
                bool isEnabled = _config.IsOverlayEnabled;
                if (ImGui.Checkbox("Enable Overlay", ref isEnabled))
                {
                    _config.IsOverlayEnabled = isEnabled;
                    _config.Save();
                }

                bool showRing = _config.ShowHighlightRing;
                if (ImGui.Checkbox("Show Highlight Ring", ref showRing))
                {
                    _config.ShowHighlightRing = showRing;
                    _config.Save();
                }

                // NEW: Toggle for the overhead tag
                bool showTag = _config.ShowVipTag;
                if (ImGui.Checkbox("Show VIP Tag", ref showTag))
                {
                    _config.ShowVipTag = showTag;
                    _config.Save();
                }

                bool showVipList = _config.ShowVipList;
                if (ImGui.Checkbox("Show VIPs In Range List", ref showVipList))
                {
                    _config.ShowVipList = showVipList;
                    _config.Save();
                }

                float vipListRange = _config.VipListRange;
                if (ImGui.SliderFloat("VIP List Range", ref vipListRange, 5.0f, 100.0f, "%.1f yalms"))
                {
                    _config.VipListRange = vipListRange;
                    _config.Save();
                }

                ImGui.Separator();

                ImGui.Text("Google Spreadsheet ID:");
                string sheetId = _config.SpreadsheetId;
                if (ImGui.InputText("##SheetID", ref sheetId, 128))
                {
                    _config.SpreadsheetId = sheetId;
                    _config.Save();
                }

                if (ImGui.Button("Reload VIP List"))
                {
                    _vipManager.LoadVipNames();
                }

                ImGui.End();
            }
        }

        public void Dispose()
        {
            Service.PluginInterface.UiBuilder.Draw -= Draw;
            Service.PluginInterface.UiBuilder.OpenConfigUi -= OnOpenConfigUi;
        }
    }
}