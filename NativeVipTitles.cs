using System;
using System.Collections.Generic;
using Dalamud.Game.Gui.NamePlate;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace VipNameChecker;

internal sealed class NativeVipTitles : IDisposable
{
    private readonly VipManager manager;
    private readonly Configuration config;
    private readonly PersistentTitleController controller;
    private bool freeCompany;
    private bool titlePrefix;
    private System.Numerics.Vector4 nativeColor;
    private NativeArmSelection? selection;
    private readonly NativeTitleObserver observer = new();
    private (Guid Profile, string Sheet, bool Enabled, bool Tag, long Revision, bool Armed)? lastState;
    private bool disposed;
    private bool armed;
    private readonly NativeEnableSession enableSession = new();
    private bool cleanupRequested;
    private bool cleanupAcknowledged = true;
    private string lastReport = "";

    public bool SafeToUnload => !armed && cleanupAcknowledged && controller.IsClean;
    public string Status
    {
        get
        {
            var desired = config.GetActiveProfile();
            return $"remembered enabled={desired.NativeVipEnabled}; session paused={enableSession.Paused}; configured={desired.TagPlacement}/{NativeTagSettings.ToHex(desired.VipTagColor)}; last captured={(freeCompany ? "fc" : titlePrefix ? "title-prefix" : "title-suffix")}/{NativeTagSettings.ToHex(nativeColor)}; armed={armed}; owned={controller.OwnedCount}; awaiting post={controller.PendingCount}; conflicts={controller.ConflictCount}; cleanup acknowledged={cleanupAcknowledged}; controlled unload ready={SafeToUnload}";
        }
    }
    public bool SetArmed(bool value) => SetArmedCore(value, true);
    public bool PrepareUnload() => SetArmedCore(false, false);
    private bool SetArmedCore(bool value, bool persistIntent)
    {
        if (disposed) return false;
        observer.Stop();
        if (value)
        {
            if (!SafeToUnload) return false;
            var profile = config.GetActiveProfile();
            if (!NativeTagSettings.IsNative(profile.TagPlacement) || !profile.IsOverlayEnabled || !profile.ShowVipTag) return false;
            if (persistIntent) { profile.NativeVipEnabled = true; config.Save(); }
            enableSession.Resume();
            selection = NativeArmSelection.From(profile);
            freeCompany = profile.TagPlacement == VipTagPlacement.NativeFreeCompany;
            titlePrefix = profile.TagPlacement == VipTagPlacement.NativeTitlePrefix;
            nativeColor = NativeTagSettings.NormalizeColor(profile.VipTagColor);
            armed = true;
            cleanupAcknowledged = false;
            cleanupRequested = false;
        }
        else
        {
            armed = false;
            enableSession.Pause();
            if (persistIntent) { config.GetActiveProfile().NativeVipEnabled = false; config.Save(); }
            cleanupRequested = true;
            cleanupAcknowledged = false;
        }
        Service.NamePlateGui.RequestRedraw();
        return true;
    }

    public bool Observe()
    {
        if (!SafeToUnload || disposed) return false;
        enableSession.Pause();
        observer.Start();
        return true;
    }

    public NativeVipTitles(VipManager manager, Configuration config)
    {
        controller = new PersistentTitleController(bytes => NativeTitleSuffix.AppendColored(bytes, freeCompany, nativeColor, titlePrefix));
        this.manager = manager;
        this.config = config;
        Service.NamePlateGui.OnDataUpdate += BeforeNativeUpdate;
        Service.NamePlateGui.OnPostDataUpdate += AfterNativeUpdate;
        Service.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        observer.Tick();
        CheckSelection();
        var profile = config.GetActiveProfile();
        if (enableSession.ShouldAutoArm(profile, SafeToUnload)) SetArmedCore(true, false);
        var state = (profile.Id, profile.SpreadsheetId, profile.IsOverlayEnabled, profile.ShowVipTag, manager.Revision, armed);
        if (lastState == state) return;
        lastState = state;
        Service.NamePlateGui.RequestRedraw();
    }

    private void BeforeNativeUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (disposed) return;
        CheckSelection();
        if (observer.Active) observer.BeforeNative(context, handlers);
        if (controller.HasWork)
        {
            var activeIndices = new HashSet<int>();
            foreach (var handler in handlers) activeIndices.Add(handler.ArrayIndex);
            controller.PruneMissing(activeIndices);
        }
        var profile = config.GetActiveProfile();
        bool mayApply = armed && profile.IsOverlayEnabled && profile.ShowVipTag;
        if (!mayApply && !controller.HasWork) return;

        foreach (var handler in handlers)
        {
            if (controller.TouchesIndex(handler.ArrayIndex))
                controller.ReconcileCurrentObjectId(handler.ArrayIndex, handler.GameObjectId);
            if (!context.IsFullUpdate && !handler.IsUpdating) continue;
            if (!mayApply && !controller.TouchesIndex(handler.ArrayIndex)) continue;
            var player = handler.PlayerCharacter;
            if (player == null) continue;
            bool wantsVip = mayApply && (freeCompany || handler.DisplayTitle) && manager.IsVip(player.Name.TextValue);
            if (!wantsVip && !controller.TouchesIndex(handler.ArrayIndex)) continue;
            try
            {
                var owner = new PlateIdentity(handler.ArrayIndex, handler.GameObjectId, player.Address);
                var current = handler.GetFieldAsSpan(freeCompany ? NamePlateStringField.FreeCompanyTag : NamePlateStringField.Title);
                // QuotedTitle is the read-only NamePlateInfo.DisplayTitle, including
                // Honorific's formatted/unquoted choice. Never write this upstream source.
                var upstream = (freeCompany ? handler.InfoView.QuotedFreeCompanyTag : handler.InfoView.QuotedTitle).Encode();
                var plan = controller.Plan(owner, current, upstream, wantsVip);
                if (plan == null) continue;
                if (!current.SequenceEqual(plan.Desired))
                {
                    var title = SeString.Parse(plan.Desired);
                    if (!title.Encode().AsSpan().SequenceEqual(plan.Desired)) continue;
                    // Supported PRE-NATIVE weak setter. It copies into UI-owned storage
                    // (managed:true) which remains in place after this callback and render.
                    if (freeCompany) handler.FreeCompanyTag = title;
                    else handler.Title = title;
                }
                controller.Commit(plan, handler.GetFieldAsSpan(freeCompany ? NamePlateStringField.FreeCompanyTag : NamePlateStringField.Title));
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "[VIP native] Pre-native title update failed.");
            }
        }
        FailClosedOnConflict();
    }

    private void AfterNativeUpdate(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        // OBSERVATION ONLY. In particular no SetField, Title assignment, pointer
        // rebinding or buffer release occurs after native consumption.
        observer.Capture(context, handlers, ProbePhase.AfterNativeBeforeCleanup);
        foreach (var handler in handlers)
        {
            if (!controller.NeedsPostCheck(handler.ArrayIndex)) continue;
            controller.ReconcileCurrentObjectId(handler.ArrayIndex, handler.GameObjectId);
            if (!controller.NeedsPostCheck(handler.ArrayIndex)) continue;
            var player = handler.PlayerCharacter;
            if (player == null) continue;
            var owner = new PlateIdentity(handler.ArrayIndex, handler.GameObjectId, player.Address);
            controller.ObserveCleanup(owner, handler.GetFieldAsSpan(freeCompany ? NamePlateStringField.FreeCompanyTag : NamePlateStringField.Title), (freeCompany ? handler.InfoView.QuotedFreeCompanyTag : handler.InfoView.QuotedTitle).Encode());
        }
        FailClosedOnConflict();
        if (cleanupRequested && controller.IsClean)
        {
            cleanupRequested = false;
            cleanupAcknowledged = true;
            Service.Chat.Print("[VIP native] Cleanup acknowledged after native callback. Visually verify the suffix is gone; controlled unload is ready.");
        }
        observer.Capture(context, handlers, ProbePhase.AfterCleanup);
        observer.EndFrame();
    }

    private void CheckSelection()
    {
        if (!armed || selection is not { } captured || captured == NativeArmSelection.From(config.GetActiveProfile())) return;
        PrepareUnload();
        Service.Chat.Print("[VIP native] Settings/profile changed: disarmed for cleanup. Wait for acknowledgement, visually verify, saved preference retained, but this session stays paused until explicit arm.");
    }

    private void FailClosedOnConflict()
    {
        if (controller.ConflictCount == 0) return;
        if (armed)
        {
            armed = false;
            enableSession.Pause();
            cleanupRequested = true;
            cleanupAcknowledged = false;
            Service.NamePlateGui.RequestRedraw();
        }
        string report = Status;
        if (lastReport == report) return;
        lastReport = report;
        Service.PluginLog.Warning("[VIP native] Another title writer prevents proven cleanup; no text was guessed or overwritten. " + report);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (!SafeToUnload)
            Service.PluginLog.Warning("[VIP native] Uncontrolled unload before cleanup acknowledgement. UI-owned storage is left intact; a visual VIP suffix may remain until a native source refresh.");
        observer.Stop();
        Service.Framework.Update -= OnFrameworkUpdate;
        Service.NamePlateGui.OnDataUpdate -= BeforeNativeUpdate;
        Service.NamePlateGui.OnPostDataUpdate -= AfterNativeUpdate;
        controller.ClearManagedState();
        // UI storage belongs to the game: do not free it, rewrite native nodes,
        // mark undocumented source flags or promise RequestRedraw regenerates text.
    }
}
