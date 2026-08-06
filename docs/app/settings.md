# Settings engine (Keyboard settings, App settings)

The settings layer of `KinesisEdit.Core`: typed models, pure line-based parsers/serializers, and a thin v-Drive-bound service for the device-side keyboard-settings files and `app_settings.txt` of spec 08. Consumes per-device applicability from `KinesisEdit.Core.Devices.SettingsCapability` ([domain-data.md](domain-data.md)) and does all file I/O through `IVDriveFileService` ([vdrive.md](vdrive.md)) — no device facts and no raw file access live here. It never parses layouts, macros, or version files.

| Namespace | Entry point(s) | Encodes / Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Settings` | `SettingsKeys`, `SettingsValueRanges`, `SettingsLineReader` | Exact key names, numeric ranges, read-side `=`-rule lookup | 08 §1–3 |
| `KinesisEdit.Core.Settings` | `KeyboardSettings`, `KeyboardSettingsParser`, `KeyboardSettingsSerializer` | Typed device-settings model, parse/serialize | 08 §2, §5 |
| `KinesisEdit.Core.Settings` | `AppSettings`, `SettingsColor`, `AppSettingsParser`, `AppSettingsSerializer` | Notification hide flags + custom colors | 08 §3 |
| `KinesisEdit.Core.Settings` | `KeyboardSettingsGate` | Advantage2 4MB write gate | 09 §1.1; 08 §2, §5.4 |
| `KinesisEdit.Core.Settings` | `LedModeValues` | The mode-string `led_mode` domain (`0`–`9`, `P`, `B`) | 08 §2, §5.3 |
| `KinesisEdit.Core.Settings` | `StartupProfileSettings` | Startup-profile ↔ `led_mode` pairing | 08 §5.1; 07 §1.2 |
| `KinesisEdit.Core.Settings` | `SettingsMessageCatalog` | Settings-panel strings | 08 §5.1, §5.4 |
| `KinesisEdit.Core.Settings` | `SettingsService` | Load/save binding to a `VDriveLocation` | 08 §1–3 |
| `KinesisEdit.Core.Devices` | `SettingsCapability` (+ `StartupSettingKind`, `LedModeKind`, `StatusSettingKind`, `VDriveSettingKind`) | Which keys the app writes per device, and their forms | 08 §2, §5 |
| `KinesisEdit.Services` | `ISettingsService`, `SettingsServiceAdapter` | The app layer's fakeable seam over the sealed `SettingsService` | — |
| `KinesisEdit.ViewModels` | `KeyboardSettingsViewModel`, `KeyboardSettingsRows`, `SettingsRowViewModel` (+ slider/toggle/choice rows) | The editor's Settings tab | 08 §5 |

## Key/value lines — `SettingsLineReader`

- `FindValue(IReadOnlyList<string> lines, string key)` — the raw value, or null when absent; last occurrence wins (legacy loads line-by-line, later assignments overwrite).

Read side of the spec 08 §1 rule, identical to `VDriveFileService.UpdateSettingsFile`'s write side: a line carries key K iff it starts with K case-insensitively **and** the char at K.Length is `=`; the value is everything after the `=`. Requiring the separator uniformly resolves every prefix collision — `v_drive` vs `v_drive_open_on_startup`, `cust_color_1..3` vs `cust_color_10..12` (08 §1), and also `status` vs `status_play_speed`.

## Keyboard settings — `KeyboardSettings`, parser, serializer

- `KeyboardSettingsParser.Parse(IReadOnlyList<string>)` — device-agnostic ("parsing is common to all devices", 08 §2), case-insensitive, tolerant: unparseable values — including numbers outside the spec ranges (`macro_speed=12`) — become null, never errors, so a later save skips the key and the on-device line survives verbatim.
- `KeyboardSettingsSerializer.Serialize(SettingsCapability, KeyboardSettings)` — the pairs to write, spec-table order; validates ranges/forms (throws `ArgumentOutOfRangeException`/`ArgumentException`).

`KeyboardSettings` is a record of nullable properties — **null = key absent**; the spec defines no defaults (08 §2 notes). One property per logical setting, not per key: `StartupProfileNumber` unifies `startup_file=layout<N>.txt` and `profile=<N>` (read: `profile` first, else the digit run of `startup_file` — the shared "file-number logic"); `StatusPlaySpeed` unifies `status_play_speed` and the Adv360 short key `status` (read: long key first). `LedMode` holds the raw dual-typed value (led file name on RGB/TKO, `0`..`9`/`P`/`B` on FS Edge). Reserved keys (`thumb_mode`, `macro_disable` on→true, `power_user` true→true, `country`, `program_key_lock`, `profile_sync_mode`) are parsed into state but have no serializer path at all.

Serialization emits a key only when the capability designates it **and** the property is non-null; everything else is silently skipped. Value semantics per 08 §2 notes: booleans write uppercase `ON`/`OFF`, except `v_drive` writes `auto`/`manual` and the Adv2 `v_drive_open_on_startup` false value writes literally lowercase `off` (true writes `ON`). `led_mode` is canonicalized (`LED3.TXT` → `led3.txt`; `p` → `P`). Ranges live once in `SettingsValueRanges` (parser rejects to null, serializer refuses, future UI reads slider bounds): profile 1–9; status play speed 0–4; macro speed 0–9 on every device — `SettingsCapability.MacroSpeedMinimum` (0 FS/Adv2, 1 RGB/TKO) is the UI slider floor, 0 is always writable as "disabled" (08 §2, §5.1).

## Per-device applicability — `SettingsCapability` (in `Devices`)

Populated per device in `DeviceCatalog`'s factories from the 08 §2 table refined by the §5 per-device UIs: RGB/TKO get `startup_file` + `led_mode` file + macro/status/v_drive/game_mode; FS Edge the mode-string `led_mode` + macro/status/v_drive/game_mode; FS Pro only macro/status/v_drive (its game-mode switch and brightness knob are hidden — 08 §5.3, spec 02 "no game mode"); Adv2 macro/status + `v_drive_open_on_startup` + both tones; Adv360 `profile` + `status` + `lock`, and **no** `macro_speed` (not in the §2 written-by column; no §5.2 control). SE2, CROSSFIRE, and Adv360 Pro are `SettingsCapability.None`.

## Advantage2 4MB gate — `KeyboardSettingsGate`

- `CanEditKeyboardSettings(DeviceId, VersionFileInfo)` — queryable for the settings UI (#16) to disable controls; false only for Advantage2 without `VersionFileInfo.HasFourMegabyteMarker`.
- `EnsureCanEditKeyboardSettings(...)` — throws `InvalidOperationException` (the write-refusal path).

Consumes the already-parsed `VersionFileInfo` from the firmware module ([firmware.md](firmware.md)) — version files are never re-parsed here. The explanatory hint the disabled panel shows is `SettingsMessageCatalog.Advantage2SettingsDisabledHint` (see below). **Demo mode is the caller's concern, not the gate's**: `KeyboardSettingsViewModel` lets demo mode pass the query the same way `FirmwareGateService` does (09 §2), because demo mode supplies `VersionFileInfo.Empty` and a "your board is the 2MB variant" hint about a keyboard nobody attached is invented; the write-side `EnsureCanEditKeyboardSettings` is never reached there, since saving in demo mode is refused outright.

## `led_mode`'s mode-string domain — `LedModeValues`

- `All` — `0`…`9`, `P`, `B` in picker order; `Normalize(string?)` — canonical spelling (trimmed, `p` → `P`) or **null** when the value is not one of them; `IsBrightness(string)`; `MinimumBrightness`/`MaximumBrightness`/`PitchBlack`/`Breathe`.

`KeyboardSettingsSerializer` validates the `LedModeKind.ModeString` form through `Normalize` and **throws** on null, so the settings panel's picker builds its options from `All` rather than restating them — the offered set and the accepted set are the same list. The *other* form of the key, the RGB/TKO's paired `led<N>.txt`, belongs to `StartupProfileSettings` (below) and is never mixed in here.

## Startup-profile pairing — `StartupProfileSettings`

- `ApplyStartupProfile(SettingsCapability, KeyboardSettings, int profileNumber)` → a new `KeyboardSettings`: sets `StartupProfileNumber` and, when `capability.LedMode == LedModeKind.LedFileName` (RGB/TKO), the paired `LedMode = led<N>.txt`. Devices whose `StartupSetting` is `None` (FS Edge/Pro, Adv2) get the input back **unchanged** (same instance).
- `GetLedFileName(int profileNumber)` → `led<N>.txt`.

Pure model-to-model, no I/O and no range check (`KeyboardSettingsSerializer` enforces 1–9). Both the settings panel's active-profile slider (08 §5.1 "Active profile slider → `startup_file` (+ paired `led_mode` file)") and `ProfileSession.SaveAs(…, setAsStartup: true)` (07 §1.2 "Save As … switches both the current layout file and the current led file at once") go through this one helper, so the pair cannot drift. `GetLedFileName` is also what `ProfileSession` builds the `lighting/led<n>.txt` path from, so the file a session reads and the name the settings point at are spelled in one place.

## Settings-panel strings — `SettingsMessageCatalog`

Plain consts, no UI framework — the same pattern as `Profiles.ProfileSaveMessageCatalog`:

- `SettingsSavedTitle` = `"Settings Saved"`, `SettingsSavedMessage` = `"Changes will be implemented when v-Drive is closed."` — the post-save dialog of 08 §5.1/§5.2, suppressible via the `savesettings_msg`/`save_msg` hide flags. The spec's sequence ends in an eject; **this app does not eject after a settings save** (see "Deliberately not here"), so the wording is shown as a toast and the drive is closed by the shell's Home path.
- `Advantage2SettingsDisabledHint` — the 08 §5.4 hint on the disabled Adv2 panel. The spec prescribes "an explanatory hint" without quoting it, so the wording states the condition `KeyboardSettingsGate.CanEditKeyboardSettings` actually tests (no `4MB` marker in the version file, 09 §1.1) rather than inventing legacy text.

## App settings — `AppSettings`, `SettingsColor`, parser, serializer

- `AppSettingsParser.Parse(IReadOnlyList<string>)` / `AppSettingsSerializer.Serialize(AppSettings)`.
- `SettingsColor.TryParse` / `ToString()` — the `[R][G][B]` decimal 0–255 file form (`[255][0][128]`), strict on parse, canonical on format.

- `AppSettings.WithCustomColor(int slotNumber, SettingsColor? color)` → a copy with one slot replaced. `slotNumber` is **1-based**, the same numbering as `SettingsKeys.GetCustomColorKey` (1 = `cust_color_1` … 12 = `cust_color_12`); `null` clears the slot; outside 1–12 throws `ArgumentOutOfRangeException`. Needed because `CustomColors` is init-only and validated to exactly 12 entries. Convert to/from the lighting pickers' `LedColor` with `Lighting.LedColorConverter` ([lighting.md](lighting.md)).

Twelve `bool?` hide flags (**`on` = hide** the notification; null = key absent = `off` = show) and `CustomColors`, a fixed list of 12 `SettingsColor?` slots (index 0 = `cust_color_1`; length-validated). Flags persist lowercase `on`/`off`; null flags and unset colors are skipped on save — a color key is never written empty (08 §3). The §4 master-app "hide all"/"show all" globals are not modeled here.

## Service — `SettingsService`

- `LoadKeyboardSettings(VDriveLocation)` / `SaveKeyboardSettings(VDriveLocation, VersionFileInfo, KeyboardSettings)`.
- `LoadAppSettings(VDriveLocation)` / `SaveAppSettings(VDriveLocation, AppSettings)`; static `GetAppSettingsFilePath(VDriveLocation)`.

Constructor takes `IVDriveFileService`. Saves go through `UpdateSettingsFile` (read-modify-write, 08 §1) so unknown/reserved lines survive verbatim; when serialization yields nothing the file is not touched. Keyboard-settings save gates Adv2 first, then serializes with `location.Device.Settings`. `app_settings.txt` lives in the drive's `settings/` folder for every device (08 §3, 03 §6) — even Adv2, whose keyboard settings live in `active/` — and, being app-owned, is the one file this module creates: a missing file on load yields `AppSettings.Empty`, on save it is created via `WriteAllLines(allowCreate: true)`. Missing-file detection is by catching `FileNotFoundException` from the file service, so a fake `IVDriveFileService` fully controls the behavior.

## The UI on top — `ISettingsService`, `KeyboardSettingsViewModel`

The app layer reaches this module through **one seam**, `KinesisEdit.Services.ISettingsService` (`LoadKeyboardSettings` / `SaveKeyboardSettings(location, versionFileInfo, settings)` / `LoadAppSettings` / `SaveAppSettings`), implemented by the behaviour-free `SettingsServiceAdapter` over the sealed `SettingsService` — the same shape as `IProfileSession`/`ProfileSessionAdapter` ([keyboard-editor.md](keyboard-editor.md)). The composition root builds one instance and hands it to `DeviceSessionManager` and `EditorViewModelFactory`.

- **`app_settings.txt` has exactly one reader and one writer.** `VDriveNotificationSuppressionStore` (the "Hide this notification?" store, [app-shell.md](app-shell.md)) no longer parses lines itself: `IsHidden`/`SetHidden` load the whole `AppSettings`, map the key onto its flag and save it back through the seam, swallowing every I/O failure. A key outside the twelve of 08 §3 is not modeled and is therefore never written. This is also what puts the file in `settings/` on the **Advantage2** — the store used to resolve it through the device's `SettingsFolderPath` (`active/`). The twelve `cust_color_N` slots ride the same route, so a colour save and a suppression save cannot clobber each other.
- **The panel is `KeyboardSettingsViewModel`** — the editor's Settings tab, described in [keyboard-editor.md](keyboard-editor.md). It renders one row per key the device's `SettingsCapability` designates (`KeyboardSettingsRows`), builds the `led_mode` picker from `LedModeValues`, clamps every value into `SettingsValueRanges` before saving, routes the active-profile row through `StartupProfileSettings.ApplyStartupProfile`, disables itself on a 2MB Advantage2 via `KeyboardSettingsGate` (demo mode excepted), and never saves in demo mode.
- **Nothing is written until something was read.** `HasLoadedSettings` — a read that *succeeded*, not merely one that finished — gates both Save and the rows' editability. The rows' constructor defaults are this app's invention (a slider rests at its floor, a toggle at false), so saving them over a device file would write `macro_speed=1`, `game_mode=OFF` and `v_drive=manual`, the last of which stops the v-Drive auto-mounting. A failed read, or a device with no drive, therefore leaves the panel read-only with an inline explanation.

## Load-bearing invariants

1. **The `=` separator is part of key matching** (08 §1): read and write sides share the rule "starts with K case-insensitively AND char at K.Length is `=`" — this, not trailing-`=` special cases, resolves `v_drive`/`v_drive_open_on_startup`, `cust_color_1`/`cust_color_10`, and `status`/`status_play_speed`.
2. **Unknown and reserved lines survive verbatim** (08 §1–2): saves only ever pass managed pairs to `UpdateSettingsFile`; reserved keys are read into the model but have no write path.
3. **`on` means hide** (08 §3): a `*_msg` flag is the "hide this notification" checkbox; missing key = `off` = show. Never invert this.
4. **Adv2 keyboard-settings writes are 4MB-gated** (09 §1.1): no marker → `InvalidOperationException` before anything touches the file; all other devices are ungated.
5. **Unset colors are skipped** (08 §3): a null color slot's key is not written — never write `cust_color_N=`.
6. **Parse case-insensitively, write canonical casing** (08 §2 notes): `ON`/`OFF` uppercase, `v_drive` `auto`/`manual`, Adv2 v-Drive false literally `off`, app flags lowercase `on`/`off`, `led_mode` lowercased file name / uppercase `P`/`B`.
7. **Write only what the capability designates and the model sets**: nullable = absent, absent = not written; device applicability lives exclusively in `SettingsCapability` in the catalog.
8. **`app_settings.txt` has one reader and one writer** — this module, reached through `ISettingsService`. Anything in the app layer that wants a flag or a colour loads the whole `AppSettings`, changes it, and saves it back; a second line-level implementation would drop whatever it does not model.
9. **A value domain is written down once.** The `led_mode` mode strings live in `LedModeValues` (the serializer's own validation list), the numeric bounds in `SettingsValueRanges`, the `led<N>.txt` naming in `StartupProfileSettings` — a UI list, a slider bound or a path builder restating any of them is how the picker starts offering what the save refuses.
10. **Absent stays absent, unknown stays unknown.** A key the file does not carry must not become a value on the next save: the panel's choice row has a real *unset* state, and both an absent and an unrecognised `led_mode` leave the property **null** — which is what keeps the device's line, since null emits no pair and unmanaged lines survive (invariant 2). Selecting the first option instead would switch a Freestyle Edge's backlight off (`led_mode=0`) the first time anyone opened the tab; writing the unknown text back instead would make the serializer throw.

## Deliberately not here

- **No local fallback location for `app_settings.txt`** when no drive is attached (03 §6 "next to the executable") — deferred by decision on issue #10; the service currently requires a `VDriveLocation`.
- **No master-app registry globals** (08 §4 `HideAllNotifs`/`ShowAllNotifs`) — out of scope for #10; the `(not hidden and not "hide all") or "show all"` display rule belongs to the future notification layer.
- **No eject after a settings save** (08 §5.1 "then the device is ejected") and **no "Do you want to save changes?" prompt on leaving the panel** — Home already ejects ([app-shell.md](app-shell.md)), and the editor has no unsaved-changes guard at all yet.
- **No app-settings dialog.** Nothing edits the twelve hide flags directly; they are still written only as the answer to a "Hide this notification?" checkbox. The twelve `cust_color_N` slots, by contrast, **are** written — by the lighting picker's `Add to Custom Colors` (`ColorPickerViewModel.StoreCustomColorAsync`, see above and [lighting.md](lighting.md)) through the same read-modify-write; do not add a second writer.
- **No typed slot for the Adv2 startup layout file** (spec 02: `state.txt` carries a "startup layout file", e.g. `q_qwerty.txt`) — `StartupProfileNumber` is numeric, so such values read as null; harmless on disk (Adv2's capability never writes the key, the line survives verbatim), surfacing it is deferred to the Adv2 editor-UI work (issues #37/#42).
