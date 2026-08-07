# Kinesis Edit

A modern, cross-platform replacement for the Kinesis SmartSet keyboard-configuration app. The legacy SmartSet apps are written in Pascal and no longer supported; this project rebuilds them with a modern architecture.

- **Stack:** C# with [Avalonia UI](https://avaloniaui.net/) for cross-platform portability.
- **Platforms:** macOS first, with Windows and Linux fully supported.
- **Status:** In development. The domain layer is complete — the device catalog and key tables, the in-memory keyboard and macro model, v-Drive discovery and file I/O, and parsers/serializers for every supported configuration-file format. The app launches to a dashboard of the keyboards it has detected and opens a full editor for two of them: the **Freestyle Edge RGB** — remap keys, record and name macros, design the lighting with the effect playing on the board in front of you, and change the board's own settings — and the **Savant Elite2** foot pedal. Other models open a placeholder until their keyboard picture is drawn. **Demo mode** opens a fully populated Freestyle Edge RGB with no hardware attached and writes nothing anywhere. Progress is tracked in [GitHub issues](https://github.com/migus88/kinesis-edit/issues/1).
- **In flight — a UI redesign.** Everything above was first built to mimic the legacy app. A design handoff now supersedes that look, and in places its behavior: a key inspector in place of the old dialogs, macros named and edited where the key is, lighting previewed on the board itself, and per-device board art for every model. The handoff lives in [`docs/design/`](docs/design/README.md) and is tracked as its own epic; its foundation — the design system, the drawn icon set and the headless UI test harness — has landed, and the screens are being rebuilt on it one issue at a time.

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
| `docs/app/` | Agent-first documentation of the new app's modules, maintained alongside the code. Start with [`docs/app/README.md`](docs/app/README.md) — it indexes which doc answers which question. |
| `docs/design/` | The design handoff for the UI redesign — the authoritative reference for anything visual or user-facing. Start with [`docs/design/README.md`](docs/design/README.md). |
| `docs/guides/` | [Coding conventions](docs/guides/Coding%20Conventions.md) and other guides. |
| `src/` | Source code of the new app: `KinesisEdit.sln` with the Avalonia app (`KinesisEdit`), the UI-free domain library (`KinesisEdit.Core`), and their xUnit test projects (`KinesisEdit.Core.Tests`, `KinesisEdit.Tests`). See [`docs/app/solution-structure.md`](docs/app/solution-structure.md). |

## Contributing notes

- Code follows the conventions in `docs/guides/Coding Conventions.md`: clean, SOLID, no god classes.
- Every module gets a token-efficient doc in `docs/app/` so AI agents (and humans) can understand a domain without reading its source, and a row in [`docs/app/README.md`](docs/app/README.md). That documentation is maintained as part of every feature change — a feature's behavior is described in its module's doc, and nowhere else. `CLAUDE.md` and this README deliberately carry no running feature narrative: both used to, and every branch collided in the same paragraph.
- Feature work runs in isolated git worktrees under `.claude/worktrees/` (one per feature branch, based on `origin/main`), so several features can be in flight at once. Worktrees are removed once their PR merges; `/clean-worktree` sweeps up any leftovers.
