using System;
using System.Collections.Generic;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace VipNameChecker;

// No handlers, native pointers to dereference, or SeString objects survive callbacks.
internal readonly record struct PlateIdentity(int ArrayIndex, ulong GameObjectId, nint PlayerAddress);

internal sealed class TitleRenderTransaction
{
    private sealed record Edit(PlateIdentity Owner, byte[] Original, byte[] Written,
        int InsertLength, byte[] VisibleWritten, int VisibleInsertOffset);

    private readonly Dictionary<int, Edit> edits = new();

    public bool HasEdits => edits.Count != 0;
    public bool TouchesIndex(int arrayIndex) => edits.ContainsKey(arrayIndex);

    public void Record(PlateIdentity identity, ReadOnlySpan<byte> original, ReadOnlySpan<byte> written)
    {
        int insertOffset = 0;
        while (insertOffset < original.Length && insertOffset < written.Length &&
               original[insertOffset] == written[insertOffset]) insertOffset++;
        int insertLength = written.Length - original.Length;
        int visibleInsertOffset = -1;
        byte[] visibleWritten = Array.Empty<byte>();
        if (insertLength > 0 && written[(insertOffset + insertLength)..].SequenceEqual(original[insertOffset..]) &&
            TryProjectText(written, out visibleWritten, out var offsets))
        {
            int index = Array.IndexOf(offsets, insertOffset);
            if (IsContiguousTextRange(offsets, index, insertOffset, insertLength)) visibleInsertOffset = index;
        }
        edits[identity.ArrayIndex] = new(identity, original.ToArray(), written.ToArray(),
            insertLength, visibleWritten, visibleInsertOffset);
    }

    public bool TryRestore(PlateIdentity identity, ReadOnlySpan<byte> current, out byte[] original)
    {
        original = Array.Empty<byte>();
        if (!edits.TryGetValue(identity.ArrayIndex, out var edit) || edit.Owner != identity) return false;
        edits.Remove(identity.ArrayIndex);
        if (current.SequenceEqual(edit.Written))
        {
            original = edit.Original;
            return true;
        }

        // A matching substring is not provenance: another writer may use that
        // phrase inside a completely different title. Require the WHOLE visible
        // text to match, and prove that all byte changes are inserted style macros.
        if (edit.VisibleInsertOffset < 0 || !TryProjectText(current, out var text, out var offsets)) return false;
        if (!text.AsSpan().SequenceEqual(edit.VisibleWritten) || !IsOnlyFormattingInsertion(current, edit.Written)) return false;
        original = RemoveTextBytes(current, offsets.AsSpan(edit.VisibleInsertOffset, edit.InsertLength));
        return true;
    }

    public void Clear() => edits.Clear();

    public static bool IsFormattingOnlyAddition(ReadOnlySpan<byte> current, ReadOnlySpan<byte> baseline) =>
        current.SequenceEqual(baseline) ||
        (TryProjectText(current, out var currentText, out _) && TryProjectText(baseline, out var baselineText, out _) &&
         currentText.AsSpan().SequenceEqual(baselineText) && IsOnlyFormattingInsertion(current, baseline));

    private static bool IsOnlyFormattingInsertion(ReadOnlySpan<byte> current, ReadOnlySpan<byte> written)
    {
        int consumedCurrent = 0;
        int consumedWritten = 0;
        bool addedStyle = false;
        foreach (var item in new ReadOnlySeStringSpan(current).GetOffsetEnumerator())
        {
            var payload = item.Payload;
            int length = payload.EnvelopeByteLength;
            // Fail closed on noncanonical envelope lengths as well. Existing
            // payloads must be retained byte-for-byte, in their original order.
            if (!payload.Validate() || item.Offset != consumedCurrent || length > current.Length - item.Offset) return false;
            var raw = current.Slice(item.Offset, length);
            consumedCurrent += length;
            if (written[consumedWritten..].StartsWith(raw))
            {
                consumedWritten += length;
                continue;
            }
            if (payload.Type != ReadOnlySePayloadType.Macro || !IsFormattingMacro(payload.MacroCode)) return false;
            addedStyle = true;
        }
        return addedStyle && consumedCurrent == current.Length && consumedWritten == written.Length;
    }

    // Explicitly exclude text-producing/semantic macros (Icon, String, Link,
    // Sheet, If, etc.). They can change a title even when plain text is identical.
    private static bool IsFormattingMacro(MacroCode code) => code is
        MacroCode.Color or MacroCode.EdgeColor or MacroCode.ShadowColor or
        MacroCode.ColorType or MacroCode.EdgeColorType or MacroCode.Bold or
        MacroCode.Italic or MacroCode.Edge or MacroCode.Shadow;

    private static bool IsContiguousTextRange(int[] offsets, int start, int rawStart, int length)
    {
        if (start < 0 || length <= 0 || length > offsets.Length - start) return false;
        for (int i = 0; i < length; i++)
            if (offsets[start + i] != rawStart + i) return false;
        return true;
    }

    private static bool TryProjectText(ReadOnlySpan<byte> encoded, out byte[] text, out int[] offsets)
    {
        var bytes = new List<byte>();
        var positions = new List<int>();
        foreach (var item in new ReadOnlySeStringSpan(encoded).GetOffsetEnumerator())
        {
            if (!item.Payload.Validate())
            {
                text = Array.Empty<byte>();
                offsets = Array.Empty<int>();
                return false;
            }
            if (item.Payload.Type != ReadOnlySePayloadType.Text) continue;
            for (int i = 0; i < item.Payload.Body.Length; i++)
            {
                bytes.Add(item.Payload.Body[i]);
                positions.Add(item.Offset + i);
            }
        }
        text = bytes.ToArray();
        offsets = positions.ToArray();
        return true;
    }

    private static byte[] RemoveTextBytes(ReadOnlySpan<byte> current, ReadOnlySpan<int> positions)
    {
        var result = new byte[current.Length - positions.Length];
        int removed = 0;
        int target = 0;
        for (int source = 0; source < current.Length; source++)
        {
            if (removed < positions.Length && positions[removed] == source) removed++;
            else result[target++] = current[source];
        }
        return result;
    }
}
