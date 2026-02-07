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
        private readonly Dictionary<string, string> _vipTypes = new();
        private readonly Dictionary<string, string> _vipDancers = new();
        private readonly Dictionary<string, string> _vipGambaTokens = new();
        private readonly Dictionary<string, string> _vipPhotos = new();
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
                        _vipTypes.Clear();
                        _vipDancers.Clear();
                        _vipGambaTokens.Clear();
                        _vipPhotos.Clear();


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
                                    string key = cleanName.ToLower();
                                    _vipNames.Add(key);

                                    string vipType = "";
                                    if (columns.Length > 2)
                                    {
                                        vipType = columns[2].Trim('"').Trim();
                                    }

                                    if (!string.IsNullOrWhiteSpace(vipType))
                                    {
                                        _vipTypes[key] = vipType;
                                    }

                                    if (columns.Length > 3)
                                    {
                                        string dancer = columns[3].Trim('"').Trim();
                                        if (!string.IsNullOrWhiteSpace(dancer))
                                        {
                                            _vipDancers[key] = dancer;
                                        }
                                    }

                                    if (columns.Length > 4)
                                    {
                                        string gambaToken = columns[4].Trim('"').Trim();
                                        if (!string.IsNullOrWhiteSpace(gambaToken))
                                        {
                                            _vipGambaTokens[key] = gambaToken;
                                        }
                                    }

                                    if (columns.Length > 5)
                                    {
                                        string photo = columns[5].Trim('"').Trim();
                                        if (!string.IsNullOrWhiteSpace(photo))
                                        {
                                            _vipPhotos[key] = photo;
                                        }
                                    }

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

        public string? GetVipType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (_lock)
            {
                _vipTypes.TryGetValue(name.ToLower(), out var vipType);
                return vipType;
            }
        }
        public string? GetVipDancer(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (_lock)
            {
                _vipDancers.TryGetValue(name.ToLower(), out var dancer);
                return dancer;
            }
        }

        public string? GetVipGambaToken(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (_lock)
            {
                _vipGambaTokens.TryGetValue(name.ToLower(), out var token);
                return token;
            }
        }

        public string? GetVipPhoto(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (_lock)
            {
                _vipPhotos.TryGetValue(name.ToLower(), out var photo);
                return photo;
            }
        }

        public List<string> GetDebugList()
        {
            lock (_lock) return _vipNames.Take(10).ToList();
        }
    }
}