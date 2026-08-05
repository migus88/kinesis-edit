# 07 — RGB Lighting System

Scope: the LED/RGB lighting subsystem — led file format, effects/modes, per-key coloring, edge lighting (TKO), LED indicators (Advantage360), defaults/reset, and effect previews — as implemented by the legacy apps for the Freestyle Edge RGB, the TKO, and the SmartSet Gaming app that embeds both.

---

## 1. Lighting architecture

### 1.1 Files on the v-Drive

Lighting is configured entirely through plain-text "led files" stored in the `lighting/` folder of the keyboard's virtual drive (alongside `layouts/` and `settings/`).

There are **9 lighting profiles**, named `led1.txt` … `led9.txt`. Each is paired 1:1 with the layout file of the same number (`layout1.txt` … `layout9.txt`) — together they form "Profile N".

### 1.2 Pairing between layout files and led files

The pairing is purely by file number:

- On app start, the app reads `settings/kbd_settings.txt`; the startup profile is stored under the key `startup_file`, and both current file names are derived from its number: `layoutN.txt` and `ledN.txt`.
- On save (RGB and TKO), the app writes `startup_file=layoutN.txt` and, under the settings key `led_mode`, `led_mode=ledN.txt`.
- "Save As" to a profile number switches both the current layout file and the current led file at once.

### 1.3 Active vs. stored profiles

The app only edits the **stored** files on the v-Drive; the keyboard firmware holds the **active** profile in its own memory. Saving serializes the layout, then the led file, then flushes/ejects the device. The post-save messaging describes the activation model:

- If the saved profile is the startup profile: "Use the Refresh Shortcut (SmartSet + Profile) to preview your Layout and Lighting updates or simply Eject the “FS EDGE RGB” drive in File Explorer and then disconnect the v-Drive (SmartSet + F8)."
- Otherwise: "To load Profile N to the keyboard, hold the SmartSet key and tap the N key."

TKO wording: refresh is "SmartSet + Right Shift + B", v-Drive disconnect "SmartSet + Right Shift + V", load profile "hold the SmartSet key + Right Shift and tap the N key".

### 1.4 Load path and file classification

- RGB: the led file is parsed as key-backlight lighting for both layers.
- TKO: on load, edge-lighting lines are first separated from the key-backlight lines, and the two sections are parsed independently. On save, the edge section is appended after the key-backlight section and written as **one** led file.
- Import: an arbitrary imported `.txt` (maximum 50 KB) is classified as a led file when its first line starts with `[` and contains `>`, **and** the file contains a known mode token (any key-backlight or `_edge` mode token) **or** a color-style value (a second `[` in the value part of a line); otherwise it is tried as a layout. For the Advantage360 the discriminator is simply whether the first line contains `[ind`.
- Export attaches both the current layout file and the current led file.
- The SmartSet Gaming app hosts the same RGB and TKO editors inside its dashboard, so all behavior described here applies to it unchanged.

### 1.5 In-memory model

The editor holds a lighting state per layer: an LED mode, per-effect effect colors and base colors, a speed, a direction, and a per-key color map. For TKO edge lighting there is a fully parallel, independent state set (edge mode, edge colors, edge speed/direction, per-LED colors), again per layer. Every lighting control reads and writes the state of the currently active layer (top or Fn/embedded) and, on the TKO, of the currently active configuration context (key backlight vs. edge lighting). The editor has three configuration contexts: layout, key-backlight lighting, and edge lighting (TKO only).

The full mode set: Freestyle (per-key), Monochrome, Breathe, Spectrum, Wave, Frozen Wave (edge only), Reactive, Ripple, Fireball, Starlight, Rebound, Loop, Pulse, Rain, Pitch Black (reserved, unused), and Disabled.

---

## 2. Led file format specification

A led file is a sequence of lines of the general shape `[config]>[value tokens]`. Parsing lowercases every line, so tokens are case-insensitive in practice; when writing, per-key key tokens use the same casing as layout files (e.g. `[F4]`).

### 2.1 Building blocks

| Element | Syntax | Notes |
|---|---|---|
| Token delimiters | `[` … `]`, separator `>` | each line is `[config]>[value tokens]` |
| Color value | `[R][G][B]` — three decimal components 0–255, e.g. `[255][128][0]` | |
| "No color" | empty string; mode lines serialize "no color" as `[0][0][0]` | |
| Speed token | `[spdN]`, N = 1–9 | out-of-range values are written as the default 5 |
| Direction token | `[dirdown]` \| `[dirleft]` \| `[dirup]` \| `[dirright]` | invalid values are written as the default (left) |
| Fn-layer prefix | the line prefix `fn ` | marks a line as belonging to the embedded/Fn layer; unprefixed lines belong to the top layer |
| Per-key color line | `[keytoken]>[R][G][B]` | `keytoken` is the key's layout-file save token |

The expected token order within a value is: color (3 tokens), then `[spdN]`, then `[dirX]`. A missing speed token yields the default speed 5; a missing or invalid direction token yields the default direction (left), subject to the per-effect direction validity rules in §2.4.

### 2.2 Line grammar per mode (top layer shown; prepend `fn ` for the Fn layer)

"Base color line" means a `[mono]>` line emitted immediately **before** the effect line, carrying the effect's background/base color.

| Mode | Line(s) written |
|---|---|
| Freestyle (per-key) | one `[keytoken]>[R][G][B]` line per key that has a color assigned (keys with no color are omitted) |
| Monochrome | `[mono]>[R][G][B]` |
| Breathe | `[breathe]>[spdN]` followed by per-key `[keytoken]>[R][G][B]` lines; if **no** key colors are configured, `[mono]>[0][0][0]` is appended instead |
| Spectrum | `[spectrum]>[spdN]` |
| Wave | `[wave]>[spdN][dirX]` |
| Reactive | `[mono]>[Rb][Gb][Bb]` then `[reactive]>[R][G][B][spdN]` |
| Ripple | `[mono]>[Rb][Gb][Bb]` then `[ripple]>[R][G][B][spdN]` |
| Fireball | `[mono]>[Rb][Gb][Bb]` then `[fireball]>[R][G][B][spdN][dirX]` |
| Starlight | `[mono]>[Rb][Gb][Bb]` then `[star]>[R][G][B][spdN]` |
| Rebound | `[mono]>[Rb][Gb][Bb]` then `[rebound]>[R][G][B][spdN][dirX]` |
| Loop | `[mono]>[Rb][Gb][Bb]` then `[loop]>[R][G][B][spdN][dirX]` |
| Pulse | `[pulse]>[spdN]` |
| Rain | `[mono]>[Rb][Gb][Bb]` then `[rain]>[R][G][B][spdN]` |
| Pitch Black | `[black]` — reserved token; the legacy RGB/TKO apps neither write nor read it |
| Disabled | nothing is written for that layer |

Key-backlight mode tokens:

```text
[mono]     [breathe]  [spectrum]  [wave]   [reactive]  [star]  (Starlight)
[rebound]  [ripple]   [fireball]  [loop]   [pulse]     [rain]
[black]    (reserved, unused)
```

### 2.3 TKO edge-lighting line grammar

Edge lines use `_edge`-suffixed mode tokens:

```text
[mono_edge]     [breathe_edge]  [spectrum_edge]  [wave_edge]
[rebound_edge]  [loop_edge]     [pulse_edge]     [frozenwave_edge]
```

The edge section is written into the **same** `ledN.txt` as the key-backlight section:

| Edge mode | Line(s) written |
|---|---|
| Freestyle | per-LED `[edgetoken]>[R][G][B]` lines |
| Monochrome | `[mono_edge]>[R][G][B]` |
| Breathe | `[breathe_edge]>[spdN]` + per-LED color lines |
| Spectrum | `[spectrum_edge]>[spdN]` |
| Wave | `[wave_edge]>[spdN][dirX]` — only left/right are valid; any other direction is read as left |
| Frozen Wave | `[frozenwave_edge]` (bare token, no `>` value) + per-LED color lines |
| Rebound | `[mono_edge]>[Rb][Gb][Bb]` then `[rebound_edge]>[R][G][B][spdN]` (no direction) |
| Loop | `[mono_edge]>[Rb][Gb][Bb]` then `[loop_edge]>[R][G][B][spdN][dirX]` — only left/right are valid |
| Pulse | `[pulse_edge]>[spdN]` |
| Disabled | nothing written |

Edge LED address tokens: the TKO has 33 addressable edge LEDs with save tokens `L1`–`L9` (left side), `B1`–`B15` (bottom/front), and `R1`–`R9` (right side). A line is recognized as edge syntax if (after stripping an optional `fn ` prefix) it starts with `[lN]`, `[rN]`, or `[bN]` (the classifier accepts N = 1..30, though only the listed LEDs exist) or with any `_edge` mode token. On load, all such lines are separated out before the key-backlight section is parsed.

### 2.4 Parsing rules

1. Parsing starts from a fully reset lighting state (all defaults, both layers).
2. Lines are split into a top-layer list and an Fn-layer list by the `fn ` prefix; each list is parsed independently, and the prefix is stripped before token matching.
3. Mode detection inspects the **first line** of a layer section (and, for two-line effects, also the **second line**): `[mono]` with exactly 1 total line → Monochrome; `[breathe]` → Breathe (remaining lines are per-key colors); `[spectrum]`, `[wave]`, `[pulse]` → 1-line modes; `[reactive]`, `[fireball]`, `[star]`, `[rebound]`, `[ripple]`, `[loop]`, `[rain]` on line 1 **or** line 2 → 2-line modes (base `[mono]` + effect line); an empty section → Disabled; anything else → **Freestyle**. So Freestyle is the fallback interpretation for a file of bare per-key color lines.
4. In Freestyle (or in Breathe, after the header line), a `[mono]>` line sets **all** keys of that layer to the given color, and per-key lines then override individual keys. A per-key color value that fails to parse falls back to the default effect color.
5. For the two-line effects, the `[mono]` line carries the base color and the effect line supplies the effect color + speed (+ direction). The canonical order is `[mono]` first, effect line second. Direction is validated per effect: Fireball accepts only left/right, Rebound only left (horizontal)/up (vertical); anything else falls back to left. **Compatibility note:** led files already in the field — including the factory Expansion Pack 2 files (§2.6) — may use the reverse line order, with the effect line first and the `[mono]` base line second. Parsers must accept both orders and assign base vs. effect colors by token, not by position.
6. Fn-layer per-key lines address keys by **top-layer** position; the color is applied to the same physical key on the Fn layer.
7. Fn-layer save-token exceptions: when the Fn layer is saved, the media/navigation actions that live on the Fn layer are written under their top-layer position tokens — `mute`→`[F1]`, `vol-`→`[F2]`, `vol+`→`[F3]`, `play`→`[F4]`, `prev`→`[F5]`, `next`→`[F6]`, `insert`→`[pause]`, `scroll`→`[del]`. The inverse mapping applies on load.

### 2.5 The sample files, line by line

The legacy app ships nine sample profile files. Annotated examples:

- `led1.txt` — `[wave]` — Wave mode; the bare token has no `>`, so the value parse is skipped and speed/direction stay at defaults (speed 5, direction left).
- `led2.txt`:

  ```text
  [rain]>[255][128][0][spd1]
  [mono]>[195][255][142]
  ```

  Rain, orange effect at speed 1, pale-green base — written in the legacy effect-first line order (see the compatibility note in §2.4). Canonical modern order puts the `[mono]` line first.
- `led3.txt`, `led6.txt`, `led7.txt` — empty → lighting Disabled.
- `led4.txt` — `[rebound]>[0][255][0][spd8][dirleft]` — Rebound, green, speed 8, horizontal.
- `led5.txt` — `[star]>[0][0][255][spd7]` — Starlight, blue, speed 7.
- `led8.txt`:

  ```text
  [breathe]>[spd7]
  [s]>[0][255][0]
  ```

  Breathe at speed 7 with a single per-key color (S key green).
- `led9.txt`:

  ```text
  [e]>[255][0][0]
  [t]>[255][0][0]
  [a]>[189][255][57]
  ```

  No mode token on line 1 → parsed as Freestyle per-key lighting.

Further examples:

- A Breathe file (`[breathe]>[spd7]`) followed by per-key lines using layout-file key tokens such as `[esc]`, `[F4]`, `[cbrk]`, `[hk6]`, `[ent]`, `[/]`.
- A Breathe file (`[breathe]>[spd9]`) coloring every key of the Freestyle Edge RGB demonstrates the full token set: `[hk0]`–`[hk10]` (hotkeys), `[tilde]`, `[hyph]`, `[=]`, `[bspc]`, `[obrk]`, `[cbrk]`, `[\]`, `[colon]`, `[apos]`, `[com]`, `[per]`, `[lshft]`/`[rshft]`, `[lctrl]`/`[rctrl]`, `[lwin]`, `[lalt]`/`[ralt]`, `[lspc]`/`[rspc]` (split space bars), `[up]`, `[lft]`, `[dwn]`, `[rght]`, `[prnt]`, `[pause]`, `[del]`, `[home]`, `[end]`, `[pup]`, `[pdn]`, `[caps]`, `[ent]`, `[tab]` — identical tokens to layout files.
- `[reactive]>[0][0][255][spd5]` — Reactive without a base `[mono]` line; the base color stays at its default (black).

### 2.6 Factory "Expansion Pack 2" defaults

The app can write a canonical set of "Expansion Pack 2" lighting defaults into all 9 profiles. Examples — profile 1:

```text
[wave]>[spd5][dirright]
fn [wave]>[spd5][dirdown]
```

and profile 2:

```text
[rain]>[0][255][0][spd5]
[mono]>[0][0][0]
fn [rain]>[0][255][0][spd5]
fn [mono]>[255][255][255]
```

(Note these files use the legacy effect-first order for the two-line effects — see §2.4.) Profiles 6–9 are Monochrome top layer + Breathe Fn layer, e.g. profile 6: `[mono]>[0][0][255]`, `fn [breathe]>[spd5]`, `fn [mono]>[0][0][255]`.

---

## 3. LED modes / effects

UI captions (RGB app): Freestyle, Monochrome, Breathe, Spectrum, Wave, Reactive, Ripple, Fireball, Starlight, Rebound, Loop, Pulse, Rain, Disable.

Which parameter panels each mode shows:

- Effect color: Freestyle, Monochrome, Breathe, Reactive, Ripple, Fireball, Starlight, Rebound, Loop, Rain.
- Base color: Reactive, Ripple, Fireball, Starlight, Rebound, Loop, Rain (on RGB only shown when LED firmware ≥ 1.0.44).
- Direction: RGB shows the direction panel for Wave, Rebound, Loop; TKO also shows it for Fireball (left/right only). For Rebound the four arrows are replaced by "Horizontal"/"Vertical" buttons that map to left/up.
- Speed: Breathe, Spectrum, Wave, Reactive, Starlight, Rebound, Ripple, Fireball, Loop, Rain, Pulse. The speed knob range is 1–9.
- Zone buttons: Freestyle and Breathe only.

Complete mode table:

| Mode (UI name) | File token | Colors | Speed 1–9 | Direction (valid values) | RGB key backlight | TKO key backlight | TKO edge | Gaming app |
|---|---|---|---|---|---|---|---|---|
| Freestyle (per-key) | none — bare per-key lines | one color per key | — | — | yes | yes | yes (per edge LED) | yes |
| Monochrome | `[mono]` (`[mono_edge]`) | 1 effect color | — | — | yes | yes | yes | yes |
| Breathe | `[breathe]` (`[breathe_edge]`) | per-key colors | yes | — | yes | yes | yes | yes |
| Spectrum | `[spectrum]` (`[spectrum_edge]`) | — (auto color cycle) | yes | — | yes | yes | yes | yes |
| Wave | `[wave]` (`[wave_edge]`) | — (auto rainbow) | yes | down/left/up/right (edge: left/right only) | yes | yes | yes | yes |
| Frozen Wave | `[frozenwave_edge]` | per-LED colors (static) | — | — | no | no | yes (edge only) | yes (TKO) |
| Reactive | `[reactive]` | effect + base | yes | — | yes | yes | no | yes |
| Ripple | `[ripple]` | effect + base | yes | — | yes (fw-gated) | yes | no | yes |
| Fireball | `[fireball]` | effect + base | yes | left/right only | yes (fw-gated; no direction UI on RGB) | yes (direction UI) | no | yes |
| Starlight | `[star]` | effect + base | yes | — | yes | yes | no | yes |
| Rebound | `[rebound]` (`[rebound_edge]`) | effect + base | yes | left (horizontal) / up (vertical); edge: none | yes | yes | yes | yes |
| Loop | `[loop]` (`[loop_edge]`) | effect + base | yes | down/left/up/right (edge: left/right) | yes | yes | yes | yes |
| Pulse | `[pulse]` (`[pulse_edge]`) | — | yes | — | yes | yes | yes | yes |
| Rain | `[rain]` | effect + base | yes | — | yes | yes | no | yes |
| Pitch Black | `[black]` (reserved) | — | — | — | no | no | no | no |
| Disable | (nothing written) | — | — | — | yes | yes | yes | yes |

Mode menus per device: the RGB key-backlight menu offers all 13 effects + Disable; the TKO key-backlight menu offers the same 13 modes + Disable; the TKO edge menu offers Freestyle, Monochrome, Breathe, Wave, Frozen Wave, Spectrum, Rebound, Loop, Pulse, and Disable.

Visual behavior: Breathe and Pulse fade the whole board in and out; Wave scrolls a rainbow across the board; Spectrum cycles hue board-wide; Reactive lights keys on key-press over the base color; Rain drops random columns; Ripple expands rings; Fireball shoots across a row; Rebound bounces a bar; Loop sweeps in the chosen direction; Starlight twinkles random keys.

Firmware gating on RGB: Ripple and Fireball require keyboard firmware ≥ 1.0.121 **and** LED firmware ≥ 1.0.58. Fn-layer lighting (the layer toggle in lighting mode) requires LED firmware ≥ 1.0.44. When the LED firmware is exactly 1.0.44 or 1.0.58 and no led file contains any `fn ` line, the app shows a "Lighting Expansion Pack" dialog offering either to mirror the current Top Layer effects to the Fn layer in all 9 profiles (every non-empty line is duplicated with the `fn ` prefix) or to load the Expansion Pack 2 effects (§2.6) into all 9 profiles.

---

## 4. Per-key coloring

- **Addressing**: identical tokens to layout files. Each colored key is written as `[keytoken]>[R][G][B]` using the key's layout-file save token, and tokens are resolved on load against the same key-token table as the layout parser. See §2.5 for the full Freestyle Edge RGB token set.
- **Layer-specific lighting**: both the top layer and the Fn (embedded) layer have independent mode + parameters + per-key colors; Fn-layer lines carry the `fn ` prefix (§2.1). Switching layers in lighting mode re-reads all controls from the newly active layer's values.
- **Freestyle mode**: pure per-key coloring — the file body is nothing but per-key lines (plus optional `[mono]>` fill-all lines). In the editor, clicking a key applies the currently selected picker color; zone buttons color predefined key groups with the selected color.
- **Breathe** is the other per-key mode: same per-key lines after the `[breathe]>[spdN]` header.
- **Zones (RGB)**: All, Number, Function, WASD, Game, Arrow, Left Module, Right Module.
- **Zones (TKO)**: All Keys, Hyperspace (the 3 split-space keys), WASD, Modifiers, Number Row, Home Row on the top layer; Media Keys, Nav Keys, Function Keys, Arrow Keys on the Fn layer.
- **Reset**: the "Reset All" button (visible only for Freestyle/Breathe) erases all per-key color assignments after confirmation — "Do you want to erase color assignments for each key".
- **Color picker**: the shared color dialog offers an HSL ring picker, R/G/B numeric edits (clamped 0–255), an HTML hex field (`#RRGGBB`), 10 premixed swatches (white default, yellow, red, lime, blue, fuchsia, orange RGB(255,128,0), azure RGB(0,128,255), aqua, black) and 6 custom slots persisted to app settings. The RGB/TKO main lighting panel additionally hosts 20 premixed and 12 custom swatches; custom colors persist in `app_settings.txt` under the keys `cust_color_1` … `cust_color_12`.

---

## 5. LED indicators (Advantage360)

The Advantage360 has **6 configurable indicator LEDs**. Each indicator has a function assignment and an array of 6 colors (one per layer where applicable). Available functions: NKRO Mode, Scroll Lock, Num Lock, Caps Lock, Layer, Profile, Battery, and Disable.

File format — each line of the Adv360 led file is `[INDn]>` (n = 1..6) followed by a function token and, except for Battery and Disable, a color:

| Function | Token(s) written after `[INDn]>` |
|---|---|
| NKRO mode | `[nkro][R][G][B]` |
| Scroll Lock | `[sclk][R][G][B]` |
| Num Lock | `[nmlk][R][G][B]` |
| Caps Lock | `[caps][R][G][B]` |
| Layer | five lines: `[layd][R][G][B]`, `[layk]…`, `[lay1]…`, `[lay2]…`, `[lay3]…` — one color per layer (base, keypad, fn1, fn2, fn3) |
| Profile | `[prof][R][G][B]` |
| Battery | `[batt]` (no color) |
| Disabled | `[null]` |

Parsing: the line's config part is matched against `ind1` … `ind6`, the function token is matched and stripped, and the remaining `[R][G][B]` is stored as that indicator's color for the relevant layer (layer 0 except for the `[lay*]` tokens); Battery and Disable take no color. Indicator reset state: function Disabled, all colors black; a full lighting reset also resets all indicators. This mechanism is Advantage360-only — RGB/TKO led files never contain `[ind…]` lines, and the `[ind` prefix in the first line is the Adv360 discriminator during file classification (§1.4).

(The RGB keyboard's own status LEDs are configured through *layout* special actions instead — profile, layer, caps lock, num lock, scroll lock, NKRO mode, and disable actions remappable onto keys.)

---

## 6. Brightness, off states, defaults, and reset

- **Brightness** is not edited by the app; it is a hardware function bound to the remappable layout actions with save tokens `led+` / `led-`. There is also a remappable LED toggle key (save token `LED`, or `ledt` on Gen2 devices).
- **Off states**: choosing "Disable" writes an empty section for that layer; an empty led file (or empty layer section) loads as Disabled. Pitch Black and LED-off state settings (settings values `P` and `0`) belong to the non-RGB Freestyle Edge app only; the RGB/TKO apps do not use them and clear them on load.
- **Defaults**: speed 5 in range 1–9; direction left; effect color lime green RGB(0,255,0); base color black; custom swatch slots empty.
- **Reset**: a lighting reset clears every per-key color on both layers, sets both layer modes to Disabled, resets every effect color to the default green, every base color to black, all speeds to 5, all directions to left, and resets all LED indicators. The TKO edge state is reset the same way. A full reset runs at app start and before every led-file parse (so a parsed file completely defines the resulting state).

---

## 7. Animated effect previews

The legacy app shows an animated on-screen preview of the selected effect at the selected speed and direction. Effects with animated previews: Breathe, Pulse, Wave (all four directions), Spectrum, Rain, Reactive, Ripple, Fireball (TKO: left/right variants), Loop (all four directions), Rebound (horizontal/vertical), and Starlight; the TKO additionally previews edge Loop (left/right) and edge Rebound.
