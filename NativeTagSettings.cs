using System;
using System.Globalization;
using System.Numerics;
namespace VipNameChecker;
public enum VipTagPlacement { Floating = 0, NativeTitle = 1, NativeFreeCompany = 2, NativeTitlePrefix = 3 }
internal static class NativeTagSettings
{
    public static bool IsNative(VipTagPlacement placement) => placement is VipTagPlacement.NativeTitle or VipTagPlacement.NativeFreeCompany or VipTagPlacement.NativeTitlePrefix;
    public static Vector4 NormalizeColor(Vector4 color) => new(
        float.IsFinite(color.X) ? Math.Clamp(color.X, 0, 1) : 0,
        float.IsFinite(color.Y) ? Math.Clamp(color.Y, 0, 1) : 1,
        float.IsFinite(color.Z) ? Math.Clamp(color.Z, 0, 1) : 0, 1);
    public static string ToHex(Vector4 color)
    {
        color = NormalizeColor(color);
        return $"#{(int)MathF.Round(color.X * 255):X2}{(int)MathF.Round(color.Y * 255):X2}{(int)MathF.Round(color.Z * 255):X2}";
    }
    public static bool TryHex(string text, out Vector4 color)
    {
        color = default;
        text = text.Trim();
        if (text.StartsWith('#')) text = text[1..];
        if (text.Length != 6 || !uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint rgb)) return false;
        color = new Vector4(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1);
        return true;
    }
}
internal readonly record struct NativeArmSelection(Guid Profile, string Sheet, VipTagPlacement Placement, Vector4 Color, bool Enabled, bool Tag, bool NativeEnabled)
{
    public static NativeArmSelection From(VipProfile profile) => new(profile.Id, profile.SpreadsheetId,
        profile.TagPlacement, NativeTagSettings.NormalizeColor(profile.VipTagColor), profile.IsOverlayEnabled, profile.ShowVipTag, profile.NativeVipEnabled);
}
// Durable preference lives on each profile; a pause belongs to this plugin session only.
internal sealed class NativeEnableSession
{
    public bool Paused { get; private set; }
    public void Pause() => Paused = true;
    public void Resume() => Paused = false;
    public bool ShouldAutoArm(VipProfile profile, bool safeToArm) => !Paused && safeToArm &&
        profile.NativeVipEnabled && NativeTagSettings.IsNative(profile.TagPlacement) && profile.IsOverlayEnabled && profile.ShowVipTag;
}