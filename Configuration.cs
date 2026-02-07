using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace VipNameChecker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;
        public string SpreadsheetId { get; set; } = "";
        public bool IsOverlayEnabled { get; set; } = true;

        public bool ShowHighlightRing { get; set; } = true;

        // NEW: Setting for the overhead VIP text tag
        public bool ShowVipTag { get; set; } = true;

        public bool ShowVipList { get; set; } = true;

        public float VipListRange { get; set; } = 30.0f;

        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}