using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;

namespace VipNameChecker
{
    public sealed class Plugin : IDalamudPlugin
    {
        public Configuration Configuration { get; init; }
        private readonly VipManager _vipManager;
        private readonly VipOverlay _overlay;
        private readonly VipGui _gui;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);

            _vipManager = new VipManager(Configuration);
            if (!string.IsNullOrEmpty(Configuration.SpreadsheetId))
            {
                _vipManager.LoadVipNames();
            }

            _overlay = new VipOverlay(_vipManager, Configuration);
            _gui = new VipGui(Configuration, _vipManager);

            Service.CommandManager.AddHandler("/vip", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the VIP plugin settings."
            });
        }

        private void OnCommand(string command, string args)
        {
            // Any argument or no argument now toggles the GUI
            _gui.IsOpen = !_gui.IsOpen;
        }

        public void Dispose()
        {
            Service.CommandManager.RemoveHandler("/vip");
            _gui?.Dispose();
            _overlay?.Dispose();
        }
    }
}