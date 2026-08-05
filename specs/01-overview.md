# 01 — Architecture Overview

Scope: what the legacy SmartSet applications are, how the application family is organized, and the shared architecture they are built on.

## What the apps do

The SmartSet apps are desktop GUI configuration tools for Kinesis Corporation programmable keyboards and foot pedals. Their core design principle: each programmable Kinesis device exposes a small FAT mass-storage volume (the "v-Drive"), and the app simply edits plain-text configuration files on it (layouts, macros, lighting, settings). The keyboard's firmware parses those files itself — there is **no custom USB/HID protocol for configuration**.

A replacement app therefore needs:

1. Drive discovery (find the mounted v-Drive by volume label + marker files).
2. Parsers and serializers for the text file formats (layouts/macros, lighting, settings).
3. An in-memory model of each keyboard (layers → keys → remaps/macros).
4. A per-device visual keyboard UI for editing that model.
5. Save/sync/eject handling so files are actually flushed to the device and reloaded by its firmware.

The legacy apps support Windows and macOS. They were originally designed to run directly from the v-Drive itself; later versions run from the desktop with the v-Drive merely connected.

## The application family

The legacy product line consists of several apps, each serving one or more devices:

| App | Devices served | Notes |
|---|---|---|
| SmartSet App (Advantage2) | Advantage2 | Standalone app; QWERTY and Dvorak base layouts. |
| SmartSet App (Freestyle) | Freestyle Edge and Freestyle Pro | One binary; the model is detected at run time from the device's version file, and Pro hides lighting/game-mode features. |
| SmartSet App (Freestyle Edge RGB) | Freestyle Edge RGB | Deprecated standalone; superseded by the gaming all-in-one app. |
| SmartSet App (TKO) | TKO | Deprecated standalone; superseded by the gaming all-in-one app. |
| SmartSet Master (Gaming) | Freestyle Edge RGB, TKO | All-in-one app with a device dashboard; hosts the per-device editors. |
| SmartSet Master (Office) | Advantage2, Advantage360, Freestyle Edge/Pro | All-in-one app with a dashboard and a v-Drive scanner; also links out for the Advantage360 Pro (configured by an external ZMK web tool). |
| SE2 SmartSet App | Savant Elite2 foot pedal | Separate, older architecture and file format (see doc 12). |

## Device identity model

Internally, every legacy app resolves the connected device to a numeric application/device ID, and nearly all device-specific behavior branches on it. These IDs also appear in this documentation as shorthand:

| Legacy ID | Device |
|---|---|
| 0 | Savant Elite2 foot pedal |
| 1 | Advantage2 |
| 2 | Freestyle Edge |
| 3 | Freestyle Pro |
| 4 | Freestyle Edge RGB |
| 5 | Crossfire keypad (never shipped) |
| 6 | TKO |
| 7 | Advantage360 |
| 8 | Advantage360 Pro (not programmable by SmartSet) |
| 100 / 200 | The Gaming / Office all-in-one apps themselves (device choosers) |

The all-in-one apps scan for any supported v-Drive, then activate the matching device's editor. Global runtime state includes the active device, mode flags (demo mode when no drive is found, debug/dev modes), and the resolved v-Drive paths (layouts folder, lighting folder, settings folder).

Device v-Drive volume labels: `FS EDGE RGB`, `FS EDGE`, `FS PRO`, `ADVANTAGE2` / `KINESIS KB` / `ADV2`, `SE2` / `KINESIS FP`, `CROSSFIRE KEYPAD`, `TKO`, `ADV360` (see doc 03 for the full detection rules).

## Shared architecture

All keyboard apps share one architecture, layered roughly as:

- **Key model** — a master key table mapping each supported key to: a numeric key code, the token text written to config files, and the display caption (plus shifted values, modifier metadata, etc.). See doc 05.
- **Layer model** — each device is modeled as a set of layers (2 for most devices, 5 for the Advantage360); each layer is an ordered list of key slots holding the original key, an optional remapped key, up to several macros, tap-and-hold configuration, and (on RGB devices) a per-key color.
- **Config-file engine** — parsers/serializers converting between the text files and the layer model. There are three dialects, one per device generation (see docs 04 and 06); the engine also validates lines and tracks invalid ones for user review.
- **File service** — v-Drive discovery, reading/writing layout/lighting/settings/version files, firmware version parsing (docs 03, 08, 09).
- **UI** — a per-device main window rendering the physical keyboard with clickable keys, plus shared feature dialogs (settings, firmware, tap-and-hold, multimodifiers, timing delays, macro selection, key search, export, diagnostics, troubleshooting — docs 10 and 11).

Data flow for the core edit loop:

```
v-Drive txt file ──load──> lines ──parse──> layers/keys model
        ▲                                        │
        │                                 user edits via UI
        └──────── serialize <── save ────────────┘
```

The legacy implementation is a native desktop app (Lazarus / Free Pascal with the LCL widget toolkit; 32-bit Windows and Cocoa macOS builds). Nothing in the file formats or device behavior depends on that technology choice.

## Documentation map

- `02-devices.md` — device catalog and per-device capabilities
- `03-vdrive-and-files.md` — v-Drive discovery, on-device folder/file structure, save/sync
- `04-layout-file-format.md` — remap/macro text file syntax (the core spec)
- `05-key-model.md` — key token tables and per-device layer definitions
- `06-macros.md` — macro semantics, triggers, speeds, limits
- `07-lighting.md` — RGB/TKO LED modes and led file format
- `08-settings.md` — keyboard settings files and app settings
- `09-firmware.md` — firmware version detection and update flow
- `10-apps-and-ui.md` — per-app UI structure and workflows
- `11-feature-dialogs.md` — tap-and-hold, multimodifiers, timing delays, export, etc.
- `12-savant-elite.md` — Savant Elite2 foot pedal app
