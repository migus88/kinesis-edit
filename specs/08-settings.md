# 08 — Settings: Keyboard Settings Files, App Settings, and Settings Dialogs

Scope: every setting key read/written by the apps — device-side keyboard settings, app-side notification/color settings, master-app registry settings — plus which UI exposes what, allowed values, and defaults.

## 1. File format

All settings files are line-oriented `key=value` text files:

- Each line is lowercased, then matched by **prefix**; the value is everything after the key name plus one separator character. Matching is therefore case-insensitive and tolerant of unknown lines.
- Writing is read-modify-write: managed keys are replaced in place (located by case-insensitive substring search) or inserted/appended; unmanaged lines are preserved.
- Because matching is by prefix, keys that are prefixes of other keys must be disambiguated by the parser: `v_drive` is a prefix of `v_drive_open_on_startup`, and `cust_color_1`/`_2`/`_3` are prefixes of `cust_color_10/11/12` (see §3 for the custom-color rule).

Settings file location per device (see doc 03 §4): `settings/kbd_settings.txt` (RGB, TKO, FS Edge, FS Pro), `active/state.txt` (Advantage 2), `settings/settings.txt` (Advantage 360 — the same file also carries version-info keys, see doc 09).

## 2. Keyboard settings (device-side)

"Written by" = devices for which the app writes the key; parsing is common to all devices unless noted.

| Key (exact) | Values | Written by app for | Meaning / behavior |
|---|---|---|---|
| `startup_file` | `layout<N>.txt` (N = 1..9) | RGB, TKO | Layout profile loaded at keyboard power-on. Reading it also derives the active layout/lighting files: `layout<N>.txt` / `led<N>.txt`. |
| `profile` | integer `1..9` | Adv360 | Adv360 equivalent of `startup_file`; saved as a bare number (`profile=<N>`), parsed with the same file-number logic. |
| `led_mode` | RGB/TKO: `led<N>.txt`; FS Edge: `0`..`9` (brightness), `P` (pitch black), `B` (breathe) | RGB, TKO (led file name); FS Edge/Pro (mode string) | RGB/TKO: startup lighting file. FS Edge: LED brightness level 0–9 from the on-screen knob, or the special modes `P` (pitch black) and `B` (breathe). |
| `macro_speed` | integer; FS/Adv2 `0..9` (0 = playback disabled), RGB/TKO `1..9` (0 via "disable" checkbox) | RGB, TKO, FS Edge/Pro, Adv2 | Global macro playback speed. Ranges: minimum 0 for FS and Adv2, minimum 1 for RGB and Adv360, maximum 9 for all. Defaults: 0 for FS and Adv2, 5 for RGB. |
| `status_play_speed` | integer `0..4` (0 = disabled) | RGB, TKO, FS Edge/Pro, Adv2 | Speed at which the keyboard "plays" its status report (types it out). UI sliders are capped at 4. |
| `status` | integer `0..4` | Adv360 | Adv360 short key for the status play speed. |
| `v_drive` | `auto` / `manual` | RGB, TKO, FS Edge/Pro | Whether the v-Drive auto-mounts on startup. Parse: value `auto` → true; save writes `auto` or `manual`. |
| `v_drive_open_on_startup` | `ON` / `off` | Adv2 | Adv2 variant; parse: value `on` → true; save writes literally `ON` (true) or `off` (false). |
| `game_mode` | `ON` / `OFF` | RGB, TKO, FS Edge/Pro | Game mode (disables the Windows key etc. on the keyboard). Parse: `on` → true. |
| `lock` | `ON` / `OFF` | Adv360 only | Program lock — disables onboard programming. Parsed only for the Adv360. |
| `key_click_tone` | `ON` / `OFF` | Adv2 | Key click sound. |
| `toggle_tone` | `ON` / `OFF` | Adv2 | Toggle (special action) tone. |

Reserved / legacy keys: the following keys may appear in device settings files and should be treated as reserved. The legacy app reads `thumb_mode` (string), `macro_disable` (`on` → true), `power_user` (`true` → true), and `country` (string) into state but exposes no UI for them and never writes them. `led` is a reserved key name for brightness. `program_key_lock` (`ON`/`OFF`, e.g. `program_key_lock=OFF`) and `profile_sync_mode` (`ON`/`OFF`, e.g. `profile_sync_mode=ON`) appear in factory-shipped settings files; like all unrecognized lines they are preserved on save.

Notes:

- Booleans are written uppercase (`ON`/`OFF`) except `v_drive` (`auto`/`manual`) and the Adv2 v-Drive false value (`off`); parsing is case-insensitive.
- There are no explicit defaults for device-side settings; missing keys leave zero/false/empty values. Pitch-black and off lighting modes are runtime-only flags forced to `false` on each load — they are never persisted.
- On Adv2 the entire settings UI is disabled unless the firmware is the 4MB variant (see doc 09 §1.1).

## 3. App settings (`app_settings.txt`)

Stored as `app_settings.txt` in the `settings/` folder of the v-Drive. Never saved in demo mode.

All `*_msg` keys are booleans persisted as `on`/`off` and **`on` means "hide this notification"** — they are set to `on` when the user ticks "Hide this notification?" in the corresponding dialog. Default (missing key) is `off` = show. Display is additionally gated by the master-app globals (§4): a notification is shown when `(not hidden and not "hide all") or "show all"`.

| Key | Suppresses |
|---|---|
| `app_intro_msg` | Startup welcome/introduction dialog |
| `saveas_msg` | "Save As" informational dialog |
| `save_msg` | "Profile N Saved" / "Settings Saved" notifications |
| `multiplay_msg` | Multiplay macro notification |
| `speed_msg` | Macro speed notification |
| `copy_macro_msg` | "Macro copied. Now select a new trigger key…" |
| `reset_key_msg` | "Do you want to reset the current Macro?" confirmation |
| `app_checkfirm_msg` | Startup firmware-update reminder |
| `savelighting_msg` | Lighting-saved notification (RGB legacy UI) |
| `savesettings_msg` | Settings-saved notification |
| `windowscombo_msg` | "Windows Combination Active" macro-recording hint |
| `updownkeystroke_msg` | "Downstroke/Upstroke Active" half-keystroke hint |

Custom colors (RGB lighting pickers, also loaded by Adv360):

| Key | Value format | Default |
|---|---|---|
| `cust_color_1` … `cust_color_12` | `[R][G][B]` with decimal 0–255 components, e.g. `[255][0][128]` | Unset (no color). An unset color serializes to empty and the key is skipped on save. |

Parse rule: `cust_color_1`, `_2`, `_3` are matched with a trailing `=` so they do not swallow `cust_color_10/11/12`.

## 4. Master-app settings (registry)

The gaming/office master apps persist two global notification switches in the Windows registry under `HKEY_CURRENT_USER\SOFTWARE\KINESIS` (on macOS the legacy app persists the same values through a registry-emulation layer backed by a local file):

| Value name | App | Meaning |
|---|---|---|
| `HideAllNotifs` / `ShowAllNotifs` | SmartSet Gaming master | `'1'`/`'0'`; global hide-all / show-all notification flags |
| `HideAllNotifsOffice` / `ShowAllNotifsOffice` | SmartSet Office master | same |

Written on settings-dialog close; read at dashboard startup. "Hide all" and "Show all" are mutually exclusive checkboxes. "Show all" wins over every per-dialog hide flag (§3).

The legacy app persists no window size/position or other app-level preferences anywhere else.

## 5. Settings dialogs and embedded settings UI per app

### 5.1 RGB and TKO — modal Settings dialog

Opened from the main form's Settings button. Controls and ranges:

| Control | Setting written | Range / values |
|---|---|---|
| Active profile slider | `startup_file` (+ paired `led_mode` file) | 1–9 |
| Global speed slider + "disable" checkbox | `macro_speed` (checkbox → 0) | 1–9, disabled = 0 |
| Status report slider + "disable" checkbox | `status_play_speed` (checkbox → 0) | 1–4, disabled = 0 |
| Game mode toggle | `game_mode` | ON/OFF |

Save button → settings written, "Settings Saved — Changes will be implemented when v-Drive is closed." dialog, then the device is ejected. Closing with unsaved changes prompts "Do you want to save changes?".

### 5.2 Advantage 360 — modal dialog, title "Global Keyboard Settings"

| Control | Setting written | Range |
|---|---|---|
| Active profile slider | `profile` | up to 9 |
| Status report slider + "disable" checkbox | `status` (checkbox → 0) | 1–4 |
| Program lock toggle | `lock` | ON/OFF |

Same save + notify + eject flow.

### 5.3 FS Edge / FS Pro — settings embedded in the main form

No separate dialog. The main window contains: a macro speed slider (0–9), a status report slider (0–4), a game-mode switch (FS Edge only — hidden for FS Pro), and — FS Edge only — a lighting brightness knob (0–9) plus "Pitch Black" and "Breathe" LED buttons writing `led_mode`. Changes are saved to the settings file as they are made.

### 5.4 Advantage 2 — settings embedded in the main form

Main-window controls: macro speed slider (0–9), status report slider (0–4), key clicks switch (`key_click_tone`), key tones switch (`toggle_tone`), auto v-Drive switch (`v_drive_open_on_startup`). All of these are enabled only on 4MB firmware; on 2MB firmware they are disabled with an explanatory hint.

### 5.5 Master apps — dialog "SmartSet App Settings"

Two checkboxes only: "hide all notifications" and "show all notifications", persisted to the registry as in §4.

### 5.6 Savant Elite 2

The pedal app has no settings file UI; it edits `active/pedals.txt` directly (see doc 03 §4.4).
