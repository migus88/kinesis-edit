# Solution structure

Solution: `src/KinesisEdit.sln` (classic .sln). Four projects, all `net10.0`, `Nullable` + `ImplicitUsings` enabled.

## Projects and dependency direction

| Project | Role | References |
|---|---|---|
| `src/KinesisEdit/` | Avalonia 11 desktop app (`OutputType=WinExe`, compiled bindings on). Entry point `Program.cs`; composition root `App.axaml.cs`; shell window `MainWindow` titled "Kinesis Edit". | `KinesisEdit.Core` |
| `src/KinesisEdit.Core/` | Domain library: device model, parsers/serializers, drive discovery. **No UI/Avalonia dependencies — keep it that way.** | (none) |
| `src/KinesisEdit.Core.Tests/` | xUnit tests for Core. Test files mirror Core's folder structure. | `KinesisEdit.Core` |
| `src/KinesisEdit.Tests/` | xUnit tests for the app layer: services, view models, converters, `ViewLocator`. Test files mirror the app's folder structure; hand-rolled fakes (no mocking library), and no Avalonia runtime is started — everything under test is toolkit-free by design. | `KinesisEdit` |

Dependency direction is one-way: `KinesisEdit.Core.Tests` → Core ← app ← `KinesisEdit.Tests`. Domain logic goes in Core so it stays testable and UI-free; app-layer logic goes in services and view models so it stays testable without a UI toolkit.

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

- `Services` — detection loop (`DeviceMonitorService` → `DeviceSnapshot`), notifications (message box, toast, loading, `Hide this notification?` suppression), the active-device session, eject, URL launching, and UI-thread dispatch. Toolkit-free apart from `AvaloniaUiDispatcher` and `MessageBoxPresenter`.
- `ViewModels` — `MainWindowViewModel` (navigation, Home → eject, status indicator), `DashboardViewModel`/`DeviceCardViewModel`/`NoDeviceViewModel`, `EditorPlaceholderViewModel`, plus the notification view models. They expose enums and strings, never Avalonia brushes.
- `Views` — the `.axaml` for each of the above plus `MainWindow`, `MessageBoxWindow`, `NotificationOverlay`. Resolved by name through `ViewLocator`.
- `Converters` — `EnumMatchConverter`, `MessageBoxIconToGlyphConverter`: the enum → style-class/glyph mapping that keeps colors in XAML.

## Core namespaces

`KinesisEdit.Core` currently contains the static domain-data layer (see [`domain-data.md`](domain-data.md)), the firmware module (see [`firmware.md`](firmware.md)), and the v-Drive services (see [`vdrive.md`](vdrive.md)):

- `Devices` — device catalog: `DeviceCatalog`/`DeviceDefinition` (volume labels, marker files, v-Drive paths, macro/lighting capabilities; specs 02 and 03 §1–4) plus `FirmwareVersion`, the immutable value type for version-file parsing and comparison (spec 09 §1.1).
- `Keys` — master key-token registry: `KeyRegistry` with 1282 entries in spec registration order across the three token dialects (spec 05 §3, §7).
- `Geometry` — physical layer geometries: `GeometryCatalog`, seven layout families with fully materialized layers (spec 05 §4).
- `Firmware` — version-file parsing and firmware feature gating: `VersionFileParser`, `FirmwareGateCatalog`/`FirmwareGateService`, `FirmwareSupportUrls` (specs 09 §1–2, 11 §11.1, 12 §1) — see [`firmware.md`](firmware.md).
- `VDrive` (+ `.Discovery`, `.Io`, `.Eject`) — v-Drive discovery (platform volume enumerators, shared scanner, polling monitor), raw 8-bit file I/O with the spec's write rules, and the flush/eject abstraction (spec 03 §2–5; 08 §1) — see [`vdrive.md`](vdrive.md).

Layout/lighting parsers and serializers are not implemented yet.

## Notes

- MVVM is `CommunityToolkit.Mvvm` 8.4.2 (`ObservableObject`, `RelayCommand`/`AsyncRelayCommand`) with plain constructor injection — **no DI container**; `App.axaml.cs` is the composition root.
- The app project pins `Tmds.DBus.Protocol` 0.21.3 directly (transitive dep of Avalonia.Desktop 11.3.12 is the vulnerable 0.21.2, GHSA-xrw6-gwf8-vvr9). Drop the pin when Avalonia updates.
- No StyleCop by design: its member-ordering defaults conflict with the repo's properties-before-fields ordering.
