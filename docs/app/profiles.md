# Profiles (load/save/eject orchestration)

The orchestration layer of `KinesisEdit.Core`: ties the layout engine, the lighting engine, the
settings engine, and v-Drive file I/O/eject together into one load/edit/save unit for a
**numbered profile** — Freestyle Edge/Pro, Freestyle Edge RGB, TKO, Advantage 360
(specs/03-vdrive-and-files.md §4.1/§4.3, §5.3). Advantage2's position-based naming is a separate
module, issue #37. Depends only on `Layouts`, `Lighting`, `Settings`, `VDrive`, `Devices`,
`Model` — no UI.

| Namespace | Entry point | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Profiles` | `ProfileSession.Load(VDriveLocation, DeviceId, int)` | Reads/parses `layout<n>.txt` + `led<n>.txt` (where present) + keyboard settings into a fresh session | 03 §4.1/§4.3; 04 §4.2 |
| `KinesisEdit.Core.Profiles` | `ProfileSession.Save()` / `.SaveAs(int, bool)` | Validate → write layout → write led → (SaveAs+startup) update settings → eject → message | 03 §5.3 |
| `KinesisEdit.Core.Profiles` | `ProfileSaveResult` | Outcome record: `Success`, `Violations`, `Ejected`, `PostSaveMessage` | 03 §5.3 |
| `KinesisEdit.Core.Profiles` | `ProfileReadOnlyException` | The Advantage 360 profile-0 guard | 02 "Profiles 0-9" |
| `KinesisEdit.Core.Profiles` | `ProfileSaveMessageCatalog` | Per-device-family post-save wording (data only, like `FirmwareGateCatalog`) | 03 §5.3; 07 §1.3; 10 |
| `KinesisEdit.Core.Profiles` | `ProfileLightingCodec` (internal) | Device → `LedFileParser`/`LedFileSerializer` dispatch | 07 §1.1-§1.4 |

## `ProfileSession`

- `Load` resolves `layout<n>.txt`/`led<n>.txt` from `VDriveLocation`'s computed folder paths and
  the device's `LayoutFileScheme` (`FirstProfileNumber`/`LastProfileNumber`/`HasReadOnlyFactoryProfile`,
  and whether the device has a *profile-orchestrated* led file — see below). Every call returns a
  **brand-new instance**; nothing is ever reloaded in place. This is deliberate: on top of
  `LayoutFileParser.Parse` already building a fresh `KeyboardLayout` per call, it is what gives
  "full-model-wipe-on-load" (04 §4.2) its guarantee at the orchestration level — there is no stale
  session lying around that a caller could half-update.
- Exposes `Layout` (`KeyboardLayout`), `Lighting` (`object?` — a `LightingModel`, `TkoLightingModel`,
  or `Advantage360LightingModel` depending on device; null where the device has none), `InvalidLines`
  (`IReadOnlyList<LayoutInvalidLine>`, `Keep` defaults false — 04 §5.2), `ProfileNumber`, `Device`.
- `IsDirty` re-serializes `Layout` (+ `Lighting`) with the *same* serializers a save would use and
  compares the lines against the lines captured right after `Load` — no bespoke model equality.
  Because the baseline is captured by serializing the just-parsed model (not the raw file text),
  `IsDirty` is false immediately after `Load` even for a non-canonical legacy input.
- `CanSave` is false only for profile 0 on a device whose `LayoutFileScheme.HasReadOnlyFactoryProfile`
  is true (the Advantage 360's factory profile, which has no on-disk file at all).
- `Save()` saves back to `ProfileNumber`. `SaveAs(targetProfileNumber, setAsStartup)` saves to a
  different slot within `[FirstProfileNumber, LastProfileNumber]` and, when `setAsStartup` is true,
  also updates `startup_file`/`led_mode` (or the Advantage 360's `profile` key) via
  `SettingsService.SaveKeyboardSettings` in the same call (07 §1.2: "Save As to a profile number
  switches both the current layout file and the current led file at once"). `setAsStartup: false`
  never touches the settings file.

## Profile-0 guard

`Load`, `Save`, and `SaveAs` all check profile 0 on a `HasReadOnlyFactoryProfile` device **before**
anything else — before validation, before any file is touched — and throw `ProfileReadOnlyException`
with the spec 02 wording verbatim: `"Profile 0 is non-programmable so you must use the Save As
Button..."`. `Load` must guard too: profile 0 has no on-disk `layout0.txt`, so there is nothing to
read. Because of this, no session can ever exist with `ProfileNumber == 0` — `CanSave`'s "false for
profile 0" branch and the same check inside `Save`/`SaveAs` are the same shared guard, exercised
directly through `Load` and through `SaveAs(0, ...)` from a session loaded at any other profile.

## Save sequence (`Save` and `SaveAs` share one private method)

1. Profile-0 guard (above).
2. `Layout.Validate()`. Any violation **stops the save**: returns
   `ProfileSaveResult { Success = false, Violations = violations, Ejected = false, PostSaveMessage = null }`
   without writing anything (04 §5.3's "validate macro capacity first" gate, applied to every
   reported limit, not only macro count — this is the save *orchestration* gating on the
   model's report; it does not change `KeyboardLayoutValidator`'s "report, don't enforce" contract
   for live editing).
3. `LayoutFileSerializer.Serialize(Layout, InvalidLines)` → `IVDriveFileService.WriteAllLines(...,
   allowCreate: true)`.
4. If `Lighting` is not null, the matching `LedFileSerializer.SerializeXxx` → `WriteAllLines(...,
   allowCreate: true)`.
5. If this is a `SaveAs` with `setAsStartup == true`: `_settings with { StartupProfileNumber =
   target }` (+ `LedMode = "led<target>.txt"` when the device's `SettingsCapability.LedMode` is
   `LedFileName`) saved via `SettingsService.SaveKeyboardSettings` — read-modify-write, so every
   other setting survives untouched.
6. `IVDriveEject.CreateForCurrentPlatform().Eject(location.RootPath)`; `Ejected` is whatever it
   reports (macOS: real `diskutil unmount`; Windows/Linux: unsupported, always false today).
7. `PostSaveMessage` from `ProfileSaveMessageCatalog.GetMessage(Device, targetProfileNumber,
   isStartupProfile)`, where `isStartupProfile` is `setAsStartup || settings.StartupProfileNumber ==
   targetProfileNumber` (the settings snapshot captured at `Load`).

## Lighting dispatch — `ProfileLightingCodec`

Only Freestyle Edge RGB, TKO, and Advantage 360 get a `Lighting` model and a written led file here.
The **Freestyle Edge** is the trap: its `LayoutFileScheme.LightingFolder` is non-null (it does ship
a `lighting/` folder, specs/03 §4.1) but its led file is a plain brightness/mode string owned by the
`led_mode` **settings** key, not the per-key/edge/indicator grammar `KinesisEdit.Core.Lighting`
implements (`docs/app/lighting.md` "Deliberately not here"). So `ProfileLightingCodec.HasSupportedLighting`
answers by `DeviceId`, not by `LightingFolder != null` — FS Edge is treated the same as FS Pro (no
lighting folder at all): `Lighting` stays null, and `led<n>.txt` is never read or written for either.

## Post-save messages — `ProfileSaveMessageCatalog`

Plain data, keyed by device family + whether the profile just saved is the startup profile — the
same shape as `FirmwareGateCatalog`'s gate messages, quoted verbatim from the specs:

| Device family | Startup profile | Non-startup profile |
|---|---|---|
| FS Edge / FS Pro | *(one wording, no startup concept in the settings capability)* | `"…use the Refresh Shortcut (SmartSet + Layout) or simply close the v-Drive (SmartSet + F8). To load this layout to the keyboard press SmartSet + <n>."` |
| Freestyle Edge RGB | `'Use the Refresh Shortcut (SmartSet + Profile) ... Eject the "FS EDGE RGB" drive ... (SmartSet + F8).'` | `"To load Profile <n> to the keyboard, hold the SmartSet key and tap the <n> key."` |
| TKO | Same shape as RGB with `SmartSet + Right Shift + B` / `"TKO"` / `SmartSet + Right Shift + V` | `"...hold the SmartSet key + Right Shift and tap the <n> key."` |
| Advantage 360 | `"Use the Refresh Shortcut (SmartSet + 'Refresh')…"` | `"To load Profile <n>…, hold the SmartSet key and tap the <n> key."` |

FS Edge/Pro have no `StartupSetting` in their `SettingsCapability` at all (spec 08 §2's write
column), so there is no per-device notion of "the startup profile" to branch on for that family —
one wording covers both cases.

## Deliberately not here

- **Advantage2** — position-based `<pos>_qwerty.txt`/`<pos>_dvorak.txt` naming, the `active/`
  folder, `state.txt`. Split into issue #37; this module refuses any non-`NumberedProfiles` device
  with `NotSupportedException`.
- **Free-named "backup file" saving** (FS Edge/Pro's Save-As-to-arbitrary-filename, 10
  "SmartSetFSEdgePro") — numbered slots 1-9 only.
- **Demo-mode gating and the "Keyboard Connection Lost" dialog** (03 §3.5) — app-layer concerns;
  Core has no concept of demo mode. A drive that vanishes mid-save surfaces whatever
  `IVDriveFileService` throws naturally (`FileNotFoundException`/`IOException`); there is no
  Core-level exception for it and no demo-mode parameter on `ProfileSession`.
- **The settings-only post-save message** (`"Changes will be implemented when v-Drive is
  closed."`, 03 §5.3) — that wording belongs to a bare keyboard-settings save with no profile
  content involved (the future settings-editor UI, issue #16); every `ProfileSession` save writes
  profile content, so this case never arises here.
- **No new dependency on `Advantage2`'s dialect, and no Gen2-header awareness beyond what
  `LayoutFileParser`/`LayoutFileSerializer` already do** — this module never inspects file text
  itself; it only moves lines between the file service and the existing parsers/serializers.
