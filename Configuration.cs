using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace VipNameChecker
{
    [Serializable]
    public class ColumnDefinition
    {
        public string Header { get; set; } = "Info";
        public string CsvColumn { get; set; } = "C"; // Defaults to column C (Index 2)
        public float Width { get; set; } = 100f;
    }

    [Serializable]
    public class VipProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Default";
        public string SpreadsheetId { get; set; } = "";

        public bool IsOverlayEnabled { get; set; } = true;
        public bool ShowHighlightRing { get; set; } = true;
        public bool ShowVipTag { get; set; } = true;
        public bool ShowVipList { get; set; } = true;
        public float VipListRange { get; set; } = 30.0f;

        public List<ColumnDefinition> Columns { get; set; } = new();
    }

    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 2;

        public List<VipProfile> Profiles { get; set; } = new();
        public int ActiveProfileIndex { get; set; } = 0;

        // --- Legacy fields for migration ---
        public string? SpreadsheetId { get; set; } = null;
        public bool? IsOverlayEnabled { get; set; } = null;
        public bool? ShowHighlightRing { get; set; } = null;
        public bool? ShowVipTag { get; set; } = null;
        public bool? ShowVipList { get; set; } = null;
        public float? VipListRange { get; set; } = null;
        // -----------------------------------

        [NonSerialized]
        private IDalamudPluginInterface? PluginInterface;

        public void Initialize(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;

            // Migration from Version 0/1 to Version 2
            if (SpreadsheetId != null || Profiles.Count == 0)
            {
                var initialProfile = new VipProfile
                {
                    Name = SpreadsheetId != null ? "Migrated Profile" : "Default Profile",
                    SpreadsheetId = this.SpreadsheetId ?? "",
                    IsOverlayEnabled = this.IsOverlayEnabled ?? true,
                    ShowHighlightRing = this.ShowHighlightRing ?? true,
                    ShowVipTag = this.ShowVipTag ?? true,
                    ShowVipList = this.ShowVipList ?? true,
                    VipListRange = this.VipListRange ?? 30.0f
                };

                // Recreate the previous hardcoded columns if migrating
                if (SpreadsheetId != null)
                {
                    initialProfile.Columns.Add(new ColumnDefinition { Header = "Type", CsvColumn = "C", Width = 90f });
                    initialProfile.Columns.Add(new ColumnDefinition { Header = "Dancer", CsvColumn = "D", Width = 90f });
                    initialProfile.Columns.Add(new ColumnDefinition { Header = "Gamba Token", CsvColumn = "E", Width = 110f });
                    initialProfile.Columns.Add(new ColumnDefinition { Header = "Photo", CsvColumn = "F", Width = 80f });
                }
                else
                {
                    // Fresh install defaults
                    initialProfile.Columns.Add(new ColumnDefinition { Header = "Info", CsvColumn = "C", Width = 150f });
                }

                if (Profiles.Count == 0)
                {
                    Profiles.Add(initialProfile);
                }

                // Clear legacy data
                SpreadsheetId = null;
                IsOverlayEnabled = null;
                ShowHighlightRing = null;
                ShowVipTag = null;
                ShowVipList = null;
                VipListRange = null;

                Save();
            }
        }

        public VipProfile GetActiveProfile()
        {
            if (Profiles.Count == 0)
            {
                var p = new VipProfile { Name = "Default" };
                p.Columns.Add(new ColumnDefinition { Header = "Info", CsvColumn = "C" });
                Profiles.Add(p);
                ActiveProfileIndex = 0;
                Save();
            }

            if (ActiveProfileIndex < 0 || ActiveProfileIndex >= Profiles.Count)
            {
                ActiveProfileIndex = 0;
            }

            return Profiles[ActiveProfileIndex];
        }

        public void Save()
        {
            this.PluginInterface!.SavePluginConfig(this);
        }
    }
}