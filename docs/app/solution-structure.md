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

`KinesisEdit.Core` currently contains the static domain-data layer — see [`domain-data.md`](domain-data.md) for the full contract:

- `Devices` — device catalog: `DeviceCatalog`/`DeviceDefinition` (volume labels, marker files, v-Drive paths, macro/lighting capabilities; specs 02 and 03 §1–4) plus `FirmwareVersion`, the immutable value type for version-file parsing and comparison (spec 09 §1.1).
- `Keys` — master key-token registry: `KeyRegistry` with 1282 entries in spec registration order across the three token dialects (spec 05 §3, §7).
- `Geometry` — physical layer geometries: `GeometryCatalog`, seven layout families with fully materialized layers (spec 05 §4).

Parsers/serializers and drive discovery are not implemented yet.

## Notes

- The app project pins `Tmds.DBus.Protocol` 0.21.3 directly (transitive dep of Avalonia.Desktop 11.3.12 is the vulnerable 0.21.2, GHSA-xrw6-gwf8-vvr9). Drop the pin when Avalonia updates.
- No StyleCop by design: its member-ordering defaults conflict with the repo's properties-before-fields ordering.
