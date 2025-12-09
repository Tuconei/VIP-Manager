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

            _overlay = new VipOverlay(_vipManager);

            // NEW: Set initial state based on saved config
            _overlay.Enabled = Configuration.IsOverlayEnabled;

            Service.CommandManager.AddHandler("/vip", new CommandInfo(OnCommand)
            {
                HelpMessage = "Controls the VIP plugin. Usage: /vip help"
            });
        }

        private void OnCommand(string command, string args)
        {
            var splitArgs = args.Split(' ', 2);
            var subCommand = splitArgs[0].ToLower();
            var value = splitArgs.Length > 1 ? splitArgs[1].Trim() : "";

            switch (subCommand)
            {
                case "reload":
                    _vipManager.LoadVipNames();
                    break;

                case "enable":
                case "on":
                    _overlay.Enabled = true;
                    // NEW: Save state
                    Configuration.IsOverlayEnabled = true;
                    Configuration.Save();
                    Service.Chat.Print("[VIP] Overlay ENABLED.");
                    break;

                case "disable":
                case "off":
                    _overlay.Enabled = false;
                    // NEW: Save state
                    Configuration.IsOverlayEnabled = false;
                    Configuration.Save();
                    Service.Chat.Print("[VIP] Overlay DISABLED.");
                    break;

                case "setid":
                    Configuration.SpreadsheetId = value;
                    Configuration.Save();
                    Service.Chat.Print("[VIP] Sheet ID saved.");
                    _vipManager.LoadVipNames();
                    break;

                default:
                    Service.Chat.Print("--- VIP Checker Commands ---");
                    Service.Chat.Print("/vip setid <spreadsheet_id>");
                    Service.Chat.Print("/vip reload");
                    Service.Chat.Print("/vip on/off");
                    break;
            }
        }

        public void Dispose()
        {
            Service.CommandManager.RemoveHandler("/vip");
            _overlay?.Dispose();
        }
    }
}