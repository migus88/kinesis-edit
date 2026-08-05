# Keyboard editor (keyboard picture + remap editing)

The editor the shell swaps in on Configure for a programmable keyboard: a keyboard-shaped picture of one loaded profile, the click-then-press remap workflow of spec 10 ("click an on-screen key — the key enters 'listening' state; the next physical keypress captured by the app becomes the new assignment"), and the reset/save actions around it. It owns **no** domain rules — placements come from `VisualCatalog` ([domain-data.md](domain-data.md)), the model and its edit paths from `KinesisEdit.Core.Model` ([keyboard-model.md](keyboard-model.md)), load/save from `ProfileSession` ([profiles.md](profiles.md)), keystrokes from `IKeystrokeCaptureService` ([keystroke-capture.md](keystroke-capture.md)), and window/navigation/notifications from the shell ([app-shell.md](app-shell.md)).

**Device coverage: Freestyle Edge RGB only.** Every other device opens `EditorPlaceholderViewModel` — except the Savant Elite2, which has an editor of its own ([savant-elite.md](savant-elite.md)) and no keyboard picture at all — because only that one board's picture is authored. Adding a device is adding data to `VisualCatalog` — issues [#39](https://github.com/migus88/kinesis-edit/issues/39) (FS Edge / FS Pro), [#40](https://github.com/migus88/kinesis-edit/issues/40) (TKO), [#41](https://github.com/migus88/kinesis-edit/issues/41) (Advantage 360), [#42](https://github.com/migus88/kinesis-edit/issues/42) (Advantage2) — never editing anything below.

| Namespace | Entry point(s) | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Geometry.Visual` | `VisualCatalog`, `KeyboardVisual`, `KeyVisual`, `KeyCluster` | Where each key position sits, in key units (UI-free data) | 05 §4; 02 |
| `KinesisEdit.Controls` | `KeyboardPanel` | The only arithmetic: key-unit rectangles → arranged, scaled, centred children | — |
| `KinesisEdit.Controls` | `KeyboardView`, `KeyCapView` | The device-agnostic picture of one layer; one key cap | 10 "Remap workflow" |
| `KinesisEdit.Views` | `KeyboardEditorView` | Header, layer switch, tab strip, listening banner, invalid-line block, Save | 10 |
| `KinesisEdit.ViewModels` | `KeyboardEditorViewModel` | The editor: load, selection/listening state machine, resets, save | 10; 04 §2.1, §5.2, §5.3; 03 §3.5, §5.3 |
| `KinesisEdit.ViewModels` | `KeyboardLayerViewModel`, `KeyboardKeyViewModel` | One layer's caps; one cap over one `KeyboardKey` | 05 §1.3, §5.3, §7.4 |
| `KinesisEdit.ViewModels` | `DeviceEditorViewModel`, `EditorTab`, `EditorTabViewModel` | What the shell needs from *any* editor ([app-shell.md](app-shell.md)); the section strip | 10 |
| `KinesisEdit.ViewModels` | `KeyCaption`, `LayerCaptions`, `KeyColorOverlay` | The three presentation rules, each testable on its own | 05 §3, §1.1; 10; 07 §4 |
| `KinesisEdit.Services` | `IProfileSession`, `IProfileSessionFactory`, `ProfileSessionAdapter`, `ProfileSessionFactory` | The fakeable seam over Core's sealed `ProfileSession` | 03 §4.1, §5.3 |
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

`DeviceEditorViewModel` — the shell's abstract editor base, shared with `EditorPlaceholderViewModel` and `SavantElitePedalViewModel` ([savant-elite.md](savant-elite.md)) — supplies `Device`, `DeviceName`, `IsDemoMode` and the virtual `LoadAsync()`/`ConfirmCloseAsync()`; `MainWindowViewModel.Editor` is typed as that base so navigation is independent of which editor a device resolves to. This class overrides `LoadAsync` and keeps the default `ConfirmCloseAsync` (always true): the unsaved-remap question is not built here yet — the pedal editor is the only one that gates its close ([savant-elite.md](savant-elite.md)).

State: `IsLoading` (true until the first load finishes), `IsBusy` (a save is in flight), `Layout`, `Layers`, `SelectedLayer`, `SelectedKey`, `ListeningKey`/`IsListening`, `ProfileCaption`, `ModifiedKeyCount`/`RemapCounterCaption`, `InvalidLineMessages`/`HasInvalidLines`, `BoardWidth`/`BoardHeight`, `Tabs`/`SelectedTab`.

Commands and what gates them:

| Command | Enabled when |
|---|---|
| `SelectKeyCommand(KeyboardKeyViewModel?)` | always (the click contract below) |
| `SelectLayerCommand(KeyboardLayerViewModel)` | always |
| `SelectTabCommand(EditorTabViewModel)` | `tab.IsEnabled` — only `EditorTab.Keys` today |
| `BeginRemapCommand` | `SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy` |
| `CancelRemapCommand` | `IsListening` |
| `ResetKeyCommand` | `SelectedKey is not null && SelectedKey.CanEdit` |
| `ResetLayerCommand` | `SelectedLayer is not null` |
| `ResetLayoutCommand` | `Layout is not null` |
| `SaveCommand` (async) | `_session is not null && _session.CanSave && !IsDemoMode && !IsLoading && !IsBusy` |

`SelectedTab` is a two-way-bindable property whose setter runs the same guard as the command, so a binding cannot open a tab the command refuses. Disabled tabs are shown, not hidden, so the editor's shape does not change when #15/#16 fill them in.

### Lifecycle — `LoadAsync`, `Dispose`

**Construction is deliberately cheap**: it resolves the device's `KeyboardVisual`, builds the tab strip and commands, and subscribes to `IKeystrokeCaptureService.KeystrokeCaptured`. No file is touched, so the shell can swap the view in immediately — `MainWindowViewModel.OpenDevice` fires `_ = editor.LoadAsync()` **after** the view swap and after hiding its own loading splash; the drive read runs against the editor's own `IsLoading`.

`LoadAsync` is **total and idempotent** (`_hasLoadStarted` / `_isDisposed` guards; a second call is a no-op), which is what makes forgetting the task safe:

- The read runs off the UI thread (`Task.Run`, resumed with `ConfigureAwait(true)`).
- **Demo mode, or a device with no `Location`** → `KeyboardLayout.Create(Device.DeviceId)` in memory, no session: `ProfileCaption` stays empty and Save is permanently unavailable (03 §3.5 — demo mode never touches the drive).
- **Otherwise** → `IProfileSessionFactory.Load(Device.Location, Device.DeviceId, Device.Device.LayoutScheme.FirstProfileNumber)`, i.e. profile 1 on every numbered-profile device.
- **Any failure** (vanished drive, unreadable file, unsupported device) degrades to the same in-memory layout plus a `LoadFailureTitle` message box — the editor never crashes the shell. If even `KeyboardLayout.Create` throws (a device with no geometry), `Layout` stays null and the picture is empty.
- `Apply` then sets `Layout`, `ProfileCaption`, `InvalidLineMessages`, builds `Layers`, selects layer 0, and refreshes `ModifiedKeyCount`.

`Dispose` (called by `MainWindowViewModel.CloseEditor` on navigate-home and on re-open, and by `App`'s exit path through the shell) detaches from `KeystrokeCaptured` and stops capture. It is idempotent. **Leaving capture started would swallow every keystroke of the dashboard behind the closed editor** — the capture service is app-wide and outlives the editor.

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
| *K* listening | `Dispose` | — | `capture.Stop()`, unsubscribe |

`CanBeginRemap` is false for a locked position (`CanEdit == false`, 05 §5.3), while loading, and while saving — clicking a locked key twice selects it and does nothing else. `BeginRemapCommand` is the same entry point without the click (bindable, and what the tests use).

**Escape is not the cancel key — this is the single most surprising thing about the workflow.** While a key listens, the capture service previews the window's key events in the tunnel phase and swallows every physical key it resolves, Escape included, so **Escape is assigned like any other key**: a keyboard must be able to carry an Escape remap. Cancelling is a pointer action — the *Cancel* button beside the listening banner, clicking the listening key again, clicking another key, or switching layer. `KeyboardEditorView` does register a tunneled `KeyDown` handler (`handledEventsToo: true`, as in `MessageBoxWindow`) that routes Escape to `CancelRemapCommand`, but it is a **safety net, not the path**: the capture handler sits on the `TopLevel` above it on the same tunnel route and has already consumed the keystroke and left listening state by the time the view sees it, leaving `CanExecute` false. It fires only when capture did *not* consume the Escape — capture suspended by text-input focus, for instance — which is exactly the case where listening would otherwise get stuck.

**Applying a captured keystroke goes through the editor path `KeyboardKey.Remap(keystroke.Key)`**, which means capturing a key's *own original action* clears the remap (04 §2.1), exactly like the legacy apps. `ResetKeyCommand` deliberately calls `ClearRemap()` instead: `Remap(OriginalKey)` also clears the position's tap-and-hold and multi-modifier as a side effect ([keyboard-model.md](keyboard-model.md), "Watch out"), which is not what "reset this key's remap" means. `ResetLayerCommand`/`ResetLayoutCommand` call Core's `KeyboardLayer.Reset()`/`KeyboardLayout.Reset()` (which clear all four rule kinds; `KeyColor` survives) and then re-read every cap.

`ModifiedKeyCount` (spec 10's `Remap (n)` counter) is `KeyboardLayout.ModifiedKeyCount`, recomputed after a load, a captured remap and each of the three resets — the only places this module writes to the model.

### Save

`SaveAsync` cancels any listening first, then: `IsBusy = true` → `try { ShowLoading("Saving...") → Task.Run(session.Save) }` → `IsBusy = false` **then** `HideLoading()` in a `finally` → report. Everything else is Core's ([profiles.md](profiles.md)): validation gating, file writes, eject, post-save wording. The ordering is load-bearing: both indicator calls fan out to the overlay and can fail, so `ShowLoading` sits inside the `try` (its failure is reported as the save failure it is) and the flag is cleared before `HideLoading`, because a stranded `IsBusy` disables Save and every editing command for as long as the editor is open.

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
- **`EditorViewModelFactory`** is the one place that picks an editor: `DeviceId.SavantElite2` → `SavantElitePedalViewModel` ([savant-elite.md](savant-elite.md)); otherwise "can this device be drawn?", answered with `VisualCatalog.TryGet(id, out _) && GeometryCatalog.TryGet(id, out _)`, → `KeyboardEditorViewModel` or `EditorPlaceholderViewModel`. The shell asks for one and swaps in whatever it gets, which is also what keeps every editor's dependencies (the profile-session factory, the capture-service accessor, `PedalFileService`) out of `MainWindowViewModel`.
- **The capture service is resolved through a `Func<IKeystrokeCaptureService>`**, not held: `AvaloniaKeystrokeCaptureService` attaches to the shell `TopLevel`, which does not exist while `App.BuildServices` wires the graph — the same ordering problem the message-box presenter solves with its `Func<Window?>` owner. `App` builds it lazily on first use, keeps the single instance, and disposes it in `OnExit` **after** the shell (which closes and disposes the editor, which stops capture first).
- `MainWindowViewModel` delegates the choice to `IEditorViewModelFactory`, keeps `Editor` typed as the abstract `DeviceEditorViewModel?`, fires `editor.LoadAsync()` once after the view swap, and disposes the outgoing editor in `CloseEditor()` on both navigate-home and re-open.

## Spec strings and deliberate deviations

From the spec: the `Remap (n)` counter and the `Reset Key` / `Reset Layer` / `Reset Layout` button captions (spec 10), and the post-save toast, which is Core's `ProfileSaveMessageCatalog` wording verbatim.

Everything else here is this app's wording: `Profile n`, `Saving...`, `Loading profile...`, the `Save Profile` / `Load Profile` dialog titles, `The profile was not saved because it exceeds the device's limits:`, `Line <n>: <text>`, `Press a key to assign it to the highlighted key.`, `Some lines of this profile could not be applied`, and the tab captions `Keys` / `Macros` / `Lighting` / `Settings` (spec 10's RGB app has `Layout` / `Lighting` tabs with the macro editor as an in-window panel; the four-tab shape is chosen so #15/#16 have somewhere to land). The strings the view models own are consts on `KeyboardEditorViewModel`/`EditorTabViewModel`/`LayerCaptions` and asserted by tests; the ones only the view uses are XAML literals.

Recorded deviations:

1. **No per-key Done/Cancel commit step.** Spec 10's Adv2 flow has "**Done** validates and marks the layout modified; **Cancel** reverts". Here a captured keystroke is applied to the model immediately, and Cancel means "stop listening" only. Validation happens once, at save time, where 04 §5.3 puts it.
2. **The layer switch is built from the model's layers**, not from a two-state toggle — two pills on a Freestyle Edge RGB, five on an Advantage 360 (#41) with no change to the view.
3. **Invalid lines are an inline collapsed block**, not the Adv360's modal "Invalid Lines dialog" (spec 10). Same content (04 §5.2), less interruption; `LayoutInvalidLine.Keep` is untouched, so they are still re-serialized as Core decides.
4. **The colour overlay is a strip under the caption**, not the cap background: the cap background already carries the four remap states, and 07 §2.1 colours must stay distinguishable next to them.

## Load-bearing invariants

1. **One visual per device, all layers.** Layers differ only in the tokens bound to a position (05 §7.4), so the picture is rebuilt per layer from the *same* rectangles; a per-layer visual would be data waiting to disagree with itself.
2. **The join is by key index, and mismatches degrade.** `KeyboardVisual.TryGetKey(key.Index, …)`; a miss skips the cap. Core's `Geometry/Visual` tests assert set equality with the logical geometry in both directions, so a mismatch is a data bug caught in CI, not a runtime crash.
3. **Every model write is followed by `RefreshFromModel()`.** Core announces nothing.
4. **Capture is never left running.** It is started only when a key enters listening, and stopped on the keystroke, on cancel, on a layer switch, on a save, and on `Dispose` — the app-wide service swallows keystrokes from the whole window while it is on.
5. **Escape is a remappable key, not a shortcut** (see the remap section). Do not add an accelerator that steals it.
6. **Captured keystrokes go through the editor path, resets through `ClearRemap`.** `Remap` implements 04 §2.1's remap-to-self clearing; `Remap(OriginalKey)` would additionally destroy tap-and-hold and multi-modifiers.
7. **View models expose enums and strings, never brushes** ([app-shell.md](app-shell.md), invariant 6). The overlay travels as `#RRGGBB` and becomes a brush in XAML through `HexColorToBrushConverter`, which converts anything unparseable to `null` rather than throwing inside a binding.
8. **Demo mode never writes** (03 §3.5): no session is created at all, so `SaveCommand` cannot become available by any other property moving.
9. **`LoadAsync` is total and idempotent.** The shell fires and forgets it; an escaping exception would be an unhandled crash, and a second call must not re-read the drive.
10. **Adding a device is adding data.** `EditorViewModelFactory`, `KeyboardView`, `KeyCapView` and `KeyboardPanel` contain no device identity; only `VisualCatalog` does.

## Testing

App-layer tests are **view-model level and run without an Avalonia runtime** (the project-wide rule — `KinesisEdit.Tests`), so the state machine, load/save orchestration and the three presentation rules are all covered while the XAML is guarded by compiled bindings and the build alone. Placement data itself is tested in Core (`KinesisEdit.Core.Tests/Geometry/Visual`: index-set equality with the geometry per layer, unique indices, no overlapping rectangles, bounds, cluster assignment).

Fakes (`KinesisEdit.Tests/Services`): `FakeProfileSessionFactory` (records every `Load`, hands back a staged `FakeProfileSession`, or throws like a vanished drive), `FakeProfileSession` (settable `Lighting`/`InvalidLines`/`ProfileNumber`/`CanSave`, counts `Save` calls, returns or throws a staged result), `FakeKeystrokeCaptureService` (counts `Start`/`Stop`/`Dispose`, exposes `HasSubscribers`, and `RaiseKeystroke(KeyDefinition)` pushes a keystroke in as the real service would), plus the shell's existing `FakeNotificationService`. `KinesisEdit.Tests/ViewModels/TestLayouts.cs` holds the fixtures the catalogs cannot supply: a layout with a locked position (the RGB geometry has none), single-layer layouts from tokens, and small hand-built `KeyboardVisual`s for the join edge cases.

`HexColorToBrushConverter` is tested directly — it touches `Avalonia.Media` but needs no app instance.

Uncovered by tests, on purpose: `KeyboardPanel`'s measure/arrange arithmetic, the style classes, and the `KeyboardEditorView` Escape handler all need a UI runtime — they are hand-verified (`dotnet run --project src/KinesisEdit`).

## Deliberately not here

- **No action palette / token picker** (spec 10: right-click or *Special Actions* → categorized popups of F13–F24, keypad, multimedia, mouse clicks, delays, alt layouts, Hyper/Meh) — a captured physical keypress is the only way to assign a key today. Issue [#15](https://github.com/migus88/kinesis-edit/issues/15).
- **No macro recording, macro panel or macro repository**, and no `Macro (n)` counter — issue #15. `KeyboardKeyViewModel.CanAssignMacro` is already exposed for it.
- **No tap-and-hold and no multi-modifier UI** (11 §11.1, §11.2) — issue #15. The model supports both, and `ResetKeyCommand` is careful not to destroy them.
- **No lighting or settings editing** — the tabs exist and are disabled; issue [#16](https://github.com/migus88/kinesis-edit/issues/16). The lighting model is read *only* to paint the per-key colour strip.
- **No profile picker, Save As, New, Import/Export, or Diagnostic** (spec 10) — the editor always opens `LayoutScheme.FirstProfileNumber` and `SaveCommand` writes back to that same slot. `ProfileSession.SaveAs` already exists in Core ([profiles.md](profiles.md)).
- **No unsaved-changes guard.** `IProfileSession.IsDirty` is exposed on the seam but nothing consumes it: Home ends the session without asking (spec 10's Adv2 "Do you want to save changes?" prompt is unimplemented), and there is no undo buffer.
- **No `Keyboard Connection Lost` dialog** (03 §3.5) — a drive that vanishes mid-save surfaces as the generic save-error box. Still owned by a later issue, as [app-shell.md](app-shell.md) records.
- **No alternate-layout generators** (Dvorak/Colemak/Workman onto chosen layers, spec 10) and **no key-to-key copy/paste** (`KeyboardKey.CopyFrom` exists in Core, unused here).
- **No per-device visuals beyond the Freestyle Edge RGB** — issues #39–#42, data only. The TKO's 33 edge lighting zones (`KeyCluster.EdgeZone`, `KeyboardLayer.EdgeKeys`) arrive with #40 and are lighting zones, not typing keys.
