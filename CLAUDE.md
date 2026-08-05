# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

A modern, cross-platform replacement for the Kinesis SmartSet keyboard-configuration app (the legacy app is written in Pascal and unsupported). Target stack: **C# with Avalonia UI**. Primary platform is **macOS** — develop Mac-first — but Windows and Linux must also work.

**Current status: greenfield.** `src/` is empty and there is no solution/build yet. When scaffolding begins, use the .NET SDK (`dotnet new` / `dotnet build` / `dotnet test`) and update this section with the real commands as soon as they exist.

## Repository layout

- `specs/` — Standalone specification of the legacy SmartSet apps, devices, and on-device file formats. This is the **authoritative domain reference** for the rebuild; do not modify it casually. Start with `specs/README.md` for the reading order and mental model.
- `docs/plan/` — Implementation plan for the new app.
- `docs/app/` — Agent-first documentation of the new app's modules (see "Documentation rules" below). Currently empty; populate as modules are built.
- `docs/guides/` — Coding conventions and other guides.
- `src/` — Source code of the new app.

## Domain model (the big picture)

Read `specs/README.md` first — but the core mental model is:

A SmartSet app is **a file editor with a keyboard-shaped UI**. Each programmable Kinesis device mounts a small FAT volume (the "v-Drive") containing plain-text config files (`layouts/layoutN.txt`, `lighting/ledN.txt`, settings); the keyboard's **firmware** parses those files — there is **no USB/HID configuration protocol**. The app therefore consists of:

1. Drive discovery (find the mounted v-Drive by volume label + marker files).
2. Parsers/serializers for the text file formats.
3. An in-memory model per keyboard (layers → keys → remaps/macros).
4. A per-device visual keyboard UI for editing that model.
5. Save/sync/eject handling so the firmware reloads the files.

Facts that shape the architecture (details in `specs/README.md` and the numbered docs):

- **Three config-file dialects**, one per device generation — Legacy (Advantage2, SE2 pedal), Gen1 (Freestyle Edge/Pro, Edge RGB, TKO), Gen2 (Advantage360). Each needs its own parser/serializer; the key-token tables in spec 05 plus the grammars in specs 04 and 06 are the interoperability-critical heart of the system.
- **Parsing is case-insensitive; saving regenerates the whole file.** Unparseable lines are collected and shown to the user, not silently dropped.
- **Read tolerantly, write the current dialect** — devices in the field contain files written by older firmware/app versions.
- **Feature availability is firmware-gated** by comparing device version files against hard-coded minimums (spec 09 has the gate table).

## Custom commands

- `/feature <issue-number | issue-url | description>` — drives a feature end to end: syncs `main`, researches and asks clarifying questions, updates or creates the GitHub issue, branches, implements via subagents (Workflow orchestration with adversarial review when ultracode is enabled), runs the full unit-test suite (all tests must pass before a PR is opened), verifies the doc updates, and opens a PR. Defined in `.claude/commands/feature.md`.

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
