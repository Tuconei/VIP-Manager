using System;
namespace VipNameChecker;
internal static class SheetLink
{
    public static bool TryCreate(string? spreadsheetId, out string url, out string error)
    {
        url = string.Empty;
        string id = spreadsheetId?.Trim() ?? string.Empty;
        if (id.Length == 0) { error = "Set the active profile's Google Spreadsheet ID first."; return false; }
        if (id.Length < 20 || id.Length > 128) { error = "Enter only a Google Spreadsheet ID (20-128 letters, digits, '-' or '_'), not a URL."; return false; }
        foreach (char c in id)
            if (!(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_'))
            { error = "Invalid Spreadsheet ID. Use the ID only, not a URL or other characters."; return false; }
        url = "https://docs.google.com/spreadsheets/d/" + id + "/edit";
        error = string.Empty;
        return true;
    }
}