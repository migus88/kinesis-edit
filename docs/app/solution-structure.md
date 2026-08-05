# Solution structure

Solution: `src/KinesisEdit.sln` (classic .sln). Three projects, all `net10.0`, `Nullable` + `ImplicitUsings` enabled.

## Projects and dependency direction

| Project | Role | References |
|---|---|---|
| `src/KinesisEdit/` | Avalonia 11 desktop app (`OutputType=WinExe`). Entry point `Program.cs`; shell window `MainWindow` titled "Kinesis Edit". | `KinesisEdit.Core` |
| `src/KinesisEdit.Core/` | Domain library: device model, parsers/serializers, drive discovery. **No UI/Avalonia dependencies — keep it that way.** | (none) |
| `src/KinesisEdit.Core.Tests/` | xUnit tests for Core. Test files mirror Core's folder structure. | `KinesisEdit.Core` |

Dependency direction is one-way: app → Core ← tests. Domain logic goes in Core so it stays testable and UI-free.

## Commands (from repo root)

- Build: `dotnet build src/KinesisEdit.sln`
- Test: `dotnet test src/KinesisEdit.sln`
- Run the app: `dotnet run --project src/KinesisEdit`

## SDK pinning

`global.json` at the repo root pins SDK `10.0.100` with `rollForward: latestFeature` (any 10.0.1xx works). `.editorconfig` at the repo root encodes the coding conventions (block-scoped namespaces, Allman braces, naming rules).

## CI

`.github/workflows/ci.yml` ("CI"): on PRs targeting `main` and pushes to `main`, matrix over macOS/Ubuntu/Windows, runs `dotnet build` + `dotnet test` on the solution in Release. SDK comes from `global.json` via `actions/setup-dotnet`.

## Core namespaces

`KinesisEdit.Core` currently contains the static domain-data layer (see [`domain-data.md`](domain-data.md)), the runtime keyboard model built on top of it (see [`keyboard-model.md`](keyboard-model.md)), the firmware module (see [`firmware.md`](firmware.md)), the v-Drive services (see [`vdrive.md`](vdrive.md)), the lighting module (see [`lighting.md`](lighting.md)), and the settings engine (see [`settings.md`](settings.md)):

- `Devices` — device catalog: `DeviceCatalog`/`DeviceDefinition` (volume labels, marker files, v-Drive paths, and the per-family capabilities `MacroCapability`, `TapAndHoldCapability`, `LightingCapability`, `SettingsCapability`, `SupportsMultiModifiers`, with `ValueRange` for their bounded values; specs 02 and 03 §1–4, 04 §5.3, 06 §6, 08 §2, 11 §11.1–11.2) plus `FirmwareVersion`, the immutable value type for version-file parsing and comparison (spec 09 §1.1).
- `Keys` — master key-token registry: `KeyRegistry` with 1282 entries in spec registration order across the three token dialects (spec 05 §3, §7).
- `Geometry` — physical layer geometries: `GeometryCatalog`, seven layout families with fully materialized layers (spec 05 §4).
- `Model` — the editable in-memory model: `KeyboardLayout` → `KeyboardLayer` → `KeyboardKey`, plus `Macro`/`Keystroke` and the limit reports of `Validate()` (spec 05 §1, §5, §7.2, §7.4; spec 06; 04 §5.3) — see [`keyboard-model.md`](keyboard-model.md).
- `Firmware` — version-file parsing and firmware feature gating: `VersionFileParser`, `FirmwareGateCatalog`/`FirmwareGateService`, `FirmwareSupportUrls` (specs 09 §1–2, 11 §11.1, 12 §1) — see [`firmware.md`](firmware.md).
- `VDrive` (+ `.Discovery`, `.Io`, `.Eject`) — v-Drive discovery (platform volume enumerators, shared scanner, polling monitor), raw 8-bit file I/O with the spec's write rules, and the flush/eject abstraction (spec 03 §2–5; 08 §1) — see [`vdrive.md`](vdrive.md).
- `Lighting` — in-memory lighting model and the `lighting/ledN.txt` parser/serializer for the RGB, TKO, and Advantage360 dialects, plus the mode/indicator catalogs and lighting firmware hooks (spec 07; 05 §5.5) — see [`lighting.md`](lighting.md).
- `Settings` — the settings engine: typed keyboard-settings and app-settings models with pure line-based parsers/serializers, the Advantage2 4MB write gate, and the `SettingsService` load/save binding (spec 08 §1–3, §5; 09 §1.1) — see [`settings.md`](settings.md).
- `Input` — the UI-free keystroke-capture state machines: `PhysicalKeyCode`/`PhysicalKeyMap`, `KeystrokeRecorder`, `KeystrokeCaptureSession` (started/suspended gating), `IKeystrokeCaptureService` (spec 10) — see [`keystroke-capture.md`](keystroke-capture.md).

Layout parsers and serializers (specs 04, 06; issue #8) are not implemented yet.

## App namespaces

`KinesisEdit/Input` holds the only platform-aware input code: `AvaloniaKeystrokeCaptureService` (tunnel-phase key preview on a `TopLevel`, delegating every decision to a Core `KeystrokeCaptureSession` and contributing only the "is a `TextBox` focused" verdict plus the `SuspendOnTextInputFocus` switch), `AvaloniaPhysicalKeyBridge`, and the harness row view `CapturedKeystrokeView`. It is the worked example of the boundary above — every capture rule lives in `KinesisEdit.Core.Input` where tests can reach it, and only the `TopLevel` adapter may reference Avalonia. `MainWindow` is currently the issue-#12 capture spike harness, not the eventual app shell.

## Notes

- The app project pins `Tmds.DBus.Protocol` 0.21.3 directly (transitive dep of Avalonia.Desktop 11.3.12 is the vulnerable 0.21.2, GHSA-xrw6-gwf8-vvr9). Drop the pin when Avalonia updates.
- No StyleCop by design: its member-ordering defaults conflict with the repo's properties-before-fields ordering.
