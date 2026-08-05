# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

A modern, cross-platform replacement for the Kinesis SmartSet keyboard-configuration app (the legacy app is written in Pascal and unsupported). Target stack: **C# with Avalonia UI**. Primary platform is **macOS** — develop Mac-first — but Windows and Linux must also work.

**Current status: static domain data, the in-memory keyboard model, the firmware module (version parsing + feature gating), the v-Drive services, the lighting module (model + led-file engine), the settings engine, and keystroke capture implemented; the app shell and device dashboard now exist — the app launches to a dashboard of detected devices and reaches demo mode with no hardware, but the per-device editors are still placeholders.** The solution lives at `src/KinesisEdit.sln` (Avalonia app `KinesisEdit`, UI-free domain library `KinesisEdit.Core`, xUnit tests `KinesisEdit.Core.Tests` for Core and `KinesisEdit.Tests` for the app layer); see `docs/app/solution-structure.md` for the layout and dependency rules. From the repo root:

- Build: `dotnet build src/KinesisEdit.sln`
- Test: `dotnet test src/KinesisEdit.sln`
- Run the app: `dotnet run --project src/KinesisEdit`

Implementation is planned and tracked in GitHub issues — see the epic ([#1](https://github.com/migus88/kinesis-edit/issues/1)) and its ordered sub-issues. There is no in-repo planning document.

## Repository layout

- `specs/` — Standalone specification of the legacy SmartSet apps, devices, and on-device file formats. This is the **authoritative domain reference** for the rebuild; do not modify it casually. Start with `specs/README.md` for the reading order and mental model.
- `docs/app/` — Agent-first documentation of the new app's modules (see "Documentation rules" below). Currently: `solution-structure.md` (projects, commands, CI), `domain-data.md` (the static domain-data layer in Core), `keyboard-model.md` (the runtime keyboard/macro model), `firmware.md` (version parsing + feature gating), `vdrive.md` (v-Drive discovery, file I/O, eject), `lighting.md` (lighting model + led-file engine), `settings.md` (the settings engine: keyboard/app settings models, parsers, 4MB gate, service), `keystroke-capture.md` (physical keystroke capture for remap/macro recording), and `app-shell.md` (the Avalonia app layer: shell navigation, device dashboard, detection loop, notifications). Add a doc per module as modules are built.
- `docs/guides/` — Coding conventions and other guides.
- `src/` — Source code of the new app.

## Domain model (the big picture)

Read `specs/README.md` first — but the core mental model is:

A SmartSet app is **a file editor with a keyboard-shaped UI**. Each programmable Kinesis device mounts a small FAT volume (the "v-Drive") containing plain-text config files (`layouts/layoutN.txt`, `lighting/ledN.txt`, settings); the keyboard's **firmware** parses those files — there is **no USB/HID configuration protocol**. The app therefore consists of:

1. Drive discovery (find the mounted v-Drive by volume label + marker files). Implemented in `KinesisEdit.Core.VDrive` (see `docs/app/vdrive.md`).
2. Parsers/serializers for the text file formats. The `lighting/ledN.txt` engine (RGB, TKO, Advantage360) is implemented in `KinesisEdit.Core.Lighting` (see `docs/app/lighting.md`) and the settings files (spec 08) in `KinesisEdit.Core.Settings` (see `docs/app/settings.md`); layout parsers are not.
3. An in-memory model per keyboard (layers → keys → remaps/macros), implemented in `KinesisEdit.Core`: the static foundation — device catalog, key-token registry, layer geometries (see `docs/app/domain-data.md`) — plus the editable runtime model built on it, `KeyboardLayout`/`KeyboardLayer`/`KeyboardKey`/`Macro` (see `docs/app/keyboard-model.md`). Device limits are reported by `Validate()`, never enforced.
4. Keystroke capture feeding that editing: real keypresses with left/right modifiers distinguished, swallowed while recording a remap or macro. Implemented in `KinesisEdit.Core.Input` (see `docs/app/keystroke-capture.md`).
5. A per-device visual keyboard UI for editing that model. The app shell that hosts it — window, device dashboard, detection loop, notifications — is implemented in `KinesisEdit` (see `docs/app/app-shell.md`); the editors themselves are not.
6. Save/sync/eject handling so the firmware reloads the files. The file I/O and eject services are implemented in `KinesisEdit.Core.VDrive` (see `docs/app/vdrive.md`); the save orchestration on top is not.

Facts that shape the architecture (details in `specs/README.md` and the numbered docs):

- **Three config-file dialects**, one per device generation — Legacy (Advantage2, SE2 pedal), Gen1 (Freestyle Edge/Pro, Edge RGB, TKO), Gen2 (Advantage360). Each needs its own parser/serializer; the key-token tables in spec 05 plus the grammars in specs 04 and 06 are the interoperability-critical heart of the system.
- **Parsing is case-insensitive; saving regenerates the whole file.** Unparseable lines are collected and shown to the user, not silently dropped.
- **Read tolerantly, write the current dialect** — devices in the field contain files written by older firmware/app versions.
- **Feature availability is firmware-gated** by comparing device version files against hard-coded minimums (spec 09 has the gate table).

## Custom commands

- `/feature <issue-number | issue-url | description>` — drives a feature end to end: fetches `origin`, researches and asks clarifying questions, updates or creates the GitHub issue, creates an isolated git worktree under `.claude/worktrees/` branched from `origin/main` (the shared checkout is never touched, so multiple sessions can work concurrently), implements via subagents (Workflow orchestration with adversarial review when ultracode is enabled), runs the full unit-test suite (all tests must pass before a PR is opened), verifies the doc updates, and opens a PR. When the user reports the PR merged, the worktree and branch are removed. Defined in `.claude/commands/feature.md`.
- `/clean-worktree [all]` — removes finished worktrees: by default only those whose branch is merged into `origin/main` and that have no uncommitted changes; `all` force-removes every worktree after user confirmation. Defined in `.claude/commands/clean-worktree.md`.

## Testing rules (important)

Everything is covered by unit tests. Concretely:

- Every feature creates unit tests for the code it adds and maintains the tests of the code it touches. Tests are part of the change, not a follow-up.
- The full unit-test suite must pass (`dotnet test` once the solution exists) before a pull request is opened. A feature with failing or missing tests is incomplete and must not reach the PR stage.
- CI runs the unit-test suite on every pull request targeting `main`; a red suite blocks the merge (workflow scoped in issue #3).

## Documentation rules (important)

Documentation here is **agent-first**: the aim is that AI agents read `docs/app/` instead of source code to understand how a domain works, dipping into source only for particulars. Therefore:

- Every developed module must have a doc in `docs/app/`. Keep it token-efficient: sufficient for an agent to understand the domain, not a prose-heavy tutorial.
- When implementing a feature, maintain the documentation as part of the change: the relevant `docs/app/` doc, this CLAUDE.md (e.g. build/test commands, new modules), and `README.md`.

## Code style

Follow `docs/guides/Coding Conventions.md`. Non-negotiables:

- Clean code, SOLID principles. No god classes — classes with thousands of lines are explicitly unwanted.
- PascalCase for types/methods/properties; camelCase for locals/parameters; `_camelCase` for private fields. No public fields — expose state through properties.
- Allman braces; always use braces, never single-line statements.
- Member ordering within a file: constants → static members → properties → events → fields → constructors → public methods → private methods → `Dispose` → nested types.
