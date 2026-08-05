# Domain data (Devices, Keys, Geometry)

The static domain-data layer of `KinesisEdit.Core`: the spec's reference tables encoded as immutable C# data. Pure data — no I/O, no filesystem probing, no file parsing. Everything is reached through three static catalogs; callers never construct these types.

| Namespace | Entry point | Encodes | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Devices` | `DeviceCatalog` | Master device table + v-Drive detection/path data | specs 02; 03 §1–4 |
| `KinesisEdit.Core.Keys` | `KeyRegistry` | Master key-token table, three dialects | spec 05 §1–3, §7 |
| `KinesisEdit.Core.Geometry` | `GeometryCatalog` | Physical layer geometry per layout family | spec 05 §1.3–1.5, §4, §5.3–5.4 |

## Devices — `DeviceCatalog`

- `All` — 9 `DeviceDefinition`s in legacy-app-id order 0→8: SE2, Advantage2, FS Edge, FS Pro, Edge RGB, CROSSFIRE keypad, TKO, Adv360, Adv360 Professional.
- `GetById(DeviceId)` — throws for unknown ids. `DeviceId` enum = `None` + those 9.
- `FindByVolumeLabel(string?)` — spec 03 §2 exact-match rule: uppercase + trim, then compare against each device's candidates (11 labels catalog-wide); null when nothing matches.

`DeviceDefinition` (record) per device: ordered volume-label candidates (≤3, primary first, stored uppercase; empty for Adv360 Pro), marker folder/file for detection (03 §3.1), version/settings folder+file with per-device overrides baked in (Adv2/SE2 `active`, Adv360 `settings.txt` doubles as version file; 03 §3.3), `LayerCount`, `LayoutScheme` (`LayoutFileScheme`/`LayoutSchemeKind`: `NumberedProfiles` 1–9 — Adv360 additionally `HasReadOnlyFactoryProfile` for profile 0, Adv2 `QwertyDvorakPositions`, SE2 `PedalFile`, `None`), `Macros` (`MacroCapability` — the FS Edge/Pro 24→100 macro bump is pure data: `GatedMaxMacroCount` + `MacroCountGateFirmware` 1.0.340, never evaluated here), `Lighting` (`LightingCapability` — `LightingKind`; TKO edge strip 9 left + 15 bottom + 9 right = 33; Adv360 6 indicator LEDs), `ServingApp`, `ConfigurationUrl`/`SupportUrl` (Adv360 Pro), `VDriveShortcutHint`, `HardwareNotes`, `IsProgrammable` (false: CROSSFIRE, Adv360 Pro), `IsFutureDevice` (CROSSFIRE).

`FirmwareVersion` (same namespace): value type for version-file text per spec 09 §1.1 — first three dot-separated numeric tokens → major/minor/revision, non-numeric minor/revision → 0, trailing text ignored; lexicographic `IComparable<FirmwareVersion>`. Gate *data* references it; gate *evaluation* does not live here.

## Keys — `KeyRegistry`

- `Entries` — ONE flat `IReadOnlyList<KeyDefinition>` of **1282** entries reproducing the spec's registration order: tables 3.1 → 3.13, row order preserved within each table. This order is the contract.
- `FindByCode(int)`, `FindByToken(string?)`, `FindByToken(string?, TokenDialect)` — return the **first** entry in registration order, or null. Token matching is case-insensitive; null/empty/whitespace queries never match; `TokenDialect.None` searches all dialects.
- Duplicated codes and tokens are **intentional** (spec 05 §7); first-match reproduces legacy resolution. Examples: token `numlk` resolves to the VK NumLock entry (144, table 3.3), not the later 10052 row in 3.6; code 10087 resolves to `dran` (3.12), which shadows the generated `d002`; the explicit `d125`/`d500` rows of 3.12 shadow their generated twins.
- `PedalPositionTokens` — 7 strings (`lpedal`, `mpedal`, `rpedal`, `jack1`..`jack4`; §3.14), deliberately *not* key-table entries.

`KeyDefinition` (record): `Code` (Windows VK < 256 or legacy internal ≥ 10000; §2), `Table` (`KeyTable` — enum values equal the spec table numbers 3.1–3.13), `Dialects` (`TokenDialects` flags = the spec's Family column), per-dialect file tokens `LegacyToken`/`Gen1Token`/`Gen2Token` stored in canonical casing (empty = absent in that dialect; only tokens ever appear on disk), display data (`DisplayText` with `\n` two-line captions, per-dialect + `MacDisplayText` overrides, `ShiftedValue`, `GlyphText` Unicode-glyph caption with `DisplayText` as fallback), `Flags` (`KeyDefinitionFlags`: `HiddenFromSearch`, `NotWritable`, `ConvertToUnicode`, `ShowShiftedValue`, `SingleEvent`). Helpers: `GetToken`, `HasToken`, `IsAvailableIn`, `GetDisplayText`.

Per-table entry counts (pinned by tests):

| 3.1 | 3.2 | 3.3 | 3.4 | 3.5 | 3.6 | 3.7 | 3.8 | 3.9 | 3.10 | 3.11 | 3.12 | 3.13 | Total |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 36 | 12 | 24 | 24 | 14 | 36 | 21 | 11 | 22 | 14 | 21 | 1014 | 33 | **1282** |

Count notes: 3.3 includes the VK_PRINT prose row; 3.6 includes the 15 Legacy keypad-layer duplicates; 3.9 includes 2 prose duplicates; 3.12 = the explicit speed/delay rows plus generated `d001`..`d999` (code = 10085 + n).

## Geometry — `GeometryCatalog`

- Seven layout-family properties: `FreestyleEdge`, `FreestyleEdgeRgb`, `FreestylePro` (2 layers × 95 each), `Tko` (2 × 63 keys + 33 `EdgeZones` per layer), `Advantage2Qwerty` / `Advantage2Dvorak` (2 × 89 each), `Advantage360` (5 × 77).
- `TryGet(DeviceId, out DeviceGeometry?)` / `TryGet(DeviceId, LayoutVariant, out ...)` — Advantage2 defaults to QWERTY; SE2 (pedal device), CROSSFIRE, and Adv360 Pro (ZMK web GUI) have no geometry → false.

Types:

- `DeviceGeometry` — `Variant` (`LayoutVariant`: `None`/`Qwerty`/`Dvorak`; only Adv2 ships Dvorak) + ordered `Layers`.
- `LayerGeometry` — spec-literal `Name` (`Qwerty-top`/`Qwerty-keypad`, `Dvorak-top`/`Dvorak-keypad`, `Base`/`Keypad`/`Fn1`/`Fn2`/`Fn3`), `Index` (spec 05 §1.4), `Keys` (dense 0..N−1, self-validated — the constructor throws on gaps or misordered indices), `EdgeZones` (TKO only: lighting zones `L1`..`L9`, `B1`..`B15`, `R1`..`R9`; kept out of `Keys` because they are not typing keys).
- `KeyPosition` (record) — `Index` (GUI button id / file-ordering key; identical positions share indices across a device's layers), `DefaultToken` (factory default in the family's dialect; empty for the Adv2 Keypad/Program buttons, which are never written to files), `PositionToken` (non-null only where the physical-position name differs from the default action — the Adv360 `(pos:x)` mechanism, e.g. `keyt` at position `kp`), `MasterAppDefaultToken` (Adv2 top-layer pedal positions 86–88 carry dual defaults: master-app vs standalone), `CanEdit` (false for locked positions: Adv2 Keypad/Program, SmartSet key on TKO/Adv360), `CanAssignMacro` (false for the physical modifier positions on Gen1 boards and Adv2; Adv360 modifiers and the TKO `fnshf` position allow macros).

Every layer is **fully materialized**: the spec's delta descriptions (RGB vs Edge, Dvorak vs QWERTY, Adv360 Keypad/Fn overlays, Fn2/Fn3 ≡ Fn1 apart from name/index) are applied at build time by internal builders (`FreestyleGeometry`, `TkoGeometry`, `Advantage2Geometry`, `Advantage360Geometry`, `LayerBuilder`). Consumers never apply deltas.

## Load-bearing invariants

1. **Spec order + first match.** `KeyRegistry.Entries` order and first-match lookup reproduce the legacy resolution of intentionally duplicated codes/tokens (spec 05 §7). Never sort, dedupe, or reorder.
2. **Case-insensitive in, canonical case out.** Lookups are case-insensitive (the parsing rule); stored tokens keep the spec's canonical casing (what serializers must write).
3. **Everything immutable.** Records and read-only lists throughout. Geometry describes the factory shape of a device; remaps/macros are stored elsewhere, never by mutating these tables.
4. **Counts are pinned.** The table counts and geometry sizes above are asserted by `KinesisEdit.Core.Tests`; changing one signals a spec deviation, not a refactor.

## Deliberately not here

- **No I/O or drive discovery** — detection *data* only (labels, marker paths); probing the filesystem is a later module.
- **No firmware-gate evaluation** (spec 09 §2) — gates are carried as data (`MacroCountGateFirmware`); comparing them to a device's actual firmware happens elsewhere.
- **No parsers/serializers** for layout/macro files (specs 04, 06) — this module is the vocabulary those will consume.
- **No legacy font/rendering metadata** from the spec 05 tables (font names/sizes of the Pascal UI).
