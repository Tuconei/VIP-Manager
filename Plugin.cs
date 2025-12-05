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

            // 1. Load Configuration
            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);

            // 2. Pass Config to Manager
            _vipManager = new VipManager(Configuration);

            // Only load if keys exist
            if (!string.IsNullOrEmpty(Configuration.GoogleApiKey))
            {
                _vipManager.LoadVipNames();
            }

            _overlay = new VipOverlay(_vipManager);

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
                    Service.Chat.Print("[VIP] Overlay ENABLED.");
                    break;

                case "disable":
                case "off":
                    _overlay.Enabled = false;
                    Service.Chat.Print("[VIP] Overlay DISABLED.");
                    break;

                // NEW: Security Configuration Commands
                case "setkey":
                    Configuration.GoogleApiKey = value;
                    Configuration.Save();
                    Service.Chat.Print("[VIP] API Key saved.");
                    break;

                case "setid":
                    Configuration.SpreadsheetId = value;
                    Configuration.Save();
                    Service.Chat.Print("[VIP] Sheet ID saved.");
                    break;

                default:
                    Service.Chat.Print("--- VIP Checker Commands ---");
                    Service.Chat.Print("/vip setkey <your_api_key> (Sets Google API Key)");
                    Service.Chat.Print("/vip setid <spreadsheet_id> (Sets Sheet ID)");
                    Service.Chat.Print("/vip reload (Refreshes list)");
                    Service.Chat.Print("/vip on/off (Toggles display)");
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