# VIP-Manager native placement testing (1.3.0.3)

This is the actual VipNameChecker plugin, not either old /vippersist or /vipplace
variant. Do NOT load them together or alongside another VipNameChecker DLL.
The developer test output is isolated in:
C:\Users\mmacc\AppData\Local\Temp\VIP-Manager-integrated-testing-final-20261118
No build installs itself, no game input is automated, and existing live config is
not edited by the development process. Loading this same-identity plugin uses the
normal VIP-Manager configuration. Back up it before testing if desired.

## Existing profiles and behavior
The new per-profile TagPlacement defaults to Floating, preserving the old floating
tag, offsets, ring, list, sheet, color and alpha. Existing migration paths are retained.
Choices in /vip: Floating overlay (original), Native title suffix, Native FC-tag suffix, Native title prefix.
The normal ring and range list remain separate independent options. A ring/floating
overlay may be visible through walls; native placement does not hide an enabled ring.
Native RGB shares the existing VipTagColor: a hex picker and explicit #RRGGBB box
apply to the local marker only. Native output is opaque RGB; editing it retains the
saved floating alpha. Invalid hex never overwrites color.

## Controlled test
1. Finish cleanup on every old dev test: disarm, acknowledgement, ready status,
   visually verify the marker gone, then unload. Leave release disabled while this
   build is loaded. Do not hot-replace an enabled/loaded DLL.
2. Load this actual-plugin candidate through the developer entry (or the separately
   published opt-in testing release). Existing/migrated profiles remain native-disabled until explicitly enabled once. Enabled native profiles resume automatically on later loads, using normal sheet membership.
3. Open /vip. Select Native title suffix. Pick RGB or enter #RRGGBB and Apply Hex.
   Ensure Enable Overlay and Show VIP Tag are on. Disable ring for a clean wall test.
4. Use existing sheet membership, or /vip native add Full Character Name for a
   session-only member. /vip native status reports configured and captured settings.
5. /vip native arm, or Enable native VIP in the UI. This SAVES enabled=true per profile. No observer prerequisite exists.
   Check one marker; Honorific titles/quotes/animation/formatting/placement survive.
   Have a paired player change their title and verify the latest arrives. Verify on
   another client without this plugin's marker that the VIP stays local-only.
6. Native title prefix places colored VIP BEFORE existing title text, inside existing quotes. It does NOT force above/below-name placement; the original title source retains that control.
   To test it, complete the cleanup below, select Native title prefix and arm explicitly.
6. /vip native prepare-unload pauses/cleans WITHOUT erasing enabled preference. WAIT for cleanup acknowledgement, check /vip native status
   says controlled unload ready=True, then VISUALLY verify the marker is removed.
7. Only after cleanup, change placement to Native FC-tag suffix. Change color if
   wanted, then arm again. Expect the ENTIRE original FC tag, then colored VIP in
   the same native FC row. A real FC named VIP is preserved. Hidden/absent native
   fields may suppress the marker; visibility is not forced.
8. Repeat cleanup before changing placement/color/profile or unloading. UI controls
   are disabled until cleanup. External profile, sheet, enabled/tag, placement or RGB
   changes automatically disarm but NEVER auto-rearm. Captured old field/color are
   kept until cleanup completes. Floating drawing is suppressed until native cleanup
   is acknowledged, so a conflict cannot silently enable both tag renderers.
9. Exercise a persistence reload: with normal sheet membership and native enabled, run /vip native prepare-unload; wait acknowledgement, ready status and visually gone; disable then re-enable the same plugin. The saved native mode/color should resume automatically after the sheet loads, without manual arm or session add.
   /vip native disarm or the Turn off button SAVES enabled=false intent (NativeVipEnabled=false); later reloads remain off. prepare-unload is a session pause; it does not auto-rearm that same instance.
10. Exercise remove/add, native visibility, wall/angle/distance, UI scale/long titles,
   zoning, range exit/reentry, profile switches, and reloading. /vip native remove
   Full Name removes only session membership; sheet membership still applies.

/vip native clear clears session names only. /vip list remains the legacy range-list
toggle, not a membership enumeration. /vip native observe is optional bounded
read-only TITLE-field pointer observation, not FC or lifetime certification.

## Safety/limitations
Writes occur only through supported pre-native Title/FreeCompanyTag weak setters,
which copy into UI-owned storage. Original bytes stay intact. No Honorific IPC,
upstream source, shared title/FC or network state is changed. Scoped color wraps only
new VIP text. Original edge/glow can affect it. Terminal suffix VIP/vip/[VIP] or existing leading VIP/[VIP] in prefix mode is not
duplicated/recolored; a natural FC VIP remains untouched with a separate local marker.

Cleanup exactly removes owned text AND color macros, preserving the latest upstream
source. Ambiguous competing writes fail closed: conflicts are not called clean and
no text/payload is guessed. Allow a proven native source refresh and retry disarm.
Unexpected unload/crash may leave displayed text until source refresh; Dispose does
not perform late writes or free UI-owned buffers. Acknowledgement proves backing
field state, not visible native redraw, so visual checks remain mandatory.

Membership remains name-only, not home-world scoped. The simple original CSV split
semantics are unchanged. Fetch completion/refresh is now marshalled to the framework,
stale profile/sheet replies are rejected and disposal cancels pending requests.

## Developer verification
From the repository (Release only; Debug retains the original dev-output workflow):
  dotnet build VipNameChecker.csproj -c Release --no-restore -p:OutputPath=C:\Users\mmacc\AppData\Local\Temp\VIP-Manager-integrated-testing-final-20261118\
  dotnet run --project tests\NativeTitleTests.csproj -c Release --no-restore --no-launch-profile
  .\Verify-PreUpdateWrites.ps1 -AssemblyPath C:\Users\mmacc\AppData\Local\Temp\VIP-Manager-integrated-testing-final-20261118\VipNameChecker.dll

Offline checks do not establish real native visuals, wall occlusion, live sync or
successful cleanup in game. Independent review and actual test results must be
reported separately. Do not describe this as a production-validated release.
## Persisted enabled-state safety (1.3.0.3)
NativeVipEnabled is per profile and defaults false for old/migrated/new profiles.
Successful arm saves true; explicit disarm saves false, even when already clean.
prepare-unload / Pause for reload keeps the flag, cleans the current field and
blocks automatic rearming in this instance. A new instance resumes only a remembered
native mode with master/tag enabled and a clean ownership boundary. Sheet data must
match active profile+sheet identity before any sheet member qualifies. Session-only
add is still session-only and not saved.

Settings changes, competing writes and observing pause the current instance without
silently erasing enabled intent. There are no automatic same-session retry loops.
Abrupt unload can leave old display bytes; a new instance never claims a field
unless its current bytes equal the genuine upstream source, so it cannot adopt a
stale marker. Safe prepare-unload+acknowledgement+visual verification remains the
recommended upgrade/reload path. Ordinary Dispose does not change saved settings.

Added offline integration tests execute the actual native runtime with no-op service
proxies and ZERO native handlers, simulate independent reloads and matching/stale
sheet snapshots, and intercept only config saves. They perform no game/network/file
settings writes. This is lifecycle/sheet-gating evidence, not a real HTTP or visual
in-game reload result.
A normal FULL game exit/restart does not require prepare-unload: the entire UI dies with the process, and remembered enabled profiles resume next launch. prepare-unload is for in-process plugin disable/update/hot reload when the UI remains alive.

## Open linked Google Sheet button (1.3.0.3)
In /vip, beside Reload VIP List, Open linked Google Sheet opens the active profile's
sheet in the default browser. Missing or invalid IDs disable the button and show an
explanation. IDs are trimmed and strictly limited to 20-128 ASCII letters, digits,
underscores or hyphens; pasted full URLs, path/query characters and controls are
rejected. The destination is always https://docs.google.com/spreadsheets/d/ID/edit.
This is normal navigation only: it does not request scopes, change sharing, fetch
credentials or edit the sheet. Google controls the browser user's permissions.
Browser-launch errors produce a chat/log error, not fallback shell commands.
Offline tests only verify URL construction and profile isolation; no browser is
opened by tests. A manual click/default-browser check remains separate.