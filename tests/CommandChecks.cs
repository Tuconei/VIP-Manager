using System.Reflection;
using Dalamud.Plugin;
using VipNameChecker;

internal static class CommandChecks
{
    public static void Run(Action<bool, string> check)
    {
        var pi = DispatchProxy.Create<IDalamudPluginInterface, ConfigOnlyProxy>();
        var first = new VipProfile { Name = "First", TagPlacement = VipTagPlacement.NativeTitle, NativeVipEnabled = true };
        var other = new VipProfile { Name = "Other" };
        var config = new Configuration { Profiles = new() { first, other } }; config.Initialize(pi);
        int settings = 0, help = 0, reload = 0, sheet = 0, redraw = 0;
        var messages = new List<string>();
        var commands = new VipCommands(config, () => settings++, () => help++, () => reload++, () => sheet++, () => redraw++, messages.Add, () => "sentinel-native-status");
        var all = new[] { "overlay", "ring", "tag", "list" };
        foreach (string verb in all)
        {
            bool Read() => verb switch { "overlay" => first.IsOverlayEnabled, "ring" => first.ShowHighlightRing, "tag" => first.ShowVipTag, _ => first.ShowVipList };
            commands.Execute(verb + " off"); check(!Read(), verb + " explicit off");
            int saves = ConfigOnlyProxy.Saves;
            commands.Execute(verb + " OFF"); check(!Read() && ConfigOnlyProxy.Saves == saves, verb + " repeated off is idempotent and no redundant save");
            commands.Execute(verb); check(Read(), verb + " bare toggles on");
            commands.Execute("  " + verb.ToUpperInvariant() + "\tToGgLe  "); check(!Read(), verb + " case and whitespace accepted");
            commands.Execute(verb + " on"); check(Read(), verb + " explicit on");
            saves = ConfigOnlyProxy.Saves; commands.Execute(verb + " on"); check(Read() && ConfigOnlyProxy.Saves == saves, verb + " repeated on idempotent");
            foreach (string bad in new[] { "maybe", "on extra", "false", "0", "toggle toggle", "on/off" })
            {
                commands.Execute(verb + " " + bad);
                check(Read() && ConfigOnlyProxy.Saves == saves, verb + " rejects " + bad + " without mutation");
                check(messages.Last().Contains("Usage:"), "invalid toggle produces usage");
            }
            check(first.NativeVipEnabled, "display command never changes saved native preference");
        }
        check(other.IsOverlayEnabled && other.ShowHighlightRing && other.ShowVipTag && other.ShowVipList, "display commands isolated to active profile");
        commands.Execute("disable"); check(!first.IsOverlayEnabled && first.ShowHighlightRing && first.ShowVipTag && first.ShowVipList, "legacy disable keeps per-feature settings");
        commands.Execute("enable"); check(first.IsOverlayEnabled, "legacy enable");
        commands.Execute(""); commands.Execute(" \t\r\n "); check(settings == 2, "empty commands toggle settings");
        commands.Execute("help"); commands.Execute("HELP"); check(help == 2 && settings == 2, "help opens guide without toggling settings");
        commands.Execute("reload"); commands.Execute("sheet"); check(reload == 1 && sheet == 1, "reload and sheet routed exactly once");
        int before = ConfigOnlyProxy.Saves;
        commands.Execute("status"); check(messages.TakeLast(3).Any(x => x.Contains("Profile: First")) && messages.Last().Contains("sentinel-native-status"), "status shows active profile and native runtime");
        check(ConfigOnlyProxy.Saves == before, "status does not save");
        foreach (string verb in new[] { "help", "reload", "sheet", "status", "enable", "disable" })
        {
            commands.Execute(verb + " extra");
            check(messages.Last().Contains("Usage:") && ConfigOnlyProxy.Saves == before, "extra argument rejected: " + verb);
        }
        commands.Execute("typo"); check(messages.Last().Contains("Unknown command") && settings == 2, "unknown command does not toggle settings");
        check(help == 2 && reload == 1 && sheet == 1, "invalid args never invoke effects");
        config.ActiveProfileIndex = 1; commands.Execute("ring off"); check(!other.ShowHighlightRing && first.ShowHighlightRing, "route resolves active profile for every command");
        commands.Execute("status"); check(messages.TakeLast(3).Any(x => x.Contains("Profile: Other")), "status follows switched profile");
        commands.Execute("overlay off"); commands.Execute("list on"); check(messages.Last().Contains("master display switch is off"), "enabled feature under master off has useful explanation");
        var saved = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(Newtonsoft.Json.JsonConvert.SerializeObject(config))!;
        check(!saved.Profiles[1].IsOverlayEnabled && !saved.Profiles[1].ShowHighlightRing && saved.Profiles[0].NativeVipEnabled, "command settings roundtrip without native intent changes");
        foreach (string verb in new[] {"overlay", "ring", "tag", "list", "enable", "disable", "help", "reload", "sheet", "status"})
            check(VipHelpContent.Commands.Any(x => x.Command.Contains("/vip " + verb)), "help documents " + verb);
        foreach (string native in new[] {"status", "arm", "disarm", "prepare-unload", "observe", "add", "remove", "clear"})
            check(VipHelpContent.Commands.Any(x => x.Command.Contains("/vip native " + native)), "help documents native " + native);
        check(VipHelpContent.Basics.Any(x => x.Text.Contains("through walls")), "help explains overlay visibility");
        check(VipHelpContent.Basics.Any(x => x.Text.Contains("controlled unload ready=True")), "help preserves cleanup instructions");
        check(VipHelpContent.Commands.Any(x => x.Command == "/vip reload" && x.Description.Contains("Does not reload or unload")), "reload clearly means data refresh");
        check(redraw > 0, "master/tag changes request redraw");
    }
}
