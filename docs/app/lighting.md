# Lighting (Model, Led-File Engine)

The lighting layer of `KinesisEdit.Core`: the in-memory lighting model and the `lighting/ledN.txt` parser/serializer for the three lighting-capable devices — Freestyle Edge RGB (key-backlight dialect), TKO (key-backlight + edge dialect in one file), Advantage360 (indicator dialect). Pure lines-in/lines-out — it never touches the filesystem (profile orchestration hands it lines via `IVDriveFileService`), never resolves which profile is active, and never shows UI.

| Namespace | Entry point | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Lighting` | `LedFileParser` / `LedFileSerializer` | Per-device led-file parse/serialize | 07 §1.4, §2, §5 |
| `KinesisEdit.Core.Lighting` | `LightingModel`, `TkoLightingModel`, `Advantage360LightingModel` | Mutable in-memory state, full-reset semantics | 07 §1.5, §6 |
| `KinesisEdit.Core.Lighting` | `LightingModeCatalog`, `IndicatorFunctionCatalog` | Static mode/function/token/direction data | 07 §2.2–§2.3, §3, §5 |
| `KinesisEdit.Core.Lighting` | `FnLayerTokenTranslator`, `TkoEdgeLineClassifier` | Fn save-token exceptions; edge-line detection | 07 §2.3, §2.4; 05 §5.5 |
| `KinesisEdit.Core.Lighting` | `LightingAvailability`, `ExpansionPackDefaults` | Firmware hooks + factory pack data | 07 §3, §2.6; 09 §2 |

## Model

- `LayerLightingState` — one layer in one context: `Mode` (`LightingMode`), `EffectColor`/`BaseColor` (`LedColor`), `Speed` 1–9, `Direction` (`LightingDirection`), `KeyColors` (key code → color). `SetKeyColor` treats black as "no color" and removes the entry (07 §2.1) — the map only ever holds non-black colors. Reset defaults (07 §6): Disabled, lime green (0,255,0), black, 5, left, empty.
- `LightingModel` — `TopLayer` + `FnLayer`; the whole RGB model and each TKO section. `TkoLightingModel` — `KeyBacklight` + `Edge`, two fully parallel `LightingModel`s (07 §1.5). `Advantage360LightingModel` — 6 `IndicatorState`s (`Function` + 5 per-layer colors via `IndicatorLayer`; non-layer functions use the `Base` slot). All models: `Reset()`, `IsEquivalentTo()` (mutable classes, so no `Equals` override).

## Engine — `LedFileParser` / `LedFileSerializer`

- `ParseRgb/ParseTko/ParseAdvantage360(IReadOnlyList<string>)` → model; `SerializeRgb/SerializeTko/SerializeAdvantage360(model)` → `IReadOnlyList<string>`.
- Parse: lowercase everything, split top/Fn sections by the `fn ` prefix, mode-detect from line 1 (line 2 too for the seven two-line effects), parse each section independently from a fully reset state. TKO: `TkoEdgeLineClassifier.IsEdgeLine` splits edge lines out **before** key-backlight parsing; tolerates addresses `[lN]/[rN]/[bN]` N = 1..30 though only L1–L9/B1–B15/R1–R9 exist (07 §2.3).
- Tolerant-read fallbacks (07 §2.4): invalid/missing speed → 5; invalid direction → left within the per-effect valid set (Fireball left/right, Rebound left/up, edge Wave/Loop left/right, edge Rebound none); unparseable per-key color → default lime; unresolvable tokens ignored; `[mono]>` in a Freestyle/Breathe body fills all keys (then per-key lines override) — fill uses the device's top-layer geometry key set.
- Serialize (canonical): top section then `fn ` lines; base `[mono]` line first for two-line effects; value order color → `[spdN]` → `[dirX]` (speed/direction always written where the mode has them, invalid values written as defaults); per-key lines in top-layer geometry order with registry-canonical casing (`[F4]`); Breathe with no key colors appends `[mono]>[0][0][0]`; Disabled and the reserved Pitch Black write nothing; TKO edge section appended after the key section into one file.
- Adv360 (07 §5): `[INDn]>` + function token (+ `[R][G][B]` except `[batt]`/`[null]`); the Layer function is five lines `[layd]/[layk]/[lay1]/[lay2]/[lay3]`; a reset model serializes six `[INDn]>[null]` lines.

## Static data

- `LightingModeCatalog.All` — 16 rows, one per `LightingMode`: 13 key-backlight tokens (12 readable + reserved `black`), 8 `_edge` tokens, line-shape flags, per-context direction sets, menu availability (RGB kb 14, TKO kb 14, TKO edge 10), `GatedByFeature`. `Find`, `FindByKeyBacklightToken`, `FindByEdgeToken` (case-insensitive). Starlight's token is `star`, **not** `starlight`.
- `IndicatorFunctionCatalog.All` — 8 functions, 12 tokens; `FindByToken` returns the row plus the `IndicatorLayer` a `lay*` token addresses.
- `FnLayerTokenTranslator.Pairs` — the 8 (memory, file) key-code pairs of 05 §5.5; `TranslateForSave`/`TranslateForLoad` pass unmapped codes through.
- `ExpansionPackDefaults.ProfileLines` — the §2.6-quoted factory files, keyed by profile number. Only profiles 1, 2, 6 are pinned by the spec; the rest are deliberately not invented.

## Firmware hooks — `LightingAvailability`

- `IsKeyBacklightModeAvailable(DeviceId, LightingMode, FirmwareState)` — menu membership + `FirmwareGateService` for gated modes: Ripple/Fireball gated on the RGB (KBD ≥ 1.0.121 and LED ≥ 1.0.58), automatically pass on the TKO (no gate row = available). `IsEdgeModeAvailable(mode)` — TKO edge menu membership.
- `IsFnLayerLightingAvailable(DeviceId, FirmwareState)` — the `LightingLayerCustomization` gate (RGB: LED ≥ 1.0.44; ungated elsewhere).
- `ContainsFnLayerLines(lines)` / `HasNoFnLayerLines(ledFiles)` — the lighting-side Expansion-Pack precondition (07 §3); callers combine it with `FirmwareFeature.ExpansionPackOffer`.

## Load-bearing invariants

1. **Freestyle is the fallback, never rejection.** A section whose first (and second) line matches no readable mode token parses as Freestyle per-key lines (07 §2.4 item 3); unknown lines are dropped, not errors. `[black]` is reserved — never matched on read, never written.
2. **Base vs. effect color by token, never by position.** Two-line effects accept both line orders (files in the field, incl. the §2.6 factory files, are effect-first); the `[mono]`/`[mono_edge]` line is always the base color (07 §2.4 item 5). Canonical output is mono-first.
3. **Round-trip is semantic, not byte-exact.** parse → serialize → parse yields an equivalent model; canonical output may reorder/expand legacy inputs (mono-first, explicit `[spd5][dirleft]`, fill-all expanded to per-key lines). Corner: Freestyle with zero colors serializes empty and reloads as Disabled (legacy-faithful).
4. **Fn translation is RGB-only, key-code → token, and the Gen1 tokens differ from the spec's key names.** Spec 05 §5.5 scopes the exception table to the FS-family bottom layer, so only the RGB key-backlight context translates (the TKO top layer has no F-row/`pause`/`del` positions). On the RGB Fn layer only: mute→`[F1]` … next→`[F6]`, insert→`[pause]`, scroll lock→`[del]`; the *emitted* tokens for the insert/scroll-lock memory keys are `ins`/`scrlk` per `KeyRegistry`, not the 07 §2.4 column names. Top-layer lines are never translated.
5. **Black is "no color".** Per-key maps never hold black; assigning it clears; fill-all with black clears the layer; "no color" serializes `[0][0][0]` (07 §2.1).
6. **Key tokens are layout-file tokens.** Per-key/per-LED addressing resolves through `KeyRegistry` Gen1, case-insensitive in, canonical casing out (07 §4); edge LEDs are the `KeyTable.EdgeZones` entries (codes 11113–11145).

## Deliberately not here

- **No FS Edge (non-RGB) backlight** — that is the `led_mode` settings value (spec 08), issue #10.
- **No disk I/O or profile pairing** (07 §1.1–§1.3) — reading/writing `ledN.txt` and the layout↔led pairing is profile orchestration, issue #11.
- **No file-import classification** (07 §1.4) — deliberately out of scope for this module.
- **No dropped-line diagnostics** — 07 §2.4 prescribes silent tolerant reading for led files, so there is no unparseable-line channel here by design; the collect-and-show rule (CLAUDE.md, specs/README.md) applies to layout files and lands with issue #8.
- **No lighting UI** — mode menus, color pickers, zones, previews (07 §3, §4, §7) are the editor UIs, issue #16; this module only answers availability queries.
