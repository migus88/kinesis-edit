# Kinesis Edit

A modern, cross-platform replacement for the Kinesis SmartSet keyboard-configuration app. The legacy SmartSet apps are written in Pascal and no longer supported; this project rebuilds them with a modern architecture.

- **Stack:** C# with [Avalonia UI](https://avaloniaui.net/) for cross-platform portability.
- **Platforms:** macOS first, with Windows and Linux fully supported.
- **Status:** In development — specification complete, solution scaffolded (app shell, core library, tests, CI), static domain data implemented (device catalog, key-token registry, layer geometries — see [`docs/app/domain-data.md`](docs/app/domain-data.md)), the in-memory keyboard/macro model built on it (see [`docs/app/keyboard-model.md`](docs/app/keyboard-model.md)), firmware version parsing and feature gating implemented (see [`docs/app/firmware.md`](docs/app/firmware.md)), v-Drive services implemented (drive discovery, file I/O, eject — see [`docs/app/vdrive.md`](docs/app/vdrive.md)), lighting model and led-file engine implemented (RGB, TKO, Advantage360 — see [`docs/app/lighting.md`](docs/app/lighting.md)), settings engine implemented (keyboard/app settings parsing and saving — see [`docs/app/settings.md`](docs/app/settings.md)). Progress is tracked in [GitHub issues](https://github.com/migus88/kinesis-edit/issues/1).

## Building and running

Requires the .NET 10 SDK (pinned via `global.json`). From the repo root:

```sh
dotnet build src/KinesisEdit.sln    # build everything
dotnet test src/KinesisEdit.sln     # run the test suite
dotnet run --project src/KinesisEdit   # launch the app
```

## How it works

Kinesis programmable keyboards mount a small FAT volume (the "v-Drive") containing plain-text configuration files (layouts, macros, lighting, settings). The keyboard's firmware parses those files itself — there is no USB/HID configuration protocol. The app is a file editor with a keyboard-shaped UI: it discovers the drive, parses the files into an in-memory model, lets the user edit it visually, and writes the files back.

## Repository layout

| Folder | Contents |
|---|---|
| `specs/` | Full specification of the legacy apps, supported devices, and on-device file formats. The authoritative domain reference — start with [`specs/README.md`](specs/README.md). |
| `docs/app/` | Agent-first documentation of the new app's modules, maintained alongside the code. |
| `docs/guides/` | [Coding conventions](docs/guides/Coding%20Conventions.md) and other guides. |
| `src/` | Source code of the new app: `KinesisEdit.sln` with the Avalonia app (`KinesisEdit`), the UI-free domain library (`KinesisEdit.Core`), and its xUnit tests (`KinesisEdit.Core.Tests`). See [`docs/app/solution-structure.md`](docs/app/solution-structure.md). |

## Contributing notes

- Code follows the conventions in `docs/guides/Coding Conventions.md`: clean, SOLID, no god classes.
- Every module gets a token-efficient doc in `docs/app/` so AI agents (and humans) can understand a domain without reading its source. Documentation — `docs/app/`, `CLAUDE.md`, and this README — is maintained as part of every feature change.
- Feature work runs in isolated git worktrees under `.claude/worktrees/` (one per feature branch, based on `origin/main`), so several features can be in flight at once. Worktrees are removed once their PR merges; `/clean-worktree` sweeps up any leftovers.
