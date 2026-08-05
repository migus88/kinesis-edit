# Kinesis Edit

A modern, cross-platform replacement for the Kinesis SmartSet keyboard-configuration app. The legacy SmartSet apps are written in Pascal and no longer supported; this project rebuilds them with a modern architecture.

- **Stack:** C# with [Avalonia UI](https://avaloniaui.net/) for cross-platform portability.
- **Platforms:** macOS first, with Windows and Linux fully supported.
- **Status:** Greenfield — specification complete, implementation not yet started. Progress is tracked in [GitHub issues](https://github.com/migus88/kinesis-edit/issues/1).

## How it works

Kinesis programmable keyboards mount a small FAT volume (the "v-Drive") containing plain-text configuration files (layouts, macros, lighting, settings). The keyboard's firmware parses those files itself — there is no USB/HID configuration protocol. The app is a file editor with a keyboard-shaped UI: it discovers the drive, parses the files into an in-memory model, lets the user edit it visually, and writes the files back.

## Repository layout

| Folder | Contents |
|---|---|
| `specs/` | Full specification of the legacy apps, supported devices, and on-device file formats. The authoritative domain reference — start with [`specs/README.md`](specs/README.md). |
| `docs/app/` | Agent-first documentation of the new app's modules, maintained alongside the code. |
| `docs/guides/` | [Coding conventions](docs/guides/Coding%20Conventions.md) and other guides. |
| `src/` | Source code of the new app. |

## Contributing notes

- Code follows the conventions in `docs/guides/Coding Conventions.md`: clean, SOLID, no god classes.
- Every module gets a token-efficient doc in `docs/app/` so AI agents (and humans) can understand a domain without reading its source. Documentation — `docs/app/`, `CLAUDE.md`, and this README — is maintained as part of every feature change.
