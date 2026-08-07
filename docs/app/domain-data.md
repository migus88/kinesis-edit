# Domain data (Devices, Keys, Geometry)

The static domain-data layer of `KinesisEdit.Core`: the spec's reference tables encoded as immutable C# data. Pure data — no I/O, no filesystem probing, no file parsing. Everything is reached through three static catalogs; callers never construct these types.

| Namespace | Entry point | Encodes | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Devices` | `DeviceCatalog` | Master device table + v-Drive detection/path data + macro/tap-and-hold limits | specs 02; 03 §1–4; 04 §5.3; 06 §1, §2.1, §4, §6; 11 §11.1 |
| `KinesisEdit.Core.Keys` | `KeyRegistry` | Master key-token table, three dialects | spec 05 §1–3, §7 |
| `KinesisEdit.Core.Geometry` | `GeometryCatalog` | Physical layer geometry per layout family | spec 05 §1.3–1.5, §4, §5.3–5.4 |
| `KinesisEdit.Core.Geometry.Visual` | `VisualCatalog` | Where each key position physically sits (board pictures, key units) | spec 05 §4 + spec 02 board descriptions |

## Devices — `DeviceCatalog`

- `All` — 9 `DeviceDefinition`s in legacy-app-id order 0→8: SE2, Advantage2, FS Edge, FS Pro, Edge RGB, CROSSFIRE keypad, TKO, Adv360, Adv360 Professional.
- `GetById(DeviceId)` — throws for unknown ids. `DeviceId` enum = `None` + those 9.
- `FindByVolumeLabel(string?)` — spec 03 §2 exact-match rule: uppercase + trim, then compare against each device's candidates (11 labels catalog-wide); null when nothing matches.
- Split across `DeviceCatalog.cs` (device definitions, detection/path data) and `DeviceCatalog.Capabilities.cs` (per-family `MacroCapability`/`TapAndHoldCapability`/`SettingsCapability` factories, shared as one instance per family).

`DeviceDefinition` (record) per device: ordered volume-label candidates (≤3, primary first, stored uppercase; empty for Adv360 Pro), marker folder/file for detection (03 §3.1), version/settings folder+file with per-device overrides baked in (Adv2/SE2 `active`, Adv360 `settings.txt` doubles as version file; 03 §3.3), `FormFactor` (`DeviceFormFactor`), `LayerCount`, `LayoutScheme` (`LayoutFileScheme`/`LayoutSchemeKind`: `NumberedProfiles` 1–9 — Adv360 additionally `HasReadOnlyFactoryProfile` for profile 0, Adv2 `QwertyDvorakPositions`, SE2 `PedalFile`, `None`), `Macros` (`MacroCapability`), `TapAndHold` (`TapAndHoldCapability`) — both tabulated below, `SupportsMultiModifiers`, `Lighting` (`LightingCapability` — `LightingKind`; TKO edge strip 9 left + 15 bottom + 9 right = 33; Adv360 6 indicator LEDs), `Settings` (`SettingsCapability` — which spec 08 §2 keys the app writes for the device and their forms: `StartupSettingKind`, `LedModeKind`, `StatusSettingKind`, `VDriveSettingKind`, tone/game/lock flags, `MacroSpeedMinimum`; consumed by the settings engine, [settings.md](settings.md); `SettingsCapability.None` for SE2/CROSSFIRE/Adv360 Pro), `ServingApp`, `ConfigurationUrl`/`SupportUrl` (Adv360 Pro), `TroubleshootingUrl`, `VDriveShortcutHint`, `HardwareNotes`, `AccessoryNote` (SE2 only), `IsProgrammable` (false: CROSSFIRE, Adv360 Pro — so `All.Count(d => d.IsProgrammable) == 7`, the "N of 7 known devices present" the dashboard counts against), `IsFutureDevice` (CROSSFIRE).

`SupportsMultiModifiers`: whether a key position may hold one of the 11 four-character combination codes (`MultiModifierCodes`). **True for the Advantage 360 alone.** 11 §11.2 titles the dialog "Advantage360 only" and its file syntax "Adv360 format only"; 05 §1.3 tags the `Multimodifiers` field "(Adv360)" and 05 §5.7 is headed "Multimodifiers (Adv360)". 04 §2.3's "written by the RGB-family serializer" is implementation lineage, not scope — 04 §4.3 calls the same writer "the RGB-family/Gen2 serializer" and 04 §1.3 scopes the detection rule to the shared "(Gen1 RGB/TKO and Gen2 parser)". The Adv360 Professional is `false`: it is configured through the ZMK web GUI and is not SmartSet-programmable at all. Not to be confused with the `hyper`/`meh` key tokens (codes 11090/11091), whose firmware gates 09 §2 calls "Hyper/Meh multimodifiers" — a different feature.

Three URL members, three different sources — never collapse them: `ConfigurationUrl`/`SupportUrl` are the Adv360 Pro ZMK GUI + help links of spec 02; `TroubleshootingUrl` is the "Troubleshooting Tips" target of the keyboard-not-detected dialog (spec 11 §11.8), set for all 7 programmable devices and null for CROSSFIRE/Adv360 Pro; the `#firmware`-anchored upgrade pages live in `KinesisEdit.Core.Firmware.FirmwareSupportUrls` (spec 09 §2). Spec 11 §11.8 gives Adv360 as "same as the Adv360 help URL" rather than a literal, so `https://kinesis-ergo.com/support/kb360/` is derived as the un-anchored form of its spec 09 firmware URL.

`VDriveShortcutHint` is set for all 7 programmable devices: spec 03 §1 covers Adv2/Adv360/RGB/TKO/FS Edge/FS Pro, and SE2's `Program + F1` comes from spec 12 §3.

`FirmwareVersion` (same namespace): value type for version-file text per spec 09 §1.1 — first three dot-separated numeric tokens → major/minor/revision, non-numeric minor/revision → 0, trailing text ignored; lexicographic `IComparable<FirmwareVersion>`. Gate *data* references it; gate *evaluation* does not live here.

`ValueRange` (same namespace): `sealed record ValueRange(int Minimum, int Maximum, int Default)` + `Contains(int)`. Inclusive bounds plus the value applied when the file carries none. Used for macro speed/repeat (06 §4) and the tap-and-hold delay (11 §11.1).

### Card meta line — `DeviceFormFactor` + `DeviceMetaLine`

`DeviceFormFactor` is the **design** vocabulary for a device's shape, not spec prose: `None` (CROSSFIRE), `SplitFlat` (FS Edge, FS Pro, Edge RGB), `SplitContoured` (Adv2, Adv360, Adv360 Pro), `SixtyPercentGaming` (TKO), `ThreeButtonFootPedal` (SE2). It is new data rather than a projection of `HardwareNotes`, which quotes spec 02 verbatim ("60% tenkeyless gaming board with tripartite space bar…") and would print the wrong string.

`DeviceMetaLine.Describe(DeviceDefinition)` → the dashboard card's one-line summary (design mockups 1b/2e), segments joined with `" · "` (U+00B7, spaced), in order: form factor → layers → lighting → `AccessoryNote`. **Segments with no data are omitted**, so there is never an empty segment or a dangling separator; a device with nothing describable yields `""` (CROSSFIRE). Layers pluralise (`1 layer`/`2 layers`) and are dropped when `LayerCount` is null or ≤ 0. Lighting reads `per-key RGB`, `per-key + edge RGB` (`PerKeyRgb` + `HasEdgeLighting`), `N indicator LEDs` (pluralised, dropped at count 0), `blue backlight` (`BlueBacklight` — no mockup shows one, so the wording follows spec 02's own phrase), nothing for `None`. Results, pinned by test: SE2 `3-button foot pedal · accessory jack`; Adv2 `Split contoured · 2 layers`; FS Edge `Split flat · 2 layers · blue backlight`; FS Pro `Split flat · 2 layers`; Edge RGB `Split flat · 2 layers · per-key RGB`; TKO `60% gaming · 2 layers · per-key + edge RGB`; Adv360 `Split contoured · 5 layers · 6 indicator LEDs`; Adv360 Pro `Split contoured`; CROSSFIRE `""`.

It lives in Core so the strings are testable headlessly and **no view carries per-device code** — a view calls `Describe` and renders the result. `AccessoryNote` is a single display-ready datum on the definition (SE2's `accessory jack`, from spec 02's "3 pedals + jack" and the `jack1`–`jack4` positions of 05 §3.14), deliberately not a general accessory capability for one device; the describer stays a pure formatter over properties.

### `MacroCapability` per device (02 master table; 04 §5.3; 06 §1, §2.1, §4, §6)

| | SE2 | Adv2 | FS Edge / FS Pro | Edge RGB / TKO | Adv360 | CROSSFIRE / 360 Pro |
|---|---|---|---|---|---|---|
| `IsSupported` | true | true | true | true | true | false (`None`) |
| `MaxMacroCount` | – | – | 24 | 100 | 100 | – |
| `GatedMaxMacroCount` / `MacroCountGateFirmware` | – | – | 100 @ 1.0.340 | – | – | – |
| `MaxCharactersPerMacro` | – | 300 (weighted keystrokes) | 300 (weighted keystrokes) | 300 (weighted keystrokes) | 500 (serialized keystroke text) | – |
| `MaxTotalKeystrokes` | – | – | 7200 | 7200 | 7200 | – |
| `SlotsPerKey` / `PersistedSlotsPerKey` | – | 5 / 3 | 5 / 3 | 5 / 5 | – (flat list) | – |
| `UsesFlatMacroList` | false | false | false | false | **true** | false |
| `MaxCoTriggersPerMacro` / `PersistedCoTriggersPerMacro` | – | 3 / 3 | 4 / **1** | 4 / 4 | 4 / 4 | – |
| `Speed` (min–max, default) | – | 0–9, 0 | 0–9, 0 | 1–9, 5 | 1–9, 5 | – |
| `Repeat` (min–max, default) | – | 0–9, 0 | 0–9, 0 | 1–9, 1 | 1–9, 1 | – |
| `PersistsRepeat` | false | **false** | true | true | true | false |
| `ClampsOutOfRangeValues` | false | false | false | false | **true** | false |

Notes: `– ` = null, i.e. the spec states no value. The two per-macro metrics are not interchangeable: "weighted keystrokes" is 1 per keystroke plus 2 per attached modifier (04 §5.3, the same count as the 7200 budget), while the Adv360 500 measures the serialized value side of the macro line (06 §6) — see [`keyboard-model.md`](keyboard-model.md). Speed `0` means "use the keyboard's global speed" (`macro_speed=` in the settings file). Persisted ≠ model on purpose, and a UI must offer only what the file keeps: the FS/Adv2 serializers write only slots 1–3 (06 §1), the old FS parser/serializer keeps only the first co-trigger (06 §2.1, §3), and the Adv2 serializer writes `{speedN}` but **no** repeat token (06 §3) — the last of which is `PersistsRepeat`, so the Adv2 models a repeat range it never writes and an editor hides the control rather than offering a value the next save discards. SE2 has no per-macro speed/repeat setting at all — the pedal dialect embeds `speed1/3/5` tokens inside the macro (12 §4.4, §6). Adv2 has no macro-count or layout-keystroke limit in the spec. Adv360 keeps macros in one flat per-layout list tagged with trigger key + layer, so it has no slots.

### `TapAndHoldCapability` per device (11 §11.1; 04 §5.3; 09 §2)

| | SE2 | Adv2 | FS Edge / FS Pro | Edge RGB | TKO | Adv360 | CROSSFIRE / 360 Pro |
|---|---|---|---|---|---|---|---|
| `IsSupported` | false (`None`) | true | true | true | true | true | false (`None`) |
| `MaxPerLayout` | – | 10 | 10 | 10 | 10 | 10 | – |
| `DelayMilliseconds` | – | 1–999 | 1–999 | 1–999 | 1–999 | 1–999 | – |
| `DefaultDelayMilliseconds` (= `DelayMilliseconds.Default`) | – | 250 | 250 | 250 | 250 | **150** | – |
| `MinimumFirmware` | – | 1.0.516 | 1.0.480 | 1.0.1 | – | – | – |

Firmware minimums are data only (never compared here). File syntax is `[position]>[tap][t&h<delay>][hold]` (11 §11.1) — parsing/serializing lives elsewhere.

## Keys — `KeyRegistry`

- `Entries` — ONE flat `IReadOnlyList<KeyDefinition>` of **1282** entries reproducing the spec's registration order: tables 3.1 → 3.13, row order preserved within each table. This order is the contract.
- `FindByCode(int)`, `FindByToken(string?)`, `FindByToken(string?, TokenDialect)` — return the **first** entry in registration order, or null. Token matching is case-insensitive; null/empty/whitespace queries never match; `TokenDialect.None` searches all dialects.
- Duplicated codes and tokens are **intentional** (spec 05 §7); first-match reproduces legacy resolution. Examples: token `numlk` resolves to the VK NumLock entry (144, table 3.3), not the later 10052 row in 3.6; code 10087 resolves to `dran` (3.12), which shadows the generated `d002`; the explicit `d125`/`d500` rows of 3.12 shadow their generated twins.
- `PedalPositionTokens` — 7 strings (`lpedal`, `mpedal`, `rpedal`, `jack1`..`jack4`; §3.14), deliberately *not* key-table entries.

`KeyDefinition` (record): `Code` (Windows VK < 256 or legacy internal ≥ 10000; §2), `Table` (`KeyTable` — enum values equal the spec table numbers 3.1–3.13), `Dialects` (`TokenDialects` flags = the spec's Family column), per-dialect file tokens `LegacyToken`/`Gen1Token`/`Gen2Token` stored in canonical casing (empty = absent in that dialect; only tokens ever appear on disk), display data (`DisplayText` with `\n` two-line captions, per-dialect + `MacDisplayText` overrides, `ShiftedValue`, `GlyphText` Unicode-glyph caption with `DisplayText` as fallback), `Flags` (`KeyDefinitionFlags`: `HiddenFromSearch`, `NotWritable`, `ConvertToUnicode`, `ShowShiftedValue`, `SingleEvent`). Helpers: `GetToken`, `HasToken`, `IsAvailableIn`, `GetDisplayText`.

**`GlyphText` is advisory, not a caption the app must print.** 05 §3.7/§6 gate the glyph column on capability, and the app renders it only where its own type can (today: nowhere — none of the 23 glyph-carrying entries' 17 distinct glyphs is in an embedded face, so every one falls back to its plain caption). Core states the spec; the choice is the UI's — see [keyboard-editor.md](keyboard-editor.md), deviation 52.

Per-table entry counts (pinned by tests):

| 3.1 | 3.2 | 3.3 | 3.4 | 3.5 | 3.6 | 3.7 | 3.8 | 3.9 | 3.10 | 3.11 | 3.12 | 3.13 | Total |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 36 | 12 | 24 | 24 | 14 | 36 | 21 | 11 | 22 | 14 | 21 | 1014 | 33 | **1282** |

Count notes: 3.3 includes the VK_PRINT prose row; 3.6 includes the 15 Legacy keypad-layer duplicates; 3.9 includes 2 prose duplicates; 3.12 = the explicit speed/delay rows plus generated `d001`..`d999` (code = 10085 + n).

**`KeySearchCatalog`** (same namespace) is the one derived view over this table: the Search Keys list of 11 §11.6, built in registration order minus the `HiddenFromSearch` rows and the entries a dialect does not name, plus a case-insensitive filter over name and token. It adds no data — see [feature-dialogs.md](feature-dialogs.md) for the composition rule and for which "non-searchable" rows §11.6 mentions are *not* flagged in the table (the §3.6 keypad duplicates and the §3.11 hotkeys, which are therefore listed).

## Geometry — `GeometryCatalog`

- Seven layout-family properties: `FreestyleEdge`, `FreestyleEdgeRgb`, `FreestylePro` (2 layers × 95 each), `Tko` (2 × 63 keys + 33 `EdgeZones` per layer), `Advantage2Qwerty` / `Advantage2Dvorak` (2 × 89 each), `Advantage360` (5 × 77).
- `TryGet(DeviceId, out DeviceGeometry?)` / `TryGet(DeviceId, LayoutVariant, out ...)` — Advantage2 defaults to QWERTY; SE2 (pedal device), CROSSFIRE, and Adv360 Pro (ZMK web GUI) have no geometry → false.

Types:

- `DeviceGeometry` — `Variant` (`LayoutVariant`: `None`/`Qwerty`/`Dvorak`; only Adv2 ships Dvorak) + ordered `Layers`.
- `LayerGeometry` — spec-literal `Name` (`Qwerty-top`/`Qwerty-keypad`, `Dvorak-top`/`Dvorak-keypad`, `Base`/`Keypad`/`Fn1`/`Fn2`/`Fn3`), `Index` (spec 05 §1.4), `Keys` (dense 0..N−1, self-validated — the constructor throws on gaps or misordered indices), `EdgeZones` (TKO only: lighting zones `L1`..`L9`, `B1`..`B15`, `R1`..`R9`; kept out of `Keys` because they are not typing keys).
- `KeyPosition` (record) — `Index` (GUI button id / file-ordering key; identical positions share indices across a device's layers), `DefaultToken` (factory default in the family's dialect; empty for the Adv2 Keypad/Program buttons, which are never written to files), `PositionToken` (non-null only where the physical-position name differs from the default action — the Adv360 `(pos:x)` mechanism, e.g. `keyt` at position `kp`), `MasterAppDefaultToken` (Adv2 top-layer pedal positions 86–88 carry dual defaults: master-app vs standalone), `CanEdit` (false for locked positions: Adv2 Keypad/Program, SmartSet key on TKO/Adv360), `CanAssignMacro` (false for the physical modifier positions on Gen1 boards and Adv2; Adv360 modifiers and the TKO `fnshf` position allow macros).

Every layer is **fully materialized**: the spec's delta descriptions (RGB vs Edge, Dvorak vs QWERTY, Adv360 Keypad/Fn overlays, Fn2/Fn3 ≡ Fn1 apart from name/index) are applied at build time by internal builders (`FreestyleGeometry`, `TkoGeometry`, `Advantage2Geometry`, `Advantage360Geometry`, `LayerBuilder`). Consumers never apply deltas.

## Visual geometry — `VisualCatalog` (`KinesisEdit.Core.Geometry.Visual`)

`GeometryCatalog` says *which* key positions a device has; `VisualCatalog` says *where they sit*. UI-free data (no Avalonia), consumed by the keyboard-shaped view ([keyboard-editor.md](keyboard-editor.md)).

- **Key units.** `1.0` = one 1U keycap, width and height. Coordinates are board-absolute, address the key's **top-left** corner, X grows right, Y grows down. A renderer picks a pixel-per-unit scale and multiplies — nothing here is in pixels, millimetres or DIPs.
- **One visual per device, shared by all its layers.** Layers differ only in the tokens bound to a position (spec 05 §7.4 — identical positions share an index across layers), never in placement, so the view draws the same rectangles for every layer and swaps only the captions.
- **Authored, not derived.** Coordinates come from the physical board (spec 02 descriptions + the row grouping of spec 05 §4's index-ordered token lists), hand-authored per device. Legible and index-complete is the requirement, not millimetre accuracy.

Types:

- `VisualCatalog` — `FreestyleEdgeRgb` property; `TryGet(DeviceId, out KeyboardVisual?)` / `TryGet(DeviceId, LayoutVariant, out ...)` with **exactly** `GeometryCatalog`'s semantics (`LayoutVariant.None` = device default; any other variant must match the authored one — no silent fallback).
- `KeyboardVisual` — `Variant`, `Keys` (authoring order, not index order), `Width`/`Height` (= max `Right`/`Bottom`; `0` when empty), `TryGetKey(int index, out KeyVisual?)` over a dictionary built in the constructor. Throws `ArgumentException` on duplicate indices.
- `KeyVisual` (record) — `Index` (**the same ordinal as `KeyPosition.Index`**), `X`, `Y`, `Width`, `Height` (both default `1.0`), `Cluster`, plus computed `Right`/`Bottom`. Constructor throws `ArgumentOutOfRangeException` for a negative index/X/Y or a non-positive width/height.
- `KeyCluster` — presentational grouping only (the logical geometry has no cluster concept): `None`/`Main`/`Thumb` (contoured boards)/`Function` (dedicated hotkey columns)/`EdgeZone` (TKO LED zones)/`Pedal`.
- `KeyVisualBuilder` (internal) — row cursor helper: `Row(x, y, cluster)` then `Key`/`Keys`/`Range` walk left to right. Same role `LayerBuilder` plays for tokens.

**Authored devices: Freestyle Edge RGB only.** `FreestyleEdgeRgbVisual` places all 95 positions as three blocks — the left-edge hotkey column (`hk0` spanning the column top, then `hk1`–`hk10` in a 2×5 grid, `KeyCluster.Function`), the left typing half, and the right typing half after a 1U `SplitGap`; both halves are `KeyCluster.Main` and the right half carries the standard ANSI row stagger. Bounds: 19.75 × 6 units. Every other `DeviceId` returns `false` — #39 authors FS Edge/Pro, #40 the TKO (incl. its 33 edge zones), #41 the Advantage 360, #42 the Advantage2 (both variants), adding data only (the types are device-agnostic).

**Invariant:** a device's visual indices must be *exactly* the index set of its logical geometry layers — no missing key, no extra key, no overlapping rectangles. Enforced in both directions by `KinesisEdit.Core.Tests/Geometry/Visual`; adding a device visual means adding the matching set-equality test.

### Spatial navigation — `KeyAdjacency`

`KeyAdjacency.Next(KeyboardVisual visual, int fromIndex, NavigationDirection direction)` → the `KeyVisual` a user moving that way should land on, or **null** when nothing lies that way (a board edge, or an index the visual does not carry — the caller keeps its current selection). `NavigationDirection` is `None`/`Up`/`Down`/`Left`/`Right`; `None` throws `ArgumentOutOfRangeException`, a null visual throws `ArgumentNullException`. Pure geometry, device-agnostic: any authored board works, including ones not yet written.

Scoring, best first — rectangle overlap plus centre distance:

1. A candidate's **centre** must lie strictly on the direction's side of the source's centre (epsilon `1e-6`; coordinates are doubles).
2. Candidates whose **perpendicular span overlaps** the source's span — the Y span for Left/Right, the X span for Up/Down — always beat those that do not.
3. A candidate with **no** perpendicular overlap is admitted only per the asymmetric rule below; when admitted it ranks behind every overlapping candidate.
4. Inside a tier: smaller primary-axis centre distance, then smaller perpendicular centre distance.
5. Tie-break on ascending `Index`, so symmetric boards answer deterministically (from the "s" cap, Down ties two shift-row caps and the lower index wins).

**The non-overlapping tier is asymmetric by direction. Do not "simplify" the asymmetry out — each half is load-bearing:**

- **Left/Right require row overlap**: a candidate that shares none of the source's Y span is discarded outright. "The key to my left" is always on my row; there is no board where it is not. Without the rule, the 2U `hk0` at the board's top-left (centre X `1.0`) pulled in every 1U hotkey below it (centre X `0.5` — strictly left, no Y overlap), and Left on the top-left key walked the user diagonally down the hotkey column instead of returning null.
- **Up/Down keep the fallback, bounded by a 45° cone** (`perpendicularDistance <= primaryDistance`). Deleting the vertical fallback would strand real keys: the Advantage2 and Advantage360 thumb clusters sit clear of the main well with **no X overlap at all**, so a thumb key one row down and one unit across must stay reachable. The cone is what keeps that from also licensing a three-row diagonal teleport across the board.

**No row or column arithmetic — this is the constraint, not a style preference.** `KeyVisual` carries only X/Y/Width/Height in key units, and the boards have no rows or columns to recover: the Freestyle Edge RGB lays hotkey column, left half, 1U split gap and right half into one continuous coordinate space, and the right half's rows are staggered by a *different* offset per row (0.0 / 0.25 / 0.5 / 0.75). Bucketing keys into integer rows or columns answers wrongly on exactly those rows. Compare centres and spans, never bare `X`/`Y` — caps differ in width and height (2U Backspace, 3.5U space bar). Crossing the split gap needs no special case: the row overlap wins the scoring on its own.

`KeyAdjacencyTests` pins the four directions, both crossings of the split gap, entering/leaving the hotkey column, the board edges (including `hk0`'s Left), the two halves of the asymmetric rule on synthetic visuals, an unknown index, **and a breadth-first walk that must reach all 95 keys** — the reachability test is what catches a scoring bug the per-direction cases miss, and it is the guard that a tighter rule has not orphaned a cap. If a key ever becomes unreachable, widen the cone; never delete the walk. A new authored board should add its own reachability test.

## Load-bearing invariants

1. **Spec order + first match.** `KeyRegistry.Entries` order and first-match lookup reproduce the legacy resolution of intentionally duplicated codes/tokens (spec 05 §7). Never sort, dedupe, or reorder.
2. **Case-insensitive in, canonical case out.** Lookups are case-insensitive (the parsing rule); stored tokens keep the spec's canonical casing (what serializers must write).
3. **Everything immutable.** Records and read-only lists throughout. Geometry describes the factory shape of a device; remaps/macros are stored elsewhere, never by mutating these tables.
4. **Counts are pinned.** The table counts and geometry sizes above are asserted by `KinesisEdit.Core.Tests`; changing one signals a spec deviation, not a refactor.

## Deliberately not here

- **No I/O or drive discovery** — detection *data* only (labels, marker paths); probing the filesystem is a later module.
- **No firmware-gate evaluation** (spec 09 §2) — gates are carried as data (`MacroCountGateFirmware`, `TapAndHoldCapability.MinimumFirmware`); comparing them to a device's actual firmware happens in `KinesisEdit.Core.Firmware` (see [`firmware.md`](firmware.md)). **Those versions are also encoded as `FirmwareGate` entries** in `FirmwareGateCatalog` — four spec 09 §2 numbers in two places: the FS Edge/Pro macro-count gate (1.0.340 ↔ `ExpandedMacroCount`) and the three tap-and-hold minimums (Adv2 1.0.516, FS Edge/Pro 1.0.480, RGB 1.0.1 ↔ `TapAndHold`). `FirmwareGateCatalog` is the authority for *evaluation*; the capability fields exist so a device's limits can be read without a firmware probe. The agreement is enforced in both directions by `KinesisEdit.Core.Tests/Integration/FirmwareGateConsistencyTests.cs` — changing one side without the other fails CI.
- **No macro/tap-and-hold behaviour** — the catalog states the limits, ranges and defaults; counting keystrokes, clamping out-of-range speed/repeat, and enforcing the caps belong to the model (see [`keyboard-model.md`](keyboard-model.md)) and the parsers.

- **No parsers/serializers** for layout/macro files (specs 04, 06) — this module is the vocabulary those consume; they live in `KinesisEdit.Core.Layouts` (see [`layout-files.md`](layout-files.md)).
- **No legacy font/rendering metadata** from the spec 05 tables (font names/sizes of the Pascal UI).
