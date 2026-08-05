# 04 — Layout File Format (Remaps, Tap-and-Hold, Multi-Modifiers, Layers)

Scope: the on-disk text format of SmartSet layout files (`layout*.txt`, `qwerty.txt`, `dvorak.txt`) and the exact parse/serialize rules for key remaps, tap-and-hold, and multi-modifier rules; macro lines are summarized here and specified fully in `06-macros.md`.

---

## 1. Files, encoding, and general structure

### 1.1 File locations and names

| Device family | Layout file(s) | Folder |
|---|---|---|
| FS Edge, FS Pro, FS Edge RGB, TKO | `layout1.txt` … `layout9.txt` (numbered 1–9) | `layouts` folder on the keyboard's v-Drive |
| Advantage2 | `qwerty.txt` / `dvorak.txt` — the file name must contain `qwerty` or `dvorak` | `active` folder on the v-Drive |
| Advantage360 (Gen2) | `layout*.txt` (same numbered naming as the RGB family) | `layouts` folder on the v-Drive |

### 1.2 Encoding and line handling

- Files are read and written line by line as plain text. There is no BOM and no encoding conversion; all tokens are plain ASCII. Line endings on write are the platform default.
- On parse, every line is lowercased and trailing whitespace is trimmed; parsing is therefore **case-insensitive** (token lookup lowercases both sides). The serializer emits each key's file token with its canonical casing (e.g. `F1`…`F24` and `LED` are written with capitals, everything else lowercase).
- **One rule per line.** There is **no comment syntax**. Blank lines are ignored on load and not preserved on save. Any non-blank line that does not parse is an *invalid line* (see section 5).

### 1.3 Line grammar

A rule line is `input>output`:

- The separator is `>`; the parser splits on the **first** `>` in the line.
- Bracket style selects the rule type:
  - `[` / `]` — "single key" rules: remap, tap-and-hold, multi-modifier.
  - `{` / `}` — macro rules (see `06-macros.md`).
- Rule-type detection (Gen1 RGB/TKO and Gen2 parser):
  - *single key* — the config (left) side contains `[` or `]`.
  - *macro* — the config side contains `{` or `}`.
  - *tap-and-hold* — a single-key rule whose value side contains the literal `[t&h`.
  - *multi-modifier* — a single-key rule whose value side is one of the 11 multi-modifier codes (see 2.3).
  - A line is considered for parsing only if it contains a `>` and one of these types matches; otherwise the whole line is flagged invalid.
- The older FS and Adv2 parsers additionally require the line to **start** with `[` or `{`.

Layout-file sniffing: an arbitrary text file is recognized as a layout file if any line (after stripping an `fn ` prefix) contains a `>` and starts with `[` or `{`, or is a tap-and-hold line.

### 1.4 Key tokens

Every token between brackets is looked up (case-insensitively) in the application's key table by its file token. Token spellings differ between the *old* apps (Adv2/Pedal), the *newer Gen1* apps (FS Edge/Pro, RGB, TKO), and *Gen2* (Adv360). Representative differences:

| Meaning | Adv2 / Pedal token | FS/RGB/TKO token | Adv360 token |
|---|---|---|---|
| Escape | `escape` | `esc` | `esc` |
| Space | `space` | `spc` | `spc` |
| Enter | `enter` | `ent` | `ent` |
| Backspace | `bspace` | `bspc` | `bspc` |
| Delete | `delete` | `del` | `del` |
| Left Shift | `lshift` | `lshft` | `lshf` |
| Right Shift | `rshift` | `rshft` | `rshf` |
| Left Ctrl | `lctrl` | `lctrl` | `lctr` |
| Hyphen | `hyphen` | `hyph` | `hyph` |
| Open bracket | `obrack` | `obrk` | `obrk` |
| Semicolon | `;` | `colon` | `scol` |
| Period | `.` | `per` | `perd` |
| Page Up / Down | `pup` / `pdown` | `pup` / `pdn` | `pgup` / `pgdn` |
| Left mouse click | `lmouse` | `lmous` | `lmou` |
| Keypad shift/toggle | `kpshift`/`kptoggle` | `kpshft`/`kptoggle` | `keys`/`keyt` |

Common tokens across devices include `tab`, `caps`, `home`, `end`, `F1`…`F24`, `a`…`z`, `0`…`9`, `kp0`…`kp9`, `kp/` (`kpdiv`), `kp*`, `kp-`, `kp+`, `kp.`, `kp=`, media (`mute`, `vol-`, `vol+`, `play`, `prev`, `next`, `stop`, `fwrd`, `rewd`, `cpau`, `ejct`, `recr`), `LED`, `led+`, `led-`, `null`, hotkeys `hk0`…`hk10`, FS split-space bars `lspc` / `rspc` / `mspc`. Adv360 layer keys: `defs`, `deft`, `keys`, `keyt`, `lfn`, `rfn`, `fn1s`, `fn1t`, `fn2s`, `fn2t`, `fn3s`, `fn3t`; profile keys `pro0`…`pro9`; RGB hotkeys `hk0`…`hk10`.

---

## 2. Single-key rules

### 2.1 Remap: `[position]>[action]`

Real examples:

```
[F1]>[a]
[caps]>[lwin]
[hyph]>[obrk]
```

- **Input** (left of `>`): the *position token* of a physical key of the target layer. Validity requires the config side to start with `[` and contain `]`, and the position to exist on the addressed layer.
- **Output**: any single token from the key table. Applying a remap marks the position as modified with the new output — **unless** the output equals the key's original action (remapping a key to itself clears the remap) or the key is not editable.
- **Duplicate lines**: each line simply overwrites the previous output, so for the same position **the last line wins**.
- An empty or unknown output token makes the value side invalid.

### 2.2 Tap-and-hold: `[position]>[tap][t&hNNN][hold]`

Serialized exactly as:

```
[layer prefix][position]>[tap][t&hNNN][hold]
```

where `NNN` is the hold-threshold delay in milliseconds. E.g. a Caps key that taps `a` and holds `lctrl` with a 250 ms threshold is `[caps]>[a][t&h250][lctrl]`.

- Parse: three `[..]` groups are consumed left to right; group 1 = the tap action, group 2 = the `t&h` token followed by the delay integer, group 3 = the hold action. The Gen1 RGB-family parser marks the line invalid if the delay is missing or negative; the older FS and Adv2 parsers substitute the default of 250 ms for an unparseable delay.
- Delay range enforced by the UI: 1–999 ms.
- Limit: at most 10 tap-and-hold actions per layout. Tap-and-hold is also refused on the same key in both layers, on macro trigger keys, and on `A–Z`/`0–9` of the top layer.

### 2.3 Multi-modifier: `[position]>[caws]`

A remap whose output is a *combination of modifiers*. The value token is a fixed 4-character code in the order Ctrl, Alt, Win, Shift with `x` as placeholder; exactly these 11 codes (all combinations of 2+ modifiers) are accepted:

```
[caws] [cawx] [cxws] [caxs] [xaws] [caxx] [cxwx] [cxxs] [xawx] [xaxs] [xxws]
```

- Code construction: `c`/`x` (Ctrl) + `a`/`x` (Alt) + `w`/`x` (Win) + `s`/`x` (Shift).
- On load the raw code string is stored with the key. On save it is written back as `[layer prefix][position]>[code]`. Multi-modifier lines are written by the RGB-family serializer; the legacy FS app does not emit them.
- A single modifier is not a multi-modifier code; it is an ordinary remap (e.g. `[caps]>[lwin]`).

---

## 3. Layer encoding (per device family)

### 3.1 FS Edge / FS Pro / RGB / TKO — `fn ` line prefix

Two layers exist: the top (default) layer and the bottom (Fn) layer. The bottom layer is encoded by prefixing the whole line with `fn ` (three characters including the trailing space):

```
fn [4]>[LED]
fn [e]>[null]
```

- A line carrying the `fn ` prefix targets the bottom layer; the 3-character prefix is stripped before parsing. Lines without `fn ` target the top layer.
- On save the serializer prepends `fn ` to every bottom-layer line.

### 3.2 Advantage2 — `kp-` token prefix

The Fn (keypad) layer is encoded **inside** the bracket, per token, with the 3-character prefix `kp-`:

```
[kp-w]>[b]
```

- Parse: if the position token contains `kp-` **or** is one of the keypad-exception tokens, the rule targets the bottom (keypad) layer and the `kp-` prefix is stripped before lookup.
- Keypad-exception tokens are bottom-layer tokens that carry *no* `kp-` prefix; the exact list: `menu play prev next calc kpshft mute vol- vol+ kp0…kp9 numlk kp= kpdiv kpmult kpmin kpplus kpenter1 kpenter2 kp. kp-insert`. These denote keypad-layer-only actions with their own Adv2-specific key identities.
- Save: the serializer prepends `kp-` inside the bracket for bottom-layer keys, except for keypad-exception tokens.

### 3.3 Advantage360 (Gen2) — layer header lines

Five layers: Base, Keypad, Fn1, Fn2, Fn3. The file is partitioned by standalone header lines; every subsequent rule belongs to the last header seen. Header tokens are exact-match:

| Header line | Layer |
|---|---|
| `<base>` | Base |
| `<keypad>` | Keypad |
| `<function1>` | Fn1 |
| `<function2>` | Fn2 |
| `<function3>` | Fn3 |

- Parse: a line that starts with `<` and ends with `>` switches the current layer; an unknown `<...>` header is an invalid line. Before the first header, the current layer is Base.
- Save: the serializer emits the header line before each layer's rules. No `fn ` prefixes are used on Gen2.

### 3.4 Edge-lighting lines (lighting files, not layout files)

TKO **edge-lighting** lines belong to lighting (`led*.txt`) files, never to layout files: lines starting with `[l1]`…`[l30]`, `[r1]`…`[r30]`, `[b1]`…`[b30]` or the edge lighting modes `[mono_edge] [breathe_edge] [spectrum_edge] [wave_edge] [frozenwave_edge] [rebound_edge] [loop_edge] [pulse_edge]`. The lighting loader extracts those lines from the LED content and parses them separately.

Similarly, a load/save token translation applies only to **LED (lighting) files**, not layout files: on the bottom (`fn`) layer, function-row position tokens are translated to the media keys those positions become on the Fn layer:

| Load (file token → key) | Save (key → file token) |
|---|---|
| F1 → Volume Mute | Volume Mute → F1 |
| F2 → Volume Down | Volume Down → F2 |
| F3 → Volume Up | Volume Up → F3 |
| F4 → Play/Pause | Play/Pause → F4 |
| F5 → Previous Track | Previous Track → F5 |
| F6 → Next Track | Next Track → F6 |
| Pause → Insert | Insert → Pause |
| Delete → Scroll Lock | Scroll Lock → Delete |

---

## 4. Load and save flow

### 4.1 Format dialects per app

| App | Dialect |
|---|---|
| FS Edge / FS Pro | FS dialect: `fn ` layer prefix; one co-trigger and three macro slots persisted per key |
| RGB / TKO | Gen1 RGB dialect: `fn ` layer prefix; full line-validity tracking; up to five macro lines per key |
| Advantage360 | Gen2 dialect: `<...>` layer headers; flat macro list |
| Advantage2 | Adv2 dialect: `kp-` token prefix; `{speedN}` macro speed syntax; three macro slots and three co-triggers persisted |
| SE2 pedal | Pedal dialect with position tokens `lpedal`, `mpedal`, `rpedal`, `jack1`…`jack4` (e.g. `[lpedal]>`, `[jack1]>[d]`) |

### 4.2 Load semantics (Gen1 RGB-family and Gen2 parser)

1. Loading wipes the entire in-memory layout, macro list, and tracked lines before parsing — the file fully replaces the model.
2. Each line is lowercased/trimmed, its layer determined (3.1/3.3 above), and recorded with its line number, layer, and original text for validity tracking.
3. Remap/tap-and-hold/multi-modifier rules are applied to the addressed key of the addressed layer as described in section 2. Macro rules are applied per `06-macros.md`.
4. Layer targeting for remaps uses the position token. Macro triggers instead match the key's *trigger identity*: normally the position key, but for the Fn1 layer-shift and keypad layer-toggle keys the original key identity is used. On Gen2, macro triggers are matched by original key across **all** layers and the macro's layer comes from the current header.
5. Duplicates: remaps overwrite (last wins); each macro line fills the first empty of the trigger key's five macro slots, so at most 5 macro lines per trigger key are retained (Gen2 instead appends every macro to the flat macro list).

The FS and Adv2 parsers implement the same model without line-validity tracking, and with the per-family layer encodings of section 3.

### 4.3 Save semantics

Saving **regenerates the entire file from the in-memory model**; nothing from the old file survives except explicitly kept invalid lines (section 5). Unmodified keys produce no lines. Order for the RGB-family/Gen2 serializer:

1. For each layer in list order (top then bottom; Base→Fn3 on Adv360, with the `<...>` header line first on Gen2):
2. For each key in physical index order:
   - tap-and-hold line, **else** multi-modifier line, **else** remap line if the key is modified;
   - then (non-Gen2) up to 5 macro lines.
3. Gen2 only: all macros of the flat list whose layer matches the current layer.
4. Kept invalid lines of that layer are appended verbatim. Kept invalid lines must be preserved exactly as loaded, including any `fn ` layer prefix.

The FS serializer writes only macro slots 1–3 per key and only the first co-trigger; the Adv2 serializer writes macro slots 1–3 and co-triggers 1–3, with `kp-` prefixes per 3.2.

---

## 5. Validation and invalid-line handling

### 5.1 What makes a line invalid (Gen1 RGB-family/Gen2 parser only)

Each parsed line is decomposed into segments, each flagged valid or invalid; a line is valid only if **all** its segments are valid. Invalidity causes:

- No `>` separator, or neither `[` nor `{` present → entire line invalid.
- Config side does not start with `[` (or `{` for macros) or lacks the closing bracket.
- Position/trigger token unknown, or not present on the addressed layer.
- Unknown output token in a remap; missing tap/hold action or delay in tap-and-hold.
- Unknown token inside a macro value.
- Unknown Adv360 `<...>` header.

### 5.2 Reporting and preservation

- After load (and after save), the app checks whether any tracked line is invalid and not yet removed; in the legacy apps only the Adv360 app surfaces the resulting dialog.
- The dialog lists each invalid line read-only, coloring the invalid segments red. A checkbox per line marks the line to be kept.
- On save, an invalid line is emitted verbatim only if it is marked kept and has not been removed; otherwise it is permanently dropped. The keep flag defaults to off, so **unchecked invalid lines are silently discarded on the first save**.

### 5.3 Limits

| Limit | Value | Meaning |
|---|---|---|
| Macro lines per trigger key | 5 | The model holds five macro slots per key; the FS and Adv2 dialects persist only the first three |
| Macros per layout (FS, firmware < 1.0.340) | 24 | |
| Macros per layout (FS ≥ 1.0.340) | 100 | |
| Macros per layout (RGB family, Adv360) | 100 | |
| Total macro characters per layout (FS, RGB) | 7200 | "Each layout can store 7200 total macro characters" |
| Keystrokes per macro (Gen1) | 300 | "Macros are limited to approximately 300 characters." |
| Keystrokes per macro (Adv360) | 500 | Measured as the **length of the serialized macro text** |
| Tap-and-hold actions per layout | 10 | |
| Numbered layout files | 1–9 | |

Keystroke accounting: each macro keystroke counts 1, plus 2 per modifier attached to it; the layout total sums every macro of every key (Gen2: every macro in the flat list).

---

## 6. Worked examples (token by token)

### 6.1 `[F1]>[a]`

| Token | Meaning |
|---|---|
| `[F1]` | Position token: physical F1 key on the **top** layer (no `fn ` prefix) |
| `>` | Separator |
| `[a]` | Output token: key `a`. F1 is marked modified with output `a` |

### 6.2 `fn [4]>[LED]`

| Token | Meaning |
|---|---|
| `fn ` | Layer prefix → rule targets the bottom (Fn) layer; the prefix (3 chars) is stripped before parsing |
| `[4]` | Position token: the `4` key position on the Fn layer |
| `[LED]` | Output token: the LED backlight toggle key (token `LED`) |

### 6.3 `[kp-w]>[b]`

| Token | Meaning |
|---|---|
| `[kp-w]` | Adv2 position token. Contains the `kp-` prefix → targets the keypad layer; prefix stripped → position `w` on the keypad layer |
| `[b]` | Output: key `b` |

### 6.4 `[hyph]>[obrk]`

| Token | Meaning |
|---|---|
| `[hyph]` | FS-family token for the `-`/`_` key |
| `[obrk]` | FS-family token for the `[`/`{` key. Note the bracket characters themselves never appear as tokens; named tokens avoid colliding with the `[...]` rule syntax |

### 6.5 A multi-modifier value, per code

`[caps]>[cxxs]` (synthesized example): Caps Lock outputs Ctrl+Shift held together — `c` (Ctrl) `x` (no Alt) `x` (no Win) `s` (Shift). The code string is stored and round-tripped verbatim.

### 6.6 Tap-and-hold line as serialized

`[lspc]>[spc][t&h250][lctrl]`: FS left space bar; tap = `spc`, threshold = 250 ms (`t&h` + integer), hold = `lctrl`.
