# VIP Name Checker

VIP Name Checker is a Dalamud plugin that highlights players whose character names appear in a published Google Sheet.

## VIP Spreadsheet Template

This repo includes a starter spreadsheet template for venues that want a simple VIP signup workflow:

[Download the VIP Tracker Template](templates/VIP-Tracker-Template.xlsx)

The template includes:

- `VIP Signups` for entering VIPs as they sign up
- `Plugin Export` for the alphabetized list that should be published as CSV
- `Lists` for customizing durations, VIP types, benefits, and payment/proof values
- `Setup Guide` with recommended plugin column mappings

To use it:

1. Download `templates/VIP-Tracker-Template.xlsx`.
2. Upload it to your own Google Drive and open it with Google Sheets.
3. Customize durations on the `Lists` tab.
4. Enter VIPs on `VIP Signups`.
5. Publish only the `Plugin Export` tab as CSV.
6. Put the spreadsheet ID into VIP Name Checker and reload the VIP list.

Recommended plugin columns:

| Header | CSV Col | Width |
| --- | --- | ---: |
| VIP Type | C | 90 |
| Benefits | D | 220 |
| Duration | E | 90 |
| Status | F | 100 |
| Notes | G | 220 |

Column A must remain the character name because the plugin uses it to match players.
