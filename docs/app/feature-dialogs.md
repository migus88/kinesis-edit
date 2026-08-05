# Feature dialogs (Tap and Hold, Macro Delays, Search Keys, Export, Import)

The advanced-feature surfaces of spec 11 that hang off the keyboard editor: the **Assign Tap and Hold Action** panel (§11.1), **Macro Timing Delays** (§11.3), **Search Keys** (§11.6), **Export files** (§11.5), and **Import** (spec 10 + 07 §1.4, which has no dialog of its own). The macro panel they feed is part of the editor ([keyboard-editor.md](keyboard-editor.md)); the rules they enforce are Core's — the four pre-dialog checks, the delay tokens, the search catalog, the import heuristic and the export plan all live in `KinesisEdit.Core` so the wording and the validation are testable apart from any window.

**The four panels are rendered inline over the editor, not as separate windows** — a deliberate departure from the `DialogWindowHost`/`MessageBoxWindow` pattern the shell uses ([app-shell.md](app-shell.md)). Keystroke capture attaches to one `TopLevel` ([keystroke-capture.md](keystroke-capture.md)), so a panel that must read real keypresses (Tap and Hold) in a second window would either capture nothing or need a second capture service; hosting it inside the editor keeps one owner for routing and one place that starts and stops the service.

| Namespace | Entry point(s) | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Model` | `MacroDelayTokens` | `dran` / `d001`..`d999` token text + dialect resolution | 11 §11.3; 06 §2.2 |
| `KinesisEdit.Core.Model` | `TapAndHoldPrecheck`, `TapAndHoldRefusal` | The four pre-dialog checks + their verbatim refusals | 11 §11.1 |
| `KinesisEdit.Core.Keys` | `KeySearchCatalog`, `KeySearchEntry` | The searchable action list and its filter | 11 §11.6; 05 §1.1, §3 |
| `KinesisEdit.Core.Transfer` | `ImportClassifier`, `ImportedFileKind` | Is an imported `.txt` a led file or a layout? + the 50 KB cap | 07 §1.4 |
| `KinesisEdit.Core.Transfer` | `ProfileExportPlanner`, `ProfileExportSelection`, `ExportFile` | What an export writes, under which base names | 11 §11.5 |
| `KinesisEdit.Core.Profiles` | `ProfileSession.Import`, `ProfileImportResult` | Replaces the session's layout **or** lighting from imported lines ([profiles.md](profiles.md)) | 10 "Import"; 07 §1.4 |
| `KinesisEdit.ViewModels` | `EditorOverlayViewModel`, `EditorOverlayHost`, `IKeystrokeSink` | The panel base class, the host that swaps/nests them, the capture contract | 11; 10 "Routing" |
| `KinesisEdit.ViewModels` | `TapAndHoldOverlayViewModel` (+ `TapAndHoldOpenResult`, `TapAndHoldField`) | §11.1's two action fields, the delay, its validation | 11 §11.1 |
| `KinesisEdit.ViewModels` | `MacroDelayOverlayViewModel` | §11.3's random/custom delay choice | 11 §11.3 |
| `KinesisEdit.ViewModels` | `SearchKeysOverlayViewModel` | §11.6's type-to-filter picker | 11 §11.6 |
| `KinesisEdit.ViewModels` | `ExportOverlayViewModel` | §11.5's three choices, the directory pick, the writes | 11 §11.5 |
| `KinesisEdit.ViewModels` | `FirmwareFeatureGate` | The shared refusal dialog + `Update Firmware` button | 09 §2; 11 §11.1 |
| `KinesisEdit.Services` | `IFolderPickerService`, `IFilePickerService`, `PickedFile`, `PickedFileReader`, `StorageProviderHost` | Native directory/file dialogs behind a toolkit-free seam | 11 §11.5; 07 §1.4 |
| `KinesisEdit.Services` | `ProfileImporter`, `ProfileImportOutcome` | The whole import flow: pick → size → classify → apply | 10 "Import"; 07 §1.4 |
| `KinesisEdit.Views` | `TapAndHoldOverlayView`, `MacroDelayOverlayView`, `SearchKeysOverlayView`, `ExportOverlayView` | XAML only; `ViewLocator` resolves them from the open panel | 11 |

## The overlay host — `EditorOverlayViewModel`, `EditorOverlayHost`

`EditorOverlayViewModel` is the base of all four panels: `Title`, `ErrorMessage`, `IsClosed`, `WasAccepted`, `AcceptCommand`/`CancelCommand`, and one `Closed` event. **Accept is two-step**: `Accept()` clears the error and calls the abstract `TryAccept()`; `false` leaves the panel open with `ErrorMessage` set to the spec's verbatim wording, `true` applies the effect, sets `WasAccepted` and closes. A closed panel ignores both commands, so a late event can never re-fire an effect.

`EditorOverlayHost` (a collaborator of `KeyboardEditorViewModel`, **not** a view model — the editor projects `Active` as its own bindable `ActiveOverlay`) owns the swap and the capture that goes with it:

| Call | Effect |
|---|---|
| `Show(overlay)` | Ends any nesting, tears the previous panel down, makes this one `Active` |
| `ShowNested(child, parent)` | Same, then re-shows `parent` when `child` raises `Closed` — accepted or cancelled alike. Only §11.1's two Search buttons use it |
| `Dismiss()` | Cancels the open panel (its `Closed` path runs); clears by hand only if the panel had already closed itself |
| `Close()` | Permanent shutdown: no nesting restored, the open panel is *cancelled* (so its own teardown hooks run), capture left un-suspended. Idempotent |

Capture follows the panel's kind, and the host is the only thing that turns it on for a panel:

- **A panel implementing `IKeystrokeSink`** (Tap and Hold only) exposes `WantsKeystrokes`; the host subscribes to its `PropertyChanged` and calls `Start()`/`Stop()` as that flag moves — so the app keeps its keyboard while the panel merely sits there, and capture runs only while a field is armed.
- **It never stops a capture it did not start.** `SyncSinkCapture` skips `Start()` when `IKeystrokeCaptureService.IsCapturing` is already true and only calls `Stop()` when its own `_isCaptureStarted` flag is set — stopping a capture someone else owns would silently deafen them.
- **The editor hands the service over before any panel opens.** `KeyboardEditorViewModel.ShowOverlay` cancels a listening key *and stops a macro recording* first ([keyboard-editor.md](keyboard-editor.md)). Without that, a recording underneath keeps the service running, the host's start/stop bookkeeping never engages, and every key aimed at the panel — Escape included — is appended to the macro instead.
- **An open sink panel takes the keystroke whether or not it is armed.** Spec 10 routes a captured key to the Tap and Hold dialog "if that dialog is open", so an unarmed panel swallows it and drops it rather than letting whatever is underneath consume keys meant for a modal panel.
- **Every other panel is a text-entry panel** (Macro Timing Delays, Search Keys, Export) and the host calls `Suspend()` for as long as it is open, `Resume()` when it closes — spec 10's "capture is suspended whenever a dialog that needs real typing is open". The Avalonia adapter's auto-suspend on `TextBox` focus is a platform judgement; this is the deterministic guarantee.

## Firmware gating — `FirmwareFeatureGate`

One refusal path for every gated feature (09 §2, 11 §11.1). `EnsureAvailableAsync(deviceId, feature, firmware, title, fallbackMessage, notifications, urlLauncher)`:

- `FirmwareGateService.IsAvailable` passes → `true`, nothing shown (demo mode always passes, 09 §2).
- Otherwise the message is `FirmwareGateCatalog.Find(deviceId, feature)?.Message ?? fallbackMessage`; an **empty** result shows nothing and simply refuses.
- The box is `MessageBoxIcon.Warning` + `Ok`, plus a custom button `Update Firmware` (id `update-firmware`) **only when `FirmwareSupportUrls.FindUrl(deviceId)` has a page**; pressing it opens that page through `IUrlLauncher`.

It lives beside the panels rather than in `Services/` because it is presentation: Core carries the gate rows, their messages and the support URLs as data and deliberately shows no dialog ([firmware.md](firmware.md)). Each gated feature exposes a one-line static wrapper (`TapAndHoldOverlayViewModel.EnsureFirmwareAvailableAsync`, `MacroDelayOverlayViewModel.EnsureFirmwareAvailableAsync`) so a caller never maps a feature onto a `FirmwareFeature` itself. The fallback messages exist because 09 §2 quotes the refusal only under the Freestyle rows while §11.1 describes one for every gated app — tests pin each fallback identical to the row that does store it.

Gated features reaching users today: `TapAndHold` (Adv2 1.0.516 / FS 1.0.480 / RGB 1.0.1; TKO and Adv360 ungated), `CustomMacroDelays` (FS Edge/Pro 1.0.340), and `ExpandedMacroCount`, which the macro panel reads without a dialog ([keyboard-editor.md](keyboard-editor.md)).

## §11.1 — Assign Tap and Hold Action

Core answers *whether it may open*; the panel answers *what it assigns*.

**`TapAndHoldPrecheck.Evaluate(layout, layer, key)`** returns the **first** matching `TapAndHoldRefusal` in the spec's order, and `MessageFor(refusal)` its verbatim text:

| Order | `TapAndHoldRefusal` | Condition | Message |
|---|---|---|---|
| 1 | `SameKeyInBothLayers` | The same key **index** carries a tap-and-hold on any other layer (05 §7.4) | `You cannot assign a Tap and Hold Action to the same key in both layers.` |
| 2 | `MaximumReached` | `TapAndHoldCount` **minus the key under edit** `>= TapAndHoldCapability.MaxPerLayout` (10); a device stating no maximum can never reach one | `You have reached the maximum number of Tap and Hold actions for this Profile.` |
| 3 | `MacroTriggerKey` | `key.IsMacro`, or `layout.FindMacros(layer.Index, key.TriggerKey.Code)` is non-empty (both macro stores of 06 §1) | `You cannot assign a Tap and Hold Action to a macro trigger key.` |
| 4 | `AlphanumericOnTopLayer` | `layer.Index == 0` and the key's **factory default** is in `KeyTable.LettersAndDigits` (05 §3.1) — the physical key, not whatever a remap put there | `You cannot assign a Tap and Hold Action to these keys (A-Z, 0-9) on the Top Layer.` |

**Check 2 excludes the key being edited — a deliberate reading.** §11.1 caps how many tap-and-hold actions a profile *has*; re-opening the dialog on a key that already carries one rewrites that assignment, it cannot produce an eleventh. Counting it would mean that once a profile holds its ten, none of those ten could ever be edited again, only deleted and re-made. A key carrying nothing yet really would be the eleventh and is still refused. The literal alternative is recorded here because it is the one place this module departs from a word-for-word reading of the spec.

`TapAndHoldOverlayViewModel.TryCreate(layout, layer, key)` runs them and answers a `TapAndHoldOpenResult` — exactly one of `Overlay` / `RefusalMessage` — so the caller shows a message box or a panel and never restates a rule. Whether the *device* supports tap-and-hold at all (`TapAndHoldCapability.IsSupported`) is a separate question, and **the caller asks it first**: `KeyboardEditorViewModel`'s `TapAndHoldCommand` is disabled unless `Layout.Device.TapAndHold.IsSupported`. Such a device also states no delay range, so the panel would open at 0 ms and the assignment it wrote would be reported as `TapAndHoldNotSupported` — blocking the *whole* save, not just that key. No shipped device reaches it today (SE2, CROSSFIRE and the Adv360 Pro have no keyboard picture), which is why the guard is covered against a hand-built definition rather than a catalog one.

The panel itself: `TapAction`/`HoldAction` (`KeyDefinition?`, rendered through the shared `KeyCaption` rule), `DelayMilliseconds`, `ArmedField` (`TapAndHoldField.None`/`Tap`/`Hold`) and `WantsKeystrokes` = "not closed and a field is armed". Arming a field is a command the view runs (`ArmTapActionCommand`/`ArmHoldActionCommand`, the *Press Key* buttons) — the next captured keystroke fills that field and disarms it. `SearchTapActionCommand`/`SearchHoldActionCommand` build a `SearchKeysOverlayViewModel` already titled for the field, wire its `Selected` back into the field, disarm capture, and raise `SearchRequested` — the editor only nests it (`EditorOverlayHost.ShowNested`). The fields open on the key's current `TapAction`/`HoldAction`, and the delay on its `TimingDelay` when the key already carries a tap-and-hold, otherwise on `TapAndHoldCapability.DefaultDelayMilliseconds` (250 ms; **150** on the Adv360) — never on a literal. The Up/Down commands clamp to the device's `DelayMilliseconds` range, while direct assignment is deliberately **unclamped** so an out-of-range value survives to be reported. `TryAccept` validates in §11.1's order — delay, tap, hold — then calls `KeyboardKey.SetTapAndHold(tap, hold, delay)`, which clears the position's remap and multi-modifier ([keyboard-model.md](keyboard-model.md), "One rule per position"). **The model has the last word**: that method refuses a position that can never be remapped (05 §5.3) and its `false` is the panel's — the panel stays open with `This key position cannot be programmed.` rather than closing as accepted with nothing written.

The Advantage360's `Macro` buttons (§11.1, kbd ≥ 1.0.69, `FirmwareFeature.TapAndHoldMacroActions`) are deliberately absent — that board has no editor yet (issue [#41](https://github.com/migus88/kinesis-edit/issues/41)).

## §11.3 — Macro Timing Delays

`MacroDelayTokens` (Core) owns the tokens: `RandomToken` = `dran`, `MinDelayMilliseconds` = **1**, `MaxDelayMilliseconds` = **999**, `BuildCustomToken(ms)` = `d` + the millisecond count zero-padded to three digits (`d050`), throwing outside 1–999. **Resolution goes through the token, never the code**: `dran` and the generated `d002` share code 10087 and `KeyRegistry.FindByCode` answers `dran` by first match (05 §7), so a code lookup would silently turn a 2 ms delay into a random one. `ResolveCustom(125|500, dialect)` lands on the explicit legacy rows 10007/10008, which shadow their generated twins (05 §3.12); the token written is `d125`/`d500` either way, and only the RGB/TKO parser reads them back as the generated codes (06 §2.2) — a code-identity difference the file cannot show.

The panel exposes `IsRandomDelay` and `CustomDelayMilliseconds` (0 = empty; **assigning any value un-checks the random radio**), Up/Down commands clamped to 1–999, and raises `Accepted` with the resolved `KeyDefinition`. **Neither control is preselected**, so an untouched panel fails validation — §11.3's outcome is "random, 1..999, or cancel/invalid". A value outside the range *and* a dialect that does not name the token are indistinguishable and report the same message.

## §11.6 — Search Keys

`KeySearchCatalog.Build(dialect)` walks `KeyRegistry.Entries` in registration order (05 §3, §7) and drops two kinds of row: entries flagged `KeyDefinitionFlags.HiddenFromSearch` (the legacy `SKIP_SEARCH` sentinel — the §3.12 speed/delay pseudo-keys, the §3.13 edge zones, the `Fn` action) and entries the dialect does not name, which can never be written to that device's files. `TokenDialect.None` lists everything under the first token that names it (Legacy → Gen1 → Gen2), mirroring `KeyRegistry.FindByToken`'s all-dialect lookup. **§11.6's "numpad duplicates and hotkeys" are *not* skipped, and that is the key table's answer, not an oversight here.** Spec 05 is the authoritative transcription of the legacy table including its `SKIP_SEARCH` sentinel, and it marks exactly three groups hidden from search: the §3.12 speed/delay pseudo-keys ("All are hidden from key search"), the §3.13 edge zones ("All hidden from search"), and the `Fn` action of §3.9 ("Hidden from key search"). The §3.6 keypad-layer duplicates and the §3.11 `hk0`..`hk10` rows carry no such marking, so `KeyRegistry` faithfully leaves them searchable and they are listed. §11.6's prose ("numpad duplicates, delay tokens, hotkeys") is a loose description of the sentinel rather than a fourth source of truth; resolving the conflict the other way would be a key-table change (`KeySearchCatalog` is the only consumer of `HiddenFromSearch`), and the transcription is what we follow.

`KeySearchEntry` = `Definition`, `SearchName`, `FileToken`, `DisplayText`. The item text is composed as *search name* + (display text when it differs) + `' (' + token + ')'` (when the display text differs from the token); a caption is flattened to one line (`\n` is a two-line cap, 05 §1.1) and falls back to the file token when blank, so the nine unlabelled Freestyle hotkeys are not a column of identical rows. `Filter(entries, query)` matches `DisplayText` **or** `FileToken`, case-insensitively — "by either name or file token" — and a blank query returns the input list unchanged.

The view model owns only the query, the selection and the validation: `Query` re-filters on every change and **drops a selection the filter hid**, so Ok can never accept an invisible row; `TryAccept` refuses an empty selection with `You must select a key` and otherwise raises `Selected`. `ChooseCommand` is select-and-accept in one step — the view binds it to the double-tap gesture (§11.6), so the view model never learns what a double click is. Four titles, one per call site: `Search Keys`, `Search Keys (Macro)`, `Search Keys (Tap Action)`, `Search Keys (Hold Action)`.

## §11.5 — Export files

`ProfileExportPlanner.Plan(session, selection)` (Core) returns the files to write, **layout first**, serialized exactly as a save to the v-Drive would write them — kept invalid lines included (04 §4.3, §5.2) — under the current profile's base names from `ProfileFileNames` (`layout<n>.txt`, `led<n>.txt`). A selection that includes lighting on a device or profile with no led file simply yields no led file; §11.5 gives no error for it. It takes the concrete `ProfileSession` because Core's three lighting models share no base type, which is also why `PlanExport` is a member of the app-side `IProfileSession` seam rather than a call the app makes on `Lighting` ([profiles.md](profiles.md)).

`ExportOverlayViewModel` owns the rest. The three choices are **mutually exclusive checkboxes** (`ProfileExportSelection.LayoutAndLighting` is the default; un-checking the live one is ignored, so exactly one is always selected). Its Ok is a separate `ExportCommand` (`IAsyncRelayCommand`) because §11.5's accept is asynchronous — it opens the directory picker — while `TryAccept` is not: the base accept stays inert until the export really wrote every file. Flow: pick a directory (`IFolderPickerService`, titled `Export files`; a cancel **or** a platform failure is silently no outcome) → `PlanExport` → `IVDriveFileService.WriteAllLines(path, lines, allowCreate: true)` per file, the same Latin1 whole-file writer the drive uses (03 §5.2), so an exported file is byte-identical to what the drive would hold → on a throw, the panel stays open and shows the failure; on success it closes and shows `Files exported successfully!`. Which failure prefix a file gets is decided by its **position** in the plan (layout is index 0 unless the selection is lighting-only) — the base names are Core-internal. `CanExport` is false with no session, i.e. in demo mode, which read no profile at all (03 §3.5).

## Import (spec 10, 07 §1.4) — no dialog of its own

Import is a plain editor command; everything between the picker and the model is `ProfileImporter`:

1. `IFilePickerService.PickTextFileAsync("Import File")` → a `PickedFile` (`Name`, `Path`, `ByteLength`, `Lines`, `IsTruncated`) or null for a cancel. A throw from the picker becomes a failure message, never an exception.
2. **Size check on the true `ByteLength`**, against `ImportClassifier.MaxImportBytes` = **51200** (50 KB, 07 §1.4). `PickedFileReader` caps what it *buffers* at `MaxReadBytes` (20 × 50 KB) but always reports the file's real length, precisely so this check can refuse a huge pick before its content matters.
3. `ImportClassifier.Classify(deviceId, lines)` → `ImportedFileKind`.
4. `IProfileSession.Import(kind, lines)` — the same parse path a load runs ([profiles.md](profiles.md)).
5. `ProfileImportOutcome`: `Cancelled` (silent), `Failed(message)` (message box), or `Applied(kind, message)` (toast + the editor re-renders).

**The classifier branches on the device's lighting hardware** (`DeviceDefinition.Lighting.Kind`), not on a device list:

| `LightingKind` | Rule (07 §1.4) |
|---|---|
| `IndicatorLeds` (Adv360) | Lighting iff the first non-blank line contains `[ind` |
| `PerKeyRgb` (Edge RGB, TKO) | Lighting iff the first non-blank line starts with `[` **and** contains `>`, **and** the file carries a known mode token (`[<token>]` from `LightingModeCatalog`, so it can never drift from the lighting engine) **or** a value part with two or more `[` (a colour style) |
| anything else | Always `Layout` — the device has no led file to import into |

An empty file, and anything the heuristic rejects, is tried as a layout. **Known quirk, faithfully reproduced:** a layout file whose first line is a tap-and-hold rule (`[caps]>[a][t&h250][lctrl]`) satisfies the colour-style test and is classified as lighting — that is the legacy heuristic as specified, not an oversight.

`PickedFileReader` decodes exactly like `VDriveFileService.ReadAllLines` ([vdrive.md](vdrive.md)): raw bytes as `Encoding.Latin1` (never UTF-8, no BOM stripping), split on CRLF/LF/lone CR with no empty trailing line — so an imported file parses identically to the same file read off the drive. Core's splitter is private, so it is duplicated here and `PickedFileReaderTests` pins the two against each other over the same bytes.

## Spec strings and deliberate deviations

Verbatim from spec 11, pinned as consts and asserted by tests:

| Panel | Strings |
|---|---|
| §11.1 | Title `Assign Tap and Hold Action`; labels `Tap Action` / `Hold Action` / `Delay (1-999ms)` and their three hints; `Search for tokens`; the note `Tap action is not sent until key is released.`; `Please select a timing delay between 1ms and 999ms.`, `Please select a Tap Action`, `Please select a Hold Action`; the four refusals in the table above; the firmware refusal `To utilize Tap and Hold Actions, please download and install the latest firmware.` |
| §11.3 | Title `Macro Timing Delays`; `Random Delay (1-150ms)`, `Custom Delay (1-999ms)`; `Please select a timing delay between 1ms and 999ms. To achieve a longer delay, insert multiple delays back-to-back.`; the firmware refusal `To utilize custom or random delays, please download and install the latest firmware.` |
| §11.6 | The four titles; `Search key`; `You must select a key` |
| §11.5 | Title `Export files`; `Layout and Lighting` / `Layout only` / `Lighting only`; `Error exporting layout file: `, `Error exporting lighting file: `, `Files exported successfully!` |
| Macro panel | `You cannot assign a macro to a modifier key` (spec 02) and `Macros are limited to approximately {0} characters.` (06 §6) — see [keyboard-editor.md](keyboard-editor.md) |

App-chosen wording (the specs quote none): the `Update Firmware` button caption; §11.1's `This key position cannot be programmed.` (the legacy apps never offered the dialog on such a key, so no wording exists to quote); every import string — `Import File`, `'{0}' is {1} KB. An imported file may be at most {2} KB.` (whole kilobytes, rounded **up**, so an over-limit file never reads as exactly the limit), `The file could not be imported: `, `Imported '{0}' as this profile's layout.` / `…as this profile's lighting.`; and the panels' button captions (`Press Key`, `Search`, `Ok`, `Cancel`).

Recorded deviations:

1. **Inline panels, not modal windows.** See the header. The scrim (`Border.overlayScrim`, translucent black in both theme variants) is what makes them read as modal — it dims the editor and, being hit-test visible, swallows clicks aimed at it.
2. **The Tap and Hold action fields are not text boxes.** They are read-only `Border.actionField` displays with an `armed` state class; focus inside a `TextBox` auto-suspends capture, which is precisely what those two fields must not do.
3. **The delay fields validate on Ok, clamp only on the arrows.** §11.1/§11.3 describe spin controls; an out-of-range value typed in has to survive long enough to produce the spec's message.
4. **Export failures are identified by plan position**, not by file name, because `ProfileFileNames` is Core-internal.
5. **No `+100` modal-result encoding** anywhere — the shell's structured `MessageBoxOutcome` carries the custom-button id ([app-shell.md](app-shell.md)).

## Load-bearing invariants

1. **Rules in Core, wording next to the panel it belongs to.** The pre-dialog checks, delay tokens, search catalog, import heuristic and export plan are UI-free and unit-tested; the view models own only inputs, exclusivity and the message they show.
2. **One capture owner at a time.** The host starts capture only for an armed sink and stops only what it started; the editor cancels a listening key **and stops any macro recording** before any panel opens, and routes every captured keystroke to an open sink panel ahead of everything else ([keyboard-editor.md](keyboard-editor.md), "Keystroke routing").
3. **A panel that fails validation stays open.** `TryAccept` returning false must set `ErrorMessage`; only a successful accept or a cancel raises `Closed`, and the editor's teardown hangs off `Closed`.
4. **Nesting is one level and only for §11.1.** `Show` ends any nesting in progress, so a picker left over from an abandoned parent can never restore it.
5. **Import is unsaved edit state.** Nothing is written; the session's `IsDirty` baseline stays the one captured at load, and the profile-0 read-only guard applies to `Import` exactly as it does to `Save`.
6. **Size is checked on the file's true length**, never on what was read — `PickedFile.IsTruncated` exists so a capped read cannot look like a small file.
7. **Firmware refusals are the gate's own words.** A feature supplies a fallback only where 09 §2 stores no message, and the fallback is pinned identical to the row that does.

## Testing

All view-model level, no Avalonia runtime (`KinesisEdit.Tests`): `EditorOverlayHostTests` (swap, nesting, the capture start/stop/suspend matrix incl. "does not stop a capture it did not start"), `TapAndHoldOverlayViewModelTests`, `MacroDelayOverlayViewModelTests`, `SearchKeysOverlayViewModelTests`, `ExportOverlayViewModelTests`, `ProfileImporterTests`, `PickedFileReaderTests`/`PickedFileTests`, and `KeyboardEditorViewModelFeatureTests`/`…RoutingTests` for the editor's side of it — including the device-support guard (over `TestLayouts.CreateLayoutWithoutTapAndHold`, an RGB board on a definition with `TapAndHoldCapability.None`), the recording stopped by `ShowOverlay`, an unarmed sink still taking the keystroke, and `IsOverlayAwaitingKeystroke`. Fakes: `FakeFolderPickerService`, `FakeFilePickerService`, plus the existing `FakeProfileSession` (now recording `Import`/`PlanExport`), `FakeKeystrokeCaptureService`, `FakeNotificationService`, `FakeUrlLauncher`, `FakeVDriveFileService`. Core's side is covered by `KinesisEdit.Core.Tests/Model/{MacroDelayTokens,TapAndHoldPrecheck}Tests`, `Keys/KeySearchCatalogTests`, `Transfer/{ImportClassifier,ProfileExportPlanner}Tests` and `Profiles/ProfileSessionImportTests`.

The two Avalonia pickers are covered only for their ownerless path (no window → a cancel) and their null-argument guard; the platform dialog itself cannot be driven from a unit test, which is why every rule lives in `PickedFileReader` and the callers instead. The four panel views, the scrim and the style classes need a UI runtime and are hand-verified (`dotnet run --project src/KinesisEdit`).

## Deliberately not here

- **No Multimodifier dialog (§11.2)** and **no Select Macro dialog (§11.4)** — both are Advantage 360 surfaces (§11.2 is titled "Advantage360 only"; §11.4 picks from that board's flat macro list), and the Adv360 has no keyboard picture yet. Deferred with it to issue [#41](https://github.com/migus88/kinesis-edit/issues/41); `KeyboardKey.TrySetMultiModifiers` and `MultiModifierCodes` already exist in Core.
- **No Diagnostics report (§11.7) and no Troubleshoot dialog (§11.8) as editor surfaces** — issue [#46](https://github.com/migus88/kinesis-edit/issues/46). §11.8's content is already rendered inline by the dashboard's empty state ([app-shell.md](app-shell.md)).
- **No Search Keys assignment onto a key.** §11.6's picker fills a Tap/Hold field or inserts into a macro; assigning a key on the board is still a captured physical keypress only ([keyboard-editor.md](keyboard-editor.md)).
- **No categorized Special Actions popups** (spec 10's right-click menus of F13–F24, keypad, multimedia, mouse, alt layouts, Hyper/Meh) — the flat searchable list is the one picker.
- **No export of anything but the current profile's two files** — no settings file, no all-profiles backup, no free-named save (spec 10's FS Edge/Pro backup file).
- **No import preview or merge.** An imported file replaces the layout **or** the lighting wholesale, exactly as a load does; there is no undo beyond not saving.
