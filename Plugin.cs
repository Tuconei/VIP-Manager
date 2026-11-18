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
        private readonly VipHelpWindow _help;
        private readonly VipCommands _commands;

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
            _help = new VipHelpWindow();
            _gui = new VipGui(Configuration, _vipManager, _native, _help.Open);
            _commands = new VipCommands(Configuration, () => _gui.IsOpen = !_gui.IsOpen,
                _help.Open, _vipManager.LoadVipNames, OpenLinkedSheet,
                () => Service.NamePlateGui.RequestRedraw(), text => Service.Chat.Print(text), () => _native.Status);

            Service.CommandManager.AddHandler("/vip", new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens VIP settings. /vip help opens the command and setup guide. Options: overlay, ring, tag, list, reload, sheet, status, native."
            });
        }

        private void OnCommand(string command, string args)
        {
            var words = args.Split((char[]?)null, 2, System.StringSplitOptions.RemoveEmptyEntries);
            if (words.Length > 0 && words[0].Equals("native", System.StringComparison.OrdinalIgnoreCase))
            { OnNativeCommand(words.Length > 1 ? words[1] : ""); return; }
            _commands.Execute(args);
        }

        private void OpenLinkedSheet()
        {
            if (!SheetLink.TryCreate(Configuration.GetActiveProfile().SpreadsheetId, out string url, out string error))
            { Service.Chat.Print("[VIP] " + error); return; }
            try { Dalamud.Utility.Util.OpenLink(url); }
            catch (System.Exception ex)
            {
                Service.PluginLog.Error(ex, "[VIP] Could not open the linked sheet in the default browser.");
                Service.Chat.Print("[VIP] Could not open the linked sheet. Check your default browser and /xllog.");
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
            _help.Dispose();
            _overlay?.Dispose();
            _vipManager?.Dispose();
        }
    }
}
