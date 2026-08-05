# Firmware (Version Parsing, Feature Gates)

The firmware layer of `KinesisEdit.Core`: turns the text lines of a device's version file into structured firmware info, and answers "is this feature available on this device with this firmware?" against the hard-coded gate table of spec 09 §2. Pure text-in/data-out — it never touches the filesystem (drive discovery hands it lines), never talks to the update endpoint, and never shows a dialog; it only carries the dialog wording as data.

| Namespace | Entry point | Encodes | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Firmware` | `VersionFileParser` | Per-device version-file line parsing | spec 09 §1; spec 12 §1 |
| `KinesisEdit.Core.Firmware` | `FirmwareGateCatalog` + `FirmwareGateService` | Minimum-firmware feature gates (data + evaluation) | spec 09 §2; spec 11 §11.1; spec 07 |
| `KinesisEdit.Core.Firmware` | `FirmwareSupportUrls` | Per-device firmware support pages | spec 09 §2 |

## Version files — `VersionFileParser`

- `Parse(DeviceId, IEnumerable<string>)` — pure function from version-file lines to an immutable `VersionFileInfo`; throws only on null lines.

Implements the spec 09 §1.1 line rule: prefix-match each line case-insensitively, skip exactly one separator character after the prefix (the `:` of version files or the `=` of the Adv360 settings file), trim the remainder. Prefix sets per device: FS Edge / FS Pro / Adv2 → `model name`, `firmware version`; RGB / TKO → `model name`, `kbd firmware`, `led firmware` (the `led bootloader` line matches no prefix and is never parsed); Adv360 → `model`, `kbd_fw_r` (its "version file" is `settings/settings.txt`). SE2 gets its own rule (spec 12 §1): its file is free text (`Firmware version is 1.0.44`), so the parser scans lines in order for the first dotted numeric token and parses that as the keyboard firmware. Versions are parsed by `FirmwareVersion.TryParse` (spec 09 §1.2, lives in `KinesisEdit.Core.Devices`), which takes the first three dot-separated tokens, so trailing text like `.us (4MB), 03/08/2019` is ignored without pre-trimming. Devices with no version data (CROSSFIRE keypad, Adv360 Professional — null version paths in the device catalog) and unknown ids return `VersionFileInfo.Empty`.

`VersionFileInfo` (record): `ModelName` (raw), `KeyboardFirmware`/`LedFirmware` (`FirmwareVersion?`, null when absent or unparseable) each paired with the raw text after the prefix (`KeyboardFirmwareText`/`LedFirmwareText` — the firmware dialog displays the full value, e.g. `1.0.1709.us (4MB), 03/08/2019`), and `HasFourMegabyteMarker` (true when any line contains `4MB` case-insensitively; spec 09 §1.1 — enables Adv2 settings editing, absence = 2MB board). Helper `ResolveFreestyleModel()` implements the spec 02 runtime disambiguation: model name `FS PRO` (case-insensitive) → `FreestylePro`, anything else → `FreestyleEdge`.

## Feature gates — `FirmwareGateCatalog`, `FirmwareGateService`, `FirmwareState`

- `FirmwareGateCatalog.All` — 17 `FirmwareGate` rows encoding the spec 09 §2 table (below), grouped by device in legacy-app-id order. `Find(DeviceId, FirmwareFeature)` — the row for the pair, or null when the pair is ungated.
- `FirmwareGateService.IsAvailable(DeviceId, FirmwareFeature, FirmwareState)` — the single evaluation entry point.
- `FirmwareState` (readonly record struct) — what a query runs against: `KeyboardFirmware`/`LedFirmware` (nullable) + `IsDemoMode` (spec 03 §3.5: not connected or no read/write access). `FromVersionFile(VersionFileInfo, isDemoMode)` bridges from the parser.

`FirmwareGate` (record) is one table row: `Device`, `Feature` (`FirmwareFeature` enum), the requirements (`MinimumKeyboardFirmware`, `MinimumLedFirmware`, `ExactKeyboardFirmware`, `ExactLedFirmwareVersions` — null/empty means "not part of the rule"; every declared requirement must hold), and `Message` — the dialog wording where specs 09 §2 / 11 §11.1 give it (FS custom-delay, multimodifier, and tap-and-hold refusals; the TKO startup warning), null elsewhere. Evaluation semantics: demo mode → available, always, for every gate; no row for the pair → available (the service answers *gating* only — whether a device supports a feature at all is device-catalog knowledge); row present but the relevant version unknown → not available; compound row → all conditions must hold. The gate table (spec 09 §2, restated in specs 07 and 11 §11.1 — note TKO and Adv360 have *no* tap-and-hold gate, so `TapAndHold` is ungated there):

| Device(s) | Requirement | Feature(s) |
|---|---|---|
| FS Edge, FS Pro | KBD ≥ 1.0.340 | `ExpandedMacroCount` (100 instead of 24), `CustomMacroDelays` |
| FS Edge, FS Pro | KBD ≥ 1.0.480 | `Multimodifiers`, `TapAndHold` |
| Advantage2 | KBD ≥ 1.0.516 | `Multimodifiers`, `TapAndHold` |
| RGB | KBD ≥ 1.0.1 | `Multimodifiers`, `TapAndHold` |
| RGB | KBD ≥ 1.0.121 and LED ≥ 1.0.58 | `RippleAndFireballEffects` |
| RGB | LED ≥ 1.0.44 | `LightingLayerCustomization` |
| RGB | LED = 1.0.44 or LED = 1.0.58 (exact) | `ExpansionPackOffer` |
| TKO | KBD = 1.0.0 (exact) | `MacroFirmwareWarning` |
| Advantage 360 | KBD ≥ 1.0.69 | `TapAndHoldMacroActions` |

## Support pages — `FirmwareSupportUrls`

- `FindUrl(DeviceId)` — the spec 09 §2 firmware support page the legacy "Upgrade Firmware" buttons open (FS Pro, FS Edge, Adv2, RGB, TKO, Adv360), null for other devices. Kept here rather than in the device catalog because spec 02 gives `DeviceDefinition.SupportUrl` only for the Adv360 Professional.

## Load-bearing invariants

1. **Parsing is case-insensitive; raw text keeps its original casing.** Prefixes and the `4MB` marker match case-insensitively (the parsing rule of CLAUDE.md/spec 09 §1.1), but `ModelName` and the firmware texts are extracted from the unlowered line — they are display values.
2. **Unknown version ≠ passing gate.** A gated feature with a null relevant version is unavailable; only demo mode bypasses gates (spec 09 §2: "Every gate also passes in demo mode").
3. **Ungated means available.** `Find` returning null makes `IsAvailable` true — the catalog encodes only the spec's rows; adding a row is a spec change, not a refactor. Tests pin the row count (17) and every threshold.
4. **Data and evaluation stay separated.** `FirmwareGateCatalog` never compares versions; `FirmwareGateService` never defines thresholds. The FS Edge/Pro macro-count gate also exists as data on `MacroCapability.MacroCountGateFirmware` (domain-data.md); this module is where it is evaluated.
5. **Messages are spec text.** Gate messages are stored verbatim where quoted. One exception: spec 09 §2 truncates the multimodifier refusal to `To utilize Multimodifiers…`; the stored message completes it with the same suffix as the two fully-quoted refusals.

## Deliberately not here

- **No file I/O** — the parser takes lines, never paths; reading `version.txt`/`settings.txt` off the v-Drive is drive discovery (issue #6).
- **No update-check dialog or endpoint** (spec 09 §3–§4) — the online version JSON, `update.upd`, and app self-update are issue #17.
- **No expansion-pack lighting-state precondition** — the `ExpansionPackOffer` gate covers only the LED-version condition; "no led file contains the `fn ` prefix yet" (spec 07) is lighting-file knowledge, issue #9.
- **No refusal-dialog UI** — gates carry the wording and support URLs as data; showing dialogs with "Update Firmware" buttons is the editor UIs, issues #15/#16.
