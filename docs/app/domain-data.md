# Domain data (Devices, Keys, Geometry)

The static domain-data layer of `KinesisEdit.Core`: the spec's reference tables encoded as immutable C# data. Pure data — no I/O, no filesystem probing, no file parsing. Everything is reached through three static catalogs; callers never construct these types.

| Namespace | Entry point | Encodes | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Devices` | `DeviceCatalog` | Master device table + v-Drive detection/path data + macro/tap-and-hold limits | specs 02; 03 §1–4; 04 §5.3; 06 §1, §2.1, §4, §6; 11 §11.1 |
| `KinesisEdit.Core.Keys` | `KeyRegistry` | Master key-token table, three dialects | spec 05 §1–3, §7 |
| `KinesisEdit.Core.Geometry` | `GeometryCatalog` | Physical layer geometry per layout family | spec 05 §1.3–1.5, §4, §5.3–5.4 |

## Devices — `DeviceCatalog`

- `All` — 9 `DeviceDefinition`s in legacy-app-id order 0→8: SE2, Advantage2, FS Edge, FS Pro, Edge RGB, CROSSFIRE keypad, TKO, Adv360, Adv360 Professional.
- `GetById(DeviceId)` — throws for unknown ids. `DeviceId` enum = `None` + those 9.
- `FindByVolumeLabel(string?)` — spec 03 §2 exact-match rule: uppercase + trim, then compare against each device's candidates (11 labels catalog-wide); null when nothing matches.
- Split across `DeviceCatalog.cs` (device definitions, detection/path data) and `DeviceCatalog.Capabilities.cs` (per-family `MacroCapability`/`TapAndHoldCapability`/`SettingsCapability` factories, shared as one instance per family).

`DeviceDefinition` (record) per device: ordered volume-label candidates (≤3, primary first, stored uppercase; empty for Adv360 Pro), marker folder/file for detection (03 §3.1), version/settings folder+file with per-device overrides baked in (Adv2/SE2 `active`, Adv360 `settings.txt` doubles as version file; 03 §3.3), `LayerCount`, `LayoutScheme` (`LayoutFileScheme`/`LayoutSchemeKind`: `NumberedProfiles` 1–9 — Adv360 additionally `HasReadOnlyFactoryProfile` for profile 0, Adv2 `QwertyDvorakPositions`, SE2 `PedalFile`, `None`), `Macros` (`MacroCapability`), `TapAndHold` (`TapAndHoldCapability`) — both tabulated below, `SupportsMultiModifiers`, `Lighting` (`LightingCapability` — `LightingKind`; TKO edge strip 9 left + 15 bottom + 9 right = 33; Adv360 6 indicator LEDs), `Settings` (`SettingsCapability` — which spec 08 §2 keys the app writes for the device and their forms: `StartupSettingKind`, `LedModeKind`, `StatusSettingKind`, `VDriveSettingKind`, tone/game/lock flags, `MacroSpeedMinimum`; consumed by the settings engine, [settings.md](settings.md); `SettingsCapability.None` for SE2/CROSSFIRE/Adv360 Pro), `ServingApp`, `ConfigurationUrl`/`SupportUrl` (Adv360 Pro), `TroubleshootingUrl`, `VDriveShortcutHint`, `HardwareNotes`, `IsProgrammable` (false: CROSSFIRE, Adv360 Pro), `IsFutureDevice` (CROSSFIRE).

`SupportsMultiModifiers`: whether a key position may hold one of the 11 four-character combination codes (`MultiModifierCodes`). **True for the Advantage 360 alone.** 11 §11.2 titles the dialog "Advantage360 only" and its file syntax "Adv360 format only"; 05 §1.3 tags the `Multimodifiers` field "(Adv360)" and 05 §5.7 is headed "Multimodifiers (Adv360)". 04 §2.3's "written by the RGB-family serializer" is implementation lineage, not scope — 04 §4.3 calls the same writer "the RGB-family/Gen2 serializer" and 04 §1.3 scopes the detection rule to the shared "(Gen1 RGB/TKO and Gen2 parser)". The Adv360 Professional is `false`: it is configured through the ZMK web GUI and is not SmartSet-programmable at all. Not to be confused with the `hyper`/`meh` key tokens (codes 11090/11091), whose firmware gates 09 §2 calls "Hyper/Meh multimodifiers" — a different feature.

Three URL members, three different sources — never collapse them: `ConfigurationUrl`/`SupportUrl` are the Adv360 Pro ZMK GUI + help links of spec 02; `TroubleshootingUrl` is the "Troubleshooting Tips" target of the keyboard-not-detected dialog (spec 11 §11.8), set for all 7 programmable devices and null for CROSSFIRE/Adv360 Pro; the `#firmware`-anchored upgrade pages live in `KinesisEdit.Core.Firmware.FirmwareSupportUrls` (spec 09 §2). Spec 11 §11.8 gives Adv360 as "same as the Adv360 help URL" rather than a literal, so `https://kinesis-ergo.com/support/kb360/` is derived as the un-anchored form of its spec 09 firmware URL.

`VDriveShortcutHint` is set for all 7 programmable devices: spec 03 §1 covers Adv2/Adv360/RGB/TKO/FS Edge/FS Pro, and SE2's `Program + F1` comes from spec 12 §3.

`FirmwareVersion` (same namespace): value type for version-file text per spec 09 §1.1 — first three dot-separated numeric tokens → major/minor/revision, non-numeric minor/revision → 0, trailing text ignored; lexicographic `IComparable<FirmwareVersion>`. Gate *data* references it; gate *evaluation* does not live here.

`ValueRange` (same namespace): `sealed record ValueRange(int Minimum, int Maximum, int Default)` + `Contains(int)`. Inclusive bounds plus the value applied when the file carries none. Used for macro speed/repeat (06 §4) and the tap-and-hold delay (11 §11.1).

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
| `ClampsOutOfRangeValues` | false | false | false | false | **true** | false |

Notes: `– ` = null, i.e. the spec states no value. The two per-macro metrics are not interchangeable: "weighted keystrokes" is 1 per keystroke plus 2 per attached modifier (04 §5.3, the same count as the 7200 budget), while the Adv360 500 measures the serialized value side of the macro line (06 §6) — see [`keyboard-model.md`](keyboard-model.md). Speed `0` means "use the keyboard's global speed" (`macro_speed=` in the settings file). Persisted ≠ model on purpose: the FS/Adv2 serializers write only slots 1–3 (06 §1) and the old FS parser/serializer keeps only the first co-trigger (06 §2.1, §3); the Adv2 serializer writes `{speedN}` but **no** repeat token (06 §3). SE2 has no per-macro speed/repeat setting at all — the pedal dialect embeds `speed1/3/5` tokens inside the macro (12 §4.4, §6). Adv2 has no macro-count or layout-keystroke limit in the spec. Adv360 keeps macros in one flat per-layout list tagged with trigger key + layer, so it has no slots.

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
- **No firmware-gate evaluation** (spec 09 §2) — gates are carried as data (`MacroCountGateFirmware`, `TapAndHoldCapability.MinimumFirmware`); comparing them to a device's actual firmware happens in `KinesisEdit.Core.Firmware` (see [`firmware.md`](firmware.md)). **Those versions are also encoded as `FirmwareGate` entries** in `FirmwareGateCatalog` — four spec 09 §2 numbers in two places: the FS Edge/Pro macro-count gate (1.0.340 ↔ `ExpandedMacroCount`) and the three tap-and-hold minimums (Adv2 1.0.516, FS Edge/Pro 1.0.480, RGB 1.0.1 ↔ `TapAndHold`). `FirmwareGateCatalog` is the authority for *evaluation*; the capability fields exist so a device's limits can be read without a firmware probe. The agreement is enforced in both directions by `KinesisEdit.Core.Tests/Integration/FirmwareGateConsistencyTests.cs` — changing one side without the other fails CI.
- **No macro/tap-and-hold behaviour** — the catalog states the limits, ranges and defaults; counting keystrokes, clamping out-of-range speed/repeat, and enforcing the caps belong to the model (see [`keyboard-model.md`](keyboard-model.md)) and the parsers.

- **No parsers/serializers** for layout/macro files (specs 04, 06) — this module is the vocabulary those consume; they live in `KinesisEdit.Core.Layouts` (see [`layout-files.md`](layout-files.md)).
- **No legacy font/rendering metadata** from the spec 05 tables (font names/sizes of the Pascal UI).
