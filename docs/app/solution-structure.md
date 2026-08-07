# Solution structure

Solution: `src/KinesisEdit.sln` (classic .sln). Four projects, all `net10.0`, `Nullable` + `ImplicitUsings` enabled.

## Projects and dependency direction

| Project | Role | References |
|---|---|---|
| `src/KinesisEdit/` | Avalonia 11 desktop app (`OutputType=WinExe`, compiled bindings on). Entry point `Program.cs`; composition root `App.axaml.cs`; shell window `MainWindow` titled "Kinesis Edit". | `KinesisEdit.Core` |
| `src/KinesisEdit.Core/` | Domain library: device model, parsers/serializers, drive discovery. **No UI/Avalonia dependencies — keep it that way.** | (none) |
| `src/KinesisEdit.Core.Tests/` | xUnit tests for Core. Test files mirror Core's folder structure. | `KinesisEdit.Core` |
| `src/KinesisEdit.Tests/` | xUnit tests for the app layer: services, view models, converters, `ViewLocator`, **plus the headless UI suites** (`Headless/`, `Design/`, `Controls/`). Test files mirror the app's folder structure; hand-rolled fakes, no mocking library. | `KinesisEdit` |

Dependency direction is one-way: `KinesisEdit.Core.Tests` → Core ← app ← `KinesisEdit.Tests`. Domain logic goes in Core so it stays testable and UI-free; app-layer logic goes in services and view models so it stays testable without a UI toolkit.

**Most of `KinesisEdit.Tests` is still toolkit-free** — services and view models are written so they need no UI runtime, and those suites start none. The exception is deliberate: `Headless/`, `Design/` and `Controls/` boot a real Avalonia application through `Avalonia.Headless` to cover the views, the styles and the design tokens, which cannot be exercised any other way. See [`design-system.md`](design-system.md) § "Testing UI work in this repo".

## Commands (from repo root)

- Build: `dotnet build src/KinesisEdit.sln`
- Test: `dotnet test src/KinesisEdit.sln`
- Run the app: `dotnet run --project src/KinesisEdit`

## SDK pinning

`global.json` at the repo root pins SDK `10.0.100` with `rollForward: latestFeature` (any 10.0.1xx works). `.editorconfig` at the repo root encodes the coding conventions (block-scoped namespaces, Allman braces, naming rules).

## CI

`.github/workflows/ci.yml` ("CI"): matrix over macOS/Ubuntu/Windows, runs `dotnet build` + `dotnet test` on the solution in Release. SDK comes from `global.json` via `actions/setup-dotnet`. **The automatic triggers are currently commented out** (`pull_request` on `main`, `push` to `main`), leaving only `workflow_dispatch` — disabled temporarily in commit 28c9e2c; uncomment them to restore CI on pull requests.

## App namespaces

`KinesisEdit` is the app shell and device dashboard — see [`app-shell.md`](app-shell.md):

- `Services` — detection loop (`DeviceMonitorService` → `DeviceSnapshot`), notifications (message box, toast, loading, `Don't ask this again` suppression), the active-device session, eject, URL launching, UI-thread dispatch, and the editor's native folder/file pickers with the profile importer behind them (see [`feature-dialogs.md`](feature-dialogs.md)). Toolkit-free apart from the bridging services `AvaloniaUiDispatcher`, `AvaloniaFolderPickerService`, `AvaloniaFilePickerService` and their shared `StorageProviderHost` helper — `MessageBoxPresenter` stopped being one when the message box became an in-app overlay, and is now a lazy forwarder onto the shell's `NotificationOverlay`.
- `ViewModels` — `MainWindowViewModel` (navigation, the unsaved-changes guard on all three exits, status indicator), `DashboardViewModel`/`DeviceCardViewModel`/`NoDeviceViewModel`, the editors hanging off `DeviceEditorViewModel` (`EditorPlaceholderViewModel`, the keyboard editor `KeyboardEditorViewModel` with its layer/key view models — see [`keyboard-editor.md`](keyboard-editor.md) — and the pedal editor `SavantElitePedalViewModel` with its row and Special Actions view models — see [`savant-elite.md`](savant-elite.md)), plus the notification view models. They expose enums and strings, never Avalonia brushes.
- `Views` — the `.axaml` for each of the above plus `MainWindow`, `NotificationOverlay` and the three surfaces it hosts (`MessageBoxView`, `ToastView`, `LoadingView`). Resolved by name through `ViewLocator`. The app has exactly one `Window`. Also here: `KeystrokeCaptureSpikeWindow`, the issue-#12 capture harness, which is not part of the shipped UI — `App` opens it as the main window instead of the shell only when started with `--keystroke-spike` (see [`keystroke-capture.md`](keystroke-capture.md)).
- `Themes`, `Styles`, `Assets/Fonts` — the design system (see [`design-system.md`](design-system.md)). `Themes/` holds the tokens (`Tokens.axaml` colour roles in `Dark`/`Light` theme dictionaries, `Geometry.axaml` shape, `Typography.axaml` families + type scale, `Motion.axaml` the motion budget, `Controls.axaml` the FluentTheme alias map); `Themes/Icons.axaml`, `DeviceArt.axaml` and `LightingMarks.axaml` the colourless icon geometry; `Styles/` the shared style layer split by concern (`Text`, `Surfaces`, `Buttons`, `Keyboard`, `Editor`, `Icons`); `Assets/Fonts/` the embedded IBM Plex Sans/Mono TTFs plus their SIL OFL licence, shipped as `AvaloniaResource`. `App.axaml` merges them and defines nothing itself — **no colour, size or style may be added to it**. `Themes/Controls.axaml` must stay merged into `Application.Resources`, never `Application.Styles`, or FluentTheme's own palette wins.
- `Services` also carries the motion switch: `IMotionSettings`/`MotionSettings`, `IReduceMotionDetector` (`MacOsReduceMotionDetector` via Core's `IProcessRunner`, `UnsupportedReduceMotionDetector` elsewhere) and `MotionResourceBinder`, resolved once by `App.OnFrameworkInitializationCompleted` before any window exists.
- `Converters` — `EnumMatchConverter`, `MenuItemToggleTypeConverter`, `HexColorToBrushConverter`, `HexColorToColorConverter`, plus the icon lookups `DeviceArtConverter`/`LightingModeMarkConverter` over the shared `GeometryResources`: the enum → style-class/toggle-type/geometry and `#RRGGBB` → brush/`Color` mapping that keeps colors and control-theme detail in XAML. (The glyph converter that used to sit here retired with issue #87 — dialog marks are geometry now.)
- `Input` — the only platform-aware input code: `AvaloniaKeystrokeCaptureService` (tunnel-phase key preview on a `TopLevel`, delegating every decision to a Core `KeystrokeCaptureSession` and contributing only the "is a `TextBox` focused" verdict plus the `SuspendOnTextInputFocus` switch), `AvaloniaPhysicalKeyBridge`, and the harness row view `CapturedKeystrokeView`. It is the worked example of the layering rule — every capture rule lives in `KinesisEdit.Core.Input` where tests can reach it, and only the `TopLevel` adapter may reference Avalonia.

## Core namespaces

`KinesisEdit.Core` currently contains the static domain-data layer (see [`domain-data.md`](domain-data.md)), the runtime keyboard model built on top of it (see [`keyboard-model.md`](keyboard-model.md)), the firmware module (see [`firmware.md`](firmware.md)), the v-Drive services (see [`vdrive.md`](vdrive.md)), the lighting module (see [`lighting.md`](lighting.md)), the settings engine (see [`settings.md`](settings.md)), the layout-file engine (see [`layout-files.md`](layout-files.md)), the profile load/save/eject orchestration built on top of all of them (see [`profiles.md`](profiles.md)), and the Savant Elite2 pedal-file engine (see [`savant-elite.md`](savant-elite.md)):

- `Devices` — device catalog: `DeviceCatalog`/`DeviceDefinition` (volume labels, marker files, v-Drive paths, and the per-family capabilities `MacroCapability`, `TapAndHoldCapability`, `LightingCapability`, `SettingsCapability`, `SupportsMultiModifiers`, with `ValueRange` for their bounded values; specs 02 and 03 §1–4, 04 §5.3, 06 §6, 08 §2, 11 §11.1–11.2) plus `FirmwareVersion`, the immutable value type for version-file parsing and comparison (spec 09 §1.1).
- `Keys` — master key-token registry: `KeyRegistry` with 1282 entries in spec registration order across the three token dialects (spec 05 §3, §7).
- `Geometry` — physical layer geometries: `GeometryCatalog`, seven layout families with fully materialized layers (spec 05 §4).
- `Model` — the editable in-memory model: `KeyboardLayout` → `KeyboardLayer` → `KeyboardKey`, plus `Macro`/`Keystroke` and the limit reports of `Validate()` (spec 05 §1, §5, §7.2, §7.4; spec 06; 04 §5.3) — see [`keyboard-model.md`](keyboard-model.md).
- `Firmware` — version-file parsing and firmware feature gating: `VersionFileParser`/`VersionFileInfo`, `FirmwareGateCatalog`/`FirmwareGateService`/`FirmwareState`, `FirmwareSupportUrls` (specs 09 §1–2, 11 §11.1, 12 §1) — see [`firmware.md`](firmware.md).
- `VDrive` (+ `.Discovery`, `.Io`, `.Eject`) — v-Drive discovery (platform volume enumerators, shared scanner, polling monitor), raw 8-bit file I/O with the spec's write rules, and the flush/eject abstraction (spec 03 §2–5; 08 §1) — see [`vdrive.md`](vdrive.md).
- `Lighting` — in-memory lighting model and the `lighting/ledN.txt` parser/serializer for the RGB, TKO, and Advantage360 dialects, plus the mode/zone/indicator catalogs (`LightingModeCatalog` with UI display names, `LightingZoneCatalog`, `IndicatorFunctionCatalog`), the device-aware lighting firmware hooks in `LightingAvailability`, and `LedColorConverter` for the `cust_color` slots (spec 07; 05 §5.5) — see [`lighting.md`](lighting.md).
- `Settings` — the settings engine: typed keyboard-settings and app-settings models with pure line-based parsers/serializers, the Advantage2 4MB write gate, the `StartupProfileSettings` startup/`led_mode` pairing, the `SettingsMessageCatalog` UI strings, and the `SettingsService` load/save binding (spec 08 §1–3, §5; 09 §1.1) — see [`settings.md`](settings.md).
- `Input` — the UI-free keystroke-capture state machines: `PhysicalKeyCode`/`PhysicalKeyMap`, `KeystrokeRecorder`, `KeystrokeCaptureSession` (started/suspended gating), `IKeystrokeCaptureService` (spec 10) — see [`keystroke-capture.md`](keystroke-capture.md).
- `Layouts` — the layout-file engine: `LayoutFileParser`/`LayoutFileSerializer` for `layouts/layoutN.txt` and the Adv2 `qwerty.txt`/`dvorak.txt` (remaps, tap-and-hold, multi-modifiers, macros) across the four in-scope dialects, with invalid-line tracking and the Adv2 keypad-exception map (specs 04, 06; 05 §3.6, §3.12, §5.4) — see [`layout-files.md`](layout-files.md).
- `Profiles` — load/save/eject orchestration for numbered-profile devices (FS Edge/Pro, Edge RGB, TKO, Advantage360): `ProfileSession` (load, `IsDirty`, `CanSave`, `Save`/`SaveAs`), `ProfileSaveResult`, `ProfileReadOnlyException` (the Adv360 profile-0 guard), `ProfileSaveMessageCatalog`, `ProfileLightingCodec` (spec 03 §4.1/§4.3, §5.3; 04 §5.3; 07 §1.1-§1.3) — see [`profiles.md`](profiles.md).
- `SavantElite` — the Savant Elite2 pedal-file engine: `PedalConfiguration`/`PedalInput` plus `PedalFileParser`/`PedalFileSerializer`/`PedalFileService` for `active/pedals.txt` (seven inputs, single actions and macros, the `{ }` marker, the pedal-only token spellings) and `PedalActionDescriber` for assignment display text (spec 12 §4-§6) — see [`savant-elite.md`](savant-elite.md). Outside `Profiles`: the pedal has one live file, no numbered profiles, and no eject.

## Notes

- MVVM is `CommunityToolkit.Mvvm` 8.4.2 (`ObservableObject`, `RelayCommand`/`AsyncRelayCommand`) with plain constructor injection — **no DI container**; `App.axaml.cs` is the composition root.
- The colour picker uses `Avalonia.Controls.ColorPicker` (pinned to the same 11.3.12 as the other Avalonia packages). It ships its own control theme, so `App.axaml` must include `avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml` after `<FluentTheme/>` or `ColorView` renders untemplated.
- **Fonts ship with the app.** `Avalonia.Fonts.Inter` and `.WithInterFont()` are gone; `Program.BuildAvaloniaApp` registers no font package. IBM Plex Sans/Mono are `<AvaloniaResource Include="Assets/Fonts/**" />` and addressed as `avares://KinesisEdit/Assets/Fonts#<family>`.
- **Test-project packages for the UI suites:** `Avalonia.Headless.XUnit` and `Avalonia.Skia`, both pinned to the app's 11.3.12 — a headless platform out of step with the framework it boots will not load. `KinesisEdit.Tests.csproj` also embeds the app's `**/*.axaml` as `EmbeddedResource` (`LinkBase="Xaml"`) so the resource-key guard can read the markup, which compiled XAML leaves nowhere else.
- The app project pins `Tmds.DBus.Protocol` 0.21.3 directly (transitive dep of Avalonia.Desktop 11.3.12 is the vulnerable 0.21.2, GHSA-xrw6-gwf8-vvr9). Drop the pin when Avalonia updates.
- No StyleCop by design: its member-ordering defaults conflict with the repo's properties-before-fields ordering.
