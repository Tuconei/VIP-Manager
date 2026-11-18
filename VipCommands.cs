using System;

namespace VipNameChecker;

// UI-free routing keeps chat commands testable without a game or browser.
internal sealed class VipCommands
{
    private readonly Configuration config;
    private readonly Action toggleSettings, openHelp, reload, openSheet, redraw;
    private readonly Action<string> print;
    private readonly Func<string> nativeStatus;

    public VipCommands(Configuration config, Action toggleSettings, Action openHelp,
        Action reload, Action openSheet, Action redraw, Action<string> print, Func<string> nativeStatus)
    {
        this.config = config; this.toggleSettings = toggleSettings; this.openHelp = openHelp;
        this.reload = reload; this.openSheet = openSheet; this.redraw = redraw;
        this.print = print; this.nativeStatus = nativeStatus;
    }

    public void Execute(string args)
    {
        var words = args.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) { toggleSettings(); return; }
        string verb = words[0].ToLowerInvariant();
        var profile = config.GetActiveProfile();
        switch (verb)
        {
            case "overlay": case "ring": case "tag": case "list":
                bool current = verb switch { "overlay" => profile.IsOverlayEnabled, "ring" => profile.ShowHighlightRing,
                    "tag" => profile.ShowVipTag, _ => profile.ShowVipList };
                if (!TryToggle(words, current, out bool value))
                { print($"[VIP] Usage: /vip {verb} [on|off|toggle]. No settings changed."); return; }
                SetDisplay(profile, verb, value);
                return;
            case "enable": case "disable":
                if (!NoExtra(words, verb)) return;
                SetDisplay(profile, "overlay", verb == "enable"); return;
            case "help": if (NoExtra(words, verb)) openHelp(); return;
            case "reload": if (NoExtra(words, verb)) reload(); return;
            case "sheet": if (NoExtra(words, verb)) openSheet(); return;
            case "status":
                if (!NoExtra(words, verb)) return;
                print($"[VIP] Profile: {profile.Name}; all displays: {OnOff(profile.IsOverlayEnabled)}; ring: {OnOff(profile.ShowHighlightRing)}; tag: {OnOff(profile.ShowVipTag)}; range list: {OnOff(profile.ShowVipList)}.");
                print($"[VIP] Placement: {profile.TagPlacement}; sheet: {(string.IsNullOrWhiteSpace(profile.SpreadsheetId) ? "not configured" : "configured")}; auto refresh: {(profile.AutoRefreshEnabled ? profile.AutoRefreshIntervalMinutes + " minutes" : "off")}.");
                print("[VIP native] " + nativeStatus()); return;
            default: print("[VIP] Unknown command. Use /vip help for commands and the setup guide."); return;
        }
    }

    private bool NoExtra(string[] words, string verb)
    {
        if (words.Length == 1) return true;
        print($"[VIP] Usage: /vip {verb}. No settings changed."); return false;
    }

    internal static bool TryToggle(string[] words, bool current, out bool value)
    {
        value = current;
        if (words.Length > 2) return false;
        switch (words.Length == 1 ? "toggle" : words[1].ToLowerInvariant())
        {
            case "on": value = true; return true;
            case "off": value = false; return true;
            case "toggle": value = !current; return true;
            default: return false;
        }
    }

    private void SetDisplay(VipProfile profile, string verb, bool value)
    {
        bool current = verb switch { "overlay" => profile.IsOverlayEnabled, "ring" => profile.ShowHighlightRing,
            "tag" => profile.ShowVipTag, _ => profile.ShowVipList };
        if (current != value)
        {
            switch (verb)
            {
                case "overlay": profile.IsOverlayEnabled = value; break;
                case "ring": profile.ShowHighlightRing = value; break;
                case "tag": profile.ShowVipTag = value; break;
                case "list": profile.ShowVipList = value; break;
            }
            config.Save();
            if (verb is "overlay" or "tag") redraw();
        }
        string label = verb switch { "overlay" => "All VIP displays", "ring" => "Highlight ring", "tag" => "VIP tag", _ => "VIPs-in-range list" };
        print($"[VIP] {label}: {OnOff(value)}.");
        if (value && (verb is "ring" or "tag" or "list") && !profile.IsOverlayEnabled)
            print("[VIP] The master display switch is off. Use /vip overlay on to show enabled features.");
        if ((verb is "overlay" or "tag") && NativeTagSettings.IsNative(profile.TagPlacement))
            print("[VIP] Native cleanup/resume safeguards still apply. If paused, wait for cleanup, then use /vip native arm to resume. /vip native status shows readiness.");
    }

    private static string OnOff(bool value) => value ? "on" : "off";
}
