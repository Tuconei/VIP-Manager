using System.Reflection;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using VipNameChecker;

internal static class RuntimeEnableChecks
{
    private static object Stub<T>() where T : class => DispatchProxy.Create<T, RuntimeStub>();
    private static void ServiceStub<T>(string name) where T : class => typeof(Service).GetProperty(name, BindingFlags.Public | BindingFlags.Static)!.SetValue(null, Stub<T>());
    private static void Tick(NativeVipTitles native) => typeof(NativeVipTitles).GetMethod("OnFrameworkUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(native, new object?[] { null });
    private static void Acknowledge(NativeVipTitles native) => typeof(NativeVipTitles).GetMethod("AfterNativeUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(native, new object?[] { null, Array.Empty<INamePlateUpdateHandler>() });
    private static void SheetSnapshot(VipManager manager, VipProfile profile, string person)
    {
        typeof(VipManager).GetField("loadedProfile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, profile.Id);
        typeof(VipManager).GetField("loadedSheet", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(manager, profile.SpreadsheetId);
        ((HashSet<string>)typeof(VipManager).GetField("names", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!).Add(person);
    }
    public static void Run(Action<bool, string> check)
    {
        // No game, network or external writes. Proxies only absorb subscriptions,
        // redraw requests, log/chat and SavePluginConfig; callbacks use ZERO handlers.
        ServiceStub<IFramework>("Framework"); ServiceStub<INamePlateGui>("NamePlateGui");
        ServiceStub<IChatGui>("Chat"); ServiceStub<IPluginLog>("PluginLog");
        var pi = DispatchProxy.Create<IDalamudPluginInterface, ConfigOnlyProxy>();
        var profile = new VipProfile { TagPlacement = VipTagPlacement.NativeTitlePrefix, NativeVipEnabled = true, SpreadsheetId = "mock-sheet" };
        var config = new Configuration { Profiles = new() { profile } }; config.Initialize(pi);
        using var manager = new VipManager(config);
        var native = new NativeVipTitles(manager, config);
        int saves = ConfigOnlyProxy.Saves;
        Tick(native);
        check(native.Status.Contains("armed=True") && profile.NativeVipEnabled, "remembered native intent autoarms actual runtime on startup");
        Tick(native); Tick(native);
        check(ConfigOnlyProxy.Saves == saves, "startup and steady frames never rewrite saved enabled preference");
        check(!manager.IsVip("Sheet Person"), "restored enabled mode does not mark anyone before sheet identity is loaded");
        SheetSnapshot(manager, profile, "Sheet Person");
        check(manager.IsVip("Sheet Person"), "matching sheet snapshot supplies normal VIP membership after remembered startup");
        check(native.PrepareUnload() && profile.NativeVipEnabled, "prepare-unload pauses actual runtime without clearing saved intent");
        Acknowledge(native); Tick(native);
        check(native.SafeToUnload && native.Status.Contains("session paused=True") && !native.Status.Contains("armed=True"), "cleanup acknowledgement cannot auto-rearm same prepared instance");
        native.Dispose();
        var restored = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(Newtonsoft.Json.JsonConvert.SerializeObject(config))!; restored.Initialize(pi);
        using var reloadedManager = new VipManager(restored);
        var reloadedNative = new NativeVipTitles(reloadedManager, restored);
        Tick(reloadedNative);
        check(reloadedNative.Status.Contains("armed=True") && restored.GetActiveProfile().NativeVipEnabled, "new runtime after safe unload resumes persisted enabled mode without manual arm");
        check(!reloadedManager.IsVip("Sheet Person"), "reload does not reuse previous manager sheet cache");
        SheetSnapshot(reloadedManager, restored.GetActiveProfile(), "Sheet Person");
        check(reloadedManager.IsVip("Sheet Person"), "normal sheet membership resumes when new reload snapshot is ready");
        restored.GetActiveProfile().SpreadsheetId = "other-sheet";
        check(!reloadedManager.IsVip("Sheet Person"), "old sheet snapshot cannot mark newly selected sheet");
        Tick(reloadedNative); Acknowledge(reloadedNative); Tick(reloadedNative);
        check(reloadedNative.SafeToUnload && restored.GetActiveProfile().NativeVipEnabled && reloadedNative.Status.Contains("session paused=True"), "sheet transition cleans and pauses without erasing remembered intent or auto-looping");
        check(reloadedNative.SetArmed(true), "explicit resume works after transition cleanup");
        check(reloadedNative.SetArmed(false) && !restored.GetActiveProfile().NativeVipEnabled, "explicit disarm saves disabled preference");
        Acknowledge(reloadedNative); reloadedNative.Dispose();
        var disabledConfig = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(Newtonsoft.Json.JsonConvert.SerializeObject(restored))!; disabledConfig.Initialize(pi);
        using var disabledManager = new VipManager(disabledConfig);
        var disabledNative = new NativeVipTitles(disabledManager, disabledConfig); Tick(disabledNative);
        check(disabledNative.SafeToUnload && !disabledNative.Status.Contains("armed=True"), "explicit disabled preference remains off across reload");
        disabledNative.SetArmed(false); Acknowledge(disabledNative);
        check(!disabledConfig.GetActiveProfile().NativeVipEnabled, "disarm already-clean runtime still persists off");
        disabledNative.Dispose();
        // Actual command routing against the native lifecycle, without native handlers.
        foreach (string command in new[] { "overlay", "tag" })
        {
            var commandProfile = new VipProfile { TagPlacement = VipTagPlacement.NativeTitle, NativeVipEnabled = true };
            var commandConfig = new Configuration { Profiles = new() { commandProfile } }; commandConfig.Initialize(pi);
            using var commandManager = new VipManager(commandConfig);
            using var commandNative = new NativeVipTitles(commandManager, commandConfig);
            Tick(commandNative);
            var commands = new VipCommands(commandConfig, () => {}, () => {}, () => {}, () => {}, () => {}, _ => {}, () => commandNative.Status);
            commands.Execute(command + " off"); Tick(commandNative);
            check(commandNative.Status.Contains("session paused=True") && !commandNative.Status.Contains("armed=True"), command + " off commands pause actual native runtime");
            Acknowledge(commandNative); commands.Execute(command + " on"); Tick(commandNative);
            check(commandNative.SafeToUnload && commandProfile.NativeVipEnabled && !commandNative.Status.Contains("armed=True"), command + " on preserves cleanup guard and does not silently rearm");
            check(commandNative.SetArmed(true), "explicit native resume after " + command + " command cleanup");
            commands.Execute("ring off"); commands.Execute("list off"); Tick(commandNative);
            check(commandNative.Status.Contains("armed=True"), "ring/list commands do not disarm native runtime");
            commandNative.PrepareUnload(); Acknowledge(commandNative);
        }
        var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<VipProfile>("{\"Name\":\"old\",\"TagPlacement\":1}")!;
        check(!legacy.NativeVipEnabled, "old native-placement profiles migrate with automatic enablement OFF");
        var session = new NativeEnableSession();
        legacy.NativeVipEnabled = true;
        check(session.ShouldAutoArm(legacy, true) && !session.ShouldAutoArm(legacy, false), "automatic startup always requires clean ownership boundary");
        session.Pause(); check(!session.ShouldAutoArm(legacy, true), "session pause blocks automatic retry despite true saved preference");
        check(new NativeEnableSession().ShouldAutoArm(legacy, true), "only a new session resets deliberate reload pause");
        legacy.IsOverlayEnabled = false; check(!new NativeEnableSession().ShouldAutoArm(legacy, true), "saved preference never bypasses master disabled");
        legacy.IsOverlayEnabled = true; legacy.ShowVipTag = false; check(!new NativeEnableSession().ShouldAutoArm(legacy, true), "saved preference never bypasses tag disabled");
        legacy.ShowVipTag = true; legacy.TagPlacement = VipTagPlacement.Floating; check(!new NativeEnableSession().ShouldAutoArm(legacy, true), "saved preference never enables native rendering for legacy floating mode");
    }
}

public class RuntimeStub : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method == null || method.ReturnType == typeof(void)) return null;
        if (method.ReturnType.IsValueType) return Activator.CreateInstance(method.ReturnType);
        return null;
    }
}
