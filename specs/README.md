# SmartSet Apps — Legacy Application Specification

This folder documents the capabilities, file formats, and behaviors of the legacy Kinesis SmartSet applications, at the level of detail needed to build a replacement app with a modern architecture. It is a standalone specification: it describes what the legacy apps do and the on-device file formats they read and write, not how their source code is organized.

## Contents

| Doc | Covers |
|---|---|
| [01 — Architecture Overview](01-overview.md) | What the apps are, the v-Drive/text-file design principle, the application family, the device identity model. |
| [02 — Device Catalog](02-devices.md) | Every supported device: identifiers, volume labels, layers, profiles, macro/lighting capabilities, special hardware, per-device constraints. |
| [03 — The v-Drive](03-vdrive-and-files.md) | Drive detection (Windows/macOS), on-device folder and file structure per device, file I/O semantics, save + eject sequence, app-side persistence. |
| [04 — Layout File Format](04-layout-file-format.md) | The core spec: `input>output` line grammar, remap/tap-and-hold/multi-modifier syntax, per-device layer encoding, load/save semantics, validation and invalid-line handling, worked examples. |
| [05 — Key Model](05-key-model.md) | The complete key-token tables (three token dialects), per-device physical layer definitions, modifiers/AltGr/keypad exceptions, international handling, and the legacy in-memory data model for reference. |
| [06 — Macros](06-macros.md) | Macro model and full token grammar: co-triggers, speed `{sN}`/`{speedN}`, repeat `{xN}`, delays `{dNNN}`/`{dran}`, down/up `{-k}`/`{+k}`, limits and firmware gates, worked examples. |
| [07 — RGB Lighting](07-lighting.md) | `led*.txt` format, all effect modes and parameters, per-key coloring, Fn-layer lighting, TKO edge LEDs, Adv360 indicator LEDs. |
| [08 — Settings](08-settings.md) | Every keyboard-settings key with value vocabularies and per-device support, app settings, settings dialog inventory. |
| [09 — Firmware](09-firmware.md) | Version file formats and parsing, all firmware feature gates with exact versions, the update-check flow and endpoint. |
| [10 — Applications and UI](10-apps-and-ui.md) | Per-app window structure and workflows: device scan/demo mode, remap flow, macro recording, save/load/import/export, dashboards, keystroke capture requirements. |
| [11 — Feature Dialogs](11-feature-dialogs.md) | Tap-and-hold, multimodifiers, timing delays, select-macro, search keys, export, diagnostics, troubleshooting — inputs, ranges, validation rules, resulting file syntax. |
| [12 — Savant Elite2](12-savant-elite.md) | The foot pedal app: `pedals.txt` grammar, special actions, generation differences in files found on devices. |

## The one-paragraph mental model

A SmartSet app is a file editor with a keyboard-shaped UI. Each programmable Kinesis device mounts a FAT volume (the "v-Drive") holding plain-text files; the keyboard's firmware — not the app — interprets them. The app (1) finds the volume by label + marker file, (2) parses `layouts/layoutN.txt` (remaps + macros) and `lighting/ledN.txt` into an in-memory model of layers → keys, (3) lets the user edit that model by clicking on-screen keys and capturing real keystrokes, and (4) serializes the model back and ejects/syncs the volume so the firmware reloads it. Rebuild scope is therefore: drive discovery, three parser/serializer dialects, the key-token tables, per-device layer geometry, and the editing UI — there is no USB/HID configuration protocol.

## Suggested reading order for a rebuild

1. **01 → 02 → 03** for the system model: apps, devices, and where files live.
2. **05** for the key-token tables (the vocabulary), then **04** and **06** for the grammar that uses it. These three are the heart of the spec — a new app that gets these right is interoperable with existing keyboards.
3. **07** and **08** for lighting and settings files (same style of line-oriented grammar).
4. **09 → 10 → 11** for behavior: firmware gating, UI workflows, and the advanced-feature dialogs with their validation rules.
5. **12** if the new app must also cover the SE2 foot pedal (a separate device family with its own file format).

## Cross-cutting facts worth knowing up front

- **Three config-file dialects** exist, one per device generation: Legacy (Advantage2 + SE2 pedal — e.g. tokens `escape`, `prtscr`, `{speed3}`, keypad prefix `kp-`), Gen1 (Freestyle Edge/Pro, Edge RGB, TKO — tokens `esc`, `prnt`, `{s3}{x2}`, Fn-layer prefix `fn `), and Gen2 (Advantage360 — 4-character tokens like `lctr`, layer section headers `<base>`/`<keypad>`/`<function1..3>`). Each dialect needs its own parser/serializer.
- **Parsing is case-insensitive, saving regenerates the whole file.** Layout files have no comment syntax (the SE2 `pedals.txt` preserves factory `*` comment lines; layout files do not). Unparseable lines are collected and shown to the user, who chooses which to keep.
- **Feature availability is firmware-gated** by comparing the device's version file against hard-coded minimums (tap-and-hold, the 100-macro limit, ripple/fireball effects, macros in tap-and-hold, …). See doc 09 for the full gate table.
- **Limits**: 9 profiles (layout/led file pairs), up to 100 macros (24 on old Freestyle firmware), 7,200 total keystrokes, 300 (Freestyle) / 500 (Advantage360) characters per macro, 10 tap-and-holds per layout, macro playback speeds 0–9, delays 1–999 ms.
- **Files in the field vary by generation.** Devices already in users' hands contain files written by older firmware and older app versions (e.g. `{125}` vs `{d125}` delay tokens on SE2, legacy lighting line orders). The new app should read these tolerantly and write the current dialect.

## Out of scope

Firmware-side behavior (how the keyboard executes the files) is documented only as observable contract — version gates, file semantics — not as firmware internals. The legacy app's UI framework, build system, and internal code organization are not covered except where the data model is useful as a design reference.
