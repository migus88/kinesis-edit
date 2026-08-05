# Firmware (Version Parsing, Feature Gates, Update Check)

The firmware layer of `KinesisEdit.Core`: turns the text lines of a device's version file into structured firmware info, answers "is this feature available on this device with this firmware?" against the hard-coded gate table of spec 09 §2, and compares local versions against the published-versions endpoint for the "Check for Updates" dialog of spec 09 §3. Pure text-in/data-out — it never touches the filesystem (drive discovery hands it lines), never opens a socket (the app layer performs the update-check GET), and never shows a dialog; it only carries dialog wording as data and dialog outcomes as states.

| Namespace | Entry point | Encodes | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Firmware` | `VersionFileParser` | Per-device version-file line parsing | spec 09 §1; spec 12 §1 |
| `KinesisEdit.Core.Firmware` | `FirmwareGateCatalog` + `FirmwareGateService` | Minimum-firmware feature gates (data + evaluation) | spec 09 §2; spec 11 §11.1; spec 07 |
| `KinesisEdit.Core.Firmware` | `FirmwareSupportUrls` | Per-device firmware support pages | spec 09 §2 |
| `KinesisEdit.Core.Firmware` | `VersionManifest` + `VersionEndpoints` + `IVersionManifestClient` | Published-versions payload, its URLs, the fetch contract | spec 09 §3 |
| `KinesisEdit.Core.Firmware` | `UpdateCheckService` (+ `UpdateCheckEligibility`, `UpdateCheckKeys`, `UpdateCheckUrls`) | "Check for Updates" row model and comparison | spec 09 §3-§4 |

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

**Who evaluates gates today.** The keyboard editor is the first consumer ([keyboard-editor.md](keyboard-editor.md)): `MacroPanelViewModel.ResolveMaxMacroCount` reads `ExpandedMacroCount` to pick between `MacroCapability.MaxMacroCount` and `GatedMaxMacroCount` (no dialog — the number simply changes), while `TapAndHold` and `CustomMacroDelays` are asked before their feature panels open and refuse with a dialog. That dialog is `KinesisEdit.ViewModels.FirmwareFeatureGate`, which shows `FirmwareGate.Message` plus an `Update Firmware` button wired to `FirmwareSupportUrls.FindUrl` ([feature-dialogs.md](feature-dialogs.md)); where a row stores no message — spec 09 §2 quotes the refusal only under the Freestyle rows — the calling feature supplies a fallback pinned by test to the row that does. `MacroFirmwareWarning`, `RippleAndFireballEffects`, `LightingLayerCustomization`, `ExpansionPackOffer` and `TapAndHoldMacroActions` still have no caller.

## Support pages — `FirmwareSupportUrls`

- `FindUrl(DeviceId)` — the spec 09 §2 firmware support page the legacy "Upgrade Firmware" buttons open (FS Pro, FS Edge, Adv2, RGB, TKO, Adv360), null for other devices. Kept here rather than in the device catalog because spec 02 gives `DeviceDefinition.SupportUrl` only for the Adv360 Professional.

## Update check — `VersionManifest`, `VersionEndpoints`, `UpdateCheckService`

Spec 09 §3: the dialog reads local versions off the device, GETs a published-versions JSON, compares three rows, and links each outdated row to the support site. It never transfers firmware. Core owns everything except the GET itself and the wording.

**Eligibility** (`UpdateCheckEligibility`): `SupportedDevices` = Edge RGB, TKO, Advantage 360 — §3 names those three, §4 states the FS Edge/Pro, Adv2 and SE2 apps have no update dialog at all. `IsSupported(DeviceId)`; `HasLightingRow(DeviceId)` is false for the Adv360 family (its lighting key repeats the keyboard value, so §3 hides the row and shrinks the form) and for anything unsupported. The Adv360 **Professional** follows the Adv360 rules everywhere they appear (same manifest key, same hidden row) but is *not* eligible: `DeviceDefinition.IsProgrammable` is false for it — ZMK web GUI, no v-Drive to read local versions from.

**Endpoints** (`VersionEndpoints`, §3 step 3): gaming `https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions`, office `https://kinesis-ergo.com/wp-json/ksv/v1/get_versions`. `FindUrl(ServingApp)` and `FindUrl(DeviceId)` (resolved through `DeviceDefinition.ServingApp`); null for `None`/`StandaloneOnly` and unknown ids. Endpoint resolution is independent of eligibility — Adv2/SE2/Adv360 Pro resolve to the office URL even though they show no dialog.

**Fetching** (`IVersionManifestClient.FetchAsync(endpointUrl, cancellationToken)`): declared in Core, **implemented in the app layer** by `KinesisEdit.Services.HttpVersionManifestClient` — Core has no HTTP stack and zero package references. Implementations throw on any failure (transport, status code, cancellation, non-object body); callers turn any exception into `UpdateCheckService.BuildConnectionErrorRows` (§3 step 6).

**Payload** (`VersionManifest`): `TryParse(string? json, out VersionManifest manifest)` returns false — with `manifest` set to `VersionManifest.Empty`, never null — for null/blank text, invalid JSON, or a non-object root; that is the only "malformed" signal. `RawJson` preserves the response verbatim because firmware-debug mode (`VDriveDebugFlags.FirmwareDebug`, an empty `debug_firm.on` at the drive root) dumps it in a dialog. Reading is tolerant: unknown keys are kept in `Values`, key lookup is case-insensitive, string values are taken as-is, numbers/booleans are read as raw text, null/object/array values are skipped, trailing commas and comments are accepted. Empty and whitespace values are dropped, so **an absent key and an empty value are indistinguishable** — `GetValue(key)` returns null for both, which is all §3 step 5 needs. Typed accessors mirror the keys: `KeyboardVersion`, `LightingVersion`, `WindowsGamingAppVersion`, `WindowsOfficeAppVersion`, `MacGamingAppVersion`, `MacOfficeAppVersion`, `TkoKeyboardVersion`, `TkoLightingVersion`, `Advantage360Version`. Key-name constants live in `VersionManifestKeys`.

**Key table** (`UpdateCheckKeys`, §3 step 4) — `FindKeyboardKey` / `FindLightingKey`:

| Device | Keyboard key | Lighting key |
|---|---|---|
| TKO | `tko_keyboard_version` | `tko_lighting_version` |
| Edge RGB | `keyboard_ver` | `lighting_ver` |
| Adv360 family | `kb360_version` | `kb360_version` (same value; row hidden) |

`FindAppKey(ServingApp \| DeviceId, UpdateCheckPlatform)`:

| Platform | gaming master | office master |
|---|---|---|
| `Windows` | `app_ver` | `pc_app_version` |
| `MacOs` | `mac_app_ver` | `mac_app_version` |
| `Linux` | `mac_app_ver` | `mac_app_version` |

The Linux row is a **deliberate deviation**: the legacy app had no Linux build and the endpoint publishes no Linux key, so Linux reuses the macOS keys until it does. The platform is a caller-supplied enum (`UpdateCheckPlatform`, `None` = unspecified → no key), never probed inside Core, so every OS × master-app combination is testable.

**Rows** (`UpdateCheckService`, static and pure): `BuildRows(UpdateCheckRequest)` produces the ordered rows — keyboard, lighting, app — and `BuildCheckingRows` / `BuildUnreadableRows` / `BuildConnectionErrorRows` (each taking a `DeviceId`) produce the same row set uniformly in one state for the dialog's initial, step-1 and step-6 outcomes. All four return an empty list for an unsupported device. `HasReadableKeyboardVersion(VersionFileInfo)` exposes the step-1 condition on its own, so a caller can skip the endpoint request without restating the rule.

- `UpdateCheckRequest`: `Device`, `LocalVersions` (`VersionFileInfo` from `VersionFileParser`; `VersionFileInfo.Empty` when nothing could be read), `Manifest`, `AppVersionText`, `Platform`.
- `AppVersionText` is the app's own four-component `major.minor.revision.build` build-resource string (§3 step 2); `FirmwareVersion.TryParse` truncates it to three components, so the build number never affects the comparison.
- `UpdateCheckRow`: `Kind` (`UpdateRowKind.Keyboard`/`Lighting`/`App`), `State`, `LocalVersion` + `LocalVersionText`, `RemoteVersion` + `RemoteVersionText`, `TargetUrl`. `LocalVersionText` keeps the device's own wording where it has any (e.g. `1.0.1709.us (4MB), 03/08/2019`); the two `FirmwareVersion?` properties carry exactly what was compared and are null only when that side had no value at all. `TargetUrl` is set on every row regardless of state. A view model needs nothing else.
- `UpdateRowState`: `Checking`, `UpdateAvailable` (local < remote, §3 step 5), `UpToDate` (equal or newer), `RemoteMissing` (the manifest carried no value under the row's key), `LocalUnreadable`, `ConnectionError`. **States only — the captions (`Update Now`, `No update available`, `Error fetching …`, `Error reading firmware file`, `Check connection`) belong to the app layer.**
- **Empty ≠ unparseable, on both sides.** §3 step 1 triggers when the keyboard version "comes back empty" and step 5's `Error fetching …` caption belongs to an *empty* remote value, while §1.1 parses a non-numeric token as 0. So the only "no version" case is a value that is absent, empty, or whitespace; a value that is present but unparseable is compared as `0.0.0`. `{"keyboard_ver":"TBD"}` therefore reports `UpToDate`, not `RemoteMissing`, and a version-file line `KBD Firmware: unknown` compares as `0.0.0` (`UpdateAvailable`) instead of short-circuiting the dialog. The local side reads this off the **raw text** (`VersionFileInfo.KeyboardFirmwareText`/`LedFirmwareText`, `UpdateCheckRequest.AppVersionText`), never off the pre-parsed `FirmwareVersion?`, which is null for both cases and so cannot tell them apart.
- §3 step 1: an empty **keyboard** firmware makes the whole set `LocalUnreadable`, app row included, and nothing is compared. An empty LED or app version marks only its own row — a deliberate refinement over the legacy app, which initialized those to `-1` and would have advertised an update.
- Both sides always go through `FirmwareVersion` (§1.1 parsing, §1.2 lexicographic major→minor→revision ordering); nothing here re-implements version parsing. `FirmwareVersion.TryParse` is stricter than §1.1 in one spot — it rejects a non-numeric *first* token instead of reading it as 0 — so `us.1.0` lands on `0.0.0` rather than `0.1.0`. Both are below every published version, so no row outcome changes.
- `BuildRows` guards `request`, `request.LocalVersions` and `request.Manifest` with `ArgumentNullException`: the two members are `required`, but `required` does not stop a caller passing `null!`.

**Row targets** (`UpdateCheckUrls`, §3 step 7): keyboard and lighting rows use `FindFirmwareUrl`, which *is* `FirmwareSupportUrls.FindUrl` — verified identical to `TroubleshootingUrl` + `#firmware` for RGB/TKO and to `https://kinesis-ergo.com/support/kb360/#firmware-updates` for the Adv360, so the §2 and §3 URL sets can never drift. The app row uses `FindAppUrl` = `DeviceDefinition.TroubleshootingUrl` + `#smartset-app` (`UpdateCheckUrls.AppAnchor`).

**The dialog itself is the app layer** ([app-shell.md](app-shell.md)), and everything Core deliberately leaves out lives there: `HttpVersionManifestClient` (the GET, one reused `HttpClient`, throws on every failure), `AssemblyAppVersionProvider` (`IAppVersionProvider` → the four-component `AppVersionText`), `UpdateCheckPlatformResolver` (the only `RuntimeInformation` probe; feeds `UpdateCheckRequest.Platform`), `FirmwareUpdateViewModel`/`FirmwareUpdateRowViewModel` (flow, wording, the raw-JSON debug box, the `IUrlLauncher` click), and `FirmwareUpdateWindow` behind `IFirmwareUpdatePresenter`. The state → caption map, verbatim from §3: `Checking` → `Checking for update...`, `UpdateAvailable` → `Update Now`, `UpToDate` → `No update available`, `LocalUnreadable` → `Error reading firmware file`, `ConnectionError` → `Check connection`, `RemoteMissing` → `Error fetching keyboard firmware` / `Error fetching lighting firmware` / `Error fetching app version` by row. Entry point: the device card's second button, which reads `Check for Updates` instead of `Scan for v-Drive` when the drive is connected and `UpdateCheckEligibility.IsSupported(deviceId)`.

## Load-bearing invariants

1. **Parsing is case-insensitive; raw text keeps its original casing.** Prefixes and the `4MB` marker match case-insensitively (the parsing rule of CLAUDE.md/spec 09 §1.1), but `ModelName` and the firmware texts are extracted from the unlowered line — they are display values.
2. **Unknown version ≠ passing gate.** A gated feature with a null relevant version is unavailable; only demo mode bypasses gates (spec 09 §2: "Every gate also passes in demo mode").
3. **Ungated means available.** `Find` returning null makes `IsAvailable` true — the catalog encodes only the spec's rows; adding a row is a spec change, not a refactor. Tests pin the row count (17) and every threshold.
4. **Data and evaluation stay separated.** `FirmwareGateCatalog` never compares versions; `FirmwareGateService` never defines thresholds. Four of its thresholds also exist as device-catalog data (domain-data.md) so limits are readable without a firmware probe — the FS Edge/Pro macro-count gate on `MacroCapability.MacroCountGateFirmware` (1.0.340) and the Adv2/FS Edge/FS Pro/RGB tap-and-hold minimums on `TapAndHoldCapability.MinimumFirmware` (1.0.516 / 1.0.480 / 1.0.1); this module is where all of them are evaluated, and `KinesisEdit.Core.Tests/Integration/FirmwareGateConsistencyTests.cs` fails if the two copies drift apart in either direction.
5. **Messages are spec text.** Gate messages are stored verbatim where quoted. One exception: spec 09 §2 truncates the multimodifier refusal to `To utilize Multimodifiers…`; the stored message completes it with the same suffix as the two fully-quoted refusals.
6. **States, not captions.** The update check answers with `UpdateRowState`/`UpdateRowKind` and URLs; every caption, color, cursor and window-size rule of the dialog is app-layer presentation. Core carries gate *messages* (§2 quotes them as data) but never update-dialog wording.
7. **No transport in Core.** `IVersionManifestClient` is a contract, not an implementation: `KinesisEdit.Core` has zero package references and uses no `HttpClient`. Comparison logic takes an already-fetched `VersionManifest`, so it is pure and every branch is unit-testable.

## Deliberately not here

- **No file I/O** — the parser takes lines, never paths; reading `version.txt`/`settings.txt` off the v-Drive is drive discovery (issue #6).
- **No HTTP and no update dialog** — Core defines `IVersionManifestClient`, the manifest, and the row model; the actual GET, the firmware-debug raw-JSON dialog, the captions and the hidden-lighting-row window shrink are the app layer ([app-shell.md](app-shell.md)).
- **No `update.upd` handling and no app self-update** (spec 09 §3.1, §4) — the legacy hands-free flow (download ZIP → unzip → replace `firmware/update.upd`) was never enabled and there is no self-updater; every update goes through the support links of §3 step 7.
- **No expansion-pack lighting-state precondition** — the `ExpansionPackOffer` gate covers only the LED-version condition; "no led file contains the `fn ` prefix yet" (spec 07) is lighting-file knowledge, issue #9.
- **No refusal-dialog UI** — gates carry the wording and support URLs as data; the dialog with the "Update Firmware" button lives in the app layer (`FirmwareFeatureGate`, [feature-dialogs.md](feature-dialogs.md)), and the lighting/settings gates are still waiting for issue #16.
