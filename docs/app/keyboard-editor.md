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
| `KinesisEdit.ViewModels` | `DeviceEditorViewModel`, `EditorTab`, `EditorTabViewModel` | What the shell needs from *any* editor ([app-shell.md](app-shell.md)); the device-driven section strip | 10 |
| `KinesisEdit.ViewModels` | `KeyboardSettingsViewModel`, `KeyboardSettingsRows`, `SettingsRowViewModel` + `SettingsSliderRowViewModel` / `SettingsToggleRowViewModel` / `SettingsChoiceRowViewModel` / `SettingsChoice` | The Settings tab: capability-driven rows, load/save ([settings.md](settings.md)) | 08 §5 |
| `KinesisEdit.Views` | `KeyboardSettingsView` | One `DataTemplate` per row kind; no device knowledge | 08 §5 |
| `KinesisEdit.ViewModels` | `LightingTabViewModel` + `LightingLayerViewModel` / `LightingModeViewModel` / `LightingDirectionViewModel` / `LightingZoneViewModel` / `LightingColorSlotViewModel` / `LightingPanelVisibility` | The Lighting tab: layer switch, mode menu, per-mode panels, per-key + zone painting ([lighting.md](lighting.md)) | 07 §3, §4 |
| `KinesisEdit.ViewModels` | `ColorPickerViewModel`, `ColorSwatch`, `CustomColorSlotViewModel` | The shared colour picker and its twelve persisted slots | 07 §4; 08 §3 |
| `KinesisEdit.Views`, `.Controls` | `LightingTabView`, `ColorPickerView` | XAML only; the picker wraps Avalonia's `ColorView` | 07 §3, §4 |
| `KinesisEdit.ViewModels` | `KeyCaption`, `LayerCaptions`, `KeyColorOverlay` | The three presentation rules, each testable on its own | 05 §3, §1.1; 10; 07 §4 |
| `KinesisEdit.Services` | `IProfileSession`, `IProfileSessionFactory`, `ProfileSessionAdapter`, `ProfileSessionFactory` | The fakeable seam over Core's sealed `ProfileSession` | 03 §4.1, §5.3 |
| `KinesisEdit.Services` | `ISettingsService`, `SettingsServiceAdapter` | The fakeable seam over Core's sealed `SettingsService` | 08 §1-3 |
| `KinesisEdit.Services` | `IEditorViewModelFactory`, `EditorViewModelFactory` | Which editor a device opens into | 10 "Opening a device" |
| `KinesisEdit.Converters` | `HexColorToBrushConverter`, `HexColorToColorConverter` | `#RRGGBB` → brush; `#RRGGBB` ↔ `Color` (two-way, for the picker) | 07 §2.1, §4 |

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

State: `IsLoading` (true until the first load finishes), `IsBusy` (a save is in flight), `Layout`, `Layers`, `SelectedLayer`, `SelectedKey`, `ListeningKey`/`IsListening`, `ProfileCaption`, `ModifiedKeyCount`/`RemapCounterCaption`, `InvalidLineMessages`/`HasInvalidLines`, `BoardWidth`/`BoardHeight`, `Tabs`/`SelectedTab`.

Commands and what gates them:

| Command | Enabled when |
|---|---|
| `SelectKeyCommand(KeyboardKeyViewModel?)` | always (the click contract below) |
| `SelectLayerCommand(KeyboardLayerViewModel)` | always |
| `SelectTabCommand(EditorTabViewModel)` | `tab.IsEnabled` — `Keys` and `Settings` today |
| `BeginRemapCommand` | `SelectedKey is not null && SelectedKey.CanEdit && !IsLoading && !IsBusy` |
| `CancelRemapCommand` | `IsListening` |
| `ResetKeyCommand` | `SelectedKey is not null && SelectedKey.CanEdit` |
| `ResetLayerCommand` | `SelectedLayer is not null` |
| `ResetLayoutCommand` | `Layout is not null` |
| `SaveCommand` (async) | `_session is not null && _session.CanSave && !IsDemoMode && !IsLoading && !IsBusy` |

`SelectedTab` is a two-way-bindable property whose setter runs the same guard as the command, so a binding cannot open a tab the command refuses — and the guard now refuses an **absent** tab too, not just a disabled one. **Switching tabs cancels an in-flight remap**, because listening belongs to the keyboard picture and capture is never left running (invariant 4).

### The tab strip is device-driven

`EditorTabViewModel.CreateAll(DeviceDefinition device, bool isLightingEnabled)`:

| Tab | Present when | Enabled |
|---|---|---|
| `Keys` | always | yes |
| `Macros` | always | no — issue #15 |
| `Lighting` | `device.Lighting.Kind != LightingKind.None` — **omitted** on the Freestyle Pro and Advantage2 | `LightingTabViewModel.IsSupported(device)` — the RGB only; the TKO and Adv360 tabs are present but dark |
| `Settings` | `device.Settings != SettingsCapability.None` — **omitted** on the Savant Elite2, CROSSFIRE and Advantage 360 Professional | yes |

**Omitted, not disabled, is the point**: a device with no lighting hardware has no led file to edit and a device with no settings capability has no settings file, so a greyed-out tab would promise something that will never arrive — whereas `Macros` is greyed out precisely because it *is* arriving, and the TKO/Adv360 Lighting tabs are greyed out because their editors are issues #40/#41. **The lighting flag is a device-level question on purpose**: the editor's constructor runs before any profile is read, and demo mode never reads one, so it cannot be `session.Lighting is LightingModel`. **Enablement is therefore fixed at construction** — `EditorTabViewModel.IsEnabled` is get-only and `SelectTabCommand`'s predicate reads nothing but its parameter, so `NotifyCommands()` deliberately does *not* re-ask it; a tab that had to change state would need `IsEnabled` to raise change notification first. Two `EditorTabViewModelTests` cases assert, for every catalog device, that the Settings tab exists exactly when `KeyboardSettingsRows.Create` yields rows and that the Lighting tab is enabled exactly where the panel can edit the led file — a tab and its panel can never disagree.

### The Settings tab — `KeyboardSettingsViewModel`

The device-side settings of spec 08 §5, hosted as `KeyboardEditorViewModel.Settings` and rendered by `KeyboardSettingsView` through `ViewLocator`. It is always constructed (cheap, no I/O) and only reachable when the tab exists.

- **Rows come from the capability, never from a device id.** `KeyboardSettingsRows.Create(SettingsCapability)` emits, in spec 08 §2 table order: active profile (1–9), `led_mode` (only in its `ModeString` form), macro speed, status report speed, the v-Drive switch in whichever of its two key forms applies, then game mode / program lock / key clicks / key tones. Every board therefore gets its panel the moment its picture is authored, with no per-device code. Row kinds are `SettingsSliderRowViewModel`, `SettingsToggleRowViewModel` and `SettingsChoiceRowViewModel`, each carrying a reader/writer delegate pair — no row knows a key name, and the `led_mode` options are Core's `LedModeValues.All` (the serializer's own domain) with captions added here.
- **The active-profile row writes through `StartupProfileSettings.ApplyStartupProfile`**, so the paired `led_mode=led<N>.txt` of 08 §5.1 / 07 §1.2 cannot drift from `ProfileSession.SaveAs`.
- **Clamping is the slider's job.** `KeyboardSettingsSerializer` throws on a value outside `SettingsValueRanges`, and an exception must never escape into the UI, so `Value` is clamped in its setter *and* in `ApplyTo`. The "Disable" checkbox writes `0`; it only exists where the slider floor is above 0 (`SettingsCapability.MacroSpeedMinimum`).
- **A key absent from the file reads as its unset default** — spec 08 §2's "missing keys leave zero/false/empty values": speeds load as *disabled*, toggles as off, and the profile number as 1 (it has no zero). The choice row is the exception, and deliberately so: absent means **unset**, not `Options[0]`. `SelectedOption` is nullable, an unrecognised value (a `led_mode` newer firmware wrote) is held in `UnrecognizedValue` for the `ComboBox`'s `Placeholder`, and an unset row writes **null** — which is exactly what preserves the device's line, because a null property emits no pair and a save only passes managed pairs to `UpdateSettingsFile` (08 §1). Handing the unknown text back to the serializer would preserve nothing: it throws outside `LedModeValues`, taking the whole panel's Save with it. Mapping absent → `0` would switch a Freestyle Edge's backlight fully off on the first save.
- **Advantage2 4MB lock** (08 §5.4): `KeyboardSettingsGate.CanEditKeyboardSettings(DeviceId, VersionFile)` false ⇒ `IsLocked`, every row disabled, Save unavailable, and `SettingsMessageCatalog.Advantage2SettingsDisabledHint` shown verbatim. **Demo mode passes the gate**, as every firmware gate does (`FirmwareGateService`, 09 §2): it supplies `VersionFileInfo.Empty`, and a 2MB diagnosis about an unattached board would be fabricated. Nothing is written there anyway.
- **Demo mode never writes, but it does read** (03 §3.5, 08 §3 — the ban is on *saving*): with a `Location` the file is loaded exactly as usual and `DemoModeHint` explains that Save is unavailable, the same rule the colour picker's custom slots follow. Demo mode is entered for a drive that is merely not writable, so inventing values there would be showing wrong settings for connected hardware. Only a device with **no `Location`** reads nothing, and it says so with `NoDriveHint` rather than claiming the rows are the device's.
- **Save is unavailable until a read has succeeded.** `HasLoadedSettings` — not `!IsLoading`, which is also true after a read that threw — gates `SaveCommand` *and* the rows' `IsEnabled`, so the panel can never write its constructor defaults (`v_drive=manual` among them) over a file nobody managed to read.
- **Load and save are total.** `LoadAsync` (fired by the editor's own `LoadAsync`, idempotent) reports a read failure *inline* rather than stacking a second modal on the editor's, and `StatusMessage` is composed from its two independent parts (demo/no-drive hint, read failure) so one never hides the other; `SaveAsync` mirrors the profile save — `IsBusy`, `ShowLoading`/`HideLoading` in a `finally`, failures through `TryShowMessageBoxAsync` titled `Save Settings`, success as a toast carrying Core's `SettingsSavedTitle`/`SettingsSavedMessage`.

### The Lighting tab — `LightingTabViewModel`

The per-layer LED editor of spec 07 §3/§4, hosted as `KeyboardEditorViewModel.Lighting` and rendered by `LightingTabView`. Like the settings panel it is always constructed and only reachable when its tab is enabled; unlike it, **it has no save path of its own** — `ProfileSession.Save` serializes whatever `IProfileSession.Lighting` holds, so mutating that model *is* the save (see [profiles.md](profiles.md)). The editor's `Apply` hands it the model and the same `KeyboardLayerViewModel`s the Keys tab draws.

- **Every rule is Core's.** Mode membership and gating are `LightingAvailability.IsKeyBacklightModeAvailable`, captions are `LightingModeDefinition.DisplayName`, directions are `LightingAvailability.GetKeyBacklightDirections` (device-aware, so Fireball shows no arrows on the RGB), zones are `LightingZoneCatalog`, speed bounds are `LayerLightingState.MinimumSpeed`/`MaximumSpeed` ([lighting.md](lighting.md)). No mode name is spelled out in this layer.
- **`LightingPanelVisibility.For(deviceId, mode, isLayerCustomizationAvailable)`** is the §3 "which panels each mode shows" table, derived from the catalog flags rather than restated: effect colour = `WritesEffectColor || HasPerKeyColors` (§3 lists Freestyle and Breathe alongside the eight that write one), base colour = `HasBaseMonoLine` **and** the firmware gate, speed = `WritesSpeed`, direction = a non-empty availability list, per-key/zones/Reset All = `HasPerKeyColors`. It is one value on the view model, so the tab has one property instead of seven and the matrix is unit-tested on its own.
- **Two layers, fully independent** (§4). `Layers` is always Top + Fn — the two a led file describes — regardless of how many layers the picture has. A layer switch re-reads mode, colours, speed, direction and the picture from the new state; the Fn pill **and** the base-colour swatch sit behind `LightingAvailability.IsFnLayerLightingAvailable` (`LightingLayerCustomization`, LED ≥ 1.0.44), read from `DeviceSnapshot.Firmware`. Demo mode reports every gate as passing.
- **One picker, two targets.** `EffectColor`/`BaseColor` are `LightingColorSlotViewModel`s carrying a reader/writer pair onto `LayerLightingState` (the same shape the settings rows use); `SelectColorSlotCommand` points the picker at one, and the picker's `ColorChanged` writes through. A mode change re-selects the first *visible* slot, so the picker never edits a swatch the mode does not have — and the picker itself is bound to `Panels.ShowsAnyColor`, so in Disabled/Spectrum/Wave/Pulse it is **hidden** rather than rendered with nothing to write to (`ShowsAnyColor` covers per-key painting too, because a per-key mode's effect colour is the colour a click paints with).
- **Per-key painting and zones use `Picker.Color`** (§4: "clicking a key applies the currently selected picker color"). Keys are addressed by **memory key code** (`key.Key.OriginalKey.Code`), and **black clears the entry** — `SetKeyColor`'s contract (§2.1) is honoured rather than worked around, which also makes the black premixed swatch the eraser. **Zone codes are authored against the top layer**, so on the Fn layer each is re-resolved top code → top index → the Fn key at that index (§2.4 item 6): the Function zone lands on the Fn layer's media keys, not on F1–F6 it does not have.
- **A direction the mode rejects is normalised** on mode change (§2.4 item 5 writes it as the default anyway), so the control and the file agree before the save rather than after it.
- **"Reset All"** is offered for the per-key modes only (§4) and calls `LayerLightingState.ClearKeyColors()` after a Yes/No box whose message is the spec's wording verbatim. A box that cannot be shown erases nothing.
- **Repainting.** After every model write the tab rebuilds `KeyColorOverlay.Build(...)` and pushes it through `KeyboardLayerViewModel.ApplyColorOverlays`, which is whole-layer (a key the map no longer mentions loses its strip). Both tabs share the cap view models, so a recoloured key repaints on the Keys tab too.

#### The colour picker — `ColorPickerViewModel` + `Controls/ColorPickerView`

`ColorPickerView` wraps Avalonia's `ColorView` (package `Avalonia.Controls.ColorPicker`, pinned to the same **11.3.12** as every other Avalonia package) for the HSV ring, the R/G/B sliders and the hex field, and adds the two things the stock control has no notion of:

- **The ten premixed swatches of §4**, in the spec's order (white, yellow, red, lime, blue, fuchsia, orange 255/128/0, azure 0/128/255, aqua, black), as a static list on the view model.
- **The twelve custom slots**, `cust_color_1`…`cust_color_12` in `app_settings.txt`. They load through `ISettingsService` on `LoadAsync` (any drive, even in demo mode — spec 08 §3 bans *saving*, not loading) and store through a **read-modify-write**: load the whole `AppSettings`, `WithCustomColor`, save, so the notification hide flags the suppression store owns survive. `LedColorConverter` does the `LedColor` ↔ `SettingsColor` conversion. `Add to Custom Colors` fills the first empty slot, then rotates. **`CanStoreCustomColors` is false in demo mode and without a drive**, and the command is disabled rather than silently forgetting the slot. Every I/O failure is swallowed — a picker preference is not worth a dialog.

`ColorView` ships its control theme in its own assembly, so `App.axaml` includes `avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml` after `FluentTheme`; without it the picker renders untemplated. The `#RRGGBB` ↔ `Color` conversion is `HexColorToColorConverter` (two-way, alpha dropped, `BindingOperations.DoNothing` on a non-colour), so no Avalonia colour ever reaches a view model.

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
| *K* listening | tab switch | *K* selected | `capture.Stop()` |
| any | `SelectKeyCommand(null)` | nothing selected | `capture.Stop()` |
| *K* listening | `Dispose` | — | `capture.Stop()`, unsubscribe |

`CanBeginRemap` is false for a locked position (`CanEdit == false`, 05 §5.3), while loading, and while saving — clicking a locked key twice selects it and does nothing else. `BeginRemapCommand` is the same entry point without the click (bindable, and what the tests use).

**Escape is not the cancel key — this is the single most surprising thing about the workflow.** While a key listens, the capture service previews the window's key events in the tunnel phase and swallows every physical key it resolves, Escape included, so **Escape is assigned like any other key**: a keyboard must be able to carry an Escape remap. Cancelling is a pointer action — the *Cancel* button beside the listening banner, clicking the listening key again, clicking another key, or switching layer. `KeyboardEditorView` does register a tunneled `KeyDown` handler (`handledEventsToo: true`, as in `MessageBoxWindow`) that routes Escape to `CancelRemapCommand`, but it is a **safety net, not the path**: the capture handler sits on the `TopLevel` above it on the same tunnel route and has already consumed the keystroke and left listening state by the time the view sees it, leaving `CanExecute` false. It fires only when capture did *not* consume the Escape — capture suspended by text-input focus, for instance — which is exactly the case where listening would otherwise get stuck.

**Applying a captured keystroke goes through the editor path `KeyboardKey.Remap(keystroke.Key)`**, which means capturing a key's *own original action* clears the remap (04 §2.1), exactly like the legacy apps. `ResetKeyCommand` deliberately calls `ClearRemap()` instead: `Remap(OriginalKey)` also clears the position's tap-and-hold and multi-modifier as a side effect ([keyboard-model.md](keyboard-model.md), "Watch out"), which is not what "reset this key's remap" means. `ResetLayerCommand`/`ResetLayoutCommand` call Core's `KeyboardLayer.Reset()`/`KeyboardLayout.Reset()` (which clear all four rule kinds; `KeyColor` survives) and then re-read every cap.

`ModifiedKeyCount` (spec 10's `Remap (n)` counter) is `KeyboardLayout.ModifiedKeyCount`, recomputed after a load, a captured remap and each of the three resets — the only places this module writes to the model.

### Save

`SaveAsync` cancels any listening first, then: `IsBusy = true` → `ShowLoading("Saving...")` → `Task.Run(session.Save)` → `HideLoading()` + `IsBusy = false` in a `finally` → report. Everything else is Core's ([profiles.md](profiles.md)): validation gating, file writes, eject, post-save wording.

- **Threw** → message box `Save Profile` / `SaveErrorMessagePrefix + message`.
- **`Success == false`** → message box listing `SaveRejectedMessage` followed by one line per `ModelViolation.Message` (04 §5.3's gate, surfaced rather than silently dropped).
- **Success with a `PostSaveMessage`** → toast titled `Save Profile` carrying the device's refresh wording verbatim; no message, no toast.

Both boxes go through `TryShowMessageBoxAsync`, which swallows a box that cannot be put on screen — the editor state already carries the outcome, and a closed owner window must not bring the app down.

## Presentation rules

**`KeyCaption`** — the caption of `KeyboardKey.ModifiedOrOriginalKey` (so a remapped cap reads its new action), resolved in this order: `GlyphText` → `MacDisplayText` when `OperatingSystem.IsMacOS()` → `GetDisplayText(dialect)` → **the dialect's file token when all of those are blank**. That last step exists because 05 §3.11 registers `hk0`–`hk8` with `' '` (the physical caps are unlabelled), which would otherwise draw a column of indistinguishable empty caps; the token is what the user sees for that position in the layout file, which makes it the honest fallback rather than an invented label. `\n` is preserved verbatim — splitting it into a two-line cap (05 §1.1) is the view's job. `KeyCaption.For(key, dialect, isMacOs)` takes the platform as a parameter so both directions are testable; `IsMacOs` is resolved once as a static.

**`LayerCaptions`** — the geometry's `Name` is the spec-literal file-side name (`Qwerty-top`/`Qwerty-keypad`), which is not what an editor shows. Gen2 keeps the raw names (Base, Keypad, Fn1–Fn3 are already presentation-ready); Legacy/Gen1 map index **0 → `Top`**, **1 → `Fn`** (spec 10 describes both switches that way); anything else falls back to the raw name.

**`KeyColorOverlay`** — projects a profile's lighting onto the picture as `key index → #RRGGBB`. It is more than a lookup for two reasons, which is why it lives in one tested place: `LayerLightingState.KeyColors` is keyed by **memory key code** (07 §4), not by key index, so every entry is resolved through `KeyboardLayer.FindByOriginalKeyCode`; and `KeyboardKey.KeyColor` exists but **no parser ever fills it** (the led file is parsed into the lighting model, never into the layout model), so reading the key would always show nothing — which is also why `KeyboardKeyViewModel.RefreshFromModel()` cannot reach the overlay and the Lighting tab pushes a fresh map in through `KeyboardLayerViewModel.ApplyColorOverlays` instead. Empty unless the device is `LightingKind.PerKeyRgb` *and* `IProfileSession.Lighting` is a `LightingModel` (TKO's `TkoLightingModel` is deliberately not matched yet) *and* the layer is one of the two that model describes — layout layer **0 ↔ `LightingModel.TopLayer`**, **1 ↔ `FnLayer`**. `LedColor.IsBlack` is "no colour" (07 §2.1) and yields no entry; an unknown key code is skipped. `ToHex`/`TryParseHex` are the module's `LedColor` ↔ `#RRGGBB` pair, used in both directions by the picker.

## Seams and composition

- **`IProfileSession` / `IProfileSessionFactory`** exist because Core's `ProfileSession` is sealed with a static `Load` — unsubstitutable in a test. `ProfileSessionAdapter` is a pass-through with no behaviour of its own; `ProfileSessionFactory` calls `ProfileSession.Load` and wraps it. Same shape as `IMessageBoxPresenter`.
- **`ISettingsService` / `SettingsServiceAdapter`** are the same seam over Core's sealed `SettingsService` ([settings.md](settings.md)), and the app builds **one** instance: the settings panel and the notification-suppression store both go through it, so `app_settings.txt` has a single reader and a single writer.
- **`EditorViewModelFactory`** is the one place that picks an editor: `DeviceId.SavantElite2` → `SavantElitePedalViewModel` ([savant-elite.md](savant-elite.md)); otherwise "can this device be drawn?", answered with `VisualCatalog.TryGet(id, out _) && GeometryCatalog.TryGet(id, out _)`, → `KeyboardEditorViewModel` or `EditorPlaceholderViewModel`. The shell asks for one and swaps in whatever it gets, which is also what keeps every editor's dependencies (the profile-session factory, the capture-service accessor, `PedalFileService`) out of `MainWindowViewModel`.
- **The capture service is resolved through a `Func<IKeystrokeCaptureService>`**, not held: `AvaloniaKeystrokeCaptureService` attaches to the shell `TopLevel`, which does not exist while `App.BuildServices` wires the graph — the same ordering problem the message-box presenter solves with its `Func<Window?>` owner. `App` builds it lazily on first use, keeps the single instance, and disposes it in `OnExit` **after** the shell (which closes and disposes the editor, which stops capture first).
- `MainWindowViewModel` delegates the choice to `IEditorViewModelFactory`, keeps `Editor` typed as the abstract `DeviceEditorViewModel?`, fires `editor.LoadAsync()` once after the view swap, and disposes the outgoing editor in `CloseEditor()` on both navigate-home and re-open.

## Spec strings and deliberate deviations

From the spec: the `Remap (n)` counter and the `Reset Key` / `Reset Layer` / `Reset Layout` button captions (spec 10); the post-save toast, which is Core's `ProfileSaveMessageCatalog` wording verbatim; and on the Lighting tab the `Reset All` button, its confirmation `Do you want to erase color assignments for each key` (07 §4, quoted exactly, missing question mark included), the mode captions (Core's `LightingModeDefinition.DisplayName`, so `Disable` not "Disabled"), the `Horizontal` / `Vertical` relabelling of Rebound's arrows (07 §3), and the eight RGB zone names (Core's `LightingZoneCatalog`).

Everything else here is this app's wording: `Profile n`, `Saving...`, `Loading profile...`, the `Save Profile` / `Load Profile` dialog titles, `The profile was not saved because it exceeds the device's limits:`, `Line <n>: <text>`, `Press a key to assign it to the highlighted key.`, `Some lines of this profile could not be applied`, and the tab captions `Keys` / `Macros` / `Lighting` / `Settings` (spec 10's RGB app has `Layout` / `Lighting` tabs with the macro editor as an in-window panel; the four-tab shape is chosen so #15/#16 have somewhere to land). The strings the view models own are consts on `KeyboardEditorViewModel`/`EditorTabViewModel`/`LayerCaptions` and asserted by tests; the ones only the view uses are XAML literals.

Recorded deviations:

1. **No per-key Done/Cancel commit step.** Spec 10's Adv2 flow has "**Done** validates and marks the layout modified; **Cancel** reverts". Here a captured keystroke is applied to the model immediately, and Cancel means "stop listening" only. Validation happens once, at save time, where 04 §5.3 puts it.
2. **The layer switch is built from the model's layers**, not from a two-state toggle — two pills on a Freestyle Edge RGB, five on an Advantage 360 (#41) with no change to the view.
3. **Invalid lines are an inline collapsed block**, not the Adv360's modal "Invalid Lines dialog" (spec 10). Same content (04 §5.2), less interruption; `LayoutInvalidLine.Keep` is untouched, so they are still re-serialized as Core decides.
4. **The colour overlay is a strip under the caption**, not the cap background: the cap background already carries the four remap states, and 07 §2.1 colours must stay distinguishable next to them.
5. **The settings panel is one list of rows for every device**, not the four different forms of 08 §5.1–§5.4 (a modal dialog on RGB/TKO/Adv360, controls embedded in the main window on FS/Adv2). Same keys, same ranges, one shape — and it is a tab, so nothing is modal and nothing saves as you type.
6. **The status-report slider is always 1–4 plus a "Disable" checkbox** (the §5.1/§5.2 shape). §5.3/§5.4 draw it as a plain 0–4 slider for the FS boards and the Adv2; both write exactly the same values, so one control serves every device. The macro-speed slider keeps its per-device floor, because Core supplies it (`SettingsCapability.MacroSpeedMinimum`).
7. **The RGB/TKO settings panel shows a v-Drive row that §5.1's control table does not list.** It comes from `SettingsCapability.VDriveSetting`, i.e. spec 08 §2's written-by column, where `v_drive` *is* an RGB/TKO key — the panel is capability-driven, so it renders every key the app writes rather than the legacy dialog's subset. Worth knowing because it is also the row with the nastiest failure mode: `v_drive=manual` stops the drive auto-mounting, which is why the panel refuses to save anything until a read has actually succeeded.
8. **"Settings Saved" is a toast, not a modal** — the same deviation the profile save already records. Its wording is Core's `SettingsMessageCatalog` verbatim.
9. **The lighting editor is a tab, not the legacy app's second window mode**, and it has **one Save — the editor's**. Spec 07 §1.3 has the app write layout then led then eject in one sequence, which is exactly `ProfileSession.Save`; there is no separate "save lighting" action and no `savelighting_msg` notification.
10. **One picker, not two.** Spec 07 §4 describes a shared *dialog* with 10 premixed + 6 custom slots and a *main panel* with 20 premixed + 12 custom. This app has one inline picker with the 10 named swatches (the 20 are never enumerated in the spec) and the full 12 custom slots, because those are the twelve keys `app_settings.txt` actually persists.
11. **The two colour swatches are selectable targets**, not two separate pickers: clicking Effect Color or Base Color points the single picker at it. The spec does not say how the legacy panel routed the picker, and one picker is what fits beside the mode menu.
12. **No animated effect preview** (07 §7) — the mode list is captions only.

## Load-bearing invariants

1. **One visual per device, all layers.** Layers differ only in the tokens bound to a position (05 §7.4), so the picture is rebuilt per layer from the *same* rectangles; a per-layer visual would be data waiting to disagree with itself.
2. **The join is by key index, and mismatches degrade.** `KeyboardVisual.TryGetKey(key.Index, …)`; a miss skips the cap. Core's `Geometry/Visual` tests assert set equality with the logical geometry in both directions, so a mismatch is a data bug caught in CI, not a runtime crash.
3. **Every model write is followed by `RefreshFromModel()`.** Core announces nothing. The colour strip is the exception that proves it: it lives in the lighting model, so it is refreshed by `ApplyColorOverlays` — but a lighting write that skips it leaves a stale picture just the same.
4. **Capture is never left running.** It is started only when a key enters listening, and stopped on the keystroke, on cancel, on a layer switch, on a save, and on `Dispose` — the app-wide service swallows keystrokes from the whole window while it is on.
5. **Escape is a remappable key, not a shortcut** (see the remap section). Do not add an accelerator that steals it.
6. **Captured keystrokes go through the editor path, resets through `ClearRemap`.** `Remap` implements 04 §2.1's remap-to-self clearing; `Remap(OriginalKey)` would additionally destroy tap-and-hold and multi-modifiers.
7. **View models expose enums and strings, never brushes** ([app-shell.md](app-shell.md), invariant 6). The overlay travels as `#RRGGBB` and becomes a brush in XAML through `HexColorToBrushConverter`, which converts anything unparseable to `null` rather than throwing inside a binding.
8. **Demo mode never writes** (03 §3.5): no session is created at all, so `SaveCommand` cannot become available by any other property moving. The Lighting tab still opens on an **in-memory** `LightingModel` so it stays explorable, and the picker's custom slots are read-only there. Reading, on the other hand, is *not* forbidden — the picker's slots and the settings panel both load whenever a `VDriveLocation` exists, because demo mode is entered for a drive that is merely not writable and showing invented values for connected hardware is worse than showing none.
9. **A panel never writes what it did not read.** The settings panel's `HasLoadedSettings` gate exists because its rows have plausible-looking defaults that are pure invention; anything else on this tab that grows a save path needs the same "a read succeeded" flag, not `!IsLoading`.
10. **`LoadAsync` is total and idempotent.** The shell fires and forgets it; an escaping exception would be an unhandled crash, and a second call must not re-read the drive.
11. **Adding a device is adding data.** `EditorViewModelFactory`, `KeyboardView`, `KeyCapView` and `KeyboardPanel` contain no device identity; only `VisualCatalog` does.

## Testing

App-layer tests are **view-model level and run without an Avalonia runtime** (the project-wide rule — `KinesisEdit.Tests`), so the state machine, load/save orchestration and the three presentation rules are all covered while the XAML is guarded by compiled bindings and the build alone. Placement data itself is tested in Core (`KinesisEdit.Core.Tests/Geometry/Visual`: index-set equality with the geometry per layer, unique indices, no overlapping rectangles, bounds, cluster assignment).

The Lighting tab's own coverage (`LightingTabViewModelTests`, `ColorPickerViewModelTests`) is the per-mode panel matrix against the §3 table, the firmware gates in both directions, layer independence, the per-key set/clear contract, zone application on both layers, the "Reset All" confirmation, custom-slot persistence, and one **acceptance** case that serializes the edited model with `LedFileSerializer` and re-parses it — `ProfileSession.Save` is exactly that plus a file write, so it is the app-layer half of "a lighting edit survives Save → reload".

`KeyboardSettingsViewModelTests` carries the same acceptance shape for settings, in both `led_mode` forms: the edited rows are saved, run through the **real** `KeyboardSettingsSerializer` into `key=value` lines, re-parsed with `KeyboardSettingsParser` and loaded into a second panel. That is where the asymmetry gets caught — the active-profile slider leaves as `startup_file=layout3.txt` (+ `led_mode=led3.txt`) and comes back as the number `3`. The rest of its coverage is the per-device row sets, clamping, the 4MB lock and its demo-mode exception, the "nothing is written until a read succeeded" gate, and the choice row's unset/unrecognised states.

Fakes (`KinesisEdit.Tests/Services`): `FakeProfileSessionFactory` (records every `Load`, hands back a staged `FakeProfileSession`, or throws like a vanished drive), `FakeProfileSession` (settable `Lighting`/`InvalidLines`/`ProfileNumber`/`CanSave`, counts `Save` calls, returns or throws a staged result), `FakeSettingsService` (staged `KeyboardSettings`/`AppSettings`, records every save with its location and version file, throws on demand on either file), `FakeKeystrokeCaptureService` (counts `Start`/`Stop`/`Dispose`, exposes `HasSubscribers`, and `RaiseKeystroke(KeyDefinition)` pushes a keystroke in as the real service would), plus the shell's existing `FakeNotificationService`. `TestDevices.CreateSettingsService(fileService)` hands back the **real** adapter where a test asserts on the file that was touched. `KinesisEdit.Tests/ViewModels/TestLayouts.cs` holds the fixtures the catalogs cannot supply: a layout with a locked position (the RGB geometry has none), single-layer layouts from tokens, and small hand-built `KeyboardVisual`s for the join edge cases.

`HexColorToBrushConverter` is tested directly — it touches `Avalonia.Media` but needs no app instance.

Uncovered by tests, on purpose: `KeyboardPanel`'s measure/arrange arithmetic, the style classes, and the `KeyboardEditorView` Escape handler all need a UI runtime — they are hand-verified (`dotnet run --project src/KinesisEdit`).

## Deliberately not here

- **No action palette / token picker** (spec 10: right-click or *Special Actions* → categorized popups of F13–F24, keypad, multimedia, mouse clicks, delays, alt layouts, Hyper/Meh) — a captured physical keypress is the only way to assign a key today. Issue [#15](https://github.com/migus88/kinesis-edit/issues/15).
- **No macro recording, macro panel or macro repository**, and no `Macro (n)` counter — issue #15. `KeyboardKeyViewModel.CanAssignMacro` is already exposed for it.
- **No tap-and-hold and no multi-modifier UI** (11 §11.1, §11.2) — issue #15. The model supports both, and `ResetKeyCommand` is careful not to destroy them.
- **No TKO edge-lighting editor and no Advantage 360 indicator editor.** The Lighting tab edits the plain two-layer key-backlight model only (`LightingTabViewModel.IsSupported`: per-key RGB **without** an edge strip = the Freestyle Edge RGB). The TKO's key backlight + 33-zone edge tab needs its board picture first (issue [#40](https://github.com/migus88/kinesis-edit/issues/40)) and the Adv360's six-indicator editor needs its own (issue [#41](https://github.com/migus88/kinesis-edit/issues/41)); both boards' tabs are therefore present but disabled, and Core's `TkoLightingModel`/`Advantage360LightingModel` deliberately do not light the tab up.
- **No "Lighting Expansion Pack" dialog** (07 §3, §2.6) — `LightingAvailability.HasNoFnLayerLines` and `ExpansionPackDefaults` exist in Core, but nothing offers to mirror the Top layer into Fn or to write the factory pack across all nine profiles.
- **No animated effect previews** (07 §7) and no per-key preview of an effect — the picture shows stored per-key colours only.
- **No `savelighting_msg` suppression** and no separate lighting save — lighting rides the profile save (07 §1.3).
- **No settings beyond the keys spec 08 §2 lists.** The reserved keys (`thumb_mode`, `power_user`, `country`, …) are parsed into state and have no control, as Core intends; there is no eject after a settings save, no "Do you want to save changes?" prompt on leaving the tab, and no app-settings dialog for the twelve `*_msg` flags.
- **No profile picker, Save As, New, Import/Export, or Diagnostic** (spec 10) — the editor always opens `LayoutScheme.FirstProfileNumber` and `SaveCommand` writes back to that same slot. `ProfileSession.SaveAs` already exists in Core ([profiles.md](profiles.md)).
- **No unsaved-changes guard.** `IProfileSession.IsDirty` is exposed on the seam but nothing consumes it: Home ends the session without asking (spec 10's Adv2 "Do you want to save changes?" prompt is unimplemented), and there is no undo buffer.
- **No `Keyboard Connection Lost` dialog** (03 §3.5) — a drive that vanishes mid-save surfaces as the generic save-error box. Still owned by a later issue, as [app-shell.md](app-shell.md) records.
- **No alternate-layout generators** (Dvorak/Colemak/Workman onto chosen layers, spec 10) and **no key-to-key copy/paste** (`KeyboardKey.CopyFrom` exists in Core, unused here).
- **No per-device visuals beyond the Freestyle Edge RGB** — issues #39–#42, data only. The TKO's 33 edge lighting zones (`KeyCluster.EdgeZone`, `KeyboardLayer.EdgeKeys`) arrive with #40 and are lighting zones, not typing keys.
