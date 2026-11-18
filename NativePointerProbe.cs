using System;

namespace VipNameChecker;

internal enum PointerRelation { Unknown, TitleStorageAlias, SeparateNodeStorage }
internal enum ProbePhase { BeforeNative, AfterNativeBeforeCleanup, AfterCleanup }

// Pure numeric comparisons only. No pointer value is dereferenced or retained.
internal static class NativePointerProbe
{
    public static PointerRelation Classify(nuint titleStart, int titleBytes, nuint source,
        nuint nodeStart, long nodeBytes, long nodeCapacity)
    {
        if (titleBytes < 0 || nodeBytes <= 0 || nodeCapacity < nodeBytes || nodeCapacity > 16 * 1024 * 1024 ||
            source == 0 || !TryEnd(titleStart, (nuint)titleBytes + 1, out var titleEnd) ||
            !TryEnd(nodeStart, (nuint)nodeBytes, out var nodeEnd) ||
            !TryEnd(nodeStart, (nuint)nodeCapacity, out var nodeCapacityEnd)) return PointerRelation.Unknown;
        if ((source >= titleStart && source < titleEnd) || (nodeStart < titleEnd && titleStart < nodeCapacityEnd))
            return PointerRelation.TitleStorageAlias;
        return source >= nodeStart && source < nodeEnd
            ? PointerRelation.SeparateNodeStorage : PointerRelation.Unknown;
    }

    private static bool TryEnd(nuint start, nuint length, out nuint end)
    {
        end = 0;
        if (start == 0 || length == 0 || length > nuint.MaxValue - start) return false;
        end = start + length;
        return true;
    }
}

// Bounded lifecycle/counters contain no addresses or game objects.
internal sealed class PointerProbeBudget
{
    public const int FrameLimit = 6;
    public const int PlatesPerFrame = 4;
    public const int SampleLimit = FrameLimit * PlatesPerFrame * 3;
    public bool Active { get; private set; }
    public int Frames { get; private set; }
    public int Samples { get; private set; }
    public int Separate { get; private set; }
    public int Aliases { get; private set; }
    public int Unknown { get; private set; }
    public bool CanArm => false; // Snapshot observations never prove complete native lifetime.

    public void Start()
    {
        Frames = Samples = Separate = Aliases = Unknown = 0;
        Active = true;
    }

    public bool BeginFrame()
    {
        if (!Active || Frames >= FrameLimit) return false;
        Frames++;
        return true;
    }

    public bool Record(ProbePhase phase, PointerRelation relation)
    {
        if (!Active || Frames == 0 || Samples >= SampleLimit) return false;
        Samples++;
        if (phase == ProbePhase.AfterNativeBeforeCleanup)
        {
            if (relation == PointerRelation.SeparateNodeStorage) Separate++;
            else if (relation == PointerRelation.TitleStorageAlias) Aliases++;
            else Unknown++;
        }
        return true;
    }

    public void EndFrame() { if (Frames >= FrameLimit) Active = false; }
    public void Stop() => Active = false;
}
