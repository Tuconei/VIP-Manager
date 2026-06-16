# VIP Name Checker

VIP Name Checker is a Dalamud plugin that highlights players whose character names appear in a published Google Sheet.

## VIP Spreadsheet Template

This repo includes a starter spreadsheet template for venues that want a simple VIP signup workflow:

[Download the VIP Tracker Template](templates/VIP-Tracker-Template.xlsx)

The template includes:

- `VIP Signups` for entering VIPs as they sign up
- `Plugin Export` for the alphabetized list that should be published as CSV
- `Benefit Resets` for tracking benefits that refresh daily, weekly, monthly, quarterly, yearly, one-time, or manually
- `Lists` for customizing durations, VIP types, benefits, and payment/proof values
- `Setup Guide` with recommended plugin column mappings

### Setting up the spreadsheet

1. Download `templates/VIP-Tracker-Template.xlsx`.
2. Go to [Google Drive](https://drive.google.com/).
3. Click `New` > `File upload`, then upload `VIP-Tracker-Template.xlsx`.
4. Open the uploaded file with Google Sheets.
5. Choose `File` > `Save as Google Sheets` if Drive opened it in Excel preview mode.
6. Customize durations, VIP types, benefits, resettable benefits, and payment/proof values on the `Lists` tab.
7. Enter VIPs on `VIP Signups`.
8. Track recurring benefit usage on `Benefit Resets`.

### Resettable benefits

Use `Benefit Resets` for benefits that come back on a schedule, such as a monthly photo slot or weekly token.

1. Add the character name.
2. Choose the benefit.
3. Choose the reset period.
4. Set `Max Uses` for benefits that can be claimed more than once per reset period.
5. Update `Uses This Period` and `Last Used` when the benefit is claimed.
6. Leave `Include In Plugin?` as `Yes` if staff should see the reset status in game.

The sheet calculates `Remaining Uses`, `Next Reset`, `Available?`, and a visible note automatically. Weekly, monthly, quarterly, and yearly resets are anchored to the VIP's original signup date, not the last-used date. For example, a monthly benefit for a VIP who signed up on the 1st resets on the 1st of each month even if they used that benefit later in the month.

When a new signup-date anchored reset period begins, `Remaining Uses` returns to `Max Uses`. This means a monthly benefit with `Max Uses` set to `3` can show `2/3 left`, `1/3 left`, or `0/3 left; resets yyyy-mm-dd`.

This does not erase history. It calculates availability from `Last Used`, which keeps the sheet auditable and avoids needing a Google Apps Script.

### Publishing the plugin export tab

The plugin reads a public CSV export from Google Sheets. Publish only the generated `Plugin Export` tab, not the whole workbook.

1. In Google Sheets, open the copied template.
2. Select `File` > `Share` > `Publish to web`.
3. In the first dropdown, choose `Plugin Export`.
4. In the second dropdown, choose `Comma-separated values (.csv)`.
5. Click `Publish`.

### Finding the spreadsheet ID

After publishing, copy the spreadsheet ID from the Google Sheets URL. It is the long value between `/d/` and `/edit`.

Example:

```text
https://docs.google.com/spreadsheets/d/THIS_IS_THE_SPREADSHEET_ID/edit
```

Paste that ID into VIP Name Checker, then reload the VIP list.

Recommended plugin columns:

| Header | CSV Col | Width |
| --- | --- | ---: |
| VIP Type | C | 90 |
| Benefits | D | 220 |
| Duration | E | 90 |
| Status | F | 100 |
| Notes | G | 220 |

Column A must remain the character name because the plugin uses it to match players.
