# 10 — Applications and UI Workflows

Scope: the legacy SmartSet applications, each main window's structure, and the concrete user workflows (v-Drive scan, key remap, macro record, save/load, import/export, demo mode).

## App inventory

| App | Executable title | Devices served | Notes |
|---|---|---|---|
| SmartSetAdv2 | `'Adv2 SmartSet App'`, app name `'SmartSet App for Advantage2'` | Advantage2 | runs standalone; also embedded in the office dashboard |
| SmartSetFSEdgePro | `'SmartSet App-Freestyle'` | Freestyle Edge **and** Freestyle Pro (model auto-detected from the device's version file) | runs standalone; also embedded in the office dashboard |
| SmartSetRGB | `'SmartSet App-Freestyle'` | Freestyle Edge RGB | runs standalone; also hosted by the gaming dashboard |
| SmartSetTKO | `'SmartSetTKO'` | TKO | not launched standalone; hosted by the gaming dashboard |
| SmartSetMaster | gaming dashboard | Freestyle Edge RGB, TKO (hosts their editor windows) | |
| SmartSetMasterOffice | office dashboard | Advantage 360 SmartSet, Advantage2 | embeds the Advantage2, Freestyle, Advantage 360, and Savant Elite 2 editors |
| SmartSetSavantElite | SE2 config app | Savant Elite 2 pedals | covered by a separate document |

Every editor main window supports the same embedding contract: it initializes either standalone or hosted by a dashboard; when hosted it runs maximized inside the dashboard's central content panel.

## Shared UI foundation

### Custom window chrome

Every window is borderless with a hand-built title bar: dragging the title panel moves the window, and minimize/maximize/close are custom buttons. "Maximize" is custom behavior — it toggles between a fixed normal size (dashboards: 1550×850 gaming / 1550×830 office) and the primary monitor's work area, with the app tracking its own normal/maximized state. Title-bar dragging is disabled when an editor is hosted inside a dashboard.

All dialog forms share a common ancestor providing the title panel, decorative border lines, and a close button; in the office app's light theme the close button swaps to a light variant.

### Dialogs and notifications

A custom modal message box replaces the OS message dialog throughout the apps. It supports injected custom buttons (caption, width, click action) and an optional "Hide this notification?" checkbox; when the checkbox is checked, the dialog's return value is offset by 100, which callers persist as a per-message "don't show again" preference in the app settings. A separate transient, non-modal notice window shows progress-style messages (e.g. eject progress). Whether a given notification appears combines its per-message flag with global "show all notifications" / "hide all notifications" preferences, which the master settings dialog stores in the registry under `HKCU\SOFTWARE\KINESIS` — values `ShowAllNotifs`/`HideAllNotifs` (gaming) and `ShowAllNotifsOffice`/`HideAllNotifsOffice` (office).

### Shared feature dialogs

| Dialog | Used for |
|---|---|
| Tap and Hold | tap/hold dual-action editor; its edit boxes receive keystrokes captured by the app's keyboard-capture mechanism |
| Macro Timing Delays (plus a Freestyle-specific variant) | insert 1–999 ms or random delays into macros |
| Multimodifiers | Adv360 multi-modifier (Hyper/Meh-style) combos |
| Search Keys | searchable list of every assignable action (Adv360 Search button) |
| Select Macro | pick an existing macro |
| Invalid Lines | review/keep/discard unparseable layout-file lines (Adv360) |
| Export | export the layout file, lighting file, or both |
| Firmware | "Check for Updates": compares local firmware/app versions against JSON fetched from the vendor endpoint `…/wp-json/ksv/v1/get_versions` |
| Diagnostics | diagnostic info dialog |
| Troubleshoot | "Keyboard not detected" dialog whose outcomes are Scan for v-Drive or Demo mode |
| Intro / Loading / About | first-run intro, loading splash, help/about |

These dialogs are specified in detail in document 11.

### Fonts, colors, theming

- Windows builds embed the "Quantify" brand font as a binary resource and load it into the process at startup.
- Per-OS base fonts: `Segoe UI` on Windows, `Tahoma`/`Tahoma Bold` on macOS; Unicode key captions use `Cambria Math`.
- The app detects the OS dark theme and switches palettes accordingly. Brand colors include Kinesis blue `RGB(0,114,206)`, an Edge-specific blue, and office green `RGB(105,199,157)`. Gaming apps are always dark; office apps honor the OS theme.

### Keyboard-input capture (remap & macro recording)

This is the core input mechanism, present in every editor:

- **Windows**: a thread-local low-level keyboard hook (`WH_KEYBOARD`) intercepts every keystroke while the app has focus. The hook decodes the key-up/key-down transition, the extended-key bit, and the scan code, and from these distinguishes **left vs right modifiers** (Ctrl/Alt via the extended bit; Shift via scan-code mapping). Keypad Enter is mapped to a distinct internal code, and Print Screen is only recognized on key-up. Non-modifier key-downs are recorded as the captured key (together with the currently held modifiers) and the keystroke is **swallowed so the OS never sees it**. Held modifiers are accumulated/released as their keys go down and up. Capture is suspended (keystrokes pass through normally) whenever a dialog that needs real typing is open (e.g. Save As).
- **macOS**: no hook; the main form previews all key events, normalizes generic modifiers to their left variants (macOS does not report a generic-modifier distinction usable here), and consumes the event.
- **Routing**: a captured key is forwarded to the Tap and Hold dialog if that dialog is open; otherwise it is applied as a remap, or appended to the active macro when the macro-entry box is focused (some editors track an explicit macro-mode flag instead of control focus).
- **Master apps**: when an editor is hosted in a dashboard, the dashboard window has focus, so its key events are delegated to whichever hosted editor is visible.

A rebuild must reproduce this contract: capture physical keystrokes including the left/right modifier distinction, swallow them from the OS while recording, and suspend capture while text-entry dialogs are open.

### Startup / v-Drive scan / demo mode (common pattern)

Every editor's startup sequence is: locate the v-Drive base directory, then initialize. Initialization = (demo mode or v-Drive check passes) → load config keys, the layer list, keyboard state settings (`kbd_settings.txt`/`state.txt`), app settings (`app_settings.txt`), and the current layout (plus the LED file where applicable), then start a polling idle timer that re-verifies the device's version file every tick and drives a green `v-Drive OK` / red `v-Drive Error` status indicator.

On failure in desktop mode (v-Drive folders not found next to the executable), the app shows the "Keyboard not detected" troubleshoot dialog whose outcomes are **Scan for v-Drive** (re-runs directory detection and initialization) or **Demo Mode**. When an editor is launched from a dashboard, demo mode is set automatically if the device is not connected or its drive is not writable. Demo mode disables Save/Save As/Load/New, settings controls, import/export, firmware and eject buttons, and shows a "Demo Mode" label. If a save/load is attempted after the drive has vanished, a "Keyboard Connection Lost" dialog offers "Scan for v-Drive" and quotes the device's onboard shortcut (Adv2: "Program + F1"; Adv360: "SmartSet + v-Drive").

Ejecting (Windows only): the app dismounts the volume, showing "Disconnecting v-Drive" then "Safe To Remove Hardware", or on failure "Cannot eject v-Drive — Close all open files and folders on the v-Drive, and try ejecting again."

## SmartSetAdv2 — Advantage2 editor

**Window structure**: custom title panel (logo, min/max/close, help); left "Menu" column with buttons **New / Load / Save**; central keyboard image built from ~90 per-key buttons plus a **Pedals** group (left/middle/right pedal buttons) and a **Thumb Keys** group; a "Displaying: layout file" label; a layer toggle switch (top layer ↔ embedded keypad layer); a bottom **Macro** panel (macro text box, 3 macro slots selected by radio buttons, six co-trigger buttons — Left/Right Shift, Ctrl, Alt — per-macro playback-speed slider, Special Actions, Backspace/Clear/Copy/Paste, Done/Cancel); a bottom **Settings** section (global macro speed slider 0–9, status-report speed slider, switches for key clicks, key tones, and auto v-Drive); status labels for v-Drive OK / v-Drive Error / Demo Mode; and counters "Remap (n)" / "Macro (n)".

**Remap workflow**: click an on-screen key — the key enters "listening" state; the next physical keypress captured by the app becomes the new assignment. Alternatively, right-click the key or use **Special Actions** to open a popup menu of tokens (function keys F13–F24, keypad actions, multimedia, mouse clicks, Windows/Mac shortcuts, delays, tap & hold, Hyper/Meh). **Done** validates and marks the layout modified; **Cancel** reverts; **Reset Key / Reset Layer / Reset Layout** clear at increasing scope. First run shows an intro dialog: "To program, first select a key by clicking on the keyboard image…".

**Macro record workflow**: select the trigger key, then click the macro box to focus it; while it is focused, captured keys append to the active macro instead of remapping. Up to 3 macros per key are selected by the radio buttons, each optionally requiring co-trigger modifier(s) chosen with the six co-trigger buttons. Macros cannot be assigned to modifier keys. **Done** finalizes the macro and shows "Macro assigned to <co-triggers + key>" (plus "in the embedded layer" when editing the bottom layer). Copy/Paste transfer a macro between keys via an internal clipboard.

**File workflows**:
- **Load**: a standard file-open dialog rooted at `<v-Drive>\active`; the chosen file name must contain `qwerty` or `dvorak` — this is also how the QWERTY/Dvorak base layout is switched.
- **New**: a custom dialog with a QWERTY/Dvorak radio, a position combo (a–z or 0–9), and a "load after save" checkbox; produces `<name>_qwerty.txt`/`<name>_dvorak.txt` and optionally sets it as the startup file.
- **Save**: serializes the layout in the Advantage2 text format, writes the current file, then writes `state.txt` with the keyboard state settings; the success dialog instructs the user to exit the app and eject the drive before closing the v-Drive. Closing with unsaved changes prompts "Do you want to save changes?".

## SmartSetFSEdgePro — Freestyle Edge / Pro editor

**Window structure**: same skeleton as Adv2 with ~95 key buttons drawn over a keyboard backdrop image; **File** menu buttons **Load / Save / Save As**; layer switch (top ↔ Fn layer); macro panel with 3 slots, co-triggers, per-macro playback speed (1–9 plus Global) and a **repeat/multiplay** slider (x1–x9); settings section with the global macro speed and status-report sliders and (Edge only) a **Game Mode** switch; Edge-only **Lighting** mini-panel: brightness knob (0–10) and Pitch Black / Breathe LED shortcuts. Branding assets are swapped per model (Edge/Pro logos).

**Model-dependent behavior**: the app re-reads the device's version file on every connectivity check and resets the active model (Edge vs Pro) from it; Pro hides all lighting and game-mode UI; the special-action menus differ (LED submenu is Edge-only). Help/tutorial/manual links are chosen per model.

**File workflows**: **Load** uses a custom dialog listing layout positions 1–9 (validation message: "You must select a layout position 1 to 9"); if the chosen `layout<n>.txt` doesn't exist yet, it is created empty. **Save As** offers positions 1–9 or a free-named backup file; save messages explain the on-keyboard refresh shortcuts: "…use the Refresh Shortcut (SmartSet + Layout) or simply close the v-Drive (SmartSet + F8). To load this layout to the keyboard press SmartSet + <n>." **Save** validates macro capacity first (maximum macro count per firmware version, and 7200 total keystrokes).

**Alternate layouts**: special-action menu items generate Dvorak or Colemak onto the top layer, the Fn layer, or both, and can place a numeric keypad on the left or right half of the Fn layer.

## SmartSetRGB — Freestyle Edge RGB editor (structure & workflows)

(Lighting semantics are documented elsewhere; this covers app structure.)

**Window structure**: full-window dark UI over a background image with three top areas — a **profile button** with a popup listing `PROFILE 1..9`, tab-like panels **Layout** / **Lighting** that switch the app between layout-editing and lighting-editing modes, and a toolbar of buttons: **Save, Save As, Import, Export, Settings, Firmware, Diagnostic, Eject** (Eject hidden on macOS). The keyboard is rendered as key buttons over per-mode keyboard images; a left action menu exposes remap categories and lighting effects; rotary knob controls (effect speed, multiplay, macro speed) share a bitmap knob rendering.

**Workflows**: the profile popup selects and loads the paired `layout<n>.txt` + `led<n>.txt`; **Save As** shows the same popup in save-as mode and writes both files; **Save** writes the layout file then the LED file and ejects the drive; **Import** loads an external `.txt` (maximum 50 KB) and auto-detects whether it is layout or LED content; **Export** copies the current layout/lighting files out via the shared Export dialog. Macro editing uses an in-window macro editor panel (opened/closed in place) with the same keystroke-capture mechanism. A layer switch toggles the top/Fn layer.

## SmartSetTKO — TKO editor

Structurally a copy of the RGB app with a third tab: **Layout / Key Lighting / Edge Lighting**, separate key-button and edge-button collections that show/hide per tab, lighting zone buttons (zones: all, number row, WASD, function row, game keys, arrows, modifiers, …), and animated effect previews driven by timers (breathe, wave, spectrum, and frame-sequence effects) rendered onto the on-screen keyboard. Profiles 1–9, the Save/Save As/Import/Export/Settings/Firmware/Diagnostic/Eject toolbar, the macro editor, and the troubleshoot/scan flows are identical in shape to RGB. Loading additionally splits edge-lighting lines out of the LED file.

## SmartSetAdv360 — Advantage 360 editor (office)

**Window structure**: light-themed (office palette; OS dark theme honored). Top bar: a **PROFILE n** button plus a profile list (entries map to `layout1..9.txt`/`led1..9.txt`; clicking an entry either selects/loads or saves-as, depending on whether the list was opened from the profile button or from Save As), tabs **Layout** / **Lighting**, and a toolbar: **Save, Save As, Import, Export, Firmware, Diagnostic, Help** (Eject hidden on macOS).

**Multi-layer editing**: five layer buttons — Base, Keypad, Fn1, Fn2, Fn3; the whole key grid reloads when the active layer changes. The left menu is a categorized action palette: Letters, Numbers, Nav Keys, Punctuation, Modifiers, Multimodifiers, Multimedia, Mouse Clicks, Function Keys, Special Actions, Layer Shifting, Layer Toggling, Numeric Keypad, Alt Layouts, Tap and Hold, Quick Thumb Keys, and Macro — each backed by a popup menu of the category's actions.

**Alt layouts**: choosing Dvorak/Colemak/Workman opens a layer-picker dialog with checkboxes for base/keypad/fn1/fn2/fn3 (at least one must be selected) and the prompt "Which layers would you like to implement the <name> layout?"; the generator then rewrites only the selected layers.

**Macro repository**: macro mode replaces per-key slots with a scrolling repository of all the profile's macros; selecting one loads it for editing, with add/copy/assign/reset-all controls, per-layer trigger-restriction buttons, a repeat/multiplay checkbox + slider, and a speed slider. The editor tracks whether it is recording a new macro, editing an existing one, or re-selecting a trigger.

**Profiles & saving**: **Save** writes the layout file then the LED file, ejects the device, and tells the user either "Use the Refresh Shortcut (SmartSet + 'Refresh')…" (when editing the startup profile) or "To load Profile n…, hold the SmartSet key and tap the n key."; **Save As** retargets the current work to `layout<n>.txt`/`led<n>.txt`. **Import** auto-detects layout vs LED files, maximum 50 KB; both Import and Export are disabled on Profile 0. Invalid layout-file lines are surfaced after load via the Invalid Lines dialog. Intro dialogs describe the app ("…custom the layout for each of the 9 Profiles, configure the 6 RGB indicator LEDs…") and warn about Profile 0. A firmware-age check runs at startup, gated by a "don't show again" preference.

**Lighting tab**: six LED-indicator buttons (3 left / 3 right) with a per-indicator function popup and color pickers (ring picker plus 12 custom color swatches persisted in `app_settings.txt` as `CustColor1..12`); details are covered in the lighting document.

## SmartSetMaster / SmartSetMasterOffice — dashboard apps

**Structure**: borderless full window (1550×850 gaming / 1550×830 office), top bar with Home / Settings / Help labels and min/max/close; a row of device "cards", each card showing the device name (brand font), connection status, startup profile, and three buttons: **Check for Updates / Scan for v-Drive**, **Eject**, **Watch Tutorial**, plus a main **Configure / Demo Mode** button.

**Device registry**: the card list is fixed per dashboard, maximum two devices. Gaming: `FREESTYLE EDGE RGB` and `TKO`. Office: `ADVANTAGE 360 SmartSet` and `ADVANTAGE 2`.

**Detection loop**: a timer periodically re-queries each device, painting status `'Connected'` (lime), `'Not Detected'` (red), `'Cannot Access'` (red; drive found but not writable), or `'Coming Soon'` for future-device placeholders. Connected cards show the startup profile ("Profile n"; the office dashboard shows "Layout <name>" for the Advantage2) and swap the scan button to "Check for Updates".

**Opening a device**: the Configure button sets demo mode from the device's connected/writable state, records the active device, shows a loading splash, embeds the device's editor window into the dashboard's content panel, and initializes it (on Windows the editor forms are pre-created shortly after startup; on macOS they are created on demand). **Home** closes the active editor, ejects the v-Drive when not in demo mode, and resumes the detection loop.

**Scan for v-Drive dialog**: a modal loop — while the device is still disconnected and the user keeps pressing "Scan", detection re-runs; the dialog shows the per-device onboard shortcut hint and a Troubleshooting Tips link. When the device is connected, the same card button opens the shared firmware-update dialog instead.

**Settings/Help**: Settings opens the notification-preferences dialog (registry-backed, see above); Help opens the About/help form. The office dashboard additionally has two Advantage 360 Pro buttons linking to the ZMK web configurator and its support page.
