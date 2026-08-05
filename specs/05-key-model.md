# 05 — In-Memory Key Model and Physical Layout/Layer Definitions

Scope: the key model of the legacy SmartSet apps — the complete table of supported key tokens (numeric code ↔ config-file token ↔ display caption) per device dialect, the per-device physical layer definitions, the special-key semantics (modifiers, AltGr, keypad-layer exceptions, non-remappable keys, international handling), and the legacy in-memory object model, documented for reference.

---

## 1. Legacy in-memory model (reference)

This section documents the legacy app's runtime object model. It is not a mandate for the new implementation; the field inventory is preserved because it enumerates everything the legacy app tracks per key, per macro, and per layer.

### 1.1 TKey — a single key/keystroke

`TKey` is the atomic unit: it describes one supported key (in the master config-key table) or one keystroke inside a macro.

| Field (property) | Type | Meaning |
|---|---|---|
| `Key` | `word` | Numeric key code. For standard keys these are standard Windows virtual-key codes; for app-specific actions they are legacy internal codes ≥ 10000 (see §2). Read-only after creation. |
| `Value` | `string` | Primary token text (e.g. `'esc'`, `'kp/'`, `'a'`). Used as display fallback and as macro output text. |
| `DisplayText` | `string` | Caption drawn on the on-screen key cap. Defaults to `Value` when empty. A line-feed character inside the string produces a two-line caption. |
| `SaveValue` | `string` | **The token written to / read from layout files** (the text between `[` `]` or `{` `}`). Defaults to `Value` when empty. Token lookup is case-insensitive on this field. |
| `ShiftedValue` | `string` | Character produced with Shift held (e.g. `'!'` for `1`). |
| `ShowShiftedValue` | `boolean` | Whether the shifted value is shown/used when Shift is an active modifier. |
| `MultiValue` | `string` | Text used inside multi-key (macro) output when the key has no modifier (e.g. `' '` for space so macros print a literal space instead of `{spc}`). |
| `Modifiers` | `string` | Modifier codes attached to this keystroke (macro context). Composed from the two-character codes in §5.1, comma-separated. |
| `WriteDownUp` | `boolean` | Macro mode: if true the key writes separate press/release events (`-` for down, `+` for up); if false it is written once. Default `True`. Speed/delay pseudo-keys set it `False`. |
| `DiffPressRel` | `boolean` | Macro mode: key has different press and release behavior. Default `False`. |
| `ConvertToUnicode` | `boolean` | Windows only: display text is produced by the OS keyboard-layout translation (`ToUnicode`) for the current keyboard layout, so punctuation/letters render per the user's national layout. Set `true` for letters, digits, and the layout-dependent punctuation keys. |
| `DisplaySize` | `integer` | Font size override for the key-cap caption (0 = default). The default small size is 8 (9 on RGB/TKO and on macOS; 7 on macOS Adv360); media glyphs use 4; arrows use 16 (Legacy dialect) or 10. |
| `FontName` | `string` | Font override; the Unicode glyph keys use `'Cambria Math'`. |
| `OtherDisplayText` | `string` | Longer, single-line name used in lists/co-trigger text (e.g. `'Print Screen'`). Defaults to the display text when empty. |
| `SearchText` | `string` | Text used by the key-search UI. The sentinel value `'SKIP_SEARCH'` hides the entry from search (used for duplicate/internal entries). |
| `ImageName` | `string` | Image resource drawn instead of text (only the SmartSet key uses `'imgSmartSet'`). |
| `UpDown` | enum (`none, down, up`) | Macro keystroke direction. `up` prefixes the token with `+`, `down` with `-` in output text. |

Keys support deep copy and field-by-field equality comparison.

### 1.2 TKeyList — an ordered list of keystrokes / a macro

`TKeyList` is an owning list of `TKey` items and doubles as the macro container:

| Field | Meaning |
|---|---|
| `Guid` | Unique id created at construction. |
| `TriggerKey` | Key code of the trigger key (initialized to `-1`). |
| `LayerIdx` | Layer index the macro belongs to (initialized to `-1`). |
| `MacroIdx` | Which macro slot (1..5) this list is. |
| `MultiKey` | `true` for macros (multi-keystroke lists). |
| `CoTrigger1..CoTrigger4` | Up to four co-trigger `TKey`s (modifiers pressed together with the trigger key). |
| `MacroSpeed` | Playback speed. Defaults per device family: FS Edge/FS Pro use 0, everything else uses 5. |
| `MacroRptFreq` | Repeat frequency. FS Edge/FS Pro: 0; others: 1. |
| `IsNew` | Flag for newly created (unsaved) macros. |

Assignment copies trigger info and macro settings and deep-copies all keystrokes. Equality requires the same `MultiKey` flag, the same count, and per-item key equality. Membership can be checked by key code.

### 1.3 TKBKey — one physical key position on the keyboard

| Field | Meaning |
|---|---|
| `OriginalKey: TKey` | Factory-default action of the position (what the key does when unmodified). |
| `ModifiedKey: TKey` | The remapped action, when `IsModified = true`. |
| `PositionKey: TKey` | The token identifying the *physical position* in layout files. Defaults to `OriginalKey` unless an explicit position key is given. Used by the Adv360 layers where the same physical key has different default actions per layer (e.g. keypad-layer `kp7` at position `u`). |
| `TapAction: TKey`, `HoldAction: TKey`, `TimingDelay: integer`, `TapAndHold: boolean` | Tap-and-hold assignment: tap action, hold action, and delay in ms (default delay 250 ms; bounds 1..999 ms; at most 10 tap-and-hold keys per layout). |
| `Index: integer` | Ordinal of the key inside its layer's key list (the GUI button index; see §4). |
| `KeyColor: TColor` | Per-key LED color (lighting mode); unset by default. |
| `IsModified` | True when `ModifiedKey` differs from `OriginalKey`. |
| `IsMacro` | True when at least one macro is assigned. |
| `CanEdit` | False for keys that cannot be remapped (see §5.3). |
| `CanAssignMacro` | False for keys that cannot carry a macro (modifier keys on most devices). |
| `Macro1..Macro5: TKeyList` | Up to five macros per key. Each is a multi-key list with `MacroIdx` 1..5. |
| `ActiveMacro: TKeyList` | Pointer to the macro currently being edited (defaults to `Macro1`). |
| `Multimodifiers: string` | Multi-modifier assignment token (Adv360; e.g. `[caws]`, see §5.7). |
| `TriggerKey` (derived) | Returns `OriginalKey` for the Fn1 layer-shift key (`fn1s`) and the keypad layer-toggle key (`keyt`), otherwise `PositionKey`. |
| `ModifiedOrOriginalKey` (derived) | `ModifiedKey` when modified, except the same two layer keys always report `OriginalKey`. |

Resetting a key clears the remap, tap-hold assignment, multimodifiers, and all macros. Copying between key positions can transfer key data only, macros only, or both.

### 1.4 TKBLayer and lists

- `TKBKeyList`: owning list of `TKBKey`.
- `TKBLayer`: one layer of the keyboard.
  - `KBKeyList: TKBKeyList` — the key positions (ordered by `Index`).
  - `EdgeKeyList: TKBKeyList` — edge-lighting zones (populated only for the TKO device).
  - `LayerIndex: integer` — layer identity (0 = top/base; 1 = bottom/keypad; Adv360 uses 0..4).
  - `LayerName: string` — e.g. `'Qwerty-top'`, `'Base'`, `'Fn1'`.
  - `LayerType: integer` — QWERTY (0) / Dvorak (1) on Legacy-dialect devices; on Adv360 it mirrors the layer index.
- `TKBLayerList`: owning list of `TKBLayer`.

Layer/keyboard index values:

| Layer concept | Value |
|---|---|
| Top layer index | 0 |
| Bottom (keypad) layer index | 1 |
| QWERTY layout type | 0 |
| Dvorak layout type | 1 |
| Adv360 Base layer | 0 |
| Adv360 Keypad layer | 1 |
| Adv360 Fn1 layer | 2 |
| Adv360 Fn2 layer | 3 |
| Adv360 Fn3 layer | 4 |

### 1.5 Runtime organization

The legacy app's key service owns:
- The **master table of supported keys** (a `TKeyList` of every `TKey` the current device supports). Its content depends on the device family and the operating system; §3 gives the complete content per family.
- The **device's layer list**, built once per device:

| Device | Layers (in order) |
|---|---|
| Advantage2, QWERTY mode | QWERTY top, QWERTY keypad |
| Advantage2, Dvorak mode | Dvorak top, Dvorak keypad |
| FS Pro | top, keypad |
| FS Edge | top, keypad |
| FS Edge RGB | top, keypad |
| TKO | top, keypad |
| Advantage360 | Base, Keypad, Fn1, Fn2, Fn3 |

- The **active-modifier set** (modifier keys currently held during macro capture).
- The **global macro list** and undo buffers for the key/macro being edited.
- The **current OS keyboard-layout name** (from Windows, defaulting to `'00000409'`, US English).

Lookup semantics:
- Lookup **by numeric code** returns the first table entry with a matching code (the shared instance).
- Lookup **by token** returns the first entry whose file token (`SaveValue`) matches, case-insensitively.
- Layer builders and assignment paths work on **copies** of table entries, never the shared instances.
- Keys within a layer can be found by original key code, by position key code (or trigger key code for macros), or by `Index`; edge-lighting zones are found the same way in the edge list; layers are found by `LayerIndex`.

Remapping a key sets `ModifiedKey`/`IsModified` when the new code differs from the original and the position is editable, and clears them when the new code equals the original. Assigning a macro places it in `Macro1` and sets `IsMacro`.

---

## 2. Key code space

Two code spaces coexist in a key's numeric code. These codes are internal to the legacy app: **they never appear on disk — only the file tokens (§3) are written to layout files.**

1. **Standard Windows virtual-key codes** (below 256): `VK_ESCAPE = $1B`, `VK_F1..VK_F24 = $70..$87`, `VK_A..VK_Z = $41..$5A`, `VK_0..VK_9 = $30..$39`, `VK_NUMPAD0..9 = $60..$69`, `VK_LSHIFT $A0`, `VK_RSHIFT $A1`, `VK_LCONTROL $A2`, `VK_RCONTROL $A3`, `VK_LMENU $A4`, `VK_RMENU $A5`, `VK_LWIN $5B`, `VK_RWIN $5C`, `VK_APPS $5D`, media keys `$AD..$B3`, `VK_OEM_102 $E2`, and the layout-dependent OEM punctuation keys: `VK_OEM_1 $BA` (`;:`), `VK_OEM_PLUS $BB` (`=+`), `VK_OEM_COMMA $BC` (`,<`), `VK_OEM_MINUS $BD` (`-_`), `VK_OEM_PERIOD $BE` (`.>`), `VK_OEM_2 $BF` (`/?`), `VK_OEM_3 $C0` (`` `~ ``), `VK_OEM_4 $DB` (`[{`), `VK_OEM_5 $DC` (`\|`), `VK_OEM_6 $DD` (`]}`), `VK_OEM_7 $DE` (`'"`).
2. **Legacy internal codes ≥ 10000** for app-specific actions (layer switches, hotkeys, pedals, mouse actions, macro pseudo-keys, lighting zones, …). The complete list, with exact values, is embedded in the token table below.

Timing-delay pseudo-keys occupy a contiguous range: 10086 is delay `d001` and each subsequent delay adds 1, through `d999` = 11084. The token is `'d'` followed by the delay in ms, zero-padded to three digits.

---

## 3. Complete key token table

Legend for the **Family** column (the token dialects):
- **Legacy** = Advantage2 and Savant Elite2 pedal apps
- **Gen1** = FS Edge, FS Pro, FS Edge RGB, TKO
- **Gen2** = Advantage360
- **All** = present for every device

Token = the file token (`SaveValue`) — the exact text between `[]`/`{}` in layout files. Where the primary value differs from the file token both are listed. `\n` denotes a line break inside the display caption. Numeric codes are legacy-internal (standard Windows VK codes below 256, app-specific codes ≥ 10000); only the file tokens matter on disk.

### 3.1 Letters and digits

| Code | Value | Token | Display | Family / notes |
|---|---|---|---|---|
| `VK_A`..`VK_Z` ($41..$5A) | `a`..`z` | `a`..`z` | `A`..`Z` | All. `MultiValue` = lowercase letter, `ShiftedValue` = uppercase, `ConvertToUnicode` + `ShowShiftedValue` = true. |
| `VK_0`..`VK_9` ($30..$39) | `0`..`9` | `0`..`9` | Legacy: `)\n0` … `(\n9`; Gen1/Gen2: `0 )` … `9 (` | Shifted values `) ! @ # $ % ^ & * (`; `ConvertToUnicode`/`ShowShiftedValue` true. |

### 3.2 Punctuation / special characters (layout-dependent keys)

All rows have `ConvertToUnicode = true`, `ShowShiftedValue = true`, `MultiValue` = unshifted char, `ShiftedValue` as shown.

| Code | Value | Token Legacy | Token Gen1 | Token Gen2 | Shifted | Display (Legacy / Gen1+Gen2) | Notes |
|---|---|---|---|---|---|---|---|
| `VK_OEM_PLUS` ($BB) | `=` | `=` | `=` | `eql` | `+` | `+\n=` / `= +` | |
| `VK_OEM_MINUS` ($BD) | `-` | `hyphen` | `hyph` | `hyph` | `_` | `_\n-` / `- _` | |
| `VK_OEM_2` ($BF) | `/` | `/` | `/` | `fsls` | `?` | `?\n/` / `/ ?` | |
| `VK_OEM_5` ($DC) | `\` | `\` | `\` | `bsls` | `\|` | `\|\n\` / `\ \|` | |
| `VK_OEM_7` ($DE) | `'` | `'` | `apos` | `apos` | `"` | `"\n'` / `' "` | |
| `VK_OEM_3` ($C0) | `` ` `` | `` ` `` | `tilde` | `grav` | `~` | ``~\n` `` / `` ` ~`` | List/search name is `'Hash'` in all dialects. |
| `VK_OEM_1` ($BA) | `;` | `;` | `colon` | `scol` | `:` | `:\n;` / `; :` | |
| `VK_OEM_COMMA` ($BC) | `,` | `,` | `com` | `comm` | `<` | `<\n,` / `, <` | |
| `VK_OEM_PERIOD` ($BE) | `.` | `.` | `per` | `perd` | `>` | `>\n.` / `. >` | |
| `VK_OEM_4` ($DB) | `[` | `obrack` | `obrk` | `obrk` | `{` | `{\n[` / `[ {` | |
| `VK_OEM_6` ($DD) | `]` | `cbrack` | `cbrk` | `cbrk` | `}` | `}\n]` / `] }` | |
| `VK_OEM_102` ($E2) | `intl-\` | `intl-\` | `intl\` | `int#` | `intl-\` | (empty display) | "International <> key between Left Shift and Z". `MultiValue` and `ShiftedValue` both `intl-\`. |

### 3.3 Whitespace, editing, and navigation

| Code | Token Legacy | Token Gen1 | Token Gen2 | Display | Notes |
|---|---|---|---|---|---|
| `VK_ESCAPE` | `escape` | `esc` | `esc` | `Esc` | |
| `VK_SPACE` | `space` | `spc` | `spc` | `Space` | `MultiValue = ' '` (literal space in macro text). |
| 10034 | `lspc` | `lspc` | `lspc` | `Space` | All devices; left space bar (FS family/TKO). `MultiValue = ' '`, list name `Left Space`. |
| 10035 | `rspc` | `rspc` | `rspc` | `Space` | Right space bar; list name `Right Space`. |
| 11093 | `mspc` | `mspc` | `mspc` | `Space` | Middle space (TKO); list name `Middle Space`. |
| `VK_TAB` | `tab` | `tab` | `tab` | `Tab` | All. |
| `VK_CAPITAL` | `caps` | `caps` | `caps` | `Caps\nLock` | All. |
| `VK_RETURN` | `enter` | `ent` | `ent` | Win: `Enter`; Mac: `Return` | OS-specific display. |
| `VK_BACK` | `bspace` | `bspc` | `bspc` | Win: `Back\nSpace`; Mac: `Delete` | |
| `VK_DELETE` | `delete` | `del` | `del` | Win: `Delete`; Mac: `Fwd \nDelete` | |
| `VK_INSERT` | `insert` | `insert` (save `ins`) | `insert` (save `ins`) | `Insert` | Gen1/Gen2: value `insert`, file token `ins`. |
| `VK_HOME` | `home` | `home` | `home` | `Home` | All. |
| `VK_END` | `end` | `end` | `end` | `End` | All. |
| `VK_PRIOR` (PgUp) | `pup` | `pup` | `pgup` | `Page\nUp` | |
| `VK_NEXT` (PgDn) | `pdown` | `pdn` | `pgdn` | `Page\nDown` | |
| `VK_SNAPSHOT` | `prtscr` | `prnt` | `prnt` | `Print\nScrn` | The legacy Print key code (`VK_PRINT`) is registered with the same token in each dialect. |
| `VK_SCROLL` | `scroll` | `scrlk` | `sclk` | `Scroll\nLock` | |
| `VK_PAUSE` | `pause` | `pause` | `paus` | `Pause\nBreak` | |
| `VK_NUMLOCK` | `numlk` | `numlk` | `nmlk` | `Num\nLock` | Duplicate registration under keypad-layer code 10052 with the same token in each dialect. |
| `VK_UP` | `up` | `up` | `up` | ↑ (U+2191) | Cambria Math, size 16 (Legacy) / 10 (Gen1/Gen2). |
| `VK_DOWN` | `down` | `dwn` | `down` | ↓ (U+2193) | |
| `VK_LEFT` | `left` | `lft` | `left` | ← (U+2190) | |
| `VK_RIGHT` | `right` | `rght` | `rght` | → (U+2192) | |

### 3.4 Function keys

| Code | Token | Display | Notes |
|---|---|---|---|
| `VK_F1`..`VK_F24` | `F1`..`F24` | `F1`..`F24` | All devices. |

### 3.5 Modifiers

| Code | Token Legacy | Token Gen1 | Token Gen2 | Display Win | Display Mac | Notes |
|---|---|---|---|---|---|---|
| `VK_LSHIFT` | `lshift` | `lshift` → save `lshft` | `lshf` | `Left\nShift` | same | Gen1: value `lshift`, file token `lshft`. |
| `VK_RSHIFT` | `rshift` | `rshift` → save `rshft` | `rshf` | `Right\nShift` | same | |
| `VK_LCONTROL` | `lctrl` | `lctrl` | `lctr` | `Left\nCtrl` | `Left\nCtrl` (search text `Left Control` on Mac Gen2) | |
| `VK_RCONTROL` | `rctrl` | `rctrl` | `rctr` | `Right\nCtrl` | same | |
| `VK_LMENU` | `lalt` | `lalt` | Win `lalt` / Mac `lopt` (save `lalt`) | `Left\nAlt` | FS Edge/Pro: `Left\nOpt` token `lalt`; other Mac apps: value `lopt`, save `lalt`. | |
| `VK_RMENU` | `ralt` | `ralt` | Win `ralt` / Mac `ropt` (save `ralt`) | `Right\nAlt` | `Right\nOpt` | |
| `VK_LWIN` | Win: `lwin` (Pedal: `win`); Mac non-Gen2: value `Cmd`, save `lwin` | same | Mac Gen2: `lwin`, display `Cmd` | `Left\nWin` | `Cmd` | |
| `VK_RWIN` | Win: `rwin` (Pedal: `win`) | `rwin` | `rwin` | `Right\nWin` | `Right\nCmd` | |
| 10038 | — | `lwin` | — | — | `Left\nCmd` | Mac only, non-Gen2 apps (dedicated Left Cmd entry). |
| `VK_SHIFT` | `Shift` → save `shift` | — | — | `Shift` | — | Pedal only. |
| `VK_CONTROL` | `Ctrl` → save `ctrl` | — | — | `Ctrl` | — | Pedal only. |
| `VK_MENU` | Win: `alt`; Mac: value `Opt`, save `alt` | — | — | `Alt` / `Opt` | | Pedal only. |
| 11090 | `hyper` | `hyper` | `hypr` | `Hyper` | | |
| 11091 | `meh` | `meh` | `meh` | `Meh` | | |

### 3.6 Keypad keys

| Code | Token Legacy | Token Gen1/Gen2 | Display | Notes |
|---|---|---|---|---|
| `VK_NUMPAD0`..`VK_NUMPAD9` | `kp0`..`kp9` | `kp0`..`kp9` | `0`..`9` | Legacy dialect also registers keypad-layer duplicates under codes 10056..10065 with the same tokens. |
| `VK_DIVIDE` | `kp/` → save `kpdiv` | `kp/` | `/` | Legacy keypad-layer duplicate under 10054. Gen1/Gen2 file token stays `kp/`. |
| `VK_MULTIPLY` | `kp*` → save `kpmult` | `kp*` | `*` | Legacy duplicate under 10055. |
| `VK_SUBTRACT` | `kp-` → save `kpmin` | `kp-` | `-` | Legacy duplicate under 10066. |
| `VK_ADD` | `kp+` → save `kpplus` | `kp+` | `+` | Legacy duplicate under 10067. |
| `VK_DECIMAL` | `kp.` | `kp.` | `.` | Legacy duplicate under 10070. |
| 10000 | `kpenter` | Gen1: `kpenter` → save `kpent`; Gen2: `kpen` | `Kp\nEnter` | Keypad Enter. |
| 10053 | `kp=` | `kp=` | `=` | |
| 10052 | `numlk` | Gen1 `numlk` / Gen2 `nmlk` | `Num\nLock` | |
| 10048 | `kpshft` | — | `Kp\nShift` | Advantage2 only. |
| 10068 | `kpenter1` | — | `Kp\nEnter` | Advantage2 only. |
| 10069 | `kpenter2` | — | `Kp\nEnter` | Advantage2 only. |

### 3.7 Media / volume keys

Display captions are Unicode glyphs on Windows 10 and later, otherwise plain text fallbacks. The glyph column shows the decimal Unicode code point.

| Code | Token | Glyph (Win10+) | Text fallback | Notes |
|---|---|---|---|---|
| `VK_VOLUME_MUTE` | `mute` | 128360 (RGB/TKO: 128264) | `Mute` | |
| `VK_VOLUME_DOWN` | `vol-` | 128361 (RGB/TKO: 128265) | `Vol-` | |
| `VK_VOLUME_UP` | `vol+` | 128362 (RGB/TKO: 128266) | `Vol+` | |
| `VK_MEDIA_STOP` | `stop` | 9724 | `Stop` | |
| `VK_MEDIA_PREV_TRACK` | `prev` | 9198 | `Prev` | |
| `VK_MEDIA_NEXT_TRACK` | `next` | 9197 | `Next` | |
| `VK_MEDIA_PLAY_PAUSE` | Gen2: `plpa`; others: `play` | 9199 | `Play\nPause` | |
| 11151 | `play` | 9654 | `Play` | Gen2 only. |
| 11147 | `fwrd` | 9193 | `Forward` | |
| 11148 | `rewd` | 9194 | `Rewind` | |
| 11149 | `cpau` | 9208 | `Pause` | |
| 11150 | `ejct` | 9167 | `Eject` | |
| 11152 | `recr` | 9210 | `Record` | |
| 11153 | `ranp` | — | `Rand\nPlay` | All devices. |
| 11154 | `plsk` | — | `Play\nSkip` | |
| 10044 | `play` | 9199 | `Play` | Keypad-layer duplicates for Advantage2; also registered on all devices. |
| 10045 | `prev` | 9198 | `Prev` | |
| 10046 | `next` | 9197 | `Next` | |
| 10049 | `mute` | 128360 | `Mute` | |
| 10050 | `vol-` | 128361 | `Vol-` | |
| 10051 | `vol+` | 128362 | `Vol+` | |

### 3.8 Mouse actions

| Code | Token Legacy (Adv2/Pedal) | Token Gen1 | Token Gen2 | Display |
|---|---|---|---|---|
| 10001 | `lmouse` | `lmous` | `lmou` | `Left\nMouse` (Pedal: no display) |
| 10002 | `mmouse` | `mmous` | `mmou` | `Middle\nMouse` |
| 10003 | `rmouse` | `rmous` | `rmou` | `Right\nMouse` |
| 10036 | — | `mous4` | `4mou` | `Mouse\nBtn 4` |
| 10037 | — | `mous5` | `5mou` | `Mouse\nBtn 5` |
| 11155 | — | — | `sumo` | `Mouse\nScroll Up` |
| 11156 | — | — | `sdmo` | `Mouse\nScroll Down` |
| 11157 | — | — | `moul` | `Mouse\nMove Left` |
| 11158 | — | — | `mour` | `Mouse\nMove Right` |
| 11159 | — | — | `mouu` | `Mouse\nMove Up` |
| 11160 | — | — | `moud` | `Mouse\nMove Down` |

The pedal app additionally uses the literal text `lmouse-dblclick` for the left-mouse double-click action.

### 3.9 Special actions and device buttons

| Code | Token | Display | Family / notes |
|---|---|---|---|
| 10042 | Legacy/Gen1: `kptoggle`; Gen2: `kp` | Legacy/Gen1: `Key-\npad`; Gen2: `Kp\nToggle` | Keypad toggle. |
| `VK_APPS` | Legacy/Gen1: `menu`; Gen2: `app` | `PC\nMenu` / `App` | Keypad-layer duplicate under 10043 with the same token. |
| 10010 | Legacy: `shutdn`; Gen1: `shutdn` → save `shtdn`; Gen2: `pwdn` | `Shut\ndown` / `Power` | |
| 10023 | Legacy: `micmute`; Gen1: `micmute`; Gen2: `mmut` | `Mic\nMute` | |
| 10009 | `calc` | `Calc` | All; keypad-layer duplicate under 10047 with the same token. |
| 10020 | Legacy: `fntoggle`; Gen1/Gen2: `fntog` | `Fn\nToggle` | |
| 10021 | Legacy: `fnshift`; Gen1/Gen2: `fnshf` | `Fn\nShift` | |
| 10022 | Gen2: `ledt`; others: `LED` | ☀ (9728) / `LED` | |
| 11161 | `led+` | 🔆 (128262) / `Led+` | |
| 11162 | `led-` | 🔅 (128261) / `Led-` | |
| 10019 | `null` | ⊗ (8855); fallback Gen2 `NUL`, others `' '` | "Null" action. |
| 10013 | `` (empty value/token) | `Pro-\ngram` | Program button; not writable to files (empty token). |
| 10017 | `Fn` | `Fn` | Hidden from key search. |
| 11146 | `ss` | (image `imgSmartSet`) | SmartSet key. |
| 11173 | `mstp` | `Stop\nmacro playback` | |
| 11207 | `pedl` | `Tab` | Adv360 pedal-jack position token. |
| 10039 | `lp-tab` | `Tab` | Advantage2 only — left foot pedal. |
| 10040 | `mp-kpshf` | `Kp\nShift` | Advantage2 only — middle pedal. |
| 10041 | `rp-kpent` | `Kp\nEnter` | Advantage2 only — right pedal. |
| 10011 | Value/token `{ }` (multi `' '`) | (empty) | "Different press and release" marker. |

### 3.10 Layer shift/toggle keys (all devices)

| Code | Token | Display |
|---|---|---|
| 10014 | `` (empty) | `Key-\npad` |
| 10016 | `kpshft` | `Kp\nShift` |
| 11163 | `defs` | `Base\nShift` |
| 11164 | `deft` | `Base\nToggle` |
| 11165 | `keys` | `Kp\nShift` |
| 11166 | `keyt` | `Kp\nToggle` |
| 11201 | `lfn` | `Left Fn\nShift` |
| 11202 | `rfn` | `Right Fn\nShift` |
| 11167 | `fn1s` | `Fn1\nShift` |
| 11168 | `fn1t` | `Fn1\nToggle` |
| 11169 | `fn2s` | `Fn2\nShift` |
| 11170 | `fn2t` | `Fn2\nToggle` |
| 11171 | `fn3s` | `Fn3\nShift` |
| 11172 | `fn3t` | `Fn3\nToggle` |

Note: the token `kpshft` is shared by the generic keypad-shift key (10016) and the Advantage2 keypad-layer variant (10048); on Advantage2 the keypad-exception rules (§5.4) resolve the keypad-layer variant explicitly.

### 3.11 Profiles and hotkeys (all devices)

| Code | Token | Display |
|---|---|---|
| 11174..11183 | `pro0`..`pro9` | `Profile 0`..`Profile 9` |
| 10071 | `hk0` | `' '` (blank; list name `hotkey 0`) |
| 10024..10031 | `hk1`..`hk8` | `' '` (blank; list name `hotkey 1..8`) |
| 10032 | `hk9` | RGB/TKO: `Fn\nShift`; all others: `Fn\nToggle` |
| 10033 | `hk10` | FS Edge/RGB/TKO: ☀ (9728) or `LED` fallback; others: `PC\nMenu` |

### 3.12 Macro speed and timing-delay pseudo-keys (all devices)

All are hidden from key search and have no display text.

| Code | Token | Notes |
|---|---|---|
| 10005 | `speed1` | `WriteDownUp = False`. Slow output. |
| 10006 | `speed3` | Default output. |
| 10012 | `speed5` | Fast output. |
| 11192..11200 | `s1`..`s9` | Per-macro speed markers. |
| 10007 | `d125` | Not available on RGB/TKO. `WriteDownUp = False`. |
| 10008 | `d500` | Same restriction. |
| 10087 | `dran` | Random delay. |
| 10086..11084 | `d001`..`d999` | Precise delays 1..999 ms; token is `'d'` + the delay zero-padded to three digits. |

### 3.13 TKO edge-lighting zones

| Code | Token |
|---|---|
| 11113..11121 | `L1`..`L9` |
| 11122..11136 | `B1`..`B15` |
| 11137..11145 | `R1`..`R9` |

All hidden from search, no display text. These are lighting zones, not typing keys; they populate the layer's edge-key list (§4.4). The entries are registered for all devices but only the TKO uses them.

### 3.14 Pedal position tokens (Savant Elite2 / FS pedals)

Not entries in the key table — plain string tokens used by the pedal app: `lpedal`, `mpedal`, `rpedal`, `jack1`..`jack4`, with bracketed single (`[lpedal]`) and multi (`{lpedal}`) forms. Pedal positions: none, left, middle, right, jack 1–4.

---

## 4. Per-device physical layouts (layer definitions)

Every layer lists its key positions in ascending index order; each position carries a default action (and, where noted, a distinct position token). Tokens below are the device family's file tokens. Markers: `*` = macro assignment not allowed (still remappable); `†` = locked (not remappable); `(pos:x)` = distinct position token.

### 4.1 FS Edge — top and bottom layers

95 positions (0–94), two layers. Top: layer index 0, name `Qwerty-top`; bottom: layer index 1, name `Qwerty-keypad`; both QWERTY layout type.

Top layer, in index order:

```
0 esc | 1-12 F1..F12 | 13 prnt | 14 scrlk | 15 pause | 16 del
17 hk1 | 18 hk2
19 ` | 20-29 1 2 3 4 5 6 7 8 9 0 | 30 hyph | 31 = | 32 bspc | 33 home
34 hk3 | 35 hk4
36 tab | 37-46 q w e r t y u i o p | 47 obrk | 48 cbrk | 49 \ | 50 end
51 hk5 | 52 hk6
53 caps | 54-62 a s d f g h j k l | 63 colon | 64 apos | 65 ent | 66 pup
67 hk7 | 68 hk8
69 lshft* | 70-76 z x c v b n m | 77 com | 78 per | 79 / | 80 rshft* | 81 up | 82 pdn
83 hk9 | 84 hk10
85 lctrl* | 86 lwin* | 87 lalt* | 88 lspc | 89 rspc | 90 ralt* | 91 rctrl* | 92 lft | 93 dwn | 94 rght
```

Bottom layer differs only at indices 1–6 and 15: `1 mute, 2 vol-, 3 vol+, 4 play, 5 prev, 6 next` (media row replaces F1–F6), `13 prnt, 14 scrlk, 15 ins, 16 del`. Hotkeys `hk1..hk10` appear in both layers at the same indices.

### 4.2 FS Edge RGB — top and bottom layers

Same 95-position shape as FS Edge but index 0 is the extra `hk0` key, shifting the top row: top = `0 hk0, 1 esc, 2-13 F1..F12, 14 prnt, 15 pause, 16 del` (there is **no** Scroll Lock position on the RGB top layer). Bottom = `0 hk0, 1 esc, 2 mute, 3 vol-, 4 vol+, 5 play, 6 prev, 7 next, 8-13 F7..F12, 14 prnt, 15 ins, 16 scrlk`. Indices 17–94 match FS Edge exactly.

### 4.3 FS Pro — top and bottom layers

Top layer is identical to FS Edge top (indices 0–94). The bottom (`Qwerty-keypad`) layer overlays an embedded numeric keypad and media row:

```
0 esc | 1 mute | 2 vol- | 3 vol+ | 4 play | 5 prev | 6 next | 7-12 F7..F12
13 prnt | 14 numlk | 15 ins | 16 del
17 hk1 | 18 hk2
19 ` | 20-25 1..6 | 26 kp7 | 27 kp8 | 28 kp9 | 29 0 | 30 kp* | 31 = | 32 bspc | 33 home
34 hk3 | 35 hk4
36 tab | 37-42 q w e r t y | 43 kp4 | 44 kp5 | 45 kp6 | 46 kp- | 47 obrk | 48 cbrk | 49 \ | 50 end
51 hk5 | 52 hk6
53 caps | 54-59 a s d f g h | 60 kp1 | 61 kp2 | 62 kp3 | 63 kp+ | 64 apos | 65 kpent | 66 pup
67 hk7 | 68 hk8
69 lshft* | 70-75 z x c v b n | 76 kp0 | 77 com | 78 kp. | 79 kp/ | 80 rshft* | 81 up | 82 pdn
83 hk9 | 84 hk10
85-94 same bottom row as FS Edge
```

### 4.4 TKO — top and bottom layers

63 key positions (0–62) plus 33 edge zones. Top layer (`Qwerty-top`):

```
0 esc | 1-10 1..0 | 11 hyph | 12 = | 13 bspc
14 tab | 15-24 q w e r t y u i o p | 25 obrk | 26 cbrk | 27 \
28 caps | 29-37 a s d f g h j k l | 38 colon | 39 apos | 40 ent
41 lshft* | 42-48 z x c v b n m | 49 com | 50 per | 51 / | 52 rshft*
53 lctrl* | 54 lwin* | 55 lalt* | 56 lspc | 57 mspc | 58 rspc | 59 ralt*
60 fnshf | 61 ss† | 62 rctrl*
```

Index 60 (`fnshf`) is remappable **and** macro-capable (unlike the other modifier positions); index 61 is the SmartSet key, fully locked.

Bottom layer (`Qwerty-keypad`) — embedded Fn actions:

```
0 ` | 1-12 F1..F12 | 13 del
14 tab | 15 lmous | 16 play | 17 prev | 18 next | 19 LED | 20 ins | 21 calc | 22 up | 23 pause | 24 pup | 25 home | 26 prnt | 27 \
28 caps | 29 rmous | 30 mute | 31 vol- | 32 vol+ | 33 menu | 34 scrlk | 35 lft | 36 dwn | 37 rght | 38 pdn | 39 end | 40 ent
41-62 same as top (z-row and bottom row)
```

Both TKO layers also populate the edge-key list (indices 0–32): `L1..L9` at 0–8, `B1..B15` at 9–23, `R1..R9` at 24–32.

### 4.5 Advantage2 — QWERTY top and bottom layers

89 positions (0–88). Top (`Qwerty-top`, QWERTY layout type):

```
0 escape | 1-12 F1..F12 | 13 prtscr | 14 scroll | 15 pause | 16 (Keypad)† | 17 (Program)†
18 = | 19-28 1..0 | 29 hyphen
30 tab | 31-40 q w e r t y u i o p | 41 \
42 caps | 43-47 a s d f g | 48 lctrl* | 49 lalt* | 50 rwin* | 51 rctrl* | 52 h | 53 j | 54 k | 55 l | 56 ; | 57 '
58 lshift* | 59 z | 60 x | 61 c | 62 v | 63 b
64 bspace | 65 delete | 66 home | 67 pup | 68 enter | 69 space
70 n | 71 m | 72 , | 73 . | 74 / | 75 rshift*
76 ` | 77 intl-\ | 78 left | 79 right | 80 end | 81 pdown | 82 up | 83 down | 84 obrack | 85 cbrack
86-88 foot pedals
```

Indices 16 and 17 are the physical `Keypad` and `Program` buttons — never remappable. Indices 86–88: when running inside the combined Kinesis master application they are `tab`, `kpshft` (keypad-layer code 10048), `kpenter`; otherwise they are the pedal tokens `lp-tab`, `mp-kpshf`, `rp-kpent`.

Bottom (`Qwerty-keypad`) — the embedded keypad layer; differences from top:

```
1 lwin | 2 ralt | 3 menu (10043) | 4 play | 5 prev | 6 next | 7 calc (10047) | 8 kpshft (10048)
9-12 F9..F12 | 13 mute | 14 vol- | 15 vol+
25 numlk (10052) | 26 kp= (10053) | 27 kp/ (10054) | 28 kp* (10055)
37 kp7 | 38 kp8 | 39 kp9 | 40 kp- (10066)
53 kp4 | 54 kp5 | 55 kp6 | 56 kp+ (10067)
69 kp0 | 71 kp1 | 72 kp2 | 73 kp3 | 74 kpenter1
77 insert | 84 kp. (10070) | 85 kpenter2
86-88 lp-tab | mp-kpshf | rp-kpent  (always the pedal tokens here)
```

All keypad-layer duplicates use the dedicated legacy internal codes (10043..10070) so the same logical action can exist independently on both layers.

### 4.6 Advantage2 Dvorak — top and bottom layers

Layer names `Dvorak-top` / `Dvorak-keypad`, Dvorak layout type. Same physical indices as the QWERTY pair with the alpha/punctuation ring rearranged to Dvorak:

- Row 31–41: `' , . p y f g c r l /` (top) — bottom keeps `kp7 kp8 kp9 kp-` at 37–40 and ends `/` at 41.
- Row 43–47: `a o e u i`; 52–57: `d h t n s \` (bottom: `d kp4 kp5 kp6 kp+ \`).
- Row 59–63: `; q j k x`; 70–74: `b m w v z` (bottom: `kp0/b kp1 kp2 kp3 kpenter1` at 69–74).

### 4.7 Advantage360 — five layers

77 positions (0–76). Layers: Base (index 0), Keypad (1), Fn1 (2), Fn2 (3), Fn3 (4). Layout-file layer headers use `<` `>` delimiters with names `<base>`, `<keypad>`, `<function1>`, `<function2>`, `<function3>`.

Base layer:

```
0 eql | 1-5 1 2 3 4 5 | 6 keyt (pos:kp) | 7 ss† | 8 6 | 9 7 | 10 8 | 11 9 | 12 0 | 13 hyph
14 tab | 15-19 q w e r t | 20 hk1 | 21 hk3 | 22 y | 23 u | 24 i | 25 o | 26 p | 27 bsls
28 esc | 29-33 a s d f g | 34 hk2 | 35 hk4 | 36 h | 37 j | 38 k | 39 l | 40 scol | 41 apos
42 lshf | 43-47 z x c v b | 48 n | 49 m | 50 comm | 51 perd | 52 fsls | 53 rshf
54 fn1s (pos:lfn) | 55 grav | 56 caps | 57 left | 58 rght | 59 up | 60 down | 61 obrk | 62 cbrk | 63 fn1s (pos:rfn)
64 lctr | 65 lalt/lopt | 66 rwin | 67 rctr          (thumb-cluster modifiers)
68 bspc | 69 del | 70 home | 71 pgup | 72 ent | 73 spc   (thumb keys)
74 end | 75 pgdn | 76 null (pos:pedl)
```

Notes:
- Index 6 is the keypad-layer toggle: default action `keyt`, position token `kp`.
- Indices 54 and 63 are the left/right Fn keys: default action `fn1s` (Fn1 shift) with position tokens `lfn` / `rfn`.
- Index 76 is the pedal jack: default action `null`, position token `pedl`.
- Index 7 is the SmartSet key (locked).
- Unlike Gen1 devices, Adv360 modifier positions are remappable and macro-capable.

Keypad layer — same skeleton; differing defaults (all with letter/number position tokens so the file token stays the physical position):

```
6 deft (Base Toggle) | 9 nmlk (pos:7) | 10 kp= (pos:8) | 11 kp/ (pos:9) | 12 kp* (pos:0)
23 kp7 (pos:u) | 24 kp8 (pos:i) | 25 kp9 (pos:o) | 26 kp- (pos:p)
37 kp4 (pos:j) | 38 kp5 (pos:k) | 39 kp6 (pos:l) | 40 kp+ (pos:scol)
49 kp1 (pos:m) | 50 kp2 (pos:comm) | 51 kp3 (pos:perd) | 52 kpen (pos:fsls)
61 kp. (pos:obrk) | 73 kp0 (pos:spc)
54/63 fn1s (pos:lfn/rfn) — unchanged
```

Fn1 layer — number row becomes function keys, Fn keys become Base Shift:

```
0-5 F1..F6 (pos: eql,1,2,3,4,5) | 6 keyt (pos:kp) | 8-13 F7..F12 (pos: 6,7,8,9,0,hyph)
54 defs (pos:lfn) | 63 defs (pos:rfn)
all other indices identical to Base
```

**Fn2 and Fn3 layers are identical to Fn1** apart from their layer index and name.

---

## 5. Special key semantics

### 5.1 Modifier representation

Modifier state on a key is a string of two-character codes:

| Code | Meaning |
|---|---|
| `'S '` | generic Shift |
| `'LS'` | Left Shift |
| `'RS'` | Right Shift |
| `'C '` | generic Ctrl |
| `'LC'` | Left Ctrl |
| `'RC'` | Right Ctrl |
| `'A '` | generic Alt |
| `'LA'` | Left Alt |
| `'RA'` | Right Alt |
| `'W '` | generic Win |
| `'LW'` | Left Win |
| `'RW'` | Right Win |

Note the trailing space in the generic codes — substring tests rely on it to distinguish `'S '` from `'LS'`/`'RS'`.

- Each modifier key code maps to its two-character code; the codes of all active modifiers are joined with `','`. During macro capture each new keystroke receives the modifier string of the currently held modifiers.
- Converting a modifier string back into keys maps generic Shift to the generic Shift key, `'LS'` to Left Shift, and so on; `'W '` and `'LW'` both map to Left Win, `'RW'` to Right Win. A modifier string can also be rendered as `token+token+…` using each modifier key's primary value.
- The modifier key set is: `VK_MENU, VK_LMENU, VK_RMENU, VK_SHIFT, VK_LSHIFT, VK_RSHIFT, VK_CONTROL, VK_LCONTROL, VK_RCONTROL, VK_LWIN, VK_RWIN` (standard Windows codes).
- The active-modifier set is deduplicated by key code.
- A modifier string is attached to a copied key on capture — but **never** onto a key that is itself a modifier.
- Counting distinct modifier codes in a string feeds keystroke counting (each modifier counts as an extra keystroke).
- Macro co-triggers: up to four modifier keys stored on the macro (`CoTrigger1..4`); their display names are joined with `' + '` for co-trigger text.

### 5.2 Shift / AltGr handling and display text

- A key counts as "shifted" when its modifier string is exactly `'S '`, `'LS'`, or `'RS'`.
- A key counts as "AltGr" when its modifier string contains both the Ctrl and Alt codes **and** is exactly one Ctrl+Alt pair, nothing else.
- Rendering a key's text:
  - Windows: if the key is layout-convertible, the caption is resolved through the OS `ToUnicode` translation for the current keyboard layout (with synthetic Shift/AltGr state; the translation is invoked twice to flush dead keys); otherwise the stored default/value is used.
  - macOS: shifted keys use `ShiftedValue`, otherwise the default/value (no OS translation).
  - The result is then prefixed with `+` (key-up) or `-` (key-down) when the keystroke has a direction.
- Macro text rendering: shifted keys render their shifted glyph; modified non-modifier keys render `{mods+key}` unless an AltGr glyph exists (Windows), in which case the single AltGr character is shown; plain keys with a `MultiValue` print it bare; everything else prints `{token}`.

### 5.3 Non-remappable and macro-restricted keys

- Never remappable: the Advantage2 `Keypad` and `Program` buttons; the SmartSet key `ss` on TKO and Adv360.
- Remap allowed but macro not allowed: all physical modifier positions on FS Edge/RGB/FS Pro/TKO/Advantage2 (`lshft/rshft/lctrl/rctrl/lwin/rwin/lalt/ralt`). The TKO `fnshf` position is an exception (both remap and macro allowed). Adv360 places no such restriction on its modifiers.
- The Fn1 layer-shift key (`fn1s`) and the keypad layer-toggle key (`keyt`) always act/report as their original layer-switch action even when "modified".

### 5.4 Keypad-layer prefixes and the Advantage2 keypad exceptions

Layer targeting in layout files is done with textual prefixes: `'kp-'` (Advantage2), `'fn '` (FS Edge/FS Pro/RGB/TKO) — both three characters — and the profile-file layer header prefix `'LAYER='`.

- On save, Advantage2 writes bottom-layer lines with prefix `'kp-'` **except** for tokens in the keypad-exception list below, which are inherently keypad-only and get no prefix. The FS-family/RGB/TKO/Adv360 flat format uses the `'fn '` prefix for bottom-layer lines.
- On load, a leading `'kp-'` (or a keypad-exception token) routes the line to the bottom layer and the prefix is stripped; a leading `'fn '` does the analogous thing in the FS formats.

Keypad-exception token list (exact): `menu, play, prev, next, calc, kpshft, mute, vol-, vol+, kp0..kp9, numlk, kp=, kpdiv, kpmult, kpmin, kpplus, kpenter1, kpenter2, kp., kp-insert`.

These tokens map to the dedicated Advantage2 keypad-layer codes:

| Token | Legacy code | Token | Legacy code |
|---|---|---|---|
| `menu` | 10043 | `kp0`..`kp9` | 10056..10065 |
| `play` | 10044 | `kpmin` | 10066 |
| `prev` | 10045 | `kpplus` | 10067 |
| `next` | 10046 | `kpenter1` | 10068 |
| `calc` | 10047 | `kpenter2` | 10069 |
| `kpshft` | 10048 | `kp.` | 10070 |
| `mute` | 10049 | `insert` | `VK_INSERT` (standard, $2D) |
| `vol-` | 10050 | `numlk` | 10052 |
| `vol+` | 10051 | `kp=` / `kpdiv` / `kpmult` | 10053 / 10054 / 10055 |
| anything else | (not a keypad exception) | | |

(The `'kp-insert'` exception is first stripped of its `'kp-'` prefix, leaving `'insert'`.)

### 5.5 Lighting-file load/save exceptions

Used when reading/writing per-key LED color files for the FS-family bottom layer, where the physical position hosts a media action instead of the top-layer key. Only applies to the bottom layer:

| Load: file token (top-layer key) → in-memory key | Save: in-memory key → file token |
|---|---|
| `VK_F1` → `VK_VOLUME_MUTE` | `VK_VOLUME_MUTE` → `VK_F1` |
| `VK_F2` → `VK_VOLUME_DOWN` | `VK_VOLUME_DOWN` → `VK_F2` |
| `VK_F3` → `VK_VOLUME_UP` | `VK_VOLUME_UP` → `VK_F3` |
| `VK_F4` → `VK_MEDIA_PLAY_PAUSE` | `VK_MEDIA_PLAY_PAUSE` → `VK_F4` |
| `VK_F5` → `VK_MEDIA_PREV_TRACK` | `VK_MEDIA_PREV_TRACK` → `VK_F5` |
| `VK_F6` → `VK_MEDIA_NEXT_TRACK` | `VK_MEDIA_NEXT_TRACK` → `VK_F6` |
| `VK_PAUSE` → `VK_INSERT` | `VK_INSERT` → `VK_PAUSE` |
| `VK_DELETE` → `VK_SCROLL` | `VK_SCROLL` → `VK_DELETE` |

### 5.6 Tap-and-hold

Stored per key position (`TapAction`, `HoldAction`, `TimingDelay`, `TapAndHold`). Both actions must resolve to valid table entries; failure of either resets the assignment. Defaults/limits: delay 250 ms, bounds 1..999 ms, at most 10 tap-and-hold keys. File syntax marker: `t&h` — a single-key line of the form `[pos]>[tap][t&h<delay>][hold]`. Loading recognizes tap-and-hold when a single-key line contains `[t&h`.

### 5.7 Multimodifiers (Adv360)

A key position can hold a 4-letter multi-modifier combo token; accepted values (case-insensitive) are exactly: `[caws]`, `[cawx]`, `[cxws]`, `[caxs]`, `[xaws]`, `[caxx]`, `[cxwx]`, `[cxxs]`, `[xawx]`, `[xaxs]`, `[xxws]` — positional letters c=Ctrl, a=Alt, w=Win, s=Shift, with `x` marking an omitted modifier. Saved as `[pos]>[combo]`.

### 5.8 Macro/up-down keystroke details

- Up/Down keystrokes: a requested key-up/key-down direction is applied only when the key has no modifiers or is itself a modifier; output text prefixes `+`/`-` (§5.2). Macro speed text literals: `'speed'` (Legacy dialect), `'s'` (Gen1/Gen2 per-macro speed), `'x'` (repeat marker), `'Global'` (global-speed label).
- Limits: 3 macros per key on FS devices; maximum macros per layout: 24 (FS), 100 (RGB), 100 (Adv360); maximum keystrokes per macro: 300 (FS), 500 (Adv360); total keystroke budget 7200 (FS/RGB).

### 5.9 File-token bracket constants

For reference (the full grammar belongs to the file-format document): `[` `]` delimit single keys, `{` `}` delimit multi/macro keys, `>` separates position from assignment. Save paths always emit the key's file token (`SaveValue`) inside the brackets.

---

## 6. International / non-US handling

- **Layout-aware rendering (Windows):** letters, digits, and the layout-dependent punctuation keys are marked layout-convertible, so their captions are produced via the OS `ToUnicode` translation for the *current* keyboard layout, including Shift and AltGr states. AltGr display substitution also applies in macro text rendering. macOS has no equivalent — it falls back to the stored value/shifted value.
- **Current layout tracking:** the current keyboard-layout name is cached from the OS (Windows), defaulting to `'00000409'` (US English).
- **US-English canonicalization (Pedal app, Windows only):** when the active layout is not `'00000409'`, saving converts the pressed key to its US-English equivalent via scan-code mapping, and loading converts back to the active layout; the token written is the mapped key's file token. This keeps pedal layout files layout-independent (positional).
- **International key:** `VK_OEM_102` (the ISO `<>` key between Left Shift and Z) has dedicated tokens `intl-\` / `intl\` / `int#` per dialect (§3.2) and appears as a physical position only on the Advantage2 layers (index 77, top layers).
- **Unicode capability gate:** glyph captions are used only on Windows 10 and later; on older Windows, media/LED keys get plain-text captions (§3.7).

---

## 7. Reconstruction notes

1. The key token table is *the* single source of truth linking numeric codes, file tokens, and captions; everything else (layer definitions, file converters, macro rendering) resolves through it. Duplicated numeric registrations (e.g. `numlk` under both the standard Num Lock code and keypad-layer code 10052) are intentional: numeric lookup returns the first match, token lookup returns the first file-token match (case-insensitive), and the Advantage2 keypad-exception rules (§5.4) disambiguate the keypad-layer variants.
2. Layer content is immutable after construction — remaps/macros/tap-hold are stored *on* the key-position objects (`ModifiedKey`, `Macro1..5`, `TapAction`/`HoldAction`), never by replacing list entries.
3. `PositionKey` decouples "what the key does by default" from "how the position is named in files"; it only differs from `OriginalKey` on the Adv360 (keypad/Fn layers, thumb Fn keys, pedal jack).
4. `Index` values are GUI button ids and file-ordering keys; they are dense (0..N-1) and unique within a layer, and identical positions share the same index across a device's layers.
