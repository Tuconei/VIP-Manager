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
        private readonly NativeVipTitles _native;
        private readonly VipGui _gui;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            pluginInterface.Create<Service>();

            Configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(pluginInterface);

            _vipManager = new VipManager(Configuration);
            _vipManager.UpdateAutoRefresh();

            // Check the active profile, not just a raw ID, as ID is now inside the profile
            if (!string.IsNullOrEmpty(Configuration.GetActiveProfile().SpreadsheetId))
            {
                _vipManager.LoadVipNames();
            }

            _native = new NativeVipTitles(_vipManager, Configuration);
            _overlay = new VipOverlay(_vipManager, Configuration, () => _native.SafeToUnload);
            _gui = new VipGui(Configuration, _vipManager, _native);

            Service.CommandManager.AddHandler("/vip", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the VIP plugin settings. Options: enable, disable, ring, tag, list, native, help."
            });
        }

        private void OnCommand(string command, string args)
        {
            if (args.Trim().Equals("native", System.StringComparison.OrdinalIgnoreCase) || args.Trim().StartsWith("native ", System.StringComparison.OrdinalIgnoreCase))
            { OnNativeCommand(args.Trim()[6..].Trim()); return; }
            var arg = args.Trim().ToLowerInvariant();

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
                    Service.Chat.Print("/vip native status|arm|disarm|add Full Name|remove Full Name|clear - Controlled native placement testing");
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

        private void OnNativeCommand(string args)
        {
            var split = args.Split(' ', 2, System.StringSplitOptions.RemoveEmptyEntries);
            string verb = split.Length == 0 ? "status" : split[0].ToLowerInvariant();
            string name = split.Length > 1 ? split[1].Trim() : "";
            switch (verb)
            {
                case "status": Service.Chat.Print("[VIP native] " + _native.Status); break;
                case "arm": Service.Chat.Print(_native.SetArmed(true)
                    ? "[VIP native] Enabled and remembered for reloads. Use prepare-unload to clean while keeping enabled, or disarm to turn off."
                    : "[VIP native] Cannot arm: select native placement, enable features/tag, and complete prior cleanup first."); break;
                case "prepare-unload": _native.PrepareUnload(); Service.Chat.Print("[VIP native] Paused for cleanup; remembered enabled state retained. Wait for acknowledgement and visually verify removal before unload."); break;
                case "disarm": _native.SetArmed(false); Service.Chat.Print("[VIP native] Disabled and remembered off; wait for cleanup acknowledgement and visually verify removal."); break;
                case "observe": Service.Chat.Print(_native.Observe() ? "[VIP native] Optional title-only observer started; see /xllog." : "[VIP native] Disarm and finish cleanup first."); break;
                case "add":
                case "remove":
                    if (name.Length == 0) Service.Chat.Print("[VIP native] Specify a full name.");
                    else { _vipManager.SetTemporaryVip(name, verb == "add"); Service.Chat.Print($"[VIP native] Session VIP {verb}: {name}"); }
                    break;
                case "clear": _vipManager.ClearTemporaryVips(); Service.Chat.Print("[VIP native] Session VIPs cleared. Sheet membership still applies."); break;
                default: Service.Chat.Print("[VIP native] status, arm, disarm, prepare-unload, observe, add Full Name, remove Full Name, clear. Set placement and hex color in /vip."); break;
            }
        }
        public void Dispose()
        {
            Service.CommandManager.RemoveHandler("/vip");
            _native.Dispose();
            _gui?.Dispose();
            _overlay?.Dispose();
            _vipManager?.Dispose();
        }
    }
}
