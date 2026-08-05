# 12 — Savant Elite2 (SE2) Foot Pedal App

**Scope:** the SE2 SmartSet App ("Savant Elite2 Config App") that programs the Kinesis Savant Elite2 USB foot pedal via a text file on the device's virtual flash drive ("v-Drive").

---

## 1. Device overview

The Savant Elite2 is a family of USB foot-pedal devices. A single control module supports **seven programmable inputs**: three pedals — left, middle, right — and four accessory jacks. Any given product has only a subset of these inputs. This is documented verbatim in the factory `pedals.txt` shipped on the device:

> "Above are the assigned actions for 7 possible inputs(left, middle, and right pedals and Jacks 1-4). Your device will only have some of these inputs"

The same factory file contains an ASCII diagram of the control module: Jack 1 and Jack 2 on the right side, Jack 3 and Jack 4 on the left side (viewed from the top, cable at the rear), two LEDs at the front, and the three pedals labeled `(lpedal)`, `(mpedal)`, `(rpedal)`.

Key hardware facts:

| Fact | Value |
|---|---|
| Programmable inputs | `lpedal`, `mpedal`, `rpedal`, `jack1`, `jack2`, `jack3`, `jack4` |
| Storage | USB mass-storage "v-Drive" containing an `active` folder |
| Config file | `active/pedals.txt` (plain text) |
| Firmware version file | `active/version.txt`, e.g. line 1 `Firmware version is 1.0.44`, line 2 a date (`01/20/2015`) |
| Mode switch | Physical slide switch between "play mode" and programming mode; changes take effect "after moving the slide switch to 'play mode' or unplugging and reconnecting the device" |
| v-Drive volume labels | `SE2` or `KINESIS FP` |
| Per input | one single action (key or mouse button) **or** one macro (sequence of actions with optional speed/delay controls) |

What is programmable per input: a single key, a single mouse click (left/middle/right), or a macro containing keystrokes with modifiers, mouse clicks, fixed delays (125 ms / 500 ms), output-speed changes (slow/default/fast), and a "different press & release" split point. There is no layer, lighting, or remap concept as in the keyboard apps.

---

## 2. Deployment

The legacy app exists in two deployment modes:

1. **Standalone application** — a single-window program titled "SE2 SmartSet App".
2. **Embedded in SmartSet Master Office** — the Office master dashboard hosts the SE2 form as an embedded child window, opening it for a detected pedal device with the loading text `'Loading SAVANT ELITE 2...'`, and forwards its own key-down/key-up events to the SE2 form while it is visible.

The SmartSet Master (gaming) app does not host SE2; only the Office master does.

---

## 3. Device detection (v-Drive)

There is no HID/USB protocol: detection means finding a mounted removable drive that looks like an SE2 v-Drive.

* Recognized volume labels: `SE2` and `KINESIS FP`.
* A drive qualifies when the folder `active` exists on it and contains `version.txt`.
* Windows: enumerate all drives and compare the uppercased volume label against the two labels. macOS: probe `/Volumes/SE2/active/` and `/Volumes/KINESIS FP/active/` directly. Write access to the drive is verified.
* If the app's own executable directory already contains `active/` (i.e. the app is run from the v-Drive itself), that location is used as-is; otherwise the app enters desktop mode and scans drives.
* Runtime presence check: the device is considered connected while its `active/version.txt` exists.
* On startup failure the app shows either the shared troubleshoot dialog titled `'Pedal not detected'` (desktop mode; options: Scan v-Drive, Demo Mode, Troubleshooting Tips) or, when launched from a drive, a warning: `'The SmartSet App cannot find the necessary layout and settings files on the v-drive. Replug the pedal to regenerate these files and try launching the App again.'` with a `'Troubleshooting Tips'` button opening `https://kinesis-ergo.com/support/savant-elite2/`.
* If the connection is lost when saving/loading, a `'Pedal Connection Lost'` dialog instructs: `'To save your changes you must use the onboard shortcut "Program + F1" to open the v-Drive and re-establish the connection with the SmartSet App.'` with `'Scan for v-Drive'` and `'Troubleshooting Tips'` buttons.
* Demo Mode disables Open/Save/Save As.

---

## 4. Config file format: `active/pedals.txt`

### 4.1 General structure

Plain text, line-oriented, read case-insensitively (lines are lowercased before matching). The parser only cares about lines that **start with** a pedal-name token; every other line (the factory instruction block starts each line with `*`) is ignored on load and preserved verbatim on save. Real factory file (header comment block trimmed):

```
[lpedal]>[lmouse]
{mpedal}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}
[rpedal]>[rmouse]
[jack1]>[lmouse]
[jack2]>[rmouse]
[jack3]>[bspace]
{jack4}>{-shift}{-t}{+t}{+shift}{-h}{+h}{-a}{+a}{-n}{+n}{-k}{+k}{-space}{+space}{-y}{+y}{-o}{+o}{-u}{+u}{-.}{+.}
```

A file freshly created by the app has exactly seven lines and no comments, e.g. (verbatim):

```
[lpedal]>
[mpedal]>
[rpedal]>
[jack1]>[d]
[jack2]>
[jack3]>
[jack4]>
```

### 4.2 Line grammar

```
<line> ::= <single-line> | <macro-line> | <other>          (other lines preserved, ignored)
<single-line> ::= "[" <input> "]" ">" [ "[" <key-token> "]" ]
<macro-line>  ::= "{" <input> "}" ">" { <macro-item> }
<input> ::= "lpedal" | "mpedal" | "rpedal" | "jack1" | "jack2" | "jack3" | "jack4"
<macro-item> ::= "{-" <token> "}"     key/modifier press
               | "{+" <token> "}"     key/modifier release
               | "{" <token> "}"      press-and-release in one step
```

* The bracket style of the *input name* selects the mode for the whole line: `[...]` = single action, `{...}` = macro. The separator between the input name and the action text is `>`.
* Loading: everything after the `>` is the action text. For single mode exactly one `[token]` is read; for macro mode the `{...}` groups are iterated, treating a leading `-` as key-down and a leading `+` as key-up.
* Modifier press/release entries maintain an "active modifier" set while loading; a non-modifier key acquires the currently-active modifiers. A modifier that is pressed and immediately released with no key in between (`{-ctrl}{+ctrl}`) is stored as a plain keystroke of that modifier.

### 4.3 What the app writes

* Single mode: `[` + token + `]`, e.g. `[pdown]`.
* Macro mode, per key: modifiers first as `{-mod}` entries, then the key as `{token}` (one group, no `-`/`+` pair for normal keys), then `{+mod}` releases. Example produced for "No": `{-shift}{n}{+shift}{o}`. Consecutive keys that are both Shift-only-modified share one `{-shift}...{+shift}` wrapper.
* "Different Press & Release" on a modified key writes the key as a down/up pair split by the `{ }` marker: `{-key}{ }{+key}`.
* The factory samples instead write *every* character as a `{-x}{+x}` pair (see 4.1) — both spellings are accepted by the loader (a bare `{x}` loads as a press-and-release in one step).

### 4.4 Token catalog

File tokens are the canonical save values; the display value differs on macOS for some keys but the **file token is identical cross-platform**.

| Category | File tokens |
|---|---|
| Letters / digits | `a`–`z`, `0`–`9` |
| Control keys | `escape`, `pause`, `prtscr`, `scroll`, `tab`, `caps`, `insert`, `home`, `end`, `pdown`, `pup`, `right`, `left`, `up`, `down`, `numlk`, `enter`, `bspace`, `delete` |
| Modifiers | `shift`, `lshift`, `rshift`, `ctrl`, `lctrl`, `rctrl`, `alt`, `lalt`, `ralt`, `win` (macOS displays `return/delete/fwd-delete/opt/cmd` but saves `enter/bspace/delete/alt/win`) |
| Function keys | `F1` … `F24` |
| Punctuation | `=`, `hyphen` (for `-`), `/`, `\`, `'`, `` ` ``, `;`, `,`, `.`, `obrack` (for `[`), `cbrack` (for `]`), `intl-\` (ISO key) |
| Numpad | `kp0`–`kp9`, `kpdiv`, `kpmult`, `kpmin`, `kpplus`, `kp.`, `kpenter` |
| Space | The save value is a single space character, so a macro space is written `{ }`; the factory samples use `{-space}{+space}` |
| Mouse | `lmouse`, `mmouse`, `rmouse` |
| Pedal response | `speed1` (slow), `speed3` (default), `speed5` (fast), `d125` (125 ms delay), `d500` (500 ms delay) |
| Media (single mode) | `mute`, `vol-`, `vol+`, `play`, `prev`, `next` |
| Other pseudo-keys | `calc` (calculator), `shutdn` (shutdown; defined but not exposed in the menu) |

**`{ }` ambiguity (a property of the file format):** `{ }` is both the space key's file token and the "Different Press & Release" split marker (§6). On load, the legacy app resolves `{ }` to the space key. A new implementation must handle this ambiguity; the firmware-side semantics of a bare `{ }` are not documented by the legacy app.

**Delay-token generation difference:** the factory sample/manual (firmware 1.0.44, 2015) uses `{125}` for the 0.125 s delay, while the app writes `{d125}`/`{d500}`. The legacy loader recognizes only `d125`/`d500`, so `{125}`-style files exist in the field but are not round-tripped by the app.

### 4.5 More real examples

From a hand-edited device file (factory `{-x}{+x}` pair style):

```
[mpedal]>[space]
{rpedal}>{-ctrl}{-alt}{-delete}{+delete}{+ctrl}{+alt}
[jack1]>[F1]
{jack4}>{-F1}{+F1}{-F2}{+F2}{-F3}{+F3}{-F4}{+F4}
```

The left-mouse double click macro (factory default for jack4): `{jack4}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}`. The app writes the same feature as `{lmouse}{d125}{lmouse}` and *displays* it as `{lmouse-dblclick}` (the lmouse / 125 ms delay / lmouse triple is detected and shown as a double click).

### 4.6 Save mechanics

If the in-memory file content is empty (new file), the app emits the seven lines in fixed order lpedal, mpedal, rpedal, jack1..jack4; otherwise it rewrites **only** the lines whose prefix matches a pedal token (single or macro form), leaving all other lines (the factory instruction block) untouched. Non-US Windows keyboard layouts are handled by converting virtual keys to their US-English equivalents on save and back on load — file tokens are always canonical US scan codes.

---

## 5. UI workflow

Single borderless window (caption `'Savant Elite2 Config App'`).

* **Left panel**: header `'TO RE-PROGRAM CLICK ON A "CONFIGURE" BUTTON, THEN SELECT ACTION(S) AT RIGHT'` and `'Image below shows all possible inputs. Your product will have only some of the pedals or jacks shown below'`, a photo of the pedal, and file buttons `Save as`, `Save`, `Open another file` plus a `File name:` label. Seven `Configure` buttons.
* **Right panel**: rows for `Left Pedal`, `Middle Pedal`, `Right Pedal`, `Jack 1`–`Jack 4`, each with a read-only memo showing the current assignment, a `'...'` expand button (grows the memo for long macros), and a hidden red `*Modified` label. Above them: the edit box, buttons `Config Pedal`, `Clear`, `Backspace`, `Cancel`, `Done`, `Single Action`, `Multiple Actions (Macro)`, `Special Actions`, `Exit program`, `Help`, and hint `'SELECT SINGLE OR MULTIPLE THEN TYPE IN THE BOX OR SELECT SPECIAL ACTION'`.

### Programming a pedal

1. **Click a Configure button.** The corresponding pedal name appears (`'Left pedal'`, `'Jack 1'`, ...), the button latches down, the edit box turns **yellow**, the current key list is backed up for Cancel, and the mode defaults to Single Action.
2. **Choose Single Action or Multiple Actions (Macro)** — switching modes clears the current entry. In single mode each new keypress replaces the previous one; in macro mode keys append with active modifiers.
3. **Type keys.** On Windows a thread-local keyboard hook captures keys during edit mode and swallows them so they don't reach the UI; the hook distinguishes the numpad Enter from the main Enter via the extended-key bit. On macOS the form previews key-down/key-up events instead. Modifiers held down are tracked in an active-modifier list; a modifier tapped alone registers as a keystroke of that modifier on key-up.
4. **Or pick a Special Action** from the popup menu (section 6).
5. **Backspace** removes the last entry; **Clear** empties the entry.
6. **Done** commits the entry to the pedal's key list, shows the red `*Modified` label for that row and sets the save-state to modified; **Cancel** restores the backup. Starting another action with an edit in progress prompts `'Key modification in progress, apply changes?'` (Yes/No/Cancel).
7. **Save** (enabled only when modified) rewrites the loaded `pedals.txt` (section 4.6) after re-checking the v-Drive, then shows: `SAVE DONE.` / `NOW CHANGE YOUR SE2 TO 'PLAY MODE' TO IMPLEMENT CHANGES.` This is the "direct-to-device" model: the app edits the file on the mounted v-Drive; the firmware applies it when switched back to play mode or replugged.
8. **Save As / Open another file** use standard file dialogs with filter `'Text files|*.txt'` and initial directory = the device's `active` folder. Opening a file that does not exist offers `'Cannot open pedals.txt configuration file.'` / `'Create a new file?'`. Multiple saved layouts can therefore be kept in `active/`; only `pedals.txt` is live.
9. Closing with unsaved changes asks `'Do you want to save changes to the pedal configuration file?'`.

### Display conventions

* The current assignment text is regenerated from the key model: single keys as `[x]`, macro items as `x` or `{mod+key}`; modifier combos, special tokens, and non-alphanumeric single keys are colored **red** in the memos.
* In the edit box, spaces are rendered as the visible open-box character U+2423.
* Dark theme (macOS) switches to white-on-dark-gray with gray memos.
* The Help button popup has `Help` (opens `SE2 Config App Help.pdf` or `SE2 SmartSet App Help.pdf` from the app/v-Drive root) and `About` (app version + `Pedal – Firmware version : <n>` parsed from the `Firmware version is …` line of `version.txt`).

---

## 6. Special Actions menu and special modes

Each menu item is restricted to single mode, macro mode, or both; selecting an item automatically switches the editor to the required mode. All items are always shown.

| Section | Item (Windows caption) | Mode | Emits (Windows / macOS) |
|---|---|---|---|
| MOUSE ACTIONS | Left Mouse Click | both | `lmouse` |
| | Left Mouse Double Click | macro | `lmouse`, `d125`, `lmouse` |
| | Middle Mouse Click | both | `mmouse` |
| | Right Mouse Click | both | `rmouse` |
| EDITING TOOLS | Cut (Ctrl + x) | macro | Ctrl+`x` / Cmd+`x` |
| | Copy (Ctrl + c) | macro | Ctrl+`c` / Cmd+`c` |
| | Paste (Ctrl + v) | macro | Ctrl+`v` / Cmd+`v` |
| | Select All (Ctrl + a) | macro | Ctrl+`a` / Cmd+`a` |
| | Undo (Ctrl + z) | macro | Ctrl+`z` / Cmd+`z` |
| MEDIA CONTROLS | Mute / Volume Up / Volume Down / Play/Pause / Previous Track / Next Track | single | `mute`, `vol+`, `vol-`, `play`, `prev`, `next` |
| COMMONLY USED SHORTCUTS | Web Browser Forward / Back | macro | Alt+Right / Alt+Left (Win), Cmd+Right / Cmd+Left (Mac) |
| | Alt + Tab (Windows only) | macro | Alt+`tab` |
| | Ctrl + Alt + Delete (Windows only) | macro | Ctrl+Alt+`delete` |
| | Calculator (Windows only) | single | `calc` |
| | Windows Combination (Win + _) (Windows only) | macro | toggles the Win modifier held state (checkbox item) |
| | Cmd + Tab (macOS only) | macro | Cmd+`tab` |
| | Snip to File / Snip to Clipboard (macOS only) | macro | Shift+Cmd+`4` / Shift+Ctrl+Cmd+`4` |
| | Force Quit (macOS only) | macro | Cmd+Alt+`escape` |
| PEDAL RESPONSE | Slow Output (speed1) | macro | `speed1` |
| | Default Output (speed3) | macro | `speed3` |
| | Fast Output (speed5) | macro | `speed5` |
| | Short Delay (125ms) | macro | `d125` |
| | Long Delay (500ms) | macro | `d500` |
| | Different Press && Release | macro | see below |

**Macro playback speed:** `speed1`/`speed3`/`speed5` tokens are inserted *into* the macro and change firmware output speed (slow / default / fast) from that point onward; they always serialize as a single `{speedN}` group (never as a `{-x}`/`{+x}` pair).

**Delays:** `d125`/`d500` insert fixed 125 ms / 500 ms pauses — the building block of the double-click macro.

**Different Press & Release:** splits a macro so part plays on pedal press and part on pedal release. If the last entered key has modifiers, the app asks `'Add "Different Press && Release" to current macro?'` and, on Yes, flags that key, which serializes as `{-key}{ }{+key}` inside its modifier wrap; otherwise a standalone `{ }` marker is appended to the macro. Note the `{ }` / space-token ambiguity described in §4.4.

---

## 7. Differences from the keyboard apps (for re-implementers)

* **No layers, no LED/lighting, no remap-vs-macro distinction, no 9 layout files** — one live file (`active/pedals.txt`), seven inputs, one action each.
* **No firmware-update UI.** The firmware version is display-only in the About box.
* The file grammar differs from the Adv2/FS keyboard files: pedal-name-in-brackets prefix per line, `>` separator, `{-x}`/`{+x}` press/release pairs, `{ }` split marker, `{125}`/`{d125}`-style delays — versus the keyboard apps' `[key]>[key]` remaps and `{x}{y}` macros with `[t&h...]`, `[speed]`, layer prefixes, etc.
