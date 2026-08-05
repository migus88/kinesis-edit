# Keyboard editor (keyboard picture + remap and macro editing)

The editor the shell swaps in on Configure for a programmable keyboard: a keyboard-shaped picture of one loaded profile, the click-then-press remap workflow of spec 10 ("click an on-screen key — the key enters 'listening' state; the next physical keypress captured by the app becomes the new assignment"), the macro panel behind the *Macros* tab, and the reset/save/export/import actions around them. It owns **no** domain rules — placements come from `VisualCatalog` ([domain-data.md](domain-data.md)), the model and its edit paths from `KinesisEdit.Core.Model` ([keyboard-model.md](keyboard-model.md)), load/save/import from `ProfileSession` ([profiles.md](profiles.md)), keystrokes from `IKeystrokeCaptureService` ([keystroke-capture.md](keystroke-capture.md)), the spec 11 panels it hosts from [feature-dialogs.md](feature-dialogs.md), and window/navigation/notifications from the shell ([app-shell.md](app-shell.md)).

**Device coverage: Freestyle Edge RGB only.** Every other device opens `EditorPlaceholderViewModel` — except the Savant Elite2, which has an editor of its own ([savant-elite.md](savant-elite.md)) and no keyboard picture at all — because only that one board's picture is authored. Adding a device is adding data to `VisualCatalog` — issues [#39](https://github.com/migus88/kinesis-edit/issues/39) (FS Edge / FS Pro), [#40](https://github.com/migus88/kinesis-edit/issues/40) (TKO), [#41](https://github.com/migus88/kinesis-edit/issues/41) (Advantage 360), [#42](https://github.com/migus88/kinesis-edit/issues/42) (Advantage2) — never editing anything below.

| Namespace | Entry point(s) | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Geometry.Visual` | `VisualCatalog`, `KeyboardVisual`, `KeyVisual`, `KeyCluster` | Where each key position sits, in key units (UI-free data) | 05 §4; 02 |
| `KinesisEdit.Controls` | `KeyboardPanel` | The only arithmetic: key-unit rectangles → arranged, scaled, centred children | — |
| `KinesisEdit.Controls` | `KeyboardView`, `KeyCapView` | The device-agnostic picture of one layer; one key cap | 10 "Remap workflow" |
| `KinesisEdit.Views` | `KeyboardEditorView` | Header, layer switch, tab strip, feature buttons, listening banner, macro panel, invalid-line block, Save, the overlay scrim | 10; 11 |
| `KinesisEdit.ViewModels` | `KeyboardEditorViewModel` | The editor: load, selection/listening state machine, keystroke routing, resets, save, the five feature commands | 10; 11; 04 §2.1, §5.2, §5.3; 03 §3.5, §5.3 |
| `KinesisEdit.ViewModels` | `KeyboardLayerViewModel`, `KeyboardKeyViewModel` | One layer's caps; one cap over one `KeyboardKey` | 05 §1.3, §5.3, §7.4 |
| `KinesisEdit.ViewModels` | `MacroPanelViewModel` (+ `MacroEditTarget`, `MacroClipboard`, `MacroStepListViewModel`, `MacroStepViewModel`, `MacroSlotViewModel`, `MacroCoTriggerViewModel`, `MacroBudgetViewModel`, `MacroListEntryViewModel`) | The macro editor: record, slots, steps, co-triggers, speed/repeat, budgets, the profile's macro list — `MacroEditTarget` is *which* macro is open (macro + key + layer + slot), `MacroClipboard` the snapshotting Copy/Paste buffer | 10 "macro editor"; 06 §1, §3–§6 |
| `KinesisEdit.ViewModels` | `EditorOverlayHost`, `EditorOverlayViewModel`, `IKeystrokeSink` | Hosting the spec 11 panels inline and routing keystrokes to one sink ([feature-dialogs.md](feature-dialogs.md)) | 11; 10 "Routing" |
| `KinesisEdit.ViewModels` | `DeviceEditorViewModel`, `EditorTab`, `EditorTabViewModel` | What the shell needs from *any* editor ([app-shell.md](app-shell.md)); the section strip | 10 |
| `KinesisEdit.ViewModels` | `KeyCaption`, `LayerCaptions`, `KeyColorOverlay` | The three presentation rules, each testable on its own | 05 §3, §1.1; 10; 07 §4 |
| `KinesisEdit.Services` | `IProfileSession`, `IProfileSessionFactory`, `ProfileSessionAdapter`, `ProfileSessionFactory` | The fakeable seam over Core's sealed `ProfileSession` (`Save`, `Import`, `PlanExport`) | 03 §4.1, §5.3; 11 §11.5 |
| `KinesisEdit.Services` | `ProfileImporter`, `IFolderPickerService`, `IFilePickerService` | The import flow and the two native pickers ([feature-dialogs.md](feature-dialogs.md)) | 10 "Import"; 07 §1.4; 11 §11.5 |
| `KinesisEdit.Services` | `IEditorViewModelFactory`, `EditorViewModelFactory` | Which editor a device opens into | 10 "Opening a device" |
| `KinesisEdit.Converters` | `HexColorToBrushConverter` | `#RRGGBB` → brush, in the view | 07 §2.1 |

## The chain: visual data → control → view model

1. **`VisualCatalog`** (Core) answers *where* — `KeyVisual { Index, X, Y, Width, Height, Cluster }` in **key units** (`1.0` = one 1U cap), board-absolute, top-left origin. One `KeyboardVisual` serves **all** of a device's layers (05 §7.4: the same position keeps its index in every layer).
2. **`KeyboardLayerViewModel.BuildAll(layout, visual, lighting)`** joins the two **by key index**: for every `KeyboardKey` of every `KeyboardLayer` it looks up `KeyboardVisual.TryGetKey(key.Index, …)` and produces a `KeyboardKeyViewModel`. A key with no placement is **skipped**, a placement with no key produces no cap — Core's tests already assert the two index sets match exactly for every authored device, so a mismatch must degrade into a missing cap, never a crashed editor.
3. **`KeyboardView`** renders `KeyboardLayerViewModel.Keys` as an `ItemsControl` whose `ItemsPanel` is a `KeyboardPanel` (fed `BoardWidth`/`BoardHeight`) and whose `ItemTemplate` is a `KeyCapView`. An `ItemsControl.Styles` entry projects each item's `X`/`Y`/`Width`/`Height` onto the generated `ContentPresenter`'s attached `KeyboardPanel.UnitX`…`UnitHeight`.
4. **`KeyCapView`** is one `Button` carrying the caption, the state classes and the colour strip.

Core's model raises no change notification (plain mutable POCOs), so `KeyboardKeyViewModel.RefreshFromModel()` — re-reading `Caption` and `IsModified` — is what the UI learns from; **every path that writes to `KeyboardKey` must end in it**, or the cap keeps showing the old assignment. `KeyboardLayerViewModel.RefreshFromModel()` is the layer-wide form.

## The generic component

`KeyboardView` is device-agnostic on purpose: it binds only to a `KeyboardLayerViewModel` and exposes a single `KeySelectedCommand` styled property that `KeyCapView` invokes through `$parent[KeyboardView]` with the clicked `KeyboardKeyViewModel` as the parameter. Neither the picture nor the cap knows the editor exists; `KeyboardEditorView` supplies `SelectKeyCommand`. **What a new device must supply is a `KeyboardVisual` and nothing else** — no XAML, no control, no view-model subclass. `EditorViewModelFactory` then resolves it automatically (it requires *both* `VisualCatalog` and `GeometryCatalog` to answer).

`KeyboardPanel` is a `Panel` subclass and the only piece doing arithmetic:

- Placement travels on **attached** `UnitX`/`UnitY`/`UnitWidth`/`UnitHeight` doubles, not off each child's `DataContext`, so the panel works with any item type. They are named `Unit*` because a static `WidthProperty`/`HeightProperty` declared here would hide `Layoutable`'s — a warning and a genuine XAML ambiguity.
- `BoardWidth`/`BoardHeight` are styled properties (`AffectsMeasure`); the four attached ones are `AffectsParentArrange`.
- `MeasureOverride` reports the board at the scale the offered space allows; `ArrangeOverride` scales **uniformly** (`min` of the two ratios), **centres** the result, and shrinks every cap by `KeyGapUnits` (**0.06** U) so neighbours never touch. A dimension that is infinite/NaN/non-positive contributes nothing, and a board that nothing constrains falls back to `NaturalUnitSize` (**44** px per unit). A board with no size still arranges every child to `default` — an unarranged child keeps stale bounds and would be painted over the empty picture.

`KeyCapView` sets the state classes `selected` / `listening` / `modified` / `locked` (`locked` = `!CanEdit`) plus a colour strip visible when `HasColorOverlay`. All chrome lives in `App.axaml`'s `keyCap*` / `layerTab` / `editorTab` / `keyboardBoard` styles, in both theme variants, applied to the button's `PART_ContentPresenter` (the Fluent theme's own `:pointerover`/`:disabled` setters target that presenter and would otherwise win). The cap states are declared in increasing precedence, so a listening key always reads as listening.

## `KeyboardEditorViewModel`

`DeviceEditorViewModel` — the shell's abstract editor base, shared with `EditorPlaceholderViewModel` and `SavantElitePedalViewModel` ([savant-elite.md](savant-elite.md)) — supplies `Device`, `DeviceName`, `IsDemoMode` and the virtual `LoadAsync()`; `MainWindowViewModel.Editor` is typed as that base so navigation is independent of which editor a device resolves to. This class overrides `LoadAsync`.

State: `IsLoading` (true until the first load finishes), `IsBusy` (a save is in flight), `Layout`, `Layers`, `SelectedLayer`, `SelectedKey`, `ListeningKey`/`IsListening`, `ProfileCaption`, `ModifiedKeyCount`/`RemapCounterCaption`, `MacroCount`/`MacroCounterCaption`, `InvalidLineMessages`/`HasInvalidLines`, `BoardWidth`/`BoardHeight`, `Tabs`/`SelectedTab`, `MacroPanel`/`IsMacroPanelVisible`, `ActiveOverlay`/`HasActiveOverlay`.

Commands and what gates them (`NotifyCommands()` re-evaluates all of them on every state move):

| Command | Enabled when |
|---|---|
| `SelectKeyCommand(KeyboardKeyViewModel?)` | always (the click contract below) |
| `SelectLayerCommand(KeyboardLayerViewModel)` | always |
| `SelectTabCommand(EditorTabViewModel)` | `tab.IsEnabled` — `EditorTab.Keys` and `EditorTab.Macros` today |
| `BeginRemapCommand` | `SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy && !MacroPanel.IsRecording && ActiveOverlay is null` |
| `CancelRemapCommand` | `IsListening` |
| `ResetKeyCommand` | `SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy` |
| `ResetLayerCommand` | `SelectedLayer is not null && !IsLoading && !IsBusy` |
| `ResetLayoutCommand` | `Layout is not null && !IsLoading && !IsBusy` |
| `SaveCommand` (async) | `_session is not null && _session.CanSave && !IsDemoMode && !IsLoading && !IsBusy` |
| `TapAndHoldCommand` (async) | `Layout`, `SelectedLayer`, `SelectedKey` all set + **`Layout.Device.TapAndHold.IsSupported`** + `SelectedKey.CanEdit` + `!IsLoading && !IsBusy && ActiveOverlay is null` |
| `InsertDelayCommand` (async), `InsertSpecialActionCommand` | `Layout is not null && IsMacroPanelVisible && MacroPanel.EditedMacro is not null && !IsLoading && !IsBusy && ActiveOverlay is null` — the **`IsMacroPanelVisible`** half is new, see below |
| `ExportCommand` | `_session is not null && !IsDemoMode && !IsLoading && !IsBusy && ActiveOverlay is null` |
| `ImportCommand` (async) | `_session is { CanSave: true } && !IsDemoMode && !IsLoading && !IsBusy && ActiveOverlay is null` — `CanSave` is what carries the Adv360 profile-0 guard |
| `CloseOverlayCommand` | `ActiveOverlay is not null` |

The three reset commands share the save guard (`!IsLoading && !IsBusy`) because a save serializes the model on a background thread and mutating it mid-save would race it. **Every feature command additionally requires no open panel** — an inline panel is modal and must not be raced by the editor underneath it.

Two of those gates are less obvious than they look:

- **Tap and Hold asks whether the *device* has the feature** before the firmware gate and before §11.1's four pre-dialog checks. A board without it also states no delay range, so the panel would open at 0 ms and the assignment would be reported as `TapAndHoldNotSupported` — which blocks the entire save, not just that key ([feature-dialogs.md](feature-dialogs.md)).
- **The two insertion commands need the macro panel on screen**, not merely a macro open. Selecting any macro-capable key opens an unassigned draft, so on the Keys tab the picked delay or action would be appended to a macro the user cannot see and never assigns. `SelectTab` therefore re-evaluates the commands.

`SelectedTab` is a two-way-bindable property whose setter runs the same guard as the command, so a binding cannot open a tab the command refuses. Leaving the Macros tab stops any macro recording. Disabled tabs (`Lighting`, `Settings`) are shown, not hidden, so the editor's shape does not change when #16 fills them in.

### Lifecycle — `LoadAsync`, `Dispose`

**Construction is deliberately cheap**: it resolves the device's `KeyboardVisual`, builds the tab strip and commands, and subscribes to `IKeystrokeCaptureService.KeystrokeCaptured`. No file is touched, so the shell can swap the view in immediately — `MainWindowViewModel.OpenDevice` fires `_ = editor.LoadAsync()` **after** the view swap and after hiding its own loading splash; the drive read runs against the editor's own `IsLoading`.

`LoadAsync` is **total and idempotent** (`_hasLoadStarted` / `_isDisposed` guards; a second call is a no-op), which is what makes forgetting the task safe:

- The read runs off the UI thread (`Task.Run`, resumed with `ConfigureAwait(true)`).
- **Demo mode, or a device with no `Location`** → `KeyboardLayout.Create(Device.DeviceId)` in memory, no session: `ProfileCaption` stays empty and Save is permanently unavailable (03 §3.5 — demo mode never touches the drive).
- **Otherwise** → `IProfileSessionFactory.Load(Device.Location, Device.DeviceId, Device.Device.LayoutScheme.FirstProfileNumber)`, i.e. profile 1 on every numbered-profile device.
- **Any failure** (vanished drive, unreadable file, unsupported device) degrades to the same in-memory layout plus a `LoadFailureTitle` message box — the editor never crashes the shell. If even `KeyboardLayout.Create` throws (a device with no geometry), `Layout` stays null and the picture is empty.
- `Apply` then sets `Layout`, `ProfileCaption`, `InvalidLineMessages`, builds `Layers`, **rebuilds the macro panel** (`AttachMacroPanel`, which detaches the old one first and subscribes to `RecordingChanged`/`MacrosChanged`/`PropertyChanged`), selects layer 0, and refreshes both counters. A successful **import** re-runs the very same `Apply` over the session, because the imported file built a brand-new model exactly as a load would have.

`Dispose` (called by `MainWindowViewModel.CloseEditor` on navigate-home and on re-open, and by `App`'s exit path through the shell) detaches from `KeystrokeCaptured`, drops the Tap and Hold hooks, **closes the overlay host**, detaches the macro panel (stopping any recording) and stops capture. It is idempotent. **Leaving capture started would swallow every keystroke of the dashboard behind the closed editor** — the capture service is app-wide and outlives the editor. The host *cancels* the open panel rather than dropping it, because the one-shot insertion hooks come off on the panel's own `Closed`; the Tap and Hold hooks are dropped **before** that, which is why a half-finished assignment cannot write back into a disposed editor.

### Remap state machine

One key listens at a time; capture is started **only** on entering listening and always stopped again on the way out.

| State | Input | Next state | Side effect |
|---|---|---|---|
| nothing selected | click key *K* | *K* selected | — |
| *K* selected | click *K* | *K* listening (only if `CanBeginRemap`) | `capture.Start()` |
| *K* selected | click *L* | *L* selected | — |
| *K* listening | click *K* | *K* selected | `capture.Stop()` |
| *K* listening | click *L* | *L* selected | `capture.Stop()` |
| *K* listening | keystroke captured | *K* selected | `Remap`, `capture.Stop()`, refresh cap + counter |
| *K* listening | `CancelRemapCommand` (Cancel button, Escape fallback) | *K* selected | `capture.Stop()`, model untouched |
| any | layer switch | nothing selected | `capture.Stop()` |
| any | `SelectKeyCommand(null)` | nothing selected | `capture.Stop()` |
| any | macro recording starts | listening cancelled | `capture.Start()` for the recording instead |
| any | a feature panel opens | listening cancelled | the panel owns capture from there ([feature-dialogs.md](feature-dialogs.md)) |
| *K* listening | `Dispose` | — | `capture.Stop()`, unsubscribe |

`CanBeginRemap` is false for a locked position (`CanEdit == false`, 05 §5.3), while loading, while saving, **while the macro panel is recording, and while a feature panel is open** — the last two would fight over the same keystrokes. Clicking a locked key twice selects it and does nothing else. `BeginRemapCommand` is the same entry point without the click (bindable, and what the tests use).

**Escape is not the cancel key — this is the single most surprising thing about the workflow.** While a key listens, the capture service previews the window's key events in the tunnel phase and swallows every physical key it resolves, Escape included, so **Escape is assigned like any other key**: a keyboard must be able to carry an Escape remap. The same holds while a Tap and Hold field is armed. Cancelling is a pointer action — the *Cancel* button beside the listening banner, clicking the listening key again, clicking another key, or switching layer. `KeyboardEditorView` registers a tunneled `KeyDown` handler (`handledEventsToo: true`, as in `MessageBoxWindow`) for the two things Escape may still do. **An open feature panel goes first and is dismissed whatever `e.Handled` says** — capture may be running for something else entirely (a macro recording used to swallow the Escape and leave the panel unclosable), and a panel that cannot be dismissed from the keyboard is worse than an Escape that also lands somewhere. The one exception is a panel that is itself waiting for a keystroke, which the view reads off `KeyboardEditorViewModel.IsOverlayAwaitingKeystroke` — an armed Tap and Hold field takes the Escape and disarms, so the next Escape closes the panel. With no panel open, `CancelRemapCommand` runs if it can: while a key is listening the capture handler on the `TopLevel` above has already assigned the Escape and left listening state, so the command is unavailable and nothing double-fires.

**Applying a captured keystroke goes through the editor path `KeyboardKey.Remap(keystroke.Key)`**, which means capturing a key's *own original action* clears the remap (04 §2.1), exactly like the legacy apps. `ResetKeyCommand` deliberately calls `ClearRemap()` instead: `Remap(OriginalKey)` also clears the position's tap-and-hold and multi-modifier as a side effect ([keyboard-model.md](keyboard-model.md), "Watch out"), which is not what "reset this key's remap" means. `ResetLayerCommand`/`ResetLayoutCommand` call Core's `KeyboardLayer.Reset()`/`KeyboardLayout.Reset()` (which clear all four rule kinds; `KeyColor` survives) and then re-read every cap.

`ResetLayerCommand`/`ResetLayoutCommand` also stop macro recording and call `MacroPanelViewModel.RefreshFromModel()` afterwards — `Reset()` empties the macro slots too, so the panel would otherwise sit on macros that no longer exist.

Both counters of spec 10 are recomputed together by `RefreshCounters()`: `ModifiedKeyCount` → `Remap (n)` (`KeyboardLayout.ModifiedKeyCount`) and `MacroCount` → `Macro (n)` (`KeyboardLayout.MacroCount`). It runs after a load or import, after a captured remap, after each reset, after an accepted tap-and-hold, and on every `MacrosChanged` the macro panel raises.

### Keystroke routing

The editor owns the **single** subscription to `IKeystrokeCaptureService.KeystrokeCaptured`, so spec 10's routing rule lives in one method (`OnKeystrokeCaptured`) and one keystroke reaches exactly one target, in this order:

1. **The open feature panel**, if it is an `IKeystrokeSink` — Tap and Hold. It takes the keystroke on being *open*, which is spec 10's own wording ("forwarded to the Tap and Hold dialog **if that dialog is open**"); an unarmed panel swallows it and drops it, so nothing under a modal panel can consume keys aimed at it.
2. **The macro panel**, if it is recording — the keystroke is appended to the macro under edit, with the modifiers held at that moment folded into the step (05 §5.1).
3. **The listening key**, applied as a remap.

Capture is *started* by whichever of the three needs it and by nobody else: `BeginRemap` for a listening key, `OnMacroRecordingChanged` for a recording (the panel only announces `RecordingChanged`; the editor turns the service on and off around it), and `EditorOverlayHost` for an armed sink. Entering recording cancels a listening key first, and opening a panel ends both.

### The macro panel

`MacroPanelViewModel` is built by `Apply` once a model exists and is shown behind the *Macros* tab (`IsMacroPanelVisible`); the board stays on screen next to it, because **the selected key is the macro's trigger** — `SetTrigger(SelectedKey, SelectedLayer?.Layer)` is called on every selection and layer change, and switching trigger stops any recording. Its UI is deliberately plain pending a later editor-wide visual pass; the rules below are the substance.

- **Every limit comes from `MacroCapability`** ([domain-data.md](domain-data.md)): slot count, co-trigger cap, speed/repeat ranges and defaults, the per-macro and per-layout keystroke budgets, and the macro count — the last raised from `MaxMacroCount` to `GatedMaxMacroCount` by `ResolveMaxMacroCount(device)`, the editor's only firmware-gate read (`FirmwareFeature.ExpandedMacroCount`, 09 §2; demo mode passes). Nothing hard-codes the RGB's numbers, and **both** macro stores of 06 §1 are supported — per-key slots and the Adv360 flat list — even though only the RGB can be opened today.
- **Trigger state**: `TriggerKey`/`HasTriggerKey`/`TriggerCaption` (the caption names `KeyboardKey.TriggerKey`, not the remapped action, because that is what a macro matches on, 05 §1.3), `CanEditMacro` = device supports macros **and** a key is selected **and** `CanAssignMacro`. `Message` carries the one line the panel has to say: `This device does not support macros.`, `Select a key on the keyboard to give it a macro.`, or spec 02's verbatim `You cannot assign a macro to a modifier key`.
- **Slots** (`MacroSlotViewModel`, one per **`PersistedSlotsPerKey`** — 3 on the Advantage2 and Freestyle Edge/Pro, 5 on the RGB family; none on the flat-list families): the strip offers what the dialect *writes*, not the five the model owns, because `LayoutFileSerializer` emits exactly the persisted count and a macro put in slot 4 of a Freestyle Edge would be gone after save+reload with nothing said. Selecting one opens `key.GetMacro(slot)` or a fresh `Layout.CreateMacro()` draft, and writes `KeyboardKey.ActiveMacroIndex` — an in-memory field only, never serialized. Each slot shows `MacroKeystrokeRenderer.RenderKeystrokes` as its preview.
- **Steps** (`MacroStepListViewModel`/`MacroStepViewModel`): append (recording or insertion), remove last, remove one, clear. **Backspace is the panel's button, never a captured key** — 06 §2.2 makes every physical key recordable content, so pressing Backspace types a backspace into the macro. A step renders as `LS + A` with `↓`/`↑` for an explicit key direction.
- **Co-triggers** (`MacroCoTriggerViewModel`): the six left/right Shift/Ctrl/Alt toggles of 06 §5. `Macro.AddCoTrigger` neither de-duplicates nor refuses, so the cap is held here — and it is **`PersistedCoTriggersPerMacro`** (`MacroPanelViewModel.MaxCoTriggers`), for the same reason as the slots: the old Freestyle serializer writes only the first, so the other three would vanish on save. Turning a toggle off clears **every** slot carrying that key.
- **Speed/repeat** are clamped to the device's `ValueRange` on assignment and written to the macro under edit; loading a macro moves the fields without writing back. **`HasRepeat` also requires `MacroCapability.PersistsRepeat`** — the Advantage2 has a repeat range in the model but its serializer writes no `{xN}` token at all (06 §3), so the control is hidden there rather than offering a value the next save discards. `HasSpeed` stays true on it: `{speedN}` *is* written.
- **Budgets** (`MacroBudgetViewModel`): `MacroLength / MaxMacroLength`, `profile keystrokes / MaxTotalKeystrokes`, `macros / MaxMacroCount`, each with an over-budget flag, plus 06 §6's verbatim `Macros are limited to approximately {0} characters.` The per-macro figure goes through **`MacroLengthMetric`**, so the Advantage360's 500 is measured as serialized macro-text characters and every other family's 300 as weighted keystrokes — the same code `Validate()` uses, which is what keeps the readout and the save gate from disagreeing. **A null limit means no limit, never zero**: the Advantage2 states no macro count, so its caption is a bare number and nothing is refused. **Breaching a budget is reported, never refused** — `Validate()` at save time is the backstop ([keyboard-model.md](keyboard-model.md), invariant 1).
- **Assign / Delete / Copy / Paste**: `Assign` stamps the macro's trigger and layer (as `MacroLineParser` does, 04 §4.2) and puts it in the chosen slot with `SetMacro` — not `AssignMacro`, which always takes the *first* free slot — or appends it to the flat list; it refuses with `This profile already holds its maximum of {0} macros.` only when the assignment really *adds* a macro, and with `Every macro slot of this key is taken.` when no slot was targeted and none is free. `Delete` removes it and re-opens the same slot with a fresh draft. `Copy`/`Paste` go through **`MacroClipboard`**, a one-key clipboard (slot families only) holding a **deep-copied snapshot**, never the source key: between the two the source can be emptied by Reset Key / Reset Layer / Reset Layout, and a live reference would then paste nothing and wipe the target. A paste replaces all five slots at once, so it is measured against the same macro count Assign is — `clipboard − target` macros added — and refused with the same wording.
- **The macro list** (`MacroListEntryViewModel.BuildAll`) is the Adv360's "macro repository" shown on every device: the flat list first, then every populated key slot in layer/key/slot order, each row rendered as the line the file will carry — **layer prefix included** (`MacroKeystrokeRenderer.LayerPrefixFor`), so a bottom-layer Gen1 macro reads `fn {q}>{s5}{x1}{a}` exactly as `LayoutFileSerializer` writes it (06 §3, 04 §3.1) — and captioned `Top · A (1)`. Selecting a row opens that macro for editing **without** moving the board's selection, but **the panel's own trigger, slot strip and every key-scoped action follow the macro** (`MacroEditTarget` = macro + key + layer + slot). Otherwise the header names the board's key while Record, Clear, Backspace, the co-triggers and the sliders all write to the row's key.
- The panel raises `MacrosChanged` after every write (the editor recounts) and `RecordingChanged` (the editor drives capture). It writes to the model directly and announces nothing else, so `RefreshFromModel()` is what an outside write (the resets) must call.

### Feature commands (spec 11)

Five commands open the inline panels; the panels themselves and every rule and message they carry are in [feature-dialogs.md](feature-dialogs.md). The editor's part is the sequencing:

| Command | Sequence |
|---|---|
| `TapAndHoldCommand` | firmware gate (awaited — the selection is re-checked afterwards, since a message box was up) → `TapAndHoldOverlayViewModel.TryCreate` → message box on a refusal, otherwise show the panel and hook `SearchRequested` (nested picker) and `Closed` (refresh the cap + counters when it was accepted) |
| `InsertDelayCommand` | firmware gate (`CustomMacroDelays`) → show `MacroDelayOverlayViewModel`, whose `Accepted` key is appended to the macro under edit |
| `InsertSpecialActionCommand` | show `SearchKeysOverlayViewModel` titled `Search Keys (Macro)`; its `Selected` key is appended the same way |
| `ExportCommand` | show `ExportOverlayViewModel` over the current session |
| `ImportCommand` | cancel listening + stop recording → `ProfileImporter.ImportAsync` → on success re-run `Apply` and toast, on failure a message box |

`ShowOverlay(overlay)` is the one entry point: it drops any Tap and Hold hooks, cancels listening, **stops any macro recording**, and hands the panel to `EditorOverlayHost`. Stopping the recording is what hands the capture service back — a recording underneath owns it, the host would then never start or stop it, and every key aimed at the panel would land in the macro instead. The two insertion panels are wired through `ShowMacroInsertOverlay`, which subscribes the "append to macro" action and **unsubscribes it on the panel's `Closed`**, so a dismissed panel can never write into the macro afterwards.

### Save

`SaveAsync` cancels any listening and stops any macro recording first, then: `IsBusy = true` → `ShowLoading("Saving...")` → `Task.Run(session.Save)` → `HideLoading()` + `IsBusy = false` in a `finally` → report. Everything else is Core's ([profiles.md](profiles.md)): validation gating, file writes, eject, post-save wording.

- **Threw** → message box `Save Profile` / `SaveErrorMessagePrefix + message`.
- **`Success == false`** → message box listing `SaveRejectedMessage` followed by one line per `ModelViolation.Message` (04 §5.3's gate, surfaced rather than silently dropped).
- **Success with a `PostSaveMessage`** → toast titled `Save Profile` carrying the device's refresh wording verbatim; no message, no toast.

Both boxes go through `TryShowMessageBoxAsync`, which swallows a box that cannot be put on screen — the editor state already carries the outcome, and a closed owner window must not bring the app down.

## Presentation rules

**`KeyCaption`** — the caption of `KeyboardKey.ModifiedOrOriginalKey` (so a remapped cap reads its new action), resolved in this order: `GlyphText` → `MacDisplayText` when `OperatingSystem.IsMacOS()` → `GetDisplayText(dialect)` → **the dialect's file token when all of those are blank**. That last step exists because 05 §3.11 registers `hk0`–`hk8` with `' '` (the physical caps are unlabelled), which would otherwise draw a column of indistinguishable empty caps; the token is what the user sees for that position in the layout file, which makes it the honest fallback rather than an invented label. `\n` is preserved verbatim — splitting it into a two-line cap (05 §1.1) is the view's job. `KeyCaption.For(key, dialect, isMacOs)` takes the platform as a parameter so both directions are testable; `IsMacOs` is resolved once as a static.

**`LayerCaptions`** — the geometry's `Name` is the spec-literal file-side name (`Qwerty-top`/`Qwerty-keypad`), which is not what an editor shows. Gen2 keeps the raw names (Base, Keypad, Fn1–Fn3 are already presentation-ready); Legacy/Gen1 map index **0 → `Top`**, **1 → `Fn`** (spec 10 describes both switches that way); anything else falls back to the raw name.

**`KeyColorOverlay`** — projects a profile's lighting onto the picture as `key index → #RRGGBB`. It is more than a lookup for two reasons, which is why it lives in one tested place: `LayerLightingState.KeyColors` is keyed by **memory key code** (07 §4), not by key index, so every entry is resolved through `KeyboardLayer.FindByOriginalKeyCode`; and `KeyboardKey.KeyColor` exists but **no parser ever fills it** (the led file is parsed into the lighting model, never into the layout model), so reading the key would always show nothing. Empty unless the device is `LightingKind.PerKeyRgb` *and* `IProfileSession.Lighting` is a `LightingModel` (TKO's `TkoLightingModel` is deliberately not matched yet) *and* the layer is one of the two that model describes — layout layer **0 ↔ `LightingModel.TopLayer`**, **1 ↔ `FnLayer`**. `LedColor.IsBlack` is "no colour" (07 §2.1) and yields no entry; an unknown key code is skipped.

## Seams and composition

- **`IProfileSession` / `IProfileSessionFactory`** exist because Core's `ProfileSession` is sealed with a static `Load` — unsubstitutable in a test. `ProfileSessionAdapter` is a pass-through with no behaviour of its own; `ProfileSessionFactory` calls `ProfileSession.Load` and wraps it. Same shape as `IMessageBoxPresenter`.
- **`EditorViewModelFactory`** is the one place that picks an editor: `DeviceId.SavantElite2` → `SavantElitePedalViewModel` ([savant-elite.md](savant-elite.md)); otherwise "can this device be drawn?", answered with `VisualCatalog.TryGet(id, out _) && GeometryCatalog.TryGet(id, out _)`, → `KeyboardEditorViewModel` or `EditorPlaceholderViewModel`. The shell asks for one and swaps in whatever it gets, which is also what keeps every editor's dependencies out of `MainWindowViewModel` — the profile-session factory, the capture-service accessor, `PedalFileService`, and the keyboard editor's feature-panel set: `IFolderPickerService`, `IFilePickerService`, `IVDriveFileService` and `IUrlLauncher`.
- **The capture service is resolved through a `Func<IKeystrokeCaptureService>`**, not held: `AvaloniaKeystrokeCaptureService` attaches to the shell `TopLevel`, which does not exist while `App.BuildServices` wires the graph — the same ordering problem the message-box presenter solves with its `Func<Window?>` owner. `App` builds it lazily on first use, keeps the single instance, and disposes it in `OnExit` **after** the shell (which closes and disposes the editor, which stops capture first).
- `MainWindowViewModel` delegates the choice to `IEditorViewModelFactory`, keeps `Editor` typed as the abstract `DeviceEditorViewModel?`, fires `editor.LoadAsync()` once after the view swap, and disposes the outgoing editor in `CloseEditor()` on both navigate-home and re-open.

## Spec strings and deliberate deviations

From the spec: the `Remap (n)` and `Macro (n)` counters and the `Reset Key` / `Reset Layer` / `Reset Layout` button captions (spec 10), the macro panel's `Macro assigned to <co-triggers + key>` confirmation (spec 10), `You cannot assign a macro to a modifier key` (spec 02) and `Macros are limited to approximately {0} characters.` (06 §6), the post-save toast (Core's `ProfileSaveMessageCatalog` verbatim), and every string of the spec 11 panels ([feature-dialogs.md](feature-dialogs.md)).

Everything else here is this app's wording: `Profile n`, `Saving...`, `Loading profile...`, the `Save Profile` / `Load Profile` dialog titles, `The profile was not saved because it exceeds the device's limits:`, `Line <n>: <text>`, `Press a key to assign it to the highlighted key.`, `Some lines of this profile could not be applied`, the macro panel's other lines (`Select a key on the keyboard to give it a macro.`, `This device does not support macros.`, `A macro can hold at most {0} co-triggers.`, `This profile already holds its maximum of {0} macros.`, `Every macro slot of this key is taken.`, `Copy a key's macros first.`), and the tab captions `Keys` / `Macros` / `Lighting` / `Settings` (spec 10's RGB app has `Layout` / `Lighting` tabs with the macro editor as an in-window panel; the four-tab shape is chosen so the macro panel and #16 have somewhere to land). The strings the view models own are consts on `KeyboardEditorViewModel`/`EditorTabViewModel`/`LayerCaptions`/`MacroPanelViewModel`/`MacroBudgetViewModel` and asserted by tests; the ones only the view uses are XAML literals.

Recorded deviations:

1. **No per-key Done/Cancel commit step.** Spec 10's Adv2 flow has "**Done** validates and marks the layout modified; **Cancel** reverts". Here a captured keystroke is applied to the model immediately, and Cancel means "stop listening" only. Validation happens once, at save time, where 04 §5.3 puts it.
2. **The layer switch is built from the model's layers**, not from a two-state toggle — two pills on a Freestyle Edge RGB, five on an Advantage 360 (#41) with no change to the view.
3. **Invalid lines are an inline collapsed block**, not the Adv360's modal "Invalid Lines dialog" (spec 10). Same content (04 §5.2), less interruption; `LayoutInvalidLine.Keep` is untouched, so they are still re-serialized as Core decides.
4. **The colour overlay is a strip under the caption**, not the cap background: the cap background already carries the four remap states, and 07 §2.1 colours must stay distinguishable next to them.
5. **The macro panel is a tab, not a docked strip or a separate window**, and its layout is deliberately plain — spec 10's Adv2 docks it at the bottom and the RGB app opens it in place. The board stays visible beside it because the selected key *is* the trigger; a visual pass over the whole editor is a later change.
6. **The spec 11 dialogs are inline panels over the editor**, not modal windows ([feature-dialogs.md](feature-dialogs.md)).

## Load-bearing invariants

1. **One visual per device, all layers.** Layers differ only in the tokens bound to a position (05 §7.4), so the picture is rebuilt per layer from the *same* rectangles; a per-layer visual would be data waiting to disagree with itself.
2. **The join is by key index, and mismatches degrade.** `KeyboardVisual.TryGetKey(key.Index, …)`; a miss skips the cap. Core's `Geometry/Visual` tests assert set equality with the logical geometry in both directions, so a mismatch is a data bug caught in CI, not a runtime crash.
3. **Every model write is followed by `RefreshFromModel()`.** Core announces nothing — on the cap (`KeyboardKeyViewModel`), on the layer, and on the macro panel after an outside write.
4. **Capture is never left running, and the editor owns it.** It is started only by a key entering listening, a macro recording, or an armed Tap and Hold field, and stopped on the keystroke, on cancel, on a layer switch, on a tab switch away from Macros, on a save, on an import, and on `Dispose` — the app-wide service swallows keystrokes from the whole window while it is on. The macro panel never touches the service; it raises `RecordingChanged` and the editor drives it.
5. **One keystroke, one target.** The subscription to `KeystrokeCaptured` is single and lives here; the precedence (open sink panel → recording macro → listening key) is the one place spec 10's routing rule is written down. The sink wins on being *open*, and `ShowOverlay` stands the other two down before the panel appears, so there is never a second consumer to race.
6. **Escape is a remappable key, not a shortcut** (see the remap section) — with one carve-out: an open feature panel that is not itself awaiting a keystroke is always dismissible with it. Do not add an accelerator that steals Escape anywhere else.
7. **Captured keystrokes go through the editor path, resets through `ClearRemap`.** `Remap` implements 04 §2.1's remap-to-self clearing; `Remap(OriginalKey)` would additionally destroy tap-and-hold and multi-modifiers.
8. **View models expose enums and strings, never brushes** ([app-shell.md](app-shell.md), invariant 6). The overlay travels as `#RRGGBB` and becomes a brush in XAML through `HexColorToBrushConverter`, which converts anything unparseable to `null` rather than throwing inside a binding.
9. **Demo mode never writes** (03 §3.5): no session is created at all, so `SaveCommand`, `ExportCommand` and `ImportCommand` cannot become available by any other property moving.
10. **Keystroke budgets are reported, never refused** — the macro panel shows the breach and lets the edit stand; `Validate()` at save time is what stops the write ([keyboard-model.md](keyboard-model.md), invariant 1). It refuses at input time in exactly three places, all of which `Validate()` would also report: a position that cannot hold a macro (05 §5.3), the co-trigger cap, and the macro count — the counts because there is no meaningful "half-added" macro to leave on screen.
11. **`LoadAsync` is total and idempotent.** The shell fires and forgets it; an escaping exception would be an unhandled crash, and a second call must not re-read the drive.
12. **Adding a device is adding data.** `EditorViewModelFactory`, `KeyboardView`, `KeyCapView` and `KeyboardPanel` contain no device identity; only `VisualCatalog` does. The macro panel is device-agnostic too — it reads `MacroCapability` and supports both macro stores.
13. **What the panel offers is what the file keeps.** Where the model holds more than the dialect persists — five slots against three, four co-triggers against one, a repeat value the Advantage2 never writes — the panel offers the persisted figure. An edit the next save would silently drop is worse than one the UI never allowed.
14. **The panel edits one macro, and names it.** `MacroEditTarget` is the single answer to "what is open"; the displayed trigger, the highlighted slot and every key-scoped command read it, never the board's selection directly.

## Testing

App-layer tests are **view-model level and run without an Avalonia runtime** (the project-wide rule — `KinesisEdit.Tests`), so the state machine, load/save orchestration and the three presentation rules are all covered while the XAML is guarded by compiled bindings and the build alone. Placement data itself is tested in Core (`KinesisEdit.Core.Tests/Geometry/Visual`: index-set equality with the geometry per layer, unique indices, no overlapping rectangles, bounds, cluster assignment).

Suites: `KeyboardEditorViewModelTests` (load/save/tabs/counters), `…RemapTests` (the state machine), `…RoutingTests` (the three-way keystroke precedence, who starts/stops capture, and that opening a panel ends a recording), `…FeatureTests` (the five feature commands, their gates and refusals, including the device-support and Macros-tab guards), `MacroPanelViewModelTests` (per-device: the persisted slot and co-trigger counts, the Advantage2's absent macro count and repeat token, the Advantage360's length metric, the `fn ` row prefix, a list row retargeting the panel, and the clipboard snapshot + its count limit), plus the panels' own suites ([feature-dialogs.md](feature-dialogs.md)). Core's share of the same rules is in `Model/{MacroLengthMetric,MacroKeystrokeRenderer,TapAndHoldPrecheck}Tests` and `Devices/MacroCapabilityTests`.

Fakes (`KinesisEdit.Tests/Services`): `FakeProfileSessionFactory` (records every `Load`, hands back a staged `FakeProfileSession`, or throws like a vanished drive), `FakeProfileSession` (settable `Lighting`/`InvalidLines`/`ProfileNumber`/`CanSave`, counts `Save` calls, records `PlanExport` selections, applies a real parse for `Import`, returns or throws a staged result), `FakeKeystrokeCaptureService` (counts `Start`/`Stop`/`Dispose`, exposes `HasSubscribers`, and `RaiseKeystroke(...)` pushes a keystroke in as the real service would), `FakeFolderPickerService`/`FakeFilePickerService`, plus the shell's existing `FakeNotificationService`/`FakeUrlLauncher`/`FakeVDriveFileService`. `KinesisEdit.Tests/ViewModels/TestLayouts.cs` holds the fixtures the catalogs cannot supply: a layout with a locked position (the RGB geometry has none), single-layer layouts from tokens, small hand-built `KeyboardVisual`s for the join edge cases, a cap-view-model factory, named RGB key indices (including the Left Shift position, the only kind that refuses a macro), a helper that fills macro slots up to a device's count limit, and an RGB board on a device definition with `TapAndHoldCapability.None` (no shipped device both draws a board and lacks the feature).

`HexColorToBrushConverter` is tested directly — it touches `Avalonia.Media` but needs no app instance.

Uncovered by tests, on purpose: `KeyboardPanel`'s measure/arrange arithmetic, the style classes, the overlay scrim, the macro panel's XAML and the `KeyboardEditorView` Escape handler all need a UI runtime — they are hand-verified (`dotnet run --project src/KinesisEdit`).

## Deliberately not here

- **No way to assign a key other than a captured physical keypress.** Search Keys (§11.6) reaches only the Tap and Hold fields and the macro under edit; spec 10's categorized *Special Actions* popups (F13–F24, keypad, multimedia, mouse clicks, alt layouts, Hyper/Meh) do not exist as a per-key palette, and there is no right-click menu on a cap.
- **No multi-modifier UI (11 §11.2) and no Select Macro dialog (11 §11.4)** — both are Advantage 360 surfaces and follow that board's picture in issue [#41](https://github.com/migus88/kinesis-edit/issues/41). The model supports multi-modifiers, and `ResetKeyCommand` is careful not to destroy them or a tap-and-hold.
- **No Diagnostics report (11 §11.7) or Troubleshoot dialog (11 §11.8) in the editor** — issue [#46](https://github.com/migus88/kinesis-edit/issues/46).
- **No lighting or settings editing** — those two tabs exist and are disabled; issue [#16](https://github.com/migus88/kinesis-edit/issues/16). The lighting model is read *only* to paint the per-key colour strip, and an **imported led file** replaces it in the session without the editor showing it anywhere but that strip.
- **No profile picker, Save As, or New** (spec 10) — the editor always opens `LayoutScheme.FirstProfileNumber` and `SaveCommand` writes back to that same slot; Export and Import work on that one profile. `ProfileSession.SaveAs` already exists in Core ([profiles.md](profiles.md)).
- **No unsaved-changes guard.** `IProfileSession.IsDirty` is exposed on the seam but nothing consumes it: Home ends the session without asking (spec 10's Adv2 "Do you want to save changes?" prompt is unimplemented), there is no undo buffer, and an import is therefore irreversible short of not saving.
- **No `Keyboard Connection Lost` dialog** (03 §3.5) — a drive that vanishes mid-save surfaces as the generic save-error box. Still owned by a later issue, as [app-shell.md](app-shell.md) records.
- **No alternate-layout generators** (Dvorak/Colemak/Workman onto chosen layers, spec 10) and **no key-data copy/paste** — the macro panel's Copy/Paste uses `KeyboardKey.CopyFrom` with `KeyCopyScopes.Macros` only; the remap/tap-and-hold half of that method is still unused here.
- **No per-device visuals beyond the Freestyle Edge RGB** — issues #39–#42, data only. The TKO's 33 edge lighting zones (`KeyCluster.EdgeZone`, `KeyboardLayer.EdgeKeys`) arrive with #40 and are lighting zones, not typing keys.
