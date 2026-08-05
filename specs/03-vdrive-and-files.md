# 03 — The v-Drive: Detection, On-Device File Layout, and File I/O

Scope: how the legacy SmartSet apps find the keyboard's virtual USB drive ("v-Drive"), the exact folder/file layout expected on each device, how files are read and written, and the save + eject sequence.

## 1. The v-Drive concept

Kinesis programmable keyboards/pedals expose an on-board mass-storage volume (the "v-Drive") containing plain text configuration files. The keyboard only mounts it when the user presses an on-board shortcut; all programming is done by reading/writing text files on that volume, then ejecting it so the keyboard re-reads the files. Per-device open shortcuts shown to users:

| Device | On-board shortcut to open v-Drive |
|---|---|
| Advantage 360 | `SmartSet + v-Drive` |
| Advantage 2 | `Program + F1` |
| Freestyle Edge RGB | `SmartSet + F8` |
| TKO | `SmartSet + Right Shift + V` |
| FS Edge / FS Pro | `SmartSet + F8` (shown in the "Keyboard Connection Lost" dialog) |

The reverse operation (disconnect the v-Drive so the keyboard reloads settings) is either done from the keyboard (`SmartSet + F8` again) or by the app ejecting the volume (section 5).

## 2. Volume labels per device

A drive is only accepted if its volume label (uppercased, trimmed) matches one of the device's known names:

| Exact label | Device |
|---|---|
| `FS EDGE RGB` | Freestyle Edge RGB |
| `FS EDGE` | Freestyle Edge |
| `FS PRO` | Freestyle Pro |
| `ADVANTAGE2` | Advantage 2 (primary) |
| `KINESIS KB` | Advantage 2 (alternate) |
| `ADV2` | Advantage 2 (alternate) |
| `SE2` | Savant Elite 2 pedal (primary) |
| `KINESIS FP` | Savant Elite 2 pedal (alternate) |
| `CROSSFIRE KEYPAD` | Crossfire keypad (reserved; device never supported) |
| `TKO` | TKO |
| `ADV360` | Advantage 360 |

The FS Edge and FS Pro apps accept *either* `FS EDGE` or `FS PRO` labels.

## 3. Drive detection

There are two detection paths that implement the same rules: one used by the standalone apps, one used by the master dashboard apps.

### 3.1 Marker folder + marker file

A candidate volume qualifies only if a specific folder **and** file exist on it:

| Device | Marker folder | Marker file |
|---|---|---|
| RGB, TKO, FS Edge, FS Pro | `firmware` | `version.txt` |
| Advantage 2 | `active` | `version.txt` |
| Savant Elite 2 | `active` | `version.txt` |
| Advantage 360 | `settings` | `settings.txt` |

### 3.2 Standalone-app detection

The standalone apps (and the Adv360 editor form) locate the drive as follows:

1. The application path starts as the executable's directory. If `<exe dir>/<marker folder>` exists next to the executable, the app assumes it *is running from the v-Drive itself* and uses that directory as the drive root (legacy distribution mode: app shipped on the drive).
2. Otherwise the app enters desktop mode and scans:
   - **Windows**: enumerates `A:\` … `Z:\`, accepting removable, fixed, network, CD-ROM, and RAM-disk drive types; critical-error dialogs are suppressed during the scan. A drive matches when the marker folder/file exist **and** its volume label equals one of up to 3 candidate names for the device.
   - **macOS**: no enumeration; fixed mount paths `/VOLUMES/<label>/` are probed for each candidate label and the marker folder/file are checked there. The label check is implicit in the mount-point name.
3. On success the drive root becomes the application path and all derived paths are recomputed (section 3.4).

### 3.3 Master-dashboard detection

The two "master" apps (gaming and office) keep a list of device records with defaults: version file `version.txt`, settings file `kbd_settings.txt`, version folder `firmware`, settings folder `settings`. Per-device overrides are applied when the dashboard builds its list — e.g. Advantage 360 (version and settings both in `settings\settings.txt`) and Advantage 2 (settings folder `active`, settings file `state.txt`).

For each device the same scan is run and fills in:

- drive letter (Windows only), root folder (e.g. `E:\` or `/VOLUMES/ADV360/`), connected = true;
- read/write access = the version folder is writable **and** the version file itself is writable.

The dashboard polls this on a timer and shows "Connected" / "Not Detected" / a no-access state, plus the active profile ("Layout `<name>`" for Adv2 from its startup file name, "Profile `<n>`" for other devices from the startup file number). A "file exists but is not writable" condition is detected and reported distinctly.

### 3.4 Paths derived from the drive root

All working paths derive from the detected drive root:

| Derived path | Value |
|---|---|
| Layout files | `<drive root>\layouts\` |
| LED files | `<drive root>\lighting\` |
| Settings files | `<drive root>\settings\` |

Debug/dev switches: dropping an empty file named `debug.on`, `debug_firm.on`, or `devmode.on` at the drive root (or next to the exe in dev runs) enables the corresponding debug behaviors (debug mode, firmware-debug mode, dev mode).

### 3.5 Demo mode / connection loss

- If no drive is found at startup, a troubleshoot dialog offers "Scan for v-Drive" or demo mode. In the master apps, opening a device that is not connected or not writable also enters demo mode (demo mode = not connected, or no read/write access).
- In demo mode all saving is suppressed: layout save aborts, app settings are never written, and save/eject buttons are hidden.
- While running, an idle timer re-checks the drive; a failed re-check on save shows "Keyboard Connection Lost" with the reopen shortcut.

## 4. On-device folder and file structure

### 4.1 RGB, TKO, FS Edge, FS Pro (Gen1 "newer" devices)

```
<drive root>/
  firmware/version.txt          (firmware/model identification; update .upd files go here)
  layouts/layout1.txt … layout9.txt
  lighting/led1.txt … led9.txt  (RGB/TKO/FS Edge only; FS Pro has no lighting)
  settings/kbd_settings.txt     (keyboard settings, see doc 08)
  settings/app_settings.txt     (app notification settings, see doc 08)
```

- Layout and led files are numbered 1–9, with base names `layout` and `led`; file names are built as `layout<n>.txt` / `led<n>.txt`. Only these numbered files are used; any other files present in `layouts/` or `lighting/` are ignored.
- Example `firmware/version.txt` content: `Model name: FS Edge RGB` / `KBD Firmware: 1.0.1709.us (4MB), 03/08/2019` / `LED Firmware: 1.0.521` / `LED Bootloader: 255.255` (the bootloader line is not parsed).

### 4.2 Advantage 2

```
<drive root>/
  active/version.txt            (firmware identification; contains "Firmware version: …" and 2MB/4MB marker)
  active/state.txt              (keyboard settings)
  active/<pos>_qwerty.txt / <pos>_dvorak.txt   (layout files; <pos> is a hotkey position)
```

Layout files are named `<position>` + `_qwerty.txt` or `_dvorak.txt`; the load validation message says a valid file must be "a valid qwerty or dvorak layout file from the Active subfolder". The tokens `qwerty` and `dvorak` in the file name select the base key map. If the `version.txt` content contains the marker `4MB`, the app allows editing settings; 2MB firmware locks the settings UI.

### 4.3 Advantage 360

```
<drive root>/
  settings/settings.txt         (single file: keyboard settings AND version info; keys 'model', 'kbd_fw_r')
  settings/app_settings.txt
  layouts/layout1.txt … layout9.txt
  lighting/led1.txt … led9.txt
```

For the Adv360, the version folder and settings folder are both `settings`, and the version file and settings file are both `settings.txt`. Layout/led numbering is identical to Gen1.

### 4.4 Savant Elite 2 pedal

```
<drive root>/
  active/pedals.txt             (pedal assignments)
  active/version.txt            (e.g. "Firmware version is 1.0.44")
```

The pedal file lives at `active/pedals.txt` next to `active/version.txt`. Pedal file lines use tokens `[lpedal]>…`, `[mpedal]>…`, `[rpedal]>…`, `[jack1]>…` … `[jack4]>…` for single-key assignments, or curly-brace forms such as `{lpedal}>…` for multi-key/macro assignments.

## 5. File read/write behavior

### 5.1 Reading

- Files are read line by line until end of file. Lines are treated as raw 8-bit strings; **no encoding conversion or BOM handling** (files are plain ASCII/ANSI; the app's own writes are ASCII).
- On a read error: for a critical file the user-facing error is `'A file error has occurred. Please disconnect and re-connect the v-Drive and try launching the SmartSet App again.'`; otherwise `'Error loading file: <path>, <error>'`.
- Missing-file errors from higher-level loaders: `'State.txt configuration file not found'`, `'<settings file> file not found'`, `'Version.txt file not found'`, `'<file> not found'` for layouts.

### 5.2 Writing

- A write is refused unless the file already exists or creating new files is explicitly allowed for that operation (error `'<file> not found'`).
- The file is **truncated and fully rewritten** line by line, in place — no temp file, no atomic rename, and no backup copy. Native platform line endings are produced.
- Settings files are updated read-modify-write: the current file is loaded, each managed key is replaced in place (or inserted/appended if absent) using case-insensitive substring matching, and unknown lines are preserved.

### 5.3 "Save to v-Drive" + eject sequence

Saving a profile in RGB/TKO/Adv360:

1. Abort in demo mode; re-check the v-Drive, otherwise show the connection-lost dialog.
2. Validate (macro count/keystroke limits).
3. Write `layout<n>.txt` then `led<n>.txt` (creating them if absent).
4. Immediately eject the device so the OS flushes and the keyboard can take the drive back.
5. Show a "Profile n Saved" notification, e.g. RGB: `'Use the Refresh Shortcut (SmartSet + Profile) to preview your Layout and Lighting updates or simply Eject the "FS EDGE RGB" drive in File Explorer and then disconnect the v-Drive (SmartSet + F8).'`

Saving keyboard settings behaves the same way and shows `'Changes will be implemented when v-Drive is closed.'` followed by the eject.

Ejecting — the keyboard firmware only reloads its files once the v-Drive has been flushed and released, so after every save the volume must be ejected/synced by the app, ejected by the user in the OS, or closed with the on-board shortcut:

- **Windows**: the app shows an info dialog `'Disconnecting v-Drive'`, then ejects the volume via the OS: open the volume handle, lock the volume (3 retries, 500 ms apart), dismount it, disable media-removal prevention, and eject the media. Success → `'Safe To Remove Hardware'` dialog; failure → `'Cannot eject v-Drive — Close all open files and folders on the v-Drive, and try ejecting again.'`.
- On other platforms the user ejects/unmounts the volume in the OS or uses the on-board close shortcut.

### 5.4 Factory reset and diagnostics

- Factory reset deletes the version folder, `layouts/`, and `lighting/` directories from the drive.
- The diagnostics report concatenates the version file, keyboard settings, app settings, `layout1..9.txt`, and `led1..9.txt` into one report, written to the user's Desktop as `<serial>.txt`.

## 6. App-side (PC/Mac) files

| Data | Location |
|---|---|
| `app_settings.txt` (notification prefs, custom colors) | **On the v-Drive**, `settings/` folder. When the app runs from the v-Drive itself (legacy distribution mode) or in dev runs with no drive attached, the path resolves next to the executable. Never written in demo mode. |
| Master-app notification toggles | Windows registry `HKEY_CURRENT_USER\SOFTWARE\KINESIS` |
| Diagnostics report | Desktop, `<serial>.txt` |
| App version | Embedded Windows/Mac version resource, read at runtime |
