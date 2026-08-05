# 02 — Device Catalog

Scope: every Kinesis device known to the legacy SmartSet applications — its identifiers, the app that serves it, layer/macro/lighting capabilities, and per-device constraints.

## Device identity model

The legacy app performs no USB/HID enumeration. A "device" is a FAT mass-storage volume (the "v-Drive") that the keyboard exposes; detection is done by scanning drive letters (Windows) or `/VOLUMES/<name>` (macOS) for a volume whose **label** matches a known drive name and which contains the expected firmware folder + version file.

(Legacy data model, for reference.) Each device is represented at runtime by a device record with these properties: device name, numeric legacy app ID, v-Drive name, connected flag, read/write-access flag, drive letter, root folder, version folder/file, settings folder/file, future-device flag, programmable flag, tutorial URL, and the hint text for the on-board v-Drive shortcut. Defaults: version file `version.txt`, settings file `kbd_settings.txt`, version folder `firmware`, settings folder `settings`. The master apps hold these records in a plain device list.

Each device has a numeric legacy app ID (used in the table below): pedal 0, Advantage2 1, FS Edge 2, FS Pro 3, FS Edge RGB 4, CROSSFIRE keypad 5, TKO 6, Advantage 360 7, Advantage 360 Pro 8. Two container ("master") apps exist: SmartSet Master (gaming — RGB, TKO) and SmartSet Master Office (office — Advantage2, Advantage 360, Savant Elite 2).

Only the Advantage 360 is treated as a "Gen2" device; all others are Gen1.

## Master device table

| Device | Legacy app ID | Serving app | v-Drive volume label(s) | Firmware/version location | Layers | Layout/profile files | Macro support | Lighting | Special hardware |
|---|---|---|---|---|---|---|---|---|---|
| Savant Elite 2 foot pedal | 0 | SmartSet Savant Elite 2 app; also embedded in the office master app | `SE2`, `KINESIS FP` | folder `active`, file `version.txt` | n/a | `active/pedals.txt` | pedal macros | none | 3 pedals + jack; covered by a separate document |
| Advantage2 | 1 | SmartSet Advantage2 app; embedded in the office master app | `ADVANTAGE2`, `KINESIS KB`, `ADV2` | folder `active`, file `version.txt`; state file `active/state.txt` | 2 (top layer + embedded keypad layer) | named files in `active\`, suffixed `_qwerty.txt` / `_dvorak.txt`, position a–z or 0–9 | 3 macros per trigger key, up to 3 co-triggers each; playback speed 0–9 | none | full keyset incl. thumb clusters; 3 foot-pedal inputs (left/middle/right pedal) |
| Freestyle Edge | 2 | SmartSet FS Edge/Pro app — one binary serves Edge and Pro | `FS EDGE` | folder `firmware`, file `version.txt` | 2 (top + Fn layer) | `layouts\layout1.txt` … `layout9.txt` + free-named backup files; lighting `lighting\led1..9.txt` | 24 macros (fw < 1.0.340) or 100 (fw ≥ 1.0.340); 300 chars per macro; 7200 total | blue backlight: brightness knob, Pitch Black and Breathe modes | split keyboard, LED backlight, Game Mode switch |
| Freestyle Pro | 3 | same binary as Edge; model chosen at run time from the model name in `version.txt` (`FS PRO`) | `FS PRO` | folder `firmware`, file `version.txt` | 2 (top + Fn layer) | same 9-position scheme as Edge | same as Edge | none (lighting panel hidden) | split keyboard; no game mode |
| Freestyle Edge RGB | 4 | SmartSet RGB app; also hosted in the gaming master app | volume label `FS EDGE RGB` | folder `firmware`, file `version.txt` | 2 (top + Fn layer) | profiles 1–9: `layouts\layout<n>.txt` + `lighting\led<n>.txt` | 100 macros, 7200 keystrokes | per-key RGB, effect modes (mono/breathe/spectrum/wave/reactive/ripple/fireball/starlight/rebound/loop/pulse/rain/pitch-black) | split gaming keyboard; v-Drive opened with onboard `SmartSet + F8` |
| CROSSFIRE Keypad | 5 | none — the device was never supported by any SmartSet app | `CROSSFIRE KEYPAD` (reserved) | — | — | — | — | — | never shipped |
| TKO | 6 | hosted in the gaming master app | `TKO` | folder `firmware`, file `version.txt` | 2 (top + Fn layer) | profiles 1–9: `layout<n>.txt` + `led<n>.txt` | 100 macros / 7200 keystrokes (same limits as RGB) | per-key RGB **plus** 33-LED edge lighting (9 left, 15 bottom, 9 right); third config tab for edge lighting | 60% tenkeyless gaming board with tripartite space bar (left/middle/right space); v-Drive opened with `SmartSet + Right Shift + V` |
| Advantage 360 (SmartSet) | 7 | hosted in the office master app | `ADV360` | folder `settings`, file `settings.txt` doubles as version file | 5: base, keypad, fn1, fn2, fn3 | profiles 1–9: `layouts\layout<n>.txt` + `lighting\led<n>.txt`; Profile 0 is read-only | 100 macros, 7200 total, 500 chars per macro | 6 RGB indicator LEDs (3 per side) mappable to Caps/Num/Scroll/Profile/Layer/NKRO/Battery states | split ergonomic contoured keyboard; optional foot pedal display; Bluetooth (battery LED token `[batt]`) |
| Advantage360 Professional (ZMK) | 8 | not programmable by SmartSet; the office dashboard shows "Access Website" → `https://kinesiscorporation.github.io/Adv360-Pro-GUI/` and help → `https://kinesis-ergo.com/support/advantage360-pro` | — | — | — | — | — | — | configured via ZMK web GUI |

## Common v-Drive folder layout

| Path on v-Drive | Purpose |
|---|---|
| `layouts\` | layout files (FS Edge/Pro, RGB, TKO, Adv360) |
| `lighting\` | LED files |
| `settings\` | settings files |
| `firmware\version.txt` | firmware version + model name (FS/RGB/TKO) |
| `active\` | Adv2 & pedal firmware/layout folder |
| `settings\settings.txt` | Adv360 version + settings |
| `debug.on`, `debug_firm.on`, `devmode.on` | presence of these files at the drive root enables debug/dev modes |

Other fixed file names: keyboard settings `kbd_settings.txt`, Advantage 360 settings `settings.txt`, app settings `app_settings.txt`, Advantage2 state file `state.txt`.

## Per-device deep dives

### Advantage2

- **Base layouts**: QWERTY and Dvorak are distinct layout-file families. The layout-file name is inspected for `qwerty`/`dvorak` and the matching key map is loaded; files matching neither are rejected with "The file you have chosen is not a layout file...".
- **Layers**: exactly two, toggled by a switch: top layer and "embedded" keypad layer.
- **State settings** (from `active\state.txt`): startup layout file, macro playback speed (0–9), status-report play speed, key-click tone, toggle tone, "v-Drive open at startup" flag.
- **2MB vs 4MB models**: settings editing is disabled on Advantage2 unless `version.txt` contains the marker `4MB`. On 2MB keyboards all settings controls carry the hint `'Modifying Settings is not supported on 2MB keyboards'`.
- **Tap & Hold**: requires firmware ≥ 1.0.516; max 10 per layout; forbidden on macro trigger keys, on the same key in both layers, and on A–Z/0–9 of the top layer; timing delay default 250 ms.
- **Macros**: 3 macros per key, each with up to 3 co-trigger modifiers; a key that is a modifier cannot take a macro ("You cannot assign a macro to a modifier key"). Macro length is limited to approximately 300 characters per macro ("Macros are limited to approximately 300 characters.").
- **Onboard shortcut to open v-Drive**: `Program + F1`.

### Freestyle Edge / Freestyle Pro

- **One app, two devices**: the model is determined at run time from the `model name` line of `firmware\version.txt` — `FS PRO` selects Freestyle Pro; anything else selects Freestyle Edge. Drive detection accepts both `FS EDGE` and `FS PRO` labels regardless of which model is active.
- **Edge-only features** (hidden on Freestyle Pro): lighting section (brightness knob 0–10, LED indicators, Pitch Black and Breathe quick modes), Game Mode switch, and the LED special-action key.
- **Look**: Pro uses the Kinesis blue theme and supports light/dark theme; Edge is always dark.
- **Firmware-gated capacity**: fw ≥ 1.0.340 → 100 macros, otherwise 24. Per-macro limit 300 keystrokes, per-layout total 7200. Tap & Hold requires fw ≥ 1.0.480.
- **Layout positions**: 9 numbered layouts (1–9), loaded on-keyboard with `SmartSet + <n>`; free-named "backup" layouts can be saved but must be assigned to positions 1–9 to be loadable.
- **Alternate layouts**: Dvorak and Colemak generators for top layer, Fn layer, or both; Fn-layer numeric keypad left or right.
- **Special key actions**: Fn Toggle and Fn Shift, 10 hotkeys (HK1–HK10), split-space left/right, Mac left-Command.

### Freestyle Edge RGB

- Available as a standalone app and also hosted in the gaming master app; when hosted, the dashboard activates the device and initializes the RGB editor.
- **Profiles**: 9, chosen from a "PROFILE 1..9" popup; each profile is the pair `layout<n>.txt` + `led<n>.txt`. The startup profile number comes from `kbd_settings.txt`.
- **Config modes**: layout and lighting tabs; lighting details are documented separately.
- **Saving ejects**: after a successful save the app ejects/flushes the v-Drive (same behavior on TKO and Adv360).
- Onboard shortcut: `SmartSet + F8` opens the v-Drive.

### TKO

- Same architecture as RGB (profiles 1–9, layout+led file pair, 100 macros/7200 keystrokes) plus a third editor tab for the case-edge LEDs; edge LED data is stored in the same led file and split out from the per-key data on load.
- Edge LED map: 9 left, 15 bottom, 9 right (33 LEDs total).
- Split spacebar: distinct left/middle/right space keys.
- Onboard shortcut: `SmartSet + Right Shift + V` opens the v-Drive.
- Served through the gaming master app.

### Advantage 360 SmartSet

- Only Gen2 device. `version.txt` does not exist; `settings\settings.txt` is used for both version info and keyboard settings.
- **5 layers** selected by buttons (base/keypad/fn1/fn2/fn3). Layer-shift and layer-toggle key actions exist per layer. Layout files tag layers with `<base>`, `<keypad>`, `<function1>`..`<function3>`.
- **Profiles 0–9**: profile 0 is factory/non-programmable — the app warns "Profile 0 is non-programmable so you must use the Save As Button..." and disables Import/Export for it. On-keyboard load: `SmartSet + <n>`.
- **LED indicators, not per-key lighting**: 6 indicator LEDs, each assignable a function token (caps `[caps]`, num `[nmlk]`, scroll `[sclk]`, profile `[prof]`, layer `[layer]`, NKRO `[nkro]`, battery `[batt]`) with per-layer colors for the layer indicator.
- **Macros**: macro repository UI (list of all macros in the profile) instead of per-key radio buttons; limits 100 macros, 500 chars each.
- **Quick Thumb Keys**: one-click "Mac Mode" / "Linux Mode" thumb-cluster presets, plus Freestyle2/Freestyle Pro hotkey emulation presets.
- Onboard shortcut: `SmartSet + v-Drive` (hotkey legend on the board) opens the v-Drive.

### Firmware update checking

A shared firmware-check dialog is used by the master dashboards and the RGB/TKO/Adv360 editors. It fetches current version metadata as JSON from `https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions` (gaming) or `https://kinesis-ergo.com/wp-json/ksv/v1/get_versions` (office), compares keyboard firmware, lighting firmware, and app version, and opens the appropriate `…#firmware` support URL per device. For the Advantage 360 the lighting-firmware row is hidden (no lighting firmware on that device).
