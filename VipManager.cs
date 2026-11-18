using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace VipNameChecker;

public sealed class VipManager : IDisposable
{
    private readonly HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> temporaryNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> data = new(StringComparer.OrdinalIgnoreCase);
    private readonly object sync = new();
    private readonly Configuration config;
    private readonly HttpClient http = new();
    private readonly CancellationTokenSource lifetime = new();
    private Timer? refreshTimer;
    private Guid loadedProfile;
    private string loadedSheet = "";
    private int request;
    private long revision;
    private bool disposed;
    public long Revision => Interlocked.Read(ref revision);

    public VipManager(Configuration config)
    {
        this.config = config;
        http.DefaultRequestHeaders.UserAgent.ParseAdd("VIP-Manager/1.3.0");
    }

    public void UpdateAutoRefresh()
    {
        refreshTimer?.Dispose();
        refreshTimer = null;
        var profile = config.GetActiveProfile();
        if (!disposed && profile.AutoRefreshEnabled && profile.AutoRefreshIntervalMinutes > 0)
        {
            var interval = TimeSpan.FromMinutes(profile.AutoRefreshIntervalMinutes);
            refreshTimer = new Timer(state =>
            {
                _ = Service.Framework.RunOnFrameworkThread(() => { if (!disposed) LoadVipNames(); });
            }, null, interval, interval);
        }
    }

    public void SetTemporaryVip(string name, bool add)
    {
        lock (sync)
        {
            if (add ? temporaryNames.Add(name.Trim()) : temporaryNames.Remove(name.Trim()))
                Interlocked.Increment(ref revision);
        }
    }

    public void ClearTemporaryVips()
    {
        lock (sync)
        {
            temporaryNames.Clear();
            Interlocked.Increment(ref revision);
        }
    }

    public void LoadVipNames()
    {
        if (disposed) return;
        var profile = config.GetActiveProfile();
        Guid profileId = profile.Id;
        string sheetId = profile.SpreadsheetId;
        int loadRequest = ++request;
        if (string.IsNullOrWhiteSpace(sheetId))
        {
            lock (sync)
            {
                names.Clear();
                data.Clear();
                loadedProfile = profileId;
                loadedSheet = sheetId;
                Interlocked.Increment(ref revision);
            }
            Service.Chat.Print("[VIP] No sheet configured. Use /vip native add Full Name for a session-only test.");
            return;
        }
        var columns = profile.Columns.Select(c => ParseColumnToIndex(c.CsvColumn)).ToArray();
        _ = FetchAsync(profileId, sheetId, columns, loadRequest, lifetime.Token);
    }

    private async Task FetchAsync(Guid profileId, string sheetId, int[] columns, int loadRequest, CancellationToken token)
    {
        try
        {
            string csv = await http.GetStringAsync($"https://docs.google.com/spreadsheets/d/{Uri.EscapeDataString(sheetId)}/export?format=csv", token).ConfigureAwait(false);
            var fetched = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            // Retain the original name-only CSV interpretation for this focused test.
            foreach (string line in csv.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var cells = line.Split(',');
                string name = cells[0].Trim('"').Split('(')[0].Trim();
                if (name.Length == 0) continue;
                fetched[name] = columns.Select(i => i >= 0 && i < cells.Length ? cells[i].Trim('"').Trim() : "-").ToList();
            }
            await Service.Framework.RunOnFrameworkThread(() =>
            {
                if (disposed || loadRequest != request) return;
                var current = config.GetActiveProfile();
                if (current.Id != profileId || current.SpreadsheetId != sheetId) return;
                lock (sync)
                {
                    names.Clear();
                    data.Clear();
                    foreach (var row in fetched) { names.Add(row.Key); data[row.Key] = row.Value; }
                    loadedProfile = profileId;
                    loadedSheet = sheetId;
                    Interlocked.Increment(ref revision);
                }
                Service.Chat.Print($"[VIP] VIP list loaded ({fetched.Count} entries).");
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested) return;
            Service.PluginLog.Error(ex, "Failed to fetch VIP names.");
            await Service.Framework.RunOnFrameworkThread(() =>
            {
                if (!disposed && loadRequest == request) Service.Chat.Print("[VIP] Load failed. Check /xllog.");
            }).ConfigureAwait(false);
        }
    }

    public bool IsVip(string name)
    {
        var current = config.GetActiveProfile();
        lock (sync)
            return temporaryNames.Contains(name) ||
                (loadedProfile == current.Id && loadedSheet == current.SpreadsheetId && names.Contains(name));
    }

    public List<string>? GetVipData(string name)
    {
        var current = config.GetActiveProfile();
        lock (sync)
            return loadedProfile == current.Id && loadedSheet == current.SpreadsheetId && data.TryGetValue(name, out var row)
                ? new List<string>(row) : null;
    }

    private static int ParseColumnToIndex(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return -1;
        input = input.ToUpperInvariant().Trim();
        if (int.TryParse(input, out int number)) return number - 1;
        int index = 0;
        foreach (char c in input)
        {
            if (c < 'A' || c > 'Z') return -1;
            index = index * 26 + c - 'A' + 1;
        }
        return index - 1;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        refreshTimer?.Dispose();
        lifetime.Cancel();
        http.Dispose();
        lifetime.Dispose();
    }
}
