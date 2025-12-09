using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace VipNameChecker
{
    public class VipManager
    {
        private readonly HashSet<string> _vipNames = new();
        private readonly object _lock = new();
        private readonly Configuration _config;
        private readonly HttpClient _http;

        public VipManager(Configuration config)
        {
            _config = config;
            _http = new HttpClient();
        }

        public void LoadVipNames()
        {
            if (string.IsNullOrEmpty(_config.SpreadsheetId))
            {
                Service.Chat.Print("[VIP] Error: Sheet ID not set! Use '/vip setid <ID>'");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    Service.PluginLog.Information("Fetching VIP list via CSV...");

                    string url = $"https://docs.google.com/spreadsheets/d/{_config.SpreadsheetId}/export?format=csv";

                    _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    var csvData = await _http.GetStringAsync(url);
                    var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    lock (_lock)
                    {
                        _vipNames.Clear();

                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            var columns = line.Split(',');

                            if (columns.Length > 0)
                            {
                                string rawName = columns[0]; // Column A
                                rawName = rawName.Trim('"');
                                string cleanName = rawName.Split('(')[0].Trim();

                                if (!string.IsNullOrWhiteSpace(cleanName))
                                {
                                    _vipNames.Add(cleanName.ToLower());
                                }
                            }
                        }
                    }
                    // CHANGED: Generic success message to avoid confusion over row counts
                    Service.Chat.Print("[VIP Checker] VIP List loaded successfully.");
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Failed to fetch VIP names.");
                    Service.Chat.Print("[VIP] Load failed. Check /xllog.");
                }
            });
        }

        public bool IsVip(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (_lock) return _vipNames.Contains(name.ToLower());
        }

        public List<string> GetDebugList()
        {
            lock (_lock) return _vipNames.Take(10).ToList();
        }
    }
}