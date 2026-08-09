# Profiles (load/save orchestration)

The orchestration layer of `KinesisEdit.Core`: ties the layout engine, the lighting engine, the
settings engine, and v-Drive file I/O together into one load/edit/save unit for a
**numbered profile** — Freestyle Edge/Pro, Freestyle Edge RGB, TKO, Advantage 360
(specs/03-vdrive-and-files.md §4.1/§4.3, §5.3). Advantage2's position-based naming is a separate
module, issue #37. Depends only on `Layouts`, `Lighting`, `Settings`, `VDrive`, `Devices`,
`Model` — no UI. **It does not eject** — see "The service seam".

| Namespace | Entry point | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Profiles` | `ProfileSession.Load(VDriveLocation, DeviceId, int, IVDriveFileService?)` | Reads/parses `layout<n>.txt` + `led<n>.txt` (where present) + keyboard settings into a fresh session; the trailing file service is optional and defaults to the shared real one | 03 §4.1/§4.3; 04 §4.2 |
| `KinesisEdit.Core.Profiles` | `ProfileSession.Save()` / `.SaveAs(int, bool)` | Validate → write layout → write led → (SaveAs+startup) update settings → message. **No eject** | 03 §5.3 |
| `KinesisEdit.Core.Profiles` | `ProfileSession.Import(ImportedFileKind, lines)` | Replaces the session's layout **or** lighting from an imported file; writes nothing | 10 "Import"; 07 §1.4 |
| `KinesisEdit.Core.Profiles` | `ProfileSession.RevertLayout()` / `.RevertLighting()` | Rebuilds **one** of the two files from the lines captured at `Load`, leaving the other untouched; writes nothing and reads no drive | 03 §4.1/§4.3 |
| `KinesisEdit.Core.Profiles` | `ProfileSaveResult`, `ProfileImportResult` | Outcome records: `Success`/`Violations`/`PostSaveMessage`; `Kind`/`InvalidLines` | 03 §5.3; 04 §5 |
| `KinesisEdit.Core.Profiles` | `ProfileReadOnlyException` | The Advantage 360 profile-0 guard | 02 "Profiles 0-9" |
| `KinesisEdit.Core.Profiles` | `ProfileSaveMessageCatalog` | Per-device-family post-save wording (data only, like `FirmwareGateCatalog`) | 03 §5.3; 07 §1.3; 10 |
| `KinesisEdit.Core.Profiles` | `ProfileLightingCodec` (internal) | Device → `LedFileParser`/`LedFileSerializer` dispatch | 07 §1.1-§1.4 |
| `KinesisEdit.Core.Profiles` | `ProfileFileNames` (internal) | The one `layout<n>.txt` / `led<n>.txt` naming, shared by the drive paths and an export's base names; its led name **delegates to** `StartupProfileSettings.GetLedFileName`, which is what also writes `led_mode` | 03 §4.1; 07 §1.2; 11 §11.5 |
| `KinesisEdit.Core.Transfer` | `ProfileExportPlanner.Plan(session, selection)` | The files an export writes, layout first ([feature-dialogs.md](feature-dialogs.md)) | 11 §11.5 |

## `ProfileSession`

- `Load` resolves `layout<n>.txt`/`led<n>.txt` from `VDriveLocation`'s computed folder paths and
  the device's `LayoutFileScheme` (`FirstProfileNumber`/`LastProfileNumber`/`HasReadOnlyFactoryProfile`,
  and whether the device has a *profile-orchestrated* led file — see below). Both names come from
  `ProfileFileNames`, whose led name is `StartupProfileSettings.GetLedFileName`
  ([settings.md](settings.md)) — the same helper that writes the paired `led_mode` value — so the
  file a session reads and the file the device is pointed at are spelled in one place. Every call returns a
  **brand-new instance**; nothing is ever reloaded in place. This is deliberate: on top of
  `LayoutFileParser.Parse` already building a fresh `KeyboardLayout` per call, it is what gives
  "full-model-wipe-on-load" (04 §4.2) its guarantee at the orchestration level — there is no stale
  session lying around that a caller could half-update.
- Exposes `Layout` (`KeyboardLayout`), `Lighting` (`object?` — a `LightingModel`, `TkoLightingModel`,
  or `Advantage360LightingModel` depending on device; null where the device has none), `InvalidLines`
  (`IReadOnlyList<LayoutInvalidLine>`, `Keep` defaults false — 04 §5.2), `ProfileNumber`, `Device`.
  The first three have a **private setter**: an `Import` replaces them wholesale, so callers must
  hold the *session* and re-read them, never cache the `KeyboardLayout` reference.
- `IsDirty` re-serializes `Layout` (+ `Lighting`) with the *same* serializers a save would use and
  compares the lines against **the baseline** — no bespoke model equality. Because the baseline is
  captured by serializing the just-parsed model (not the raw file text), `IsDirty` is false
  immediately after `Load` even for a non-canonical legacy input.
- **The baseline is always what is on the drive for this profile.** It is captured at `Load` and
  **moved by a save that wrote this profile's own files** — see "Save sequence" step 5. That is what
  makes "a profile with no changes is never rewritten" answerable: a caller that saves and asks
  again gets false, and a second `Save()` over an untouched model writes the same bytes for nothing.
  It does **not** move for a `SaveAs` to another slot (this profile's file was not written) and it
  does **not** move for an `Import` (whose whole point is that the imported content is unsaved).
  The two fields behind it are the only non-`readonly` state on the session, written from
  `ExecuteSave` and nowhere else; `IsDirty` stays a pull.
- **`RevertLayout()` / `RevertLighting()` throw one file's edits away** and rebuild that model from
  the captured lines — see "Reverting one file" below.
- `CanSave` is false only for profile 0 on a device whose `LayoutFileScheme.HasReadOnlyFactoryProfile`
  is true (the Advantage 360's factory profile, which has no on-disk file at all).
- `Save()` saves back to `ProfileNumber`. `SaveAs(targetProfileNumber, setAsStartup)` saves to a
  different slot within `[FirstProfileNumber, LastProfileNumber]` and, when `setAsStartup` is true,
  also updates `startup_file`/`led_mode` (or the Advantage 360's `profile` key) via
  `SettingsService.SaveKeyboardSettings` in the same call (07 §1.2: "Save As to a profile number
  switches both the current layout file and the current led file at once"). `setAsStartup: false`
  never touches the settings file.

## The service seam — `Load`'s one optional service

`Load` takes an optional `IVDriveFileService` after the profile number. Omit it and nothing
changes: the session uses the **shared** static defaults — one `VDriveFileService` and one
`SettingsService` over it — so a plain three-argument `Load` reads and writes exactly as it always
has.

- **The file service is all-or-nothing.** A session given one routes **every** read and write through
  it — `layout<n>.txt`, `led<n>.txt` *and* the settings file, whose `SettingsService` is constructed
  over the very same instance (`ResolveSettingsService`). Keeping a static `SettingsService` while
  letting the file service be injected would produce a session reading its layout from the substitute
  and its settings from the real disk; that split is the failure this seam exists to make impossible,
  and `ProfileSessionInjectedServicesTests` pins it (its startup-profile theory can only produce both
  post-save wordings if the settings snapshot came from the injected service).
- **A save never ejects, and there is therefore no ejector here at all.** Until issue #131
  `ExecuteSave`'s last step was `IVDriveEjector.Eject(location.RootPath)`, so every `Save()` — the
  editor toolbar's, the unsaved-changes prompt's, all of them — unmounted the volume under the user.
  That contradicted a law already written down: docs/design/README.md § "The laws that cut across
  every screen" (*"Nothing ejects implicitly… Eject is its own deliberate action on the device
  card"*), restated as [app-shell.md](app-shell.md) invariant 1. The session now holds no
  `IVDriveEjector`, `Load` takes none, `ProfileSaveResult` reports none, and this module names no
  type from `KinesisEdit.Core.VDrive.Eject`. **Do not re-add one** — `ProfileSessionInjectedServicesTests`
  asserts structurally that none is reachable, because with the seam gone there is nothing to inject
  and watch. The eject module itself is untouched and still has real consumers: `DeviceEjectService`
  → `VDriveEjectNotifier` → the dashboard card's `Eject` button ([vdrive.md](vdrive.md),
  [app-shell.md](app-shell.md)), which is the user's own deliberate release.
- **Core learns nothing new from this.** The file service is injectable, full stop: there is no demo
  flag, no demo type and no demo branch here (see "Deliberately not here"). Why an app-layer caller
  would want an in-memory drive is the app layer's business.
- The app-side counterpart is `KinesisEdit.Services.ProfileSessionFactory(IVDriveFileService?)` —
  optional, forwarded to `Load` **as given** (null included, so defaulting stays Core's single
  decision), and `new ProfileSessionFactory()` still means the real drive.

## Profile-0 guard

`Load`, `Save`, and `SaveAs` all check profile 0 on a `HasReadOnlyFactoryProfile` device **before**
anything else — before validation, before any file is touched — and throw `ProfileReadOnlyException`
with the spec 02 wording verbatim: `"Profile 0 is non-programmable so you must use the Save As
Button..."`. `Load` must guard too: profile 0 has no on-disk `layout0.txt`, so there is nothing to
read. Because of this, no session can ever exist with `ProfileNumber == 0` — `CanSave`'s "false for
profile 0" branch and the same check inside `Save`/`SaveAs` are the same shared guard, exercised
directly through `Load` and through `SaveAs(0, ...)` from a session loaded at any other profile.

**The guard is unreachable from the app, and that is on purpose rather than by luck.** Every
`NumberedProfiles` scheme in `DeviceCatalog` — the Advantage 360's included — runs
`FirstProfileNumber = 1` to `LastProfileNumber = 9`, so the editor's profile drop-down, which is
built from that range verbatim, offers **1–9 and never a `Profile 0` row**: a row whose only possible
outcome is `ProfileReadOnlyException` would violate the design's "absent, never disabled" law. The
app-side flag is still implemented (`ProfileOptionViewModel.IsReadOnly`, asked of Core's own
`profileNumber == 0 && HasReadOnlyFactoryProfile` rule and enforced by the picker's `CanExecute`), so
a scheme that ever did start at 0 gets a refusal rather than an error dialog; it is exercised through
a synthetic device, because no shipped one produces it.

## Save sequence (`Save` and `SaveAs` share one private method)

1. Profile-0 guard (above).
2. `Layout.Validate()`. Any violation **stops the save**: returns
   `ProfileSaveResult { Success = false, Violations = violations, PostSaveMessage = null }`
   without writing anything (04 §5.3's "validate macro capacity first" gate, applied to every
   reported limit, not only macro count — this is the save *orchestration* gating on the
   model's report; it does not change `KeyboardLayoutValidator`'s "report, don't enforce" contract
   for live editing).
3. `LayoutFileSerializer.Serialize(Layout, InvalidLines)` → `IVDriveFileService.WriteAllLines(...,
   allowCreate: true)`.
4. If `Lighting` is not null, the matching `LedFileSerializer.SerializeXxx` → `WriteAllLines(...,
   allowCreate: true)`.
5. **The lines just written become the baseline** `IsDirty` compares against — but only when
   `targetProfileNumber == ProfileNumber`. See the `IsDirty` bullet above for why both halves of
   that sentence are load-bearing.
6. If this is a `SaveAs` with `setAsStartup == true`: `_settings with { StartupProfileNumber =
   target }` (+ `LedMode = "led<target>.txt"` when the device's `SettingsCapability.LedMode` is
   `LedFileName`) saved via `SettingsService.SaveKeyboardSettings` — read-modify-write, so every
   other setting survives untouched.
7. **Nothing.** This step used to eject the v-Drive and no longer exists (issue #131): the writes
   are the whole save, and the volume stays mounted until the user presses `Eject` on the device
   card. See "The service seam".
8. `PostSaveMessage` from `ProfileSaveMessageCatalog.GetMessage(Device, targetProfileNumber,
   isStartupProfile)`, where `isStartupProfile` is `setAsStartup || settings.StartupProfileNumber ==
   targetProfileNumber` (the settings snapshot captured at `Load`).

## Import and export (`ProfileSession.Import`, `KinesisEdit.Core.Transfer`)

`Import(kind, lines)` is deliberately **the same operation as `Load` from a different source**: the same
`LayoutFileParser`/`ProfileLightingCodec` path runs, so an imported file behaves exactly as it would
off the drive — a brand-new model (04 §4.2's full-model-wipe-on-load), invalid lines tracked rather
than dropped (04 §5), nothing written anywhere.

- `kind` is decided **before** the call by `Transfer.ImportClassifier` (07 §1.4), and reading the file
  and enforcing its 50 KB maximum are the caller's job ([feature-dialogs.md](feature-dialogs.md)).
- A **layout** import replaces `Layout` **and** `InvalidLines`; a **lighting** import replaces only
  `Lighting` and reports the layout's invalid lines unchanged, so a caller can always re-render that
  surface from `ProfileImportResult.InvalidLines`. Importing lighting into a device with no
  profile-orchestrated led file throws `NotSupportedException` — defensive, since the classifier
  answers `Lighting` only for the three devices that have one.
- The **profile-0 guard runs first**, exactly as for `Save`: the Adv360 factory profile cannot be
  imported into either. An import **never moves the `IsDirty` baseline** — only a save does — so
  imported content is unsaved edit state and a save writes it like any other edit.

`Transfer.ProfileExportPlanner.Plan(session, selection)` is the read-only twin: it serializes the
session with the *same* serializers a save uses (kept invalid lines included) and names the files
through `ProfileFileNames`, layout first. It takes the concrete `ProfileSession` because that is the
only place the layout, the lighting model and the profile number live together — and because Core's
three lighting models share no base type, so a caller holding `object? Lighting` could not pick a
serializer for it.

## Reverting one file — `RevertLayout` / `RevertLighting`

Added for the editor's `Discard changes` ([#133](https://github.com/migus88/kinesis-edit/issues/133)),
which is scoped to **one page of one profile**: on the Keys/Macros tabs it throws the layout edits
away and on the Lighting tab the led ones ([keyboard-editor.md](keyboard-editor.md)).

**It is `Import`'s shape from a different source.** `RevertLayout` re-parses `_originalLayoutLines`
into a brand-new `KeyboardLayout` and puts it in place of `Layout`; `RevertLighting` re-parses
`_originalLightingLines` through `ProfileLightingCodec` into a new `Lighting`. Same contract as an
import, therefore, and the same warning: both replace the model **wholesale**, so a caller must hold
the *session* and re-read `Layout`/`Lighting`/`InvalidLines` afterwards — every view model over the
old instance is stale. "Every `Load` returns a brand-new instance" is untouched: this is not a load.

Three properties are why the app-layer alternative — re-reading the drive — was rejected:

- **It is scoped to one file.** A re-read is all-or-nothing and would take the other page's edits with it.
- **It cannot diverge from the dirty baseline, because it *is* that baseline.** After
  `RevertLayout()` the layout half of `IsDirty` is false by construction, not by luck; the same
  serializer that captured `_originalLayoutLines` is the one `IsDirty` compares with.
- **It needs no drive.** The lines are in memory, so a discard works over a volume that has since
  been unmounted, where a re-read would throw.

Two smaller decisions:

- `InvalidLines` goes back to the **list captured at load**, not to whatever re-parsing the baseline
  produces. The baseline was serialized *with* those lines, so the kept ones are in it and the unkept
  ones are not (04 §5.2) — restoring the captured list is what makes the round trip exact and keeps
  the caller's "some lines could not be applied" report saying what it said when the profile opened.
- `RevertLighting` on a device with **no** profile-orchestrated led file is a **no-op**, where
  `Import` throws. A revert's postcondition — "the lighting is what was loaded" — already holds
  there, so there is nothing to refuse.

**A revert restores what is on the drive**, which is the load for a profile nobody has saved and
the last `Save` for one that has — the baseline moves with the write, so a discard can never take
back work the user already committed, and a profile reverted straight after a save is clean.

`ProfileSessionRevertTests` pins all of it over a real fixture drive: each half reverting alone and
leaving the other edited, both together returning `IsDirty` to false, a fresh `Layout` instance, the
invalid-line list restored, a device with no led file no-oping, a revert **after the drive folder is
deleted**, neither file's timestamp moving, and a revert **after a save** landing on the saved state
rather than on the loaded one. `ProfileSessionDirtyTrackingTests` carries the baseline's own four
cases: false after a successful `Save()`, a second `Save()` writing the same bytes over a session
that is no longer dirty, **unchanged** after a `SaveAs()` to another slot, and unchanged after a save
validation stopped.

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

## The app-layer seam

`ProfileSession` is sealed and opened through a static `Load`, so it cannot be substituted in a test.
The app project therefore codes against `KinesisEdit.Services.IProfileSession` /
`IProfileSessionFactory` — `Layout`, `Lighting`, `InvalidLines`, `ProfileNumber`, `CanSave`,
`IsDirty`, `Save()`, `Import(kind, lines)`, `RevertLayout()`, `RevertLighting()`,
`PlanExport(selection)` — implemented for real by
`ProfileSessionAdapter` (a pure pass-through; its `PlanExport` is the one line that calls
`ProfileExportPlanner.Plan` on the wrapped session) and `ProfileSessionFactory` (calls
`ProfileSession.Load`, wraps the result; its one optional constructor dependency is the
file service it forwards — see "The service seam" above). **Nothing is re-implemented above this module**; the seam
exists only so the editor view models can be unit-tested without a drive. Its consumer is the
keyboard editor ([keyboard-editor.md](keyboard-editor.md)), which loads
`LayoutScheme.FirstProfileNumber` on open, calls `Save()` off the UI thread, and reaches `Import`
and `PlanExport` from the feature panels ([feature-dialogs.md](feature-dialogs.md)). Import is gated
on `CanSave` — the same flag that keeps the Adv360 factory profile read-only, so the editor never
even reaches Core's guard — while an export only needs a session to exist, which excludes demo mode.

**`Load` has a second consumer since issue #128**: the editor's profile drop-down calls it again, on
the same factory, for another number, and hands the result to the same `Apply` the first load uses
([keyboard-editor.md](keyboard-editor.md) § "The profile picker"). The switch **writes nothing** — it
is a read, so nothing here changes and no `Save`/`SaveAs` path is involved.

**Since issue #133 the editor holds several sessions at once**, one per profile the user has visited,
and that is worth stating here because it changes who this module's caller is. `Load` is called
**once per profile number**, lazily, on first visit — never eagerly for all nine, because
`IVDriveFileService.ReadAllLines` throws `FileNotFoundException` for a missing file and spec 03 §5.3
lets a drive ship with gaps, so an eager load would need an "empty slot" concept this module does not
have. A switch back to a visited profile calls nothing at all. `Save()` is then called once per
**changed** session, in file order — a clean one is never called at all, so its files are never
rewritten with the bytes they already hold — and the editor pre-validates the whole set with
`KeyboardLayout.Validate()` — the same call `ExecuteSave` step 2 makes — before letting any of them
write, so one over-budget profile cannot leave the earlier ones on the drive. **Nothing about that is
this module's policy**: each session still knows only its own profile, validates it and refuses it on
its own; the batching lives entirely in the app layer, which is where "which profiles has this user
opened" is known. A session is now released only when the editor is disposed, and the guard for the
day `IProfileSession` becomes `IDisposable` moved with it (`ProfileSession` still holds parsed lines
and no handle).

`IsDirty` is the toolbar `Save`'s amber and the unsaved-changes guard's question
(`KeyboardEditorViewModel.RefreshDirtyState`, now an aggregate over every held session), so the
seam's one remaining unconsumed member is Core's **`SaveAs`** — the app has no Save-As, no New and no
free-named backup file (see "Deliberately not here"), and the *settings* half of `SaveAs`'s startup
pairing is reached instead through the Settings tab's active-profile slider
([settings.md](settings.md)).

## Deliberately not here

- **Advantage2** — position-based `<pos>_qwerty.txt`/`<pos>_dvorak.txt` naming, the `active/`
  folder, `state.txt`. Split into issue #37; this module refuses any non-`NumberedProfiles` device
  with `NotSupportedException`.
- **Free-named "backup file" saving** (FS Edge/Pro's Save-As-to-arbitrary-filename, 10
  "SmartSetFSEdgePro") — numbered slots 1-9 only.
- **Demo-mode gating and the "Keyboard Connection Lost" dialog** (03 §3.5) — app-layer concerns;
  Core has no concept of demo mode. A drive that vanishes mid-save surfaces whatever
  `IVDriveFileService` throws naturally (`FileNotFoundException`/`IOException`); there is no
  Core-level exception for it and no demo-mode parameter on `ProfileSession`. `Load`'s optional
  service is **not** that parameter in disguise: it names *which* file service to use, and this
  module cannot tell a fixture-backed one from a drive-backed one.
- **The settings-only post-save message** (`"Changes will be implemented when v-Drive is
  closed."`, 03 §5.3) — that wording belongs to a bare keyboard-settings save with no profile
  content involved — it lives in `SettingsMessageCatalog` and is shown by the editor's settings
  panel (see [settings.md](settings.md)); every `ProfileSession` save writes profile content, so
  this case never arises here.
- **No new dependency on `Advantage2`'s dialect, and no Gen2-header awareness beyond what
  `LayoutFileParser`/`LayoutFileSerializer` already do** — this module never inspects file text
  itself; it only moves lines between the file service and the existing parsers/serializers.
