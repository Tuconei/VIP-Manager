using System;
using System.Text;
using Lumina.Text.ReadOnly;

namespace VipNameChecker;

// Operates on the encoded render input. Existing bytes, including unknown macros,
// are copied unchanged; only a UTF-8 text insertion is made.
internal static class NativeTitleSuffix
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public static bool TryAppend(ReadOnlySpan<byte> original, out byte[] result)
    {
        result = Array.Empty<byte>();
        if (original.IsEmpty)
        {
            result = Encoding.UTF8.GetBytes("\u300AVIP\u300B");
            return true;
        }

        var encoded = new ReadOnlySeStringSpan(original);

        var visible = new StringBuilder();
        int lastTextEnd = 0;
        try
        {
            foreach (var item in encoded.GetOffsetEnumerator())
            {
                var payload = item.Payload;
                // The installed Lumina build's whole-string Validate() returns
                // false even after valid enumeration. Validate each payload instead.
                if (!payload.Validate()) return false;
                if (payload.Type == ReadOnlySePayloadType.Text && !payload.Body.IsEmpty)
                {
                    visible.Append(StrictUtf8.GetString(payload.Body));
                    lastTextEnd = item.Offset + payload.Body.Length;
                }
            }
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        string text = visible.ToString();
        bool quoted = text.Length >= 2 && IsQuotePair(text[0], text[^1]);
        string content = quoted ? text[1..^1] : text;
        string trimmed = content.TrimEnd();
        if (trimmed.Equals("VIP", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith(" VIP", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("[VIP]", StringComparison.OrdinalIgnoreCase)) return false;

        int insertAt = quoted
            ? lastTextEnd - Encoding.UTF8.GetByteCount(text.AsSpan(text.Length - 1))
            : original.Length;
        var suffix = Encoding.UTF8.GetBytes(content.Length == 0 ? "VIP" : " VIP");
        result = new byte[original.Length + suffix.Length];
        original[..insertAt].CopyTo(result);
        suffix.CopyTo(result.AsSpan(insertAt));
        original[insertAt..].CopyTo(result.AsSpan(insertAt + suffix.Length));
        return true;
    }

    // Preserve every upstream byte; add one scoped foreground color envelope.
    // FC gets an append AFTER its original closing quote, avoiding FC quote parsing.
    public static byte[]? AppendColored(byte[] original, bool freeCompany, System.Numerics.Vector4? color = null, bool prefix = false)
    {
        var b = new Lumina.Text.SeStringBuilder();
        b.PushColorRgba(NativeTagSettings.NormalizeColor(color ?? new System.Numerics.Vector4(1f, 0.75f, 0.1f, 1f)));
        b.Append("VIP");
        b.PopColor();
        var vip = b.GetViewAsSpan().ToArray();
        if (prefix && !freeCompany) return PrependColored(original, vip);
        if (freeCompany)
        {
                        try
            {
                foreach (var p in new ReadOnlySeStringSpan(original))
                {
                    if (!p.Validate()) return null;
                    if (p.Type == ReadOnlySePayloadType.Text) _ = StrictUtf8.GetString(p.Body);
                }
            }
            catch (DecoderFallbackException) { return null; }
            // Native FC removal uses a sentinel, respected by the weak setter.
            // Empty native fields may remain hidden: do not force visibility.
            var result = new byte[original.Length + 1 + vip.Length];
            original.CopyTo(result, 0);
            result[original.Length] = (byte)' ';
            vip.CopyTo(result, original.Length + 1);
            return result;
        }
        if (!TryAppend(original, out var plain)) return null;
        int offset = 0;
        while (offset < original.Length && offset < plain.Length && original[offset] == plain[offset]) offset++;
        int insertedLength = plain.Length - original.Length;
        // Empty titles synthesize quotes; keep those quotes outside the color.
        if (original.Length == 0)
        {
            var left = Encoding.UTF8.GetBytes("《");
            var right = Encoding.UTF8.GetBytes("》");
            var result = new byte[left.Length + vip.Length + right.Length];
            left.CopyTo(result, 0); vip.CopyTo(result, left.Length); right.CopyTo(result, left.Length + vip.Length);
            return result;
        }
        var insertion = plain.AsSpan(offset, insertedLength);
        bool space = insertion.Length > 0 && insertion[0] == (byte)' ';
        var colored = new byte[original.Length + (space ? 1 : 0) + vip.Length];
        original.AsSpan(0, offset).CopyTo(colored);
        if (space) colored[offset++] = (byte)' ';
        vip.CopyTo(colored, offset);
        original.AsSpan(offset - (space ? 1 : 0)).CopyTo(colored.AsSpan(offset + vip.Length));
        return colored;
    }
    private static byte[]? PrependColored(byte[] original, byte[] vip)
    {
        var text = new StringBuilder();
        int firstTextOffset = -1;
        try
        {
            foreach (var item in new ReadOnlySeStringSpan(original).GetOffsetEnumerator())
            {
                var payload = item.Payload;
                if (!payload.Validate()) return null;
                if (payload.Type != ReadOnlySePayloadType.Text || payload.Body.IsEmpty) continue;
                if (firstTextOffset < 0) firstTextOffset = item.Offset;
                text.Append(StrictUtf8.GetString(payload.Body));
            }
        }
        catch (DecoderFallbackException) { return null; }
        string visible = text.ToString();
        bool quoted = visible.Length >= 2 && IsQuotePair(visible[0], visible[^1]);
        string content = quoted ? visible[1..^1] : visible;
        string trimmed = content.TrimStart();
        if (trimmed.Equals("VIP", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("VIP ", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("[VIP]", StringComparison.OrdinalIgnoreCase)) return null;
        if (original.Length == 0)
        {
            var left = Encoding.UTF8.GetBytes("\u300A");
            var right = Encoding.UTF8.GetBytes("\u300B");
            var empty = new byte[left.Length + vip.Length + right.Length];
            left.CopyTo(empty, 0); vip.CopyTo(empty, left.Length); right.CopyTo(empty, left.Length + vip.Length);
            return empty;
        }
        int insertAt = quoted ? firstTextOffset + Encoding.UTF8.GetByteCount(visible.AsSpan(0, 1)) : 0;
        bool space = content.Length > 0;
        var result = new byte[original.Length + vip.Length + (space ? 1 : 0)];
        original.AsSpan(0, insertAt).CopyTo(result);
        vip.CopyTo(result, insertAt);
        int after = insertAt + vip.Length;
        if (space) result[after++] = (byte)' ';
        original.AsSpan(insertAt).CopyTo(result.AsSpan(after));
        return result;
    }
    private static bool IsQuotePair(char left, char right) =>
        (left, right) is ('\u300A', '\u300B') or ('\u00AB', '\u00BB') or ('\u201C', '\u201D') or ('"', '"');
}

