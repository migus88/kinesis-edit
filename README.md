# Kinesis Edit

A modern, cross-platform replacement for the Kinesis SmartSet keyboard-configuration app. The legacy SmartSet apps are written in Pascal and no longer supported; this project rebuilds them with a modern architecture.

- **Stack:** C# with [Avalonia UI](https://avaloniaui.net/) for cross-platform portability.
- **Platforms:** macOS first, with Windows and Linux fully supported.
- **Status:** In development — specification complete, static domain data implemented (device catalog, key-token registry, layer geometries — see [`docs/app/domain-data.md`](docs/app/domain-data.md)), the in-memory keyboard/macro model built on it (see [`docs/app/keyboard-model.md`](docs/app/keyboard-model.md)), firmware version parsing and feature gating implemented (see [`docs/app/firmware.md`](docs/app/firmware.md)), v-Drive services implemented (drive discovery, file I/O, eject — see [`docs/app/vdrive.md`](docs/app/vdrive.md)), lighting model and led-file engine implemented (RGB, TKO, Advantage360 — see [`docs/app/lighting.md`](docs/app/lighting.md)), settings engine implemented (keyboard/app settings parsing and saving — see [`docs/app/settings.md`](docs/app/settings.md)), keystroke capture for remap and macro recording implemented (see [`docs/app/keystroke-capture.md`](docs/app/keystroke-capture.md)), layout and macro file parsers/serializers implemented (Freestyle, RGB/TKO, Advantage360, and Advantage2 dialects — see [`docs/app/layout-files.md`](docs/app/layout-files.md)), profile load/save/eject orchestration implemented for the numbered-profile devices — FS Edge/Pro, Edge RGB, TKO, Advantage360 (see [`docs/app/profiles.md`](docs/app/profiles.md)), the Savant Elite2 pedal-file engine implemented (`active/pedals.txt` parsing, saving, and display text — see [`docs/app/savant-elite.md`](docs/app/savant-elite.md)), and the app now launches to a device dashboard that lists detected keyboards, opens them from a card, and reaches demo mode with no hardware attached (see [`docs/app/app-shell.md`](docs/app/app-shell.md)). The first keyboard editor is in: opening a Freestyle Edge RGB draws its keyboard, switches between the top and Fn layers, remaps a key by clicking it and pressing the key you want, resets a key/layer/layout, and saves the profile back to the v-Drive; its **Macros** tab records a macro on the selected key by pressing the keys you want, with macro slots, co-trigger modifiers, playback speed and repeat, live keystroke-budget readouts and a list of every macro in the profile; its **Lighting** tab picks an effect (filtered by the board's mode matrix and firmware gates), sets effect and base colours from a picker with twelve saved custom slots, adjusts speed and direction, and paints individual keys or whole zones; its **Settings** panel shows exactly the settings the connected device supports — startup profile, macro and status speeds, v-Drive behaviour, game mode and the rest — locked with an explanation on an Advantage2 without the 4MB firmware, and read-only in demo mode (see [`docs/app/keyboard-editor.md`](docs/app/keyboard-editor.md)). The advanced dialogs sit over all of it — Tap and Hold, macro timing delays, a searchable key picker, exporting the layout/lighting files to a folder, and importing an external `.txt` back over the profile — each refusing politely when the keyboard's firmware is too old (see [`docs/app/feature-dialogs.md`](docs/app/feature-dialogs.md)); the Savant Elite2 opens a read-only view of its seven pedal inputs. The other devices still open a placeholder until their keyboard picture is drawn — which also gates the TKO edge-LED tab and the Advantage360 indicator editor. Progress is tracked in [GitHub issues](https://github.com/migus88/kinesis-edit/issues/1).

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
| `src/` | Source code of the new app: `KinesisEdit.sln` with the Avalonia app (`KinesisEdit`), the UI-free domain library (`KinesisEdit.Core`), and their xUnit test projects (`KinesisEdit.Core.Tests`, `KinesisEdit.Tests`). See [`docs/app/solution-structure.md`](docs/app/solution-structure.md). |

## Contributing notes

- Code follows the conventions in `docs/guides/Coding Conventions.md`: clean, SOLID, no god classes.
- Every module gets a token-efficient doc in `docs/app/` so AI agents (and humans) can understand a domain without reading its source. Documentation — `docs/app/`, `CLAUDE.md`, and this README — is maintained as part of every feature change.
- Feature work runs in isolated git worktrees under `.claude/worktrees/` (one per feature branch, based on `origin/main`), so several features can be in flight at once. Worktrees are removed once their PR merges; `/clean-worktree` sweeps up any leftovers.
