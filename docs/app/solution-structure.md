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

## Seed domain type

`KinesisEdit.Core/Devices/FirmwareVersion.cs` (`KinesisEdit.Core.Devices.FirmwareVersion`): immutable value type for versions read from device version files, per `specs/09-firmware.md` §1.1: first three dot-separated numeric tokens → major/minor/revision (non-numeric minor/revision token → 0, trailing text like `.us (4MB), 03/08/2019` ignored); lexicographic ordering via `IComparable<FirmwareVersion>`. It exists because firmware-version comparison gates feature availability (spec 09 §2) and it anchors the `Devices` namespace.

## Notes

- The app project pins `Tmds.DBus.Protocol` 0.21.3 directly (transitive dep of Avalonia.Desktop 11.3.12 is the vulnerable 0.21.2, GHSA-xrw6-gwf8-vvr9). Drop the pin when Avalonia updates.
- No StyleCop by design: its member-ordering defaults conflict with the repo's properties-before-fields ordering.
