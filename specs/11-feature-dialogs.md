# 11 — Feature Dialogs

Scope: the shared dialogs that implement the advanced programming features (tap-and-hold, multimodifiers, macro timing delays, macro/key pickers, export, diagnostics, troubleshooting) plus the small generic dialogs, and exactly how each dialog's result is written into the key data model and the layout text-file format.

All of these dialogs share the common dialog chrome (title bar, border, close button), receive the calling app's theme colors as parameters, and use the shared message box (section 11.9) for validation errors. Office-branded apps load a different OK/Cancel button image set; this is styling only.

---

## 11.1 Tap and Hold

**Purpose:** assign two actions to one physical key: a **Tap Action** (sent when the key is tapped and released faster than the delay) and a **Hold Action** (sent when the key is held longer than the delay), with a configurable timing delay in milliseconds. Dialog title: `'Assign Tap and Hold Action'`.

### Invocation

- Modal for Adv2 / FS Edge / FS Pro / RGB / TKO; **non-modal** (stay-on-top) for the Advantage360, which applies results via an accept callback instead of a modal result.
- Default delay when the key has no delay yet: **250 ms**, or **150 ms** on the Advantage360.
- While the dialog is open, the Adv360 main window disables feature buttons that would conflict with it (e.g. the Multimodifiers button).

### Controls

| Control | Label / hint | Behavior |
|---|---|---|
| Tap Action field | label `'Tap Action'`; hint `'Designate the action sent when the key is tapped and released faster than the delay'` | Captures the next physical keystroke via the app's keyboard-capture mechanism (on Windows the main window routes hook-captured keys into the open dialog; on macOS the field handles the key event directly). |
| Hold Action field | label `'Hold Action'`; hint `'Designate the action sent when the key is held longer than the delay'` | Same capture mechanism. |
| Delay field | label `'Delay (1-999ms)'`; hint `'Designate the time interval used to differentiate between the Tap and Hold actions'` | Numeric text; Up/Down arrow keys increment/decrement, clamped to 1–999. |
| Search buttons (one per action) | hint `'Search for tokens'` | Open the Search Keys dialog titled `'Search Keys (Tap Action)'` / `'Search Keys (Hold Action)'`. |
| Macro buttons (one per action) | `'Macro'` | Open the Select Macro dialog (titled for the corresponding action) to assign an existing macro's trigger as the Tap or Hold action; **visible only on the Advantage360 with keyboard firmware >= 1.0.69** (or in demo mode). |
| Note label | `'Tap action is not sent until key is released.'` | Static note. |
| Info label | `'Use your keyboard to input the desired actions or use the menu at left to select a special action'` | Visible only in non-modal (Adv360) mode. |
| Ok / Cancel | `'Ok'` / `'Cancel'` | Ok runs validation; Cancel closes. |

Dialog result: the tap action, the hold action, and the timing delay.

### Validation

| Condition | Error message |
|---|---|
| Timing delay outside 1–999 | `'Please select a timing delay between 1ms and 999ms.'` |
| No tap action selected | `'Please select a Tap Action'` |
| No hold action selected | `'Please select a Hold Action'` |

### Pre-dialog validation in the calling apps

Before opening the dialog, each app enforces (messages identical across Adv2/FS/RGB/TKO):

| Check | Message |
|---|---|
| Same key already tap-and-hold on the other layer | `'You cannot assign a Tap and Hold Action to the same key in both layers.'` |
| Maximum of 10 tap-and-hold actions per profile reached | `'You have reached the maximum number of Tap and Hold actions for this Profile.'` |
| Key is a macro trigger | `'You cannot assign a Tap and Hold Action to a macro trigger key.'` |
| Top layer and key is A–Z or 0–9 | `'You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.'` |

Firmware gating (the feature is refused with an update prompt otherwise — FS message: `'To utilize Tap and Hold Actions, please download and install the latest firmware.'` with an extra `'Update Firmware'` button):

| App | Minimum keyboard firmware |
|---|---|
| Advantage2 | 1.0.516 |
| FS Edge / FS Pro | 1.0.480 |
| FS Edge RGB | 1.0.1 |
| TKO | none |
| Advantage360 | none for the dialog; 1.0.69 for the macro-as-action buttons |

(Demo mode bypasses every firmware check.)

### Data model change

On accept, the target key stores the resolved tap action, hold action, and timing delay, and is flagged as tap-and-hold. Resetting the key clears both actions, zeroes the delay, and removes the flag; a plain remap of the key likewise clears its tap-and-hold configuration.

### File syntax

Tap-and-hold uses the token `t&h` and is serialized as a single-key (`[...]`) line:

```
[<position key>]>[<tap action>][t&h<delay ms>][<hold action>]
```

Example: `[caps]>[a][t&h250][lctrl]` — Caps Lock taps `a`, holds Left Ctrl, 250 ms delay.

Writers (all produce the same triple-bracket value):

| Format | Layer marking |
|---|---|
| Adv360/Gen2 format (layers delimited by the headers `<base>`, `<keypad>`, `<function1>`..`<function3>`) | none per line |
| FS-family format | prefix `fn ` before the line for the embedded (Fn) layer, e.g. `fn [caps]>[a][t&h250][lctrl]` |
| Advantage2 format | prefix `kp-` **inside** the first bracket for the keypad layer, e.g. `[kp-caps]>...` |

Parsing: a line is tap-and-hold when it is a single-key line (starts with `[`) and contains `[t&h`. The three bracketed values on the right are read in order tap action, delay, hold action; the delay is parsed from the text after `t&h`, falling back to 250 ms if unparseable.

---

## 11.2 Multimodifiers

**Purpose:** remap one key to emit several modifiers simultaneously. Dialog title: `'Multimodifiers'`; instruction label: `'Select one or more action to create a multi-modifier'`. **Advantage360 only.**

### Controls

Four toggle buttons: `'Control'`, `'Alt'`, `'Windows'`, `'Shift'`. On macOS the captions become `'Command'` (Windows button) and `'Option'` (Alt button). The dialog pre-checks buttons from the key's existing multimodifier code by testing for the letters `c`, `a`, `w`, `s`.

### Validation

At least two modifiers must be selected; otherwise the dialog rejects with: `'You must select at least 2 modifiers to create a multimodifier'`.

### Data model change and encoding

On OK the key's multimodifier value is written as a fixed-order 4-character code — one character per modifier in the order **Control, Alt, Windows, Shift**, using the modifier letter if selected or `x` as placeholder:

| Position | Selected | Not selected |
|---|---|---|
| 1 | `c` (Control) | `x` |
| 2 | `a` (Alt/Option) | `x` |
| 3 | `w` (Windows/Command) | `x` |
| 4 | `s` (Shift) | `x` |

Example: Ctrl+Shift = `cxxs`; Ctrl+Alt+Win+Shift = `caws`.

### File syntax

Serialized (Adv360 format only) as a single-key remap whose value is the 4-letter code:

```
[<position key>]>[<combo>]
```

Example line: `[caps]>[cxxs]`.

On load, a line is treated as a multimodifier when its value matches one of the **11 recognized combos**: `[caws]`, `[cawx]`, `[cxws]`, `[caxs]`, `[xaws]`, `[caxx]`, `[cxwx]`, `[cxxs]`, `[xawx]`, `[xaxs]`, `[xxws]` — i.e. every combination of 2, 3, or 4 of the four modifiers. The raw code is stored on the key, and the Adv360 key button displays the raw code as its caption.

---

## 11.3 Macro Timing Delays

**Purpose:** insert a delay token into a macro being edited. Dialog title: `'Macro Timing Delays'`.

Result: `0` for a random delay, `1..999` for a custom delay, or cancel/invalid.

### Controls

| Control | Caption | Behavior |
|---|---|---|
| Random radio | `'Random Delay (1-150ms)'` | Selecting returns the random-delay result. Typing in the custom field un-checks it. |
| Custom delay field | `'Custom Delay (1-999ms)'` | Numeric; Up/Down arrows increment/decrement, clamped to 1–999. |

Validation error on Accept: `'Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.'`

### Resulting data / tokens

The caller converts the returned value into a delay entry appended to the active macro:

| Return | Token |
|---|---|
| random | `dran` |
| `n` (1..999) | `d001` .. `d999` (the value zero-padded to three digits) |

Legacy fixed delay tokens `d125` (125 ms) and `d500` (500 ms) are recognized only in the non-gaming apps (not RGB/TKO).

In the layout file these appear inside a macro's value list using macro braces, e.g. `{caps}>{h}{d250}{i}` (macro tokens are serialized as `{<token>}`).

Availability: RGB, TKO, and Adv360 use this common dialog; the FS Edge/Pro app has its own equivalent variant.

---

## 11.4 Select Macro

**Purpose:** pick one of the currently defined macros (the keyboard supports multiple macros per trigger key). Used by the Tap and Hold dialog to assign a macro trigger as a Tap or Hold action on the Adv360.

Result: the **trigger key code** of the selected macro, or cancel.

### Controls

A grid with one row per defined macro and three columns:

| Column title | Content |
|---|---|
| `'Trigger'` | trigger key display text |
| `'Co-Trigger'` | the macro's co-trigger modifiers joined with `' + '` |
| `'Macro'` | readable macro content |

Validation on Accept with no row selected: `'You must select a macro'`.

---

## 11.5 Export

**Purpose:** export the currently loaded layout and/or lighting configuration as text files to a user-chosen directory (e.g. for backup, since the working copies live on the keyboard's v-Drive). Dialog title: `'Export files'`. Callers: Adv360, RGB, and TKO.

### Controls

Three mutually exclusive checkboxes (checking one un-checks the others); `'Layout and Lighting'` defaults to checked:

| Checkbox |
|---|
| `'Layout and Lighting'` |
| `'Layout only'` |
| `'Lighting only'` |

### Behavior on Accept

1. Opens a native directory-selection dialog.
2. Layout: the current layout serialized to text is written into the chosen directory, keeping the current layout file's base name (e.g. `layout1.txt`).
3. Lighting: the current lighting configuration serialized to text is written the same way with the current LED file's base name (e.g. `led1.txt`).

| Outcome | Message |
|---|---|
| Layout save failure | `'Error exporting layout file: '` + error |
| Lighting save failure | `'Error exporting lighting file: '` + error |
| Both succeed | `'Files exported successfully!'` |

There is no import path in this dialog; "import" in these apps is loading/overwriting the numbered layout files on the v-Drive (handled by the main windows, not here).

---

## 11.6 Search Keys

**Purpose:** type-to-filter search across every assignable key/token. Used by the Tap and Hold dialog and by the Adv360 main window (titles passed in: `'Search Keys'`, `'Search Keys (Macro)'`, `'Search Keys (Tap Action)'`, `'Search Keys (Hold Action)'`).

Result: the selected key code, or cancel.

### Behavior

- Label `'Search key'` over a filter edit attached to a list box — incremental filtering as the user types.
- List population: iterates every assignable action, **skipping** entries flagged as non-searchable (numpad duplicates, delay tokens, hotkeys). Item text = the action's search name, plus its display text when different, plus `' (' + <file token> + ')'` when the display text differs from the layout-file token, so users can search by either name or file token.
- Double-clicking an item accepts immediately.
- Validation on Accept with nothing selected: `'You must select a key'`.

---

## 11.7 Diagnostics

**Purpose:** produce a support diagnostics text file. Dialog title: `'Diagnostics'`. Callers: Adv360, RGB, TKO.

### UI (two-step flow)

- Step 1 label: `'Step 1: Please enter the keyboard's serial number which is found on the underside of the right key module (eg SERXX).'` — `SERXX` is replaced at runtime with an example serial: `'97BRNUSAA0000'`, or `'s360GB10000'` on the Advantage360.
- Serial-number field + button `'Create Diagnostics File'`.
- Step 2 label: `'Step 2: Contact Kinesis Tech Support and upload the diagnostic file just created to the Ticket Submission page.'` + button `'Contact Kinesis Tech Support'` → opens `https://gaming.kinesis-ergo.com/contact-tech-support/` in the gaming apps, else `https://kinesis-ergo.com/support/contact-a-technician/`.

### Diagnostics file content

Created only when the serial field is non-empty; written to the user's **Desktop** as `<serial>.txt`. Content is assembled from the connected device's files, each section preceded by its file name and a `'--------------'` separator line:

| Section | Source |
|---|---|
| Header | `'Diagnostic file, '` + `yyyy-mm-dd hh:nn` timestamp |
| Firmware | the device's version file |
| Keyboard settings | the device's settings file |
| App settings | `app_settings.txt`, or `'No app settings'` |
| Layouts | `layout1.txt` .. `layout9.txt` |
| Lighting | `led1.txt` .. `led9.txt` |

Unreadable files insert the load error message in place of content. Result dialogs: `'Diagnostics file saved to Desktop!'` / `'Error creating diagnostics file: '` + error.

---

## 11.8 Troubleshoot

**Purpose:** shown at startup when the keyboard's v-Drive is not detected (dialog caption `'Keyboard not detected'`). Offers rescan, demo mode, or a link to online troubleshooting.

Result: **Scan for v-Drive**, **Launch in Demo Mode** (which also puts the app into demo mode), or dismissed.

### Per-app message

| App | Presentation | Instruction text (exact) |
|---|---|---|
| Adv2, FS Pro, FS Edge, SE2 pedals | text only | `'Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by first enabling Power User Mode (if necessary) using the onboard shortcut Program + Shift + Esc, and then connecting the v-Drive using the shortcut Program + F1. Please connect the v-Drive and then click the "Scan for v-Drive" button below.'` |
| RGB, TKO | text with an inline SmartSet-key image | `'Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by using the onboard shortcut      + F8. ...'` (the gap holds the SmartSet key image) |
| Adv360 | text with an inline SmartSet-key image (light variant) | `'Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by using the onboard shortcut      + v-Drive. ...'` |

### Buttons

`'Scan for v-Drive'`, `'Launch in Demo Mode'`, `'Troubleshooting Tips'` — the last opens a per-app URL:

| App | URL |
|---|---|
| RGB | `https://gaming.kinesis-ergo.com/fs-edge-rgb-support/` |
| TKO | `https://gaming.kinesis-ergo.com/tko-support/` |
| FS Edge | `https://gaming.kinesis-ergo.com/fs-edge-support/` |
| FS Pro | `https://kinesis-ergo.com/support/freestyle-pro/` |
| Adv2 | `https://kinesis-ergo.com/support/advantage2/` |
| Adv360 | same as the Adv360 help URL |
| Pedal | `https://kinesis-ergo.com/support/savant-elite2/` |

---

## 11.9 Minor dialogs

### Intro

First-run informational dialog: displays a caller-supplied title/message with buttons `'Continue'`, `'Watch Tutorial'`, `'Read Manual'` and a `'Hide this notification?'` checkbox. The tutorial/manual buttons open per-app tutorial and manual URLs. When the checkbox is checked on close, the "hide intro" preference is persisted in the app settings.

### Loading

Non-modal borderless progress window with a caller-supplied title/message. Default caption `'Loading...'`.

### Info dialog

Self-dismissing toast-style notification with a title, message, position, and timeout; a timer closes it after the given number of seconds (default 5). It can be centered or positioned in the bottom-right of the primary monitor's work area, and can also be closed programmatically.

### Message box

The app-wide replacement for the OS message dialog, used for every validation/error message in this document:

- Icon selected by dialog type: confirmation, warning, error, information, plus custom FS Edge and FS Pro app icons.
- Buttons created dynamically from the standard set (`Yes`, `No`, `OK`, `Cancel`) and/or an array of custom buttons with per-button captions, widths, and click handlers (used e.g. for the `'Update Firmware'` button).
- Optional checkbox (e.g. `'Hide this notification?'`): when checked, the returned value is the modal result **plus 100** — callers test for this offset to implement "don't show again".
- Enter activates OK/Yes; Escape cancels.
- Height and width are caller-adjustable (default height 210).

### About dialogs

- All variants show `'App Version : '` + the executable's file version, and firmware info: the gaming variant shows `'KBD Firmware: <x>'` + `'LED Firmware: <y>'` or `'Keyboard Firmware : not found'`; the office variant shows `'Keyboard Firmware: v<x>'`.
- App title: `'Kinesis Gaming SmartSet App'` (gaming) / `'Kinesis Ergo SmartSet App'` (office).
- Buttons link to the per-app manual, tutorial, and support URLs; the office variant also shows `'Kinesis Corporation'` / `'www.Kinesis-Ergo.com'` (gaming: `'Kinesis Gaming'` / `'www.KinesisGaming.com'`) and a `mailto:tech@kinesis.com` link.
- A dashboard (master app) variant switches branding between gaming and office.

### Lighting-effect preview animations

The RGB and TKO apps preview animated LED effects on the on-screen keyboard using hard-coded per-effect frame sequences: each frame is a list of key indexes, and the app steps through the active effect's frames on a timer, lighting the listed keys. Effects with frame sequences include Rain, Reactive, Ripple, Fireball (and left/right variants), Loop up/down/left/right, Rebound horizontal/vertical, Starlight, and TKO edge variants. These previews are app-side only; they do not serialize into the layout or LED files.
