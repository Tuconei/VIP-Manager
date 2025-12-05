VIP Name Checker (Dalamud Plugin)

A simple Dalamud plugin that highlights specific players in-game based on a Google Sheet.

Features

Fetches names from a Google Sheet (Column A).

Displays a "★ VIP" tag next to players in the world.

Supports "Name (World)" formatting (strips world name automatically).

Secure: API Key and Sheet ID are stored locally, not in the code.

Setup

Install the plugin.

Generate a Google Cloud API Key (restricted to Sheets API).

Get the ID of your Google Sheet (from the URL).

Run the setup commands in-game:

/vip setkey <your_api_key>

/vip setid <your_sheet_id>

/vip reload

Commands

/vip reload - Refreshes the list from Google Sheets.

/vip on / /vip off - Toggles the overlay.

/vip setkey - Sets your Google API Key.

/vip setid - Sets the Spreadsheet ID.