using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace VipNameChecker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        // We initialize these as empty. 
        // The user must set them via command or UI.
        public string SpreadsheetId { get; set; } = "";
        public string GoogleApiKey { get; set; } = "";

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