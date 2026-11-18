namespace VipNameChecker;

internal static class VipHelpContent
{
    public static readonly (string Command, string Description)[] Commands =
    {
        ("/vip", "Open or close settings."),
        ("/vip help", "Open this guide. Repeating it keeps the guide open."),
        ("/vip overlay [on|off|toggle]", "Master switch for ALL VIP displays, including native tags, rings and the range list. Keeps individual feature preferences."),
        ("/vip enable | /vip disable", "Existing aliases for /vip overlay on and /vip overlay off."),
        ("/vip ring [on|off|toggle]", "Show or hide the highlight ring around VIP players."),
        ("/vip tag [on|off|toggle]", "Show or hide the VIP tag using the placement selected in settings."),
        ("/vip list [on|off|toggle]", "Show or hide the VIPs-in-range window. This does not edit or print sheet membership."),
        ("/vip reload", "Fetch the active profile's VIP list from Google Sheets now. Does not reload or unload the plugin. Watch chat for success or failure."),
        ("/vip sheet", "Open the active profile's linked Google Sheet in your default browser. The plugin does not edit it or change Google permissions."),
        ("/vip status", "Print the active profile, display switches, placement, refresh setting and detailed native cleanup status in chat."),
        ("/vip native | /vip native status", "Print native state, remembered enable preference and cleanup readiness."),
        ("/vip native arm", "Enable/resume native VIP after selecting a native placement and enabling master/tag. Remembers enabled for the next plugin load. Requires completed cleanup."),
        ("/vip native disarm", "Turn off native VIP, save disabled, and request cleanup. Wait for acknowledgement and visually verify removal."),
        ("/vip native prepare-unload", "Pause and clean native VIP while KEEPING its saved enabled preference. Wait for acknowledgement, controlled unload ready=True and visible removal before disabling/updating the plugin."),
        ("/vip native add Full Character Name", "Add a session-only test VIP. Not saved, not written to the sheet and not shared with staff."),
        ("/vip native remove Full Character Name", "Remove a session-only test entry. Normal sheet membership still applies."),
        ("/vip native clear", "Clear session-only test entries, not the Google Sheet."),
        ("/vip native observe", "Advanced diagnostic: start a bounded, read-only title observer; see /xllog. Requires disarm and completed cleanup; not needed for normal use.")
    };

    public static readonly (string Title, string Text)[] Basics =
    {
        ("Quick start", "Open /vip, choose or create a venue profile, enter its Google Spreadsheet ID (not the full URL), then Reload VIP List. The sheet must allow the plugin's CSV fetch. Put full character names in column A and configure extra range-list columns in settings. Matching is by character name, not home world."),
        ("Shared list, local display", "Staff using the same sheet can refresh the same VIP membership. Auto Refresh is optional and configured per profile. Changes are made in Google Sheets, not in this plugin. Rings, tags, colors and display choices are local to each user's game and do not sync through Honorific or change another player's title."),
        ("Display choices", "Floating tags and highlight rings are screen overlays and can remain visible through walls. Native title prefix, title suffix and FC-tag suffix use the nameplate fields and follow their visibility. Existing titles and FC text are preserved; hidden native fields can also hide VIP. The range list shows nearby VIPs and configured sheet columns."),
        ("Toggles and macros", "For overlay, ring, tag and list, omitting the final argument toggles the setting. Explicit on/off is repeatable and useful in macros, for example /vip ring off. These settings save to the active profile. Turning the master off does not erase individual feature choices."),
        ("Native settings and safe updates", "Choose native placement and RGB/hex color in settings after cleanup. Enable native VIP once to remember it across plugin/game loads. Changing master/tag, sheet, placement, color or profile can pause native rendering for safety; it will not silently rearm the same instance. Wait for cleanup and use /vip native arm when ready. Ring/list toggles do not require native cleanup."),
        ("Before disabling or updating in game", "Use /vip native prepare-unload, wait for the cleanup acknowledgement, check controlled unload ready=True with /vip native status, and visually confirm markers are gone. This retains the saved enabled preference. A full game exit needs no special preparation. Do not run developer and installed copies together."),
        ("If something is missing", "Check /vip status: master and the individual feature must be on. Check the active profile, reload the sheet, verify exact character names and range, and inspect native readiness/visibility. A failed reload is reported in chat; details are in /xllog. Native conflicts pause safely instead of guessing or overwriting another writer's text.")
    };
}
