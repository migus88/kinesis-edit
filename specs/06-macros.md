# 06 — Macro Format and Semantics

Scope: the complete on-disk macro syntax (`{trigger}>{tokens}`), parse and serialize semantics, playback-speed/repeat settings, co-triggers, and limits. General file structure, layer prefixes, and remap rules are specified in `04-layout-file-format.md`.

---

## 1. In-memory model (legacy data model, for reference)

- A macro is a `TKeyList` — an ordered list of `TKey` objects plus metadata:
  - `CoTrigger1..CoTrigger4: TKey` — modifier keys that must be held with the trigger.
  - `MacroSpeed: integer` — playback speed (0 = use global speed; see section 4).
  - `MacroRptFreq: integer` — repeat/multiplay factor.
  - `TriggerKey: integer` and `LayerIdx: integer` — used by Gen2 (Adv360) where macros live in a flat list.
  - `Guid` — identity used to detect duplicates when editing.
- Each `TKey` inside the list carries: `Key` (virtual key code), `SaveValue` (file token), `Modifiers` (comma-separated codes of the modifiers held while this key is struck, e.g. `LS` or `LC,LA`), `UpDown` (none/up/down), `WriteDownUp`, and `DiffPressRel` (pedal-only "different press and release").
- Modifier short codes are two characters (generic codes pad with a trailing space): Shift `S `, Left Shift `LS`, Right Shift `RS`; Ctrl `C `, `LC`, `RC`; Alt `A `, `LA`, `RA`; Win `W `, `LW`, `RW`.
- Storage on the key: every `TKBKey` owns five macro slots `Macro1..Macro5` plus `IsMacro`, `ActiveMacro`, and `CanAssignMacro` flags. The FS and Adv2 serializers persist only `Macro1..Macro3`.
- Gen2 (Adv360) instead keeps all macros in a single flat list, each tagged with `TriggerKey` (the key's original key code) and `LayerIdx`.
- A key's *trigger identity* is normally its position key, but the original key for the Fn1 layer-shift and keypad layer-toggle keys; macro trigger lookup uses this identity rather than the raw position.

## 2. File syntax

General shape (all families):

```
[layer]{cotrigger}{cotrigger}...{trigger}>{speed}{repeat}{token}{token}...
```

Real lines:

```
{2}>{x1}{a}{s}{d}{f}{a}{s}{f}{d}{a}{s}{f}
{q}>{x1}{-lshft}{n}{+lshft}{a}{d}{i}{a}
{lshft}{esc}>{s2}{x2}{a}{s}{d}{f}...
{lshift}{lctrl}{lalt}{d}>{speed5}{a}{d}...{-ctrl}{d}{+ctrl}
```

### 2.1 Trigger side (left of `>`)

Parsed as a sequence of `{...}` groups:

- Every token is resolved against the key table. A token that is a **modifier** (any of Shift/Ctrl/Alt/Win, generic or left/right) *and* is followed by more tokens is collected as a **co-trigger**.
- The remaining (normally last) token is the **trigger key**. A modifier that is the final token is itself the trigger (so a macro can trigger on a bare modifier key).
- Layer selection: `fn ` line prefix (FS/RGB/TKO), `kp-` token prefix or keypad-exception token (Adv2), or the current `<...>` header (Adv360). See `04-layout-file-format.md` §3.
- Number of co-triggers kept: Gen1 RGB-family parser up to 4, Adv2 up to 3, old FS parser only 1.

### 2.2 Value side (right of `>`) — token semantics

Tokens are `{...}` groups consumed left to right. In each group:

| Token form | Exact syntax | Meaning |
|---|---|---|
| Plain key | `{a}`, `{esc}`, `{spc}`, `{F5}`, `{lmous}` … | One keystroke (press+release) of the named key. Token = the key's file token. |
| Modifier down | `{-lshft}`, `{-ctrl}` … | Leading `-` = key **down**. For a modifier: added to the set of *active modifiers*; subsequent plain keys record it as held with them. |
| Modifier up | `{+lshft}` … | Leading `+` = key **up**. For a modifier: removed from the active set. If the up arrives with no matching down (or immediately after the same key), the modifier is instead kept as a single tap in the macro. |
| Bare modifier | `{shift}`, `{lwin}` … | A modifier with no `+`/`-` is a single tap of that modifier. |
| Key down/up (non-modifier, Gen2) | `{-d}` / `{+d}` | Recorded as an explicit key-down / key-up event on the keystroke; validated so that every down has a later matching up. |
| Speed (per-macro) | `{s1}`…`{s9}`, `{s0}` | The `s` prefix + digits. Only honored as the **first** token(s), before any keystroke. The RGB family accepts 0–9; `0` means "use global speed". Adv360 clamps to a minimum of 1. |
| Repeat / multiplay | `{x1}`…`{x9}`, `{x0}` | The `x` prefix + digits. Same first-token rule. The RGB family accepts 0–9; Adv360 clamps to a minimum of 1. Number of times the macro plays per trigger press. |
| Speed (Adv2 syntax) | `{speed1}`…`{speed9}` | The `speed` prefix + digits; first-token-only in the Adv2 parser. |
| Fixed delay | `{d001}`…`{d999}` | 1–999 ms delay keystroke. Tokens are `d` + the millisecond count, always zero-padded to 3 digits (`d050` = 50 ms). |
| Legacy delays | `{d125}`, `{d500}` | 125/500 ms delay keys; registered as distinct legacy keys only when the app is **not** RGB/TKO — on RGB/TKO `d125`/`d500` resolve to the generated `dNNN` range instead. Files in the field contain both forms. |
| Random delay | `{dran}` | Random timing delay. |
| Different press/release (pedal) | `{ }` (a space in braces) | The token is a single space between braces — the space character is the key's file token. When active modifiers exist, it marks the *previous* key as "different press and release" rather than adding a keystroke. Only meaningful for the pedal app. |
| Mouse | `{lmous}` `{mmous}` `{rmous}` `{mous4}` `{mous5}` (Gen1) / `{lmou}` `{mmou}` `{rmou}` `{4mou}` `{5mou}` `{sumo}` `{sdmo}` `{moul}` `{mour}` `{mouu}` `{moud}` (Adv360) / `{lmouse}` `{mmouse}` `{rmouse}` (Adv2/Pedal) | Mouse clicks / scroll / movement, as ordinary macro keys. |
| Media | `{mute}` `{vol-}` `{vol+}` `{play}` `{prev}` `{next}` `{stop}` `{fwrd}` `{rewd}` `{cpau}` `{ejct}` `{recr}`, Adv360 `{plpa}` | Media keys as macro content. |

Additional parse details:

- Speed/repeat digits must be numeric. An out-of-range value in the Gen1 RGB-family parser is simply ignored (the default applies); the old FS parser substitutes the FS default.
- Non-US layouts (pedal app on Windows only): each parsed key is converted between the active keyboard layout and US English.
- An unknown token invalidates only that segment; the whole line is then treated as invalid and is not applied.
- After a successful parse, the macro is stored: co-triggers 1–4 copied; speed/repeat set (defaults if the tokens were absent — see section 4); then Gen2 → appended to the flat macro list with its trigger and layer, Gen1 → the first empty of the trigger key's five macro slots, marking the key as a macro key. Assignment requires the key to accept macros — modifier position keys do not.

## 3. Serialization

For each non-empty macro the emitted line is, in order:

1. The layer prefix (`fn ` for the bottom layer on non-Gen2; empty on Gen2, where the layer comes from the `<...>` header).
2. Co-triggers: `{token}` for each of up to four co-triggers.
3. Trigger: `{token}`.
4. `>`.
5. Speed: `{sN}` if the speed is in 0–9 — i.e. always written by the RGB-family serializer, including `{s0}` for "global".
6. Repeat: `{xN}` if in 0–9.
7. Keystrokes. For each keystroke:
   - Modifier transitions are computed by diffing the previous keystroke's held-modifier set against the current one: every modifier no longer held emits `{+token}`, every newly held modifier emits `{-token}`.
   - The key itself: `{token}` for a normal press+release, `{+token}` for an explicit up, `{-token}` for an explicit down. Pedal different-press-release keys are written `{-token}{ }{+token}`.
   - After the last keystroke, all still-held modifiers are closed with `{+token}`.

Variants: the old FS serializer writes speed/repeat tokens only when the value is ≥ 1 (the FS default 0 is omitted) and writes only the first co-trigger; the Adv2 serializer writes `{speedN}` for speeds 1–9 and **no repeat token**, with `kp-` prefixes on trigger/co-trigger tokens for keypad-layer macros.

## 4. Playback speed and repeat: ranges and defaults

| Setting | Value | Notes |
|---|---|---|
| Speed minimum — Adv2, FS | `0` | 0 = global speed |
| Speed minimum — RGB family, Adv360 | `1` | The Adv360 parser clamps lower values up |
| Speed maximum — all families | `9` | |
| Default speed — Adv2, FS | `0` | Applied when no speed token is present |
| Default speed — RGB family | `5` | Applied when `{sN}` is absent |
| Repeat minimum — Adv2, FS | `0` | |
| Repeat minimum — RGB family, Adv360 | `1` | Adv360 clamps up |
| Repeat maximum — FS, RGB family, Adv360 | `9` | |
| Default repeat — Adv2, FS | `0` | |
| Default repeat — RGB family | `1` | |

Global vs per-macro speed: a macro speed of 0 means "play at the keyboard's global speed". The global speed itself is not part of the layout file — it is the `macro_speed=` entry of the keyboard settings file (e.g. `macro_speed=0`).

## 5. Co-triggers and duplicate detection

- File encoding: co-triggers are the leading modifier tokens on the trigger side, e.g. `{lshft}{3}>...`, `{rctrl}{3}>...`, `{3}>...` are three distinct macros on the same key.
- A macro holds up to four co-triggers; the co-trigger count is the number of populated slots.
- Duplicate triggers are detected by comparing the co-trigger sets: two macros **collide** when every co-trigger of one exists in the other and both have the same co-trigger count, or when both have zero co-triggers.
- Editing validation rejects a key whose macro slots contain any colliding pair: two different macros may not share the same trigger key + co-trigger combination.
- Gen2 validation additionally checks: the macro is non-empty; a layer is selected; up/down integrity (no hanging downstroke — every explicit down needs a later matching up); no duplicate trigger + layer + co-trigger set; and the per-macro length limit. When the user confirms a replacement, the existing colliding macro is deleted.
- Reserved trigger rule (Gen2): the triggers `fn1s` (Fn1 layer shift) and `keyt` (keypad layer toggle) **require at least one co-trigger**. Modifier keys themselves cannot host macros on Gen1 boards.

## 6. Limits

| Limit | Value |
|---|---|
| Macros per trigger key | 5 slots; the FS and Adv2 dialects persist only slots 1–3 |
| Macros per layout | FS firmware < 1.0.340: `24`; FS ≥ 1.0.340: `100`; RGB family: `100`; Adv360: `100` |
| Keystrokes per macro | Gen1: `300` (checked while recording; "Macros are limited to approximately 300 characters."); Adv360: `500`, measured as the **length of the serialized macro text** |
| Total macro characters per layout | `7200` (FS and RGB families); counted as 1 per keystroke plus 2 per attached modifier, summed over every macro in the layout |
| Up/down integrity (Gen2) | Every explicit key-down in a macro needs a later matching key-up |

## 7. Worked examples (token by token)

### 7.1 `{q}>{x1}{-lshft}{n}{+lshft}{a}{d}{i}{a}`

| Token | Meaning |
|---|---|
| `{q}` | Trigger: the `q` key, top layer, no co-trigger |
| `>` | Separator |
| `{x1}` | Repeat factor 1 (`x` prefix + `1`) — play once |
| `{-lshft}` | Left Shift **down** (`-` prefix); Shift joins the active-modifier set |
| `{n}` | Keystroke `n`, recorded with Left Shift held → plays as Shift+N ("N") |
| `{+lshft}` | Left Shift **up**; leaves the active set |
| `{a}{d}{i}{a}` | Plain keystrokes a, d, i, a |

Net effect: typing `q` outputs `Nadia`. (No `{sN}` token → the speed defaults to 5 on load in the RGB family.)

### 7.2 `{lshft}{esc}>{s2}{x2}{a}{s}{d}{f}...{kpent}...`

| Token | Meaning |
|---|---|
| `{lshft}` | Co-trigger: Left Shift must be held |
| `{esc}` | Trigger key: Esc |
| `{s2}` | Per-macro playback speed 2 |
| `{x2}` | Repeat factor 2 — macro plays twice per trigger |
| `{a}{s}{d}{f}…{kpent}…` | Keystroke stream; `{kpent}` is the keypad-Enter token |

### 7.3 `{lshft}{3}>{x1}{esc}{-shift}{s}{+shift}{a}{l}{u}{t}{spc}{-shift}{/}{+shift}{r}{i}{c}{d999}{d001}{dran}`

| Token | Meaning |
|---|---|
| `{lshft}{3}` | Shift+3 co-trigger combination on the `3` key |
| `{x1}` | Play once |
| `{esc}` | Escape keystroke |
| `{-shift}…{+shift}` | Generic Shift held around `s` → `S`; again around `/` → `?` |
| `{a}{l}{u}{t}{spc}` | `alut` + space |
| `{r}{i}{c}` | `ric` |
| `{d999}` | Fixed delay 999 ms (`d` + zero-padded ms) |
| `{d001}` | Fixed delay 1 ms |
| `{dran}` | Random delay |

Note the same physical key can also carry `{rctrl}{3}>…` and `{3}>…` lines in the same file — three macros on one key distinguished purely by co-trigger set.

### 7.4 `{lshift}{lctrl}{lalt}{d}>{speed5}{a}{d}{s}...{shift}{shift}{-ctrl}{d}{+ctrl}`

| Token | Meaning |
|---|---|
| `{lshift}{lctrl}{lalt}` | Three co-triggers (Adv2 token spellings; the Adv2 dialect keeps up to 3) |
| `{d}` | Trigger key `d` |
| `{speed5}` | Adv2 per-macro speed syntax: the `speed` prefix + `5` |
| `{a}{d}{s}…{space}…` | Plain keystrokes (Adv2 uses `space`, not `spc`) |
| `{shift}{shift}` | Two bare-modifier taps: a modifier token without `+`/`-` is a single keystroke of that modifier |
| `{-ctrl}{d}{+ctrl}` | Ctrl held around `d` → Ctrl+D |

### 7.5 `{o}>{x1}{a}...{dran}{d050}{d999}{d125}{d125}{d500}{d974}{s}...`

| Token | Meaning |
|---|---|
| `{o}` | Trigger `o` |
| `{x1}` | Play once |
| `{dran}` | Random delay |
| `{d050}` | 50 ms delay — demonstrates the mandatory 3-digit zero padding |
| `{d999}` / `{d974}` | 999 ms / 974 ms delays |
| `{d125}{d125}{d500}` | 125, 125, 500 ms delays (on FS these resolve to the distinct legacy 125/500 ms delay keys) |

### 7.6 Pedal different press/release

`{-a}{ }{+a}` — the `{ }` token between an explicit down and up of the same key marks it as "different press and release" on the Savant Elite pedal; on read-back the flag attaches to the preceding key.
