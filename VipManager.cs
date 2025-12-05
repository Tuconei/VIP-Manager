using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VipNameChecker
{
    public class VipManager
    {
        private readonly HashSet<string> _vipNames = new();
        private readonly object _lock = new();
        private readonly Configuration _config;

        // CHANGED: Constructor now takes the Configuration
        public VipManager(Configuration config)
        {
            _config = config;
        }

        private const string Range = "'VIP List'!A8:A";

        public void LoadVipNames()
        {
            // Security Check: Don't run if keys are missing
            if (string.IsNullOrEmpty(_config.GoogleApiKey) || string.IsNullOrEmpty(_config.SpreadsheetId))
            {
                Service.Chat.Print("[VIP] Error: API Key or Sheet ID not set! Use '/vip help' for instructions.");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    Service.PluginLog.Information("Fetching VIP list...");

                    using var service = new SheetsService(new BaseClientService.Initializer()
                    {
                        // CHANGED: Use config value
                        ApiKey = _config.GoogleApiKey,
                        ApplicationName = "DalamudVipChecker"
                    });

                    // CHANGED: Use config value
                    var request = service.Spreadsheets.Values.Get(_config.SpreadsheetId, Range);
                    var response = await request.ExecuteAsync();
                    var values = response.Values;

                    lock (_lock)
                    {
                        _vipNames.Clear();
                        if (values != null)
                        {
                            foreach (var row in values)
                            {
                                if (row.Count > 0 && row[0] is string rawName)
                                {
                                    string cleanName = rawName.Split('(')[0].Trim();
                                    if (!string.IsNullOrWhiteSpace(cleanName))
                                    {
                                        _vipNames.Add(cleanName.ToLower());
                                    }
                                }
                            }
                        }
                    }
                    Service.Chat.Print($"[VIP Checker] Loaded {_vipNames.Count} names.");
                }
                catch (Exception ex)
                {
                    Service.PluginLog.Error(ex, "Failed to fetch VIP names. Check your API Key and Sheet ID.");
                    Service.Chat.Print("[VIP] Failed to load. Check /xllog for details.");
                }
            });
        }

        public bool IsVip(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            lock (_lock) return _vipNames.Contains(name.ToLower());
        }
    }
}