# Settings engine (Keyboard settings, App settings)

The settings layer of `KinesisEdit.Core`: typed models, pure line-based parsers/serializers, and a thin v-Drive-bound service for the device-side keyboard-settings files and `app_settings.txt` of spec 08. Consumes per-device applicability from `KinesisEdit.Core.Devices.SettingsCapability` ([domain-data.md](domain-data.md)) and does all file I/O through `IVDriveFileService` ([vdrive.md](vdrive.md)) — no device facts and no raw file access live here. It never parses layouts, macros, or version files.

> **Everything in this doc is *per device*, and there is a second store that is not.** `kbd_settings.txt` and `app_settings.txt` both live on a keyboard's v-Drive and travel with the board; the app also has **host preferences** — theme, motion budget and the shell window's geometry — in a JSON file in the per-user configuration directory, reached through `IHostPreferencesStore` and edited on the shell's own **Settings screen** (issue [#96](https://github.com/migus88/kinesis-edit/issues/96)). The two are not variants of one thing: a host preference has to be readable with no keyboard attached, and a device preference has to follow the board to whatever machine it is plugged into. Neither file can do the other's job, so **do not extend `IAppPreferencesStore` to carry a theme and do not put a hide-flag in `HostPreferences`** — invariant 8 below now has a sibling rule. The full comparison is in [host-preferences.md](host-preferences.md).

| Namespace | Entry point(s) | Encodes / Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Settings` | `SettingsKeys`, `SettingsValueRanges`, `SettingsLineReader` | Exact key names, numeric ranges, read-side `=`-rule lookup | 08 §1–3 |
| `KinesisEdit.Core.Settings` | `KeyboardSettings`, `KeyboardSettingsParser`, `KeyboardSettingsSerializer` | Typed device-settings model, parse/serialize | 08 §2, §5 |
| `KinesisEdit.Core.Settings` | `AppSettings`, `SettingsColor`, `AppSettingsParser`, `AppSettingsSerializer` | Sixteen hide flags + one display preference + custom colors | 08 §3 (+ 5 app-added keys) |
| `KinesisEdit.Core.Settings` | `KeyboardSettingsGate` | Advantage2 4MB write gate | 09 §1.1; 08 §2, §5.4 |
| `KinesisEdit.Core.Settings` | `LedModeValues` | The mode-string `led_mode` domain (`0`–`9`, `P`, `B`) | 08 §2, §5.3 |
| `KinesisEdit.Core.Settings` | `StartupProfileSettings` | Startup-profile ↔ `led_mode` pairing | 08 §5.1; 07 §1.2 |
| `KinesisEdit.Core.Settings` | `SettingsMessageCatalog` | Settings-panel strings | 08 §5.1, §5.4 |
| `KinesisEdit.Core.Settings` | `SettingsService` | Load/save binding to a `VDriveLocation` | 08 §1–3 |
| `KinesisEdit.Core.Devices` | `SettingsCapability` (+ `StartupSettingKind`, `LedModeKind`, `StatusSettingKind`, `VDriveSettingKind`) | Which keys the app writes per device, and their forms | 08 §2, §5 |
| `KinesisEdit.Services` | `ISettingsService`, `SettingsServiceAdapter` | The app layer's fakeable seam over the sealed `SettingsService` | — |
| `KinesisEdit.Services` | `AppPreferenceCatalog`, `AppPreferenceDescriptor`, `AppPreferencePolarity` | The seventeen user-facing preferences as data: key, wording, polarity, accessors | 08 §3; mockup 1j |
| `KinesisEdit.Services` | `IAppPreferencesStore` (+ `VDrive`/`ReadOnly`/`Null` implementations) | The session's single in-memory `app_settings.txt` | 08 §3 |
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

- `SettingsSavedTitle` = `"Settings Saved"`, `SettingsSavedMessage` = `"Changes will be implemented when v-Drive is closed."` — the post-save dialog of 08 §5.1/§5.2, suppressible via the `savesettings_msg`/`save_msg` hide flags. The spec's sequence ends in an eject; **this app does not eject after a settings save** (see "Deliberately not here"), so the wording is shown as a toast and the drive is released only when the user ejects it from the device card — Home does not ([app-shell.md](app-shell.md), deviation 22).
- `Advantage2SettingsDisabledHint` — the 08 §5.4 hint on the disabled Adv2 panel. The spec prescribes "an explanatory hint" without quoting it, so the wording states the condition `KeyboardSettingsGate.CanEditKeyboardSettings` actually tests (no `4MB` marker in the version file, 09 §1.1) rather than inventing legacy text.

The redesign's read-only treatment (mockup 1j) adds five more, quoted **verbatim from the mockup** and rendered on the *advisory* ramp — a 2MB board is a hardware fact, never an error, so no `StatusError*` brush appears on this screen:

- `Advantage2ReadOnlyBanner` = `"This Advantage2 has 2 MB firmware — device settings can't be written to it"`.
- `Advantage2ReadOnlyExplanation` — "The board reports 2MB… Remaps, macros, and layers all still save normally."
- `WhichBoardLinkCaption` = `"Which board do I have?"` — **stored without the mockup's `↗`**. The arrow is `IconExternalLink` geometry drawn beside the words, exactly as the empty state draws `NoDeviceViewModel.TroubleshootingButtonCaption` (`Button.link` + `Icon` + `IUrlLauncher`); a Latin-1 arrow in a caption is a glyph the shipped font may not carry.
- `ReadOnlyRowMarker` = `"(read-only)"` — the quiet per-row marker beside a value that is real and current but unwritable.
- `DemoModePreferencesCaveat` — the app-preferences section's demo caveat.

## App settings — `AppSettings`, `SettingsColor`, parser, serializer

- `AppSettingsParser.Parse(IReadOnlyList<string>)` / `AppSettingsSerializer.Serialize(AppSettings)`.
- `SettingsColor.TryParse` / `ToString()` — the `[R][G][B]` decimal 0–255 file form (`[255][0][128]`), strict on parse, canonical on format.

- `AppSettings.WithCustomColor(int slotNumber, SettingsColor? color)` → a copy with one slot replaced. `slotNumber` is **1-based**, the same numbering as `SettingsKeys.GetCustomColorKey` (1 = `cust_color_1` … 12 = `cust_color_12`); `null` clears the slot; outside 1–12 throws `ArgumentOutOfRangeException`. Needed because `CustomColors` is init-only and validated to exactly 12 entries. Convert to/from the lighting pickers' `LedColor` with `Lighting.LedColorConverter` ([lighting.md](lighting.md)).

Sixteen `bool?` hide flags (**`on` = hide** the notification; null = key absent = `off` = show), one `bool?` **display** preference, and `CustomColors`, a fixed list of 12 `SettingsColor?` slots (index 0 = `cust_color_1`; length-validated). Flags persist lowercase `on`/`off`; null flags and unset colors are skipped on save — a color key is never written empty (08 §3). The §4 master-app "hide all"/"show all" globals are not modeled here.

### The five keys this app adds — and the two polarities

Spec 08 §3 defines twelve `*_msg` keys. This app writes **five more** that the spec does not list. That is safe on disk: `SettingsService.SaveAppSettings` goes through `IVDriveFileService.UpdateSettingsFile` (read-modify-write, invariant 2), and the legacy Pascal app ignores keys it does not model — so an unknown key is additive, never destructive. Do not go looking for them in `specs/08-settings.md`.

**Four of them follow the spec's convention and the fifth does not.** This is the trap on this screen; get it wrong and you ship an inverted checkbox that every unit test still passes.

| Key | `AppSettings` property | `on` on disk means | Absent means | Default checkbox |
|---|---|---|---|---|
| `warn_unsaved_msg` | `IsUnsavedChangesWarningHidden` | **hide** the unsaved-changes prompt | warn | ✓ ticked |
| `reset_layer_msg` | `IsResetLayerConfirmationHidden` | **hide** the layer-reset confirmation | confirm | ✓ ticked |
| `capture_summary_msg` | `IsCaptureSummaryHidden` | **hide** the "keystrokes captured" summary | show it | ✓ ticked |
| `switch_variant_msg` | `IsSwitchVariantConfirmationHidden` | **hide** the variant-switch confirmation | confirm | ✓ ticked |
| `advisory_detail` | `IsAdvisoryDetailExpanded` | **expand** the advisory to full text | one trimmed line | ☐ unticked |

So: for the four `*_msg` keys the ticked box is the **absence** of the flag, and the UI inverts. For `advisory_detail` the ticked box **is** the flag, and the UI does not. The property names carry the difference (`Is…Hidden` vs `IsAdvisoryDetailExpanded`) and the key spelling does too — `advisory_detail` deliberately has no `_msg` suffix. `AppSettingsParser.ParseOnFlag` / `AppSettingsSerializer.AppendOnOffFlag` only ever say whether the stored value was `on`; **what `on` means is the property's business, never the parser's.**

Nothing above this file should be reasoning about polarity by hand: `AppPreferenceCatalog` does it once (below), and everything else asks it.

> Mockup 1j draws "Show the 'keystrokes captured' summary after recording" **unticked**. That is the mockup device's stored answer (`capture_summary_msg=on`), not the default — absent always means "show" on disk, and no other default is expressible.

## The preference catalog — `AppPreferenceCatalog` (in `Services`)

**One descriptor per user-facing preference, and it is the single source.** Seventeen of them: the twelve `*_msg` keys of 08 §3 plus the five above. A preference that has no descriptor is unrenderable, unwritable and untested — the view model, the store's read/write path and the tests all enumerate the catalog instead of restating the list, and `AppPreferenceCatalogTests` fails if a suppression key exists outside it.

`AppPreferenceDescriptor` carries `Key`, `Caption`, `Description`, `Polarity`, `HasLiveConsumer`, the derived `DefaultValue`/`IsSuppression`, and the two methods everything above the file uses:

- `GetValue(AppSettings) → bool` — the option **as the user sees it**, polarity already applied. Suppression: the negation of the stored flag. Display: the stored flag itself. An absent key yields `DefaultValue`.
- `SetValue(AppSettings, bool) → AppSettings` — the inverse. Bind a checkbox to this pair and the polarity cannot be got wrong.

`DefaultValue` is **derived from `Polarity`, never declared** (`Suppression` → true, `Display` → false), because absent always means `off` on disk and no other default is expressible. `HasLiveConsumer` marks the three preferences something actually reads today (`warn_unsaved_msg`, whose consumer is the editors' leave-with-unsaved prompt — [app-shell.md](app-shell.md); `reset_layer_msg`; `advisory_detail`); the other fourteen are **forward-declared** — the key exists so the feature that lands later (#42's variant switch, the spec 11 dialogs) does not invent a second one. It is metadata only: every descriptor renders its row either way, so a forward-declared preference can be pre-answered before its prompt exists.

Order is meaningful: `Featured` is mockup 1j's five in 1j's order (rendered unfolded), `Additional` is the remaining twelve in spec-table order (behind a disclosure), and `All` is the two concatenated. **The disclosure's "+N more" is `Additional.Count`, computed** — the mockup's literal "+7" assumed twelve preferences and there are seventeen.

`NotificationKeys` is now a **view** of this catalog, not a second list: its constants alias `SettingsKeys`, and `All` is the catalog filtered to `Polarity == Suppression` — sixteen keys. `advisory_detail` is absent from it by construction, so it can never become a message box's `SuppressionKey`.

## The preferences store — `IAppPreferencesStore`

**One in-memory `AppSettings` per device session; load once, mutate in memory, write through.**

```
AppSettings Current { get; }        // never null; an unreadable file yields AppSettings.Empty
bool IsWritable { get; }            // false in demo mode and with no drive
event Action? Changed;              // raised after Current changed
void Update(Func<AppSettings, AppSettings> mutate);
bool IsHidden(string key);          // inherited from INotificationSuppressionStore
void SetHidden(string key, bool hidden);
```

**Why it exists.** `app_settings.txt` has three kinds of consumer — the colour picker's twelve swatches, this screen's seventeen preferences, and every message box's "Don't ask this again". Before this store each loaded the file for itself, so a swatch stored in the picker was invisible to the settings screen until something re-read the file, and each write was a whole-file read-modify-write racing the others. Hence one store per session and `Changed`: **every consumer reads `Current` and re-reads it when the event fires.** That is invariant 8 made enforceable rather than merely written down.

It **extends** `INotificationSuppressionStore` rather than replacing it — `NotificationService` depends on the narrow pair and must not learn about swatches or display preferences.

Reach it from a view model through the active session: `IDeviceSessionAccessor.Active?.Preferences`, or `DeviceSession.Preferences` where the session is already in hand. `DeviceSession.SuppressionStore` is the *same object* seen narrowly.

Three implementations, chosen by `DeviceSessionManager.CreatePreferencesStore` under the same three-way rule as before, fixed when the session opens:

| Drive state | Store | Reads | Writes |
|---|---|---|---|
| Connected, writable | `VDriveAppPreferencesStore` | the file, once | through to the file |
| Present, not writable (demo) | `ReadOnlyAppPreferencesStore` | the file, once, via the wrapped v-Drive store | **discarded** |
| No drive at all | `NullAppPreferencesStore` | `AppSettings.Empty` | discarded |

**Demo mode keeps reading.** Spec 08 §3 bans *saving* `app_settings.txt` in demo mode, not loading it, so a demo session over a real drive still shows the notifications the user hid and the swatches they stored. Writes are discarded outright rather than kept in memory — a preference that appeared to take and then vanished would be a lie told twice; the screen says so in words instead (`DemoModePreferencesCaveat`).

Behaviour worth knowing:

- **`Update` never throws for I/O problems** — a preference must not break a dialog or a screen. `IOException`/`UnauthorizedAccessException`/`NotSupportedException` are swallowed, and **the in-memory value still moves**: the user ticked "don't ask again" and the box must stop appearing for this session, even if the drive refused the write. Nothing reached the disk, so the next session asks again.
- **The file is read once and not re-read.** A file rewritten behind the app's back is not picked up mid-session, which is exactly what stops two consumers disagreeing about what is current. The read-modify-write merge still keeps the foreign lines on disk.
- **`IsHidden`/`SetHidden` go through the catalog, not a switch.** A key with no descriptor, or one whose descriptor is a *display* preference, reads as "show" and writes nothing — an unmodelled key has no `AppSettings` property to carry it, and `advisory_detail` is not something a dialog may hide.

## Service — `SettingsService`

- `LoadKeyboardSettings(VDriveLocation)` / `SaveKeyboardSettings(VDriveLocation, VersionFileInfo, KeyboardSettings)`.
- `LoadAppSettings(VDriveLocation)` / `SaveAppSettings(VDriveLocation, AppSettings)`; static `GetAppSettingsFilePath(VDriveLocation)`.

Constructor takes `IVDriveFileService`. Saves go through `UpdateSettingsFile` (read-modify-write, 08 §1) so unknown/reserved lines survive verbatim. Keyboard-settings save gates Adv2 first, then serializes with `location.Device.Settings`; when it yields no pairs the file is not touched — a keyboard-settings save never removes anything. `app_settings.txt` lives in the drive's `settings/` folder for every device (08 §3, 03 §6) — even Adv2, whose keyboard settings live in `active/` — and, being app-owned, is the one file this module creates: a missing file on load yields `AppSettings.Empty`, on save it is created via `WriteAllLines(allowCreate: true)`. Missing-file detection is by catching `FileNotFoundException` from the file service, so a fake `IVDriveFileService` fully controls the behavior.

`SaveAppSettings` additionally passes `AppSettingsSerializer.SerializeRemovals(settings)` — the cleared colour slots (invariant 5) — so **"nothing to write" and "nothing to remove" are separate questions**: a save that only clears swatches produces no pairs and must still reach the drive. It short-circuits only when *both* sets are empty. On the create path the reverse holds: a removal against a file that does not exist is already satisfied, so removals alone never conjure an `app_settings.txt` onto a drive that never had one — only pairs do.

## The UI on top — `ISettingsService`, `KeyboardSettingsViewModel`

The app layer reaches this module through **one seam**, `KinesisEdit.Services.ISettingsService` (`LoadKeyboardSettings` / `SaveKeyboardSettings(location, versionFileInfo, settings)` / `LoadAppSettings` / `SaveAppSettings`), implemented by the behaviour-free `SettingsServiceAdapter` over the sealed `SettingsService` — the same shape as `IProfileSession`/`ProfileSessionAdapter` ([keyboard-editor.md](keyboard-editor.md)). The composition root builds one instance and hands it to `DeviceSessionManager` and `EditorViewModelFactory`.

- **`app_settings.txt` has exactly one reader and one writer**, and that is now a *type* rather than a discipline: `IAppPreferencesStore` (above). It parses no lines itself — every read and write loads/saves the whole `AppSettings` through the seam, swallowing every I/O failure. A key outside `AppPreferenceCatalog` is not modeled and is therefore never written. This is also what puts the file in `settings/` on the **Advantage2** — the store used to resolve it through the device's `SettingsFolderPath` (`active/`). Preferences and the twelve `cust_color_N` slots ride the same route and the same in-memory copy, so a colour save and a suppression save cannot clobber each other or serve each other stale state.
- **The panel is `KeyboardSettingsViewModel`** — the editor's Settings tab, described in [keyboard-editor.md](keyboard-editor.md). It renders one row per key the device's `SettingsCapability` designates (`KeyboardSettingsRows`), builds the `led_mode` picker from `LedModeValues`, clamps every value into `SettingsValueRanges` before saving, routes the active-profile row through `StartupProfileSettings.ApplyStartupProfile`, disables itself on a 2MB Advantage2 via `KeyboardSettingsGate` (demo mode excepted), and never saves in demo mode.
- **Nothing is written until something was read.** `HasLoadedSettings` — a read that *succeeded*, not merely one that finished — gates both Save and the rows' editability. The rows' constructor defaults are this app's invention (a slider rests at its floor, a toggle at false), so saving them over a device file would write `macro_speed=1`, `game_mode=OFF` and `v_drive=manual`, the last of which stops the v-Drive auto-mounting. A failed read, or a device with no drive, therefore leaves the panel read-only with an inline explanation.

## Load-bearing invariants

1. **The `=` separator is part of key matching** (08 §1): read and write sides share the rule "starts with K case-insensitively AND char at K.Length is `=`" — this, not trailing-`=` special cases, resolves `v_drive`/`v_drive_open_on_startup`, `cust_color_1`/`cust_color_10`, and `status`/`status_play_speed`.
2. **Unknown and reserved lines survive verbatim** (08 §1–2): saves only ever pass managed pairs to `UpdateSettingsFile`; reserved keys are read into the model but have no write path. **`app_settings.txt` is never rewritten wholesale** — it is shared with the legacy Pascal app, which writes keys this app does not model. The one qualification is deletion: `UpdateSettingsFile(path, values, removedKeys)` also takes a set of keys to *remove*, and `SaveAppSettings` fills it from `AppSettingsSerializer.SerializeRemovals` — the cleared colour slots, and nothing else. Removal is surgical: only keys this module manages and explicitly names are dropped, by the same `=`-separator rule (invariant 1), so everything foreign still survives byte-for-byte.
3. **`on` means hide — except once** (08 §3): every `*_msg` flag is the "hide this notification" checkbox, missing key = `off` = show. `advisory_detail` is the one key in this file whose `on` means **expand**, not hide. Never invert either, and never fold them into one abstraction: ask `AppPreferenceDescriptor.GetValue`/`SetValue`, which apply `AppPreferencePolarity` once for everyone.
4. **Adv2 keyboard-settings writes are 4MB-gated** (09 §1.1): no marker → `InvalidOperationException` before anything touches the file; all other devices are ungated.
5. **Unset colors are skipped, and therefore removed** (08 §3): a null color slot's key is not written — never write `cust_color_N=`. But *skipped* and *cleared* look identical to a merge, so skipping alone left a cleared swatch on the drive and it came back on the next load (issue #95). `AppSettingsSerializer` answers in two halves — `Serialize` (the set slots, as pairs) and `SerializeRemovals` (the unset slots, as keys to delete) — and `SaveAppSettings` hands both to `UpdateSettingsFile`. The fix is deletion, never an empty value. **Hide flags get no such treatment**: `AppPreferenceDescriptor.SetValue` always writes an explicit `on`/`off`, so a null flag means "never answered", not "cleared", and deleting its key would throw away a line this app never owned.
6. **Parse case-insensitively, write canonical casing** (08 §2 notes): `ON`/`OFF` uppercase, `v_drive` `auto`/`manual`, Adv2 v-Drive false literally `off`, app flags lowercase `on`/`off`, `led_mode` lowercased file name / uppercase `P`/`B`.
7. **Write only what the capability designates and the model sets**: nullable = absent, absent = not written; device applicability lives exclusively in `SettingsCapability` in the catalog.
8. **`app_settings.txt` has one reader and one writer** — this module, reached through `ISettingsService`, and in the app layer through the session's single `IAppPreferencesStore`. Anything that wants a flag, a preference or a colour reads `Current` and writes through `Update`; a second reader shows stale state and a second line-level implementation drops whatever it does not model. **Its sibling rule**: the *host* preferences (theme, motion, window geometry) are a different file with a different scope and a store of their own — `IHostPreferencesStore` ([host-preferences.md](host-preferences.md)) — and neither store may grow into the other. A preference about a keyboard goes on the drive; a preference about the person or the machine does not.
9. **A value domain is written down once.** The `led_mode` mode strings live in `LedModeValues` (the serializer's own validation list), the numeric bounds in `SettingsValueRanges`, the `led<N>.txt` naming in `StartupProfileSettings` — a UI list, a slider bound or a path builder restating any of them is how the picker starts offering what the save refuses.
10. **Absent stays absent, unknown stays unknown.** A key the file does not carry must not become a value on the next save: the panel's choice row has a real *unset* state, and both an absent and an unrecognised `led_mode` leave the property **null** — which is what keeps the device's line, since null emits no pair and unmanaged lines survive (invariant 2). Selecting the first option instead would switch a Freestyle Edge's backlight off (`led_mode=0`) the first time anyone opened the tab; writing the unknown text back instead would make the serializer throw.

## Deliberately not here

- **No local fallback location for `app_settings.txt`** when no drive is attached (03 §6 "next to the executable") — deferred by decision on issue #10; the service currently requires a `VDriveLocation`.
- **No master-app registry globals** (08 §4 `HideAllNotifs`/`ShowAllNotifs`) — out of scope for #10; the `(not hidden and not "hide all") or "show all"` display rule belongs to the future notification layer.
- **No eject after a settings save** (08 §5.1 "then the device is ejected") — nothing in the app ejects except the dashboard card's own button ([app-shell.md](app-shell.md), deviation 22) — and **no "Do you want to save changes?" prompt on leaving the panel**: the editor's unsaved-changes guard asks about the *profile*, and the settings file is outside the session's dirty comparison by design ([keyboard-editor.md](keyboard-editor.md), "Settings are outside the dirty model"), so a dirty settings row is still lost silently.
- ~~**No app-settings dialog.**~~ **Reversed by issue #95.** There *is* now a preferences UI — the Settings tab's "App & notifications" section — and the hide flags are edited directly there as well as through a message box's "Don't ask this again". **The warning that bullet carried still stands, and is now the store's contract rather than a rule to remember: `app_settings.txt` gets exactly one writer per session, `IAppPreferencesStore`.** Every consumer — the preferences section, the custom-swatch strip, `ColorPickerViewModel.StoreCustomColorAsync` ([lighting.md](lighting.md)) and `NotificationService` — goes through that one object and re-reads `Current` on `Changed`. Do not add a second writer, and do not reintroduce a per-view-model `LoadAppSettings`/`SaveAppSettings` pair.
- **No typed slot for the Adv2 startup layout file** (spec 02: `state.txt` carries a "startup layout file", e.g. `q_qwerty.txt`) — `StartupProfileNumber` is numeric, so such values read as null; harmless on disk (Adv2's capability never writes the key, the line survives verbatim), surfacing it is deferred to the Adv2 editor-UI work (issues #37/#42).
