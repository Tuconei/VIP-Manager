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

        // NEW: Remember if the overlay is ON or OFF. Default is ON.
        public bool IsOverlayEnabled { get; set; } = true;

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