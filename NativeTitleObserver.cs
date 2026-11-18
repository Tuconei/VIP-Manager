using System;
using System.Collections.Generic;
using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VipNameChecker;

internal sealed class NativeTitleObserver
{
    private readonly PointerProbeBudget budget = new();
    // Only callback-local slot/object IDs; never handlers, nodes or addresses.
    private readonly Dictionary<int, ulong> selected = new();
    private int ticks;
    public bool Active => budget.Active;
    public bool CanArm => budget.CanArm;

    public void Start()
    {
        budget.Start();
        selected.Clear();
        ticks = 0;
        Service.PluginLog.Information("[VIP pointer probe] Observe-only: up to 6 native updates, 4 plates/update, 72 samples. No addresses logged; rendering stays disarmed during observation.");
        Service.NamePlateGui.RequestRedraw();
    }

    public void Tick()
    {
        if (Active && ++ticks >= 300) Finish("timeout waiting for nameplate callbacks");
    }

    public void BeforeNative(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers)
    {
        if (!budget.BeginFrame()) return;
        selected.Clear();
        foreach (var handler in handlers)
        {
            if (!context.IsFullUpdate && !handler.IsUpdating) continue;
            if (handler.PlayerCharacter == null) continue;
            selected[handler.ArrayIndex] = handler.GameObjectId;
            if (selected.Count == PointerProbeBudget.PlatesPerFrame) break;
        }
        Capture(context, handlers, ProbePhase.BeforeNative);
    }

    public void Capture(INamePlateUpdateContext context, IReadOnlyList<INamePlateUpdateHandler> handlers, ProbePhase phase)
    {
        if (!Active) return;
        if (selected.Count == 0)
        {
            Log(phase, -1, PointerRelation.Unknown, "no current player plate undergoing a native update");
            return;
        }
        var seen = new HashSet<int>();
        foreach (var handler in handlers)
        {
            if (!selected.TryGetValue(handler.ArrayIndex, out var owner)) continue;
            seen.Add(handler.ArrayIndex);
            PointerRelation relation = PointerRelation.Unknown;
            string detail = "owner changed";
            try
            {
                if (handler.GameObjectId == owner)
                    relation = ReadCurrent(context, handler, out detail);
            }
            catch (Exception ex)
            {
                // Exceptions cannot prove a buffer relation. Never attempt raw
                // memory recovery or dereference the numeric values being compared.
                detail = "current-object sample unavailable: " + ex.GetType().Name;
            }
            Log(phase, handler.ArrayIndex, relation, detail);
        }
        foreach (int index in selected.Keys)
            if (!seen.Contains(index)) Log(phase, index, PointerRelation.Unknown, "selected slot no longer present in current callback");
    }

    private static unsafe PointerRelation ReadCurrent(INamePlateUpdateContext context, INamePlateUpdateHandler handler, out string detail)
    {
        detail = "current native structure unavailable";
        if (context.AddonAddress == 0 || handler.PlayerCharacter == null) return PointerRelation.Unknown;
        int index = handler.NamePlateIndex;
        if (index < 0 || index >= AddonNamePlate.NumNamePlateObjects) return PointerRelation.Unknown;
        // Traverse only the callback's documented live addon object graph. Reject
        // any handler address that is not its current slot before dereferencing it.
        var addon = (AddonNamePlate*)context.AddonAddress;
        if (addon->NamePlateObjectArray == null) return PointerRelation.Unknown;
        var plate = addon->NamePlateObjectArray + index;
        if ((nint)plate != handler.NamePlateObjectAddress) return PointerRelation.Unknown;
        var node = plate->NameText;
        if (node == null || node->Type != NodeType.Text) return PointerRelation.Unknown;

        // The title span/pointer come from the public current-frame handler. The
        // node-owned Utf8String supplies lengths; pointed-to bytes are not read.
        int titleBytes = handler.GetFieldAsSpan(NamePlateStringField.Title).Length;
        nuint title = (nuint)handler.GetFieldAsPointer(NamePlateStringField.Title);
        nuint source = (nuint)(byte*)node->OriginalTextPointer;
        nuint nodeBuffer = (nuint)(byte*)node->NodeText.StringPtr;
        var relation = NativePointerProbe.Classify(title, titleBytes, source, nodeBuffer,
            node->NodeText.BufUsed, node->NodeText.BufSize);
        detail = relation switch
        {
            PointerRelation.TitleStorageAlias => "retained source or node storage aliases title-field range",
            PointerRelation.SeparateNodeStorage => "retained source is inside separate node-owned used range; snapshot only",
            _ => "null/invalid range, or retained source is in unidentified storage"
        };
        return relation;
    }

    private void Log(ProbePhase phase, int index, PointerRelation relation, string detail)
    {
        if (budget.Record(phase, relation))
            Service.PluginLog.Information($"[VIP pointer probe] frame={budget.Frames} phase={phase} slot={index} relation={relation}; {detail}");
    }

    public void EndFrame()
    {
        if (!Active) return;
        selected.Clear();
        budget.EndFrame();
        if (Active) Service.NamePlateGui.RequestRedraw();
        else Finish("frame budget reached");
    }

    public void Stop() { budget.Stop(); selected.Clear(); }

    private void Finish(string reason)
    {
        Stop();
        string report = $"[VIP pointer probe] {reason}: frames={budget.Frames}, samples={budget.Samples}, post-native separate={budget.Separate}, alias={budget.Aliases}, unknown={budget.Unknown}. Snapshots do not establish native lifetime or automatically arm the controlled test.";
        Service.PluginLog.Information(report);
        Service.Chat.Print(report);
    }
}

