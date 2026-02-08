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

            // Check the active profile, not just a raw ID, as ID is now inside the profile
            if (!string.IsNullOrEmpty(Configuration.GetActiveProfile().SpreadsheetId))
            {
                _vipManager.LoadVipNames();
            }

            _overlay = new VipOverlay(_vipManager, Configuration);
            _gui = new VipGui(Configuration, _vipManager);

            Service.CommandManager.AddHandler("/vip", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the VIP plugin settings. Options: enable, disable, ring, tag, list, help."
            });
        }

        private void OnCommand(string command, string args)
        {
            var arg = args.Trim().ToLower();

            if (string.IsNullOrEmpty(arg))
            {
                _gui.IsOpen = !_gui.IsOpen;
                return;
            }

            var profile = Configuration.GetActiveProfile();
            bool changed = true;

            switch (arg)
            {
                case "enable":
                    profile.IsOverlayEnabled = true;
                    Service.Chat.Print("[VIP] Overlay enabled.");
                    break;
                case "disable":
                    profile.IsOverlayEnabled = false;
                    Service.Chat.Print("[VIP] Overlay disabled.");
                    break;
                case "ring":
                    profile.ShowHighlightRing = !profile.ShowHighlightRing;
                    Service.Chat.Print($"[VIP] Highlight Ring {(profile.ShowHighlightRing ? "enabled" : "disabled")}.");
                    break;
                case "tag":
                    profile.ShowVipTag = !profile.ShowVipTag;
                    Service.Chat.Print($"[VIP] Overhead Tag {(profile.ShowVipTag ? "enabled" : "disabled")}.");
                    break;
                case "list":
                    profile.ShowVipList = !profile.ShowVipList;
                    Service.Chat.Print($"[VIP] Range List {(profile.ShowVipList ? "enabled" : "disabled")}.");
                    break;
                case "help":
                    Service.Chat.Print("VIP Manager Commands:");
                    Service.Chat.Print("/vip - Open/Close Settings");
                    Service.Chat.Print("/vip enable - Enable Overlay");
                    Service.Chat.Print("/vip disable - Disable Overlay");
                    Service.Chat.Print("/vip ring - Toggle Highlight Ring");
                    Service.Chat.Print("/vip tag - Toggle Overhead Tag");
                    Service.Chat.Print("/vip list - Toggle Range List");
                    Service.Chat.Print("/vip help - Show this help message");
                    changed = false;
                    break;
                default:
                    // If argument is not recognized, toggle GUI
                    _gui.IsOpen = !_gui.IsOpen;
                    changed = false;
                    break;
            }

            if (changed)
            {
                Configuration.Save();
            }
        }

        public void Dispose()
        {
            Service.CommandManager.RemoveHandler("/vip");
            _gui?.Dispose();
            _overlay?.Dispose();
        }
    }
}