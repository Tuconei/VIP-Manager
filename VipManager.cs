using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace VipNameChecker
{
    public class VipManager
    {
        // Stores VIP names
        private readonly HashSet<string> _vipNames = new();

        // Stores data for each VIP. Key: Name (lowercase). Value: List of strings corresponding to the Active Profile's Columns.
        private readonly Dictionary<string, List<string>> _vipData = new();

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
            var profile = _config.GetActiveProfile();

            if (string.IsNullOrEmpty(profile.SpreadsheetId))
            {
                Service.Chat.Print("[VIP] Error: Sheet ID not set for current profile.");
                return;
            }

            Task.Run(async () =>
            {
                try
                {
                    Service.PluginLog.Information($"Fetching VIP list for profile '{profile.Name}'...");

                    string url = $"https://docs.google.com/spreadsheets/d/{profile.SpreadsheetId}/export?format=csv";

                    _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                    var csvData = await _http.GetStringAsync(url);
                    var lines = csvData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    // Pre-calculate column indices to avoid parsing on every row
                    var columnIndices = profile.Columns.Select(c => ParseColumnToIndex(c.CsvColumn)).ToList();

                    lock (_lock)
                    {
                        _vipNames.Clear();
                        _vipData.Clear();

                        for (int i = 0; i < lines.Length; i++)
                        {
                            var line = lines[i];
                            // Simple CSV split - standard CSV handling handles commas inside quotes, but simple split matches original implementation
                            // If your sheets have commas in cells, a more robust CSV parser is needed.
                            var columns = line.Split(',');

                            if (columns.Length > 0)
                            {
                                string rawName = columns[0]; // Column A is always assumed to be Name
                                rawName = rawName.Trim('"');
                                string cleanName = rawName.Split('(')[0].Trim();

                                if (!string.IsNullOrWhiteSpace(cleanName))
                                {
                                    string key = cleanName.ToLower();
                                    _vipNames.Add(key);

                                    var rowData = new List<string>();

                                    foreach (var index in columnIndices)
                                    {
                                        string cellValue = "-";
                                        if (index >= 0 && index < columns.Length)
                                        {
                                            cellValue = columns[index].Trim('"').Trim();
                                        }
                                        rowData.Add(cellValue);
                                    }

                                    _vipData[key] = rowData;
                                }
                            }
                        }
                    }
                    Service.Chat.Print($"[VIP Checker] VIP List loaded ({_vipNames.Count} entries).");
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

        // Returns the list of column values for a specific player
        public List<string>? GetVipData(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            lock (_lock)
            {
                if (_vipData.TryGetValue(name.ToLower(), out var data))
                {
                    return data;
                }
                return null;
            }
        }

        // Helper to convert "A", "B", "C" or "1", "2", "3" to 0-based index
        private int ParseColumnToIndex(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return -1;
            input = input.ToUpper().Trim();

            // Check if it's a number
            if (int.TryParse(input, out int result))
            {
                return result - 1; // User types 1 for Column A, convert to 0
            }

            // Convert letters to index (A=0, B=1, AA=26)
            int index = 0;
            foreach (char c in input)
            {
                if (c < 'A' || c > 'Z') return -1; // Invalid character
                index *= 26;
                index += (c - 'A' + 1);
            }
            return index - 1;
        }
    }
}