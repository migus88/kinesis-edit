# v-Drive services (Discovery, Io, Eject)

The filesystem layer of `KinesisEdit.Core`: find a mounted v-Drive, read/write its files with the exact semantics the firmware expects, and flush/release the volume so the firmware reloads them. Consumes device data from `KinesisEdit.Core.Devices.DeviceCatalog` ([domain-data.md](domain-data.md)) — no device facts are duplicated here.

| Namespace | Entry points | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.VDrive` | `VDriveLocation`, `VDriveDebugFlags` | Discovered-drive result + derived paths | 03 §3.3–3.4 |
| `KinesisEdit.Core.VDrive.Discovery` | `VDriveScanner`, `VDriveMonitor`, `PlatformVolumeEnumerator` | Find drives, poll connection states | 03 §2–3 |
| `KinesisEdit.Core.VDrive.Io` | `VDriveFileService` | Raw 8-bit line I/O + settings merge | 03 §5.1–5.2; 08 §1 |
| `KinesisEdit.Core.VDrive.Eject` | `VDriveEject.CreateForCurrentPlatform()` | Flush + release so firmware reloads | 03 §5.3 |

## Discovery

Two-stage design: platform **volume enumerators** produce `VolumeCandidate(RootPath, Label)` pairs; one shared **`VDriveScanner : IVDriveScanner`** validates candidates against the catalog. All platform variance lives in the enumerators; all validation logic is shared and unit-tested against temp-directory fixtures via a fake enumerator.

- `MacVolumeEnumerator` — lists subdirectories of `/Volumes` (injectable root); label = directory name. Handles macOS duplicate-mount suffixes: `ADV360 1` matches label `ADV360` (scanner-side, `<label> <digits>`).
- `WindowsVolumeEnumerator` — `DriveInfo.GetDrives()`, drive types Removable/Fixed/Network/CDRom/Ram per spec 03 §3.2; label = `VolumeLabel`.
- `LinuxVolumeEnumerator` — best-effort (Linux is unspecified in the specs; decision on issue #6): `/media/<user>` + `/run/media/<user>` (injectable roots).
- `PlatformVolumeEnumerator.Create()` — picks by OS; unknown platforms get an empty enumerator so the app degrades to demo mode.

`VDriveScanner.Scan()` per candidate: catalog label match (`DeviceCatalog.FindByVolumeLabel`) → skip non-detectable devices (null marker fields: CROSSFIRE, Adv360 Pro) → marker folder **and** marker file must exist, matched **case-insensitively via directory enumeration** (field drives are FAT; fixtures on Linux are case-sensitive) → writability probe → debug flags. First match per device wins; candidate-level I/O errors skip the candidate, never throw.

- **Writability** (`VDriveLocation.IsWritable`): open the version file `FileMode.Open`/`FileAccess.ReadWrite`. This is the project's implementation of spec 03 §3.3's "version folder writable AND version file writable" — a read-only FAT mount fails the probe; no stray files are created.
- **Debug flags** (`VDriveDebugFlags`, spec 03 §3.4): empty root files `debug.on` / `debug_firm.on` / `devmode.on` → `Debug` / `FirmwareDebug` / `DevMode`.
- **Derived paths** (spec 03 §3.4) are computed properties on `VDriveLocation` from catalog data, per device: `VersionFilePath`, `SettingsFolderPath`, `SettingsFilePath`, nullable `LayoutsFolderPath`/`LightingFolderPath` (Adv2/SE2 keep everything under `active/`; FS Pro has no lighting; Adv360's `settings.txt` doubles as version file).

### Monitor

`VDriveMonitor(IVDriveScanner, TimeSpan? pollInterval = 2 s)` tracks every detectable catalog device. `Poll()` runs one scan pass (tests call it directly — no timers in tests); `Start()` runs one synchronous poll (so `Statuses` is populated on return) then arms a `System.Threading.Timer`; `Stop()` disarms it; overlapping polls are dropped, not queued.

- `Statuses` — per-device `VDriveStatus` (`VDriveConnectionStatus`: `NotDetected` / `CannotAccess` (found, not writable) / `Connected`, + `Location`). Matches the legacy dashboard states (spec 03 §3.3, 10 "Detection loop").
- `StatusChanged` — `Action<VDriveStatusChange>` per device whose status changed; `VDriveStatusChange.IsConnectionLost` flags Connected → gone (the "Keyboard Connection Lost" trigger, spec 03 §3.5; the reopen hint is `DeviceDefinition.VDriveShortcutHint`).
- **Demo mode is not a monitor state**: it is the app-level condition "no device Connected" (spec 03 §3.5). In demo mode the app disables saves; nothing at this layer simulates a drive.

## Io — `VDriveFileService : IVDriveFileService`

Implements spec 03 §5.1–5.2 exactly. Encoding is **`Encoding.Latin1` everywhere** — the project's rendering of "raw 8-bit strings, no encoding conversion, no BOM handling": bytes 0x00–0xFF round-trip losslessly, a BOM in an input file is preserved as ordinary bytes, and UTF-8 is deliberately never used (load-bearing for the golden-file tests of the parser issues).

- `ReadAllLines(path)` — line-split tolerantly (`\r\n`, `\n`, lone `\r`); trailing newline yields no empty last line.
- `WriteAllLines(path, lines, allowCreate = false)` — **write refused (`FileNotFoundException`) unless the file exists**; operations the spec permits to create files (e.g. `layout<n>.txt` on save, 03 §5.3) opt in via `allowCreate`. Truncate-and-rewrite in place — no temp file, no atomic rename — native platform line endings, trailing newline. Missing parent directories are never created.
- `UpdateSettingsFile(path, values)` — read-modify-write per spec 08 §1: a line matches managed key K iff it starts with K case-insensitively **and the next char is `=`** (requiring the separator resolves the spec's prefix collisions — `v_drive` vs `v_drive_open_on_startup`, `cust_color_1` vs `cust_color_10` — without legacy's trailing-`=` special cases). Matching lines are replaced in place; unknown/reserved lines survive verbatim in order; absent keys are appended in caller order.

## Eject — `IVDriveEjector`

Rationale (spec 03 §5.3): the firmware only reloads its files once the v-Drive has been flushed and released — an unflushed save is the "save worked but nothing changed" bug class.

- `MacVDriveEjector` — runs `diskutil unmount <root>` through `IProcessRunner` (`SystemProcessRunner` wraps `Process`; tests use a fake). **`unmount`, not `eject`, is deliberate** (decision on issue #6): the spec only requires flush + release, and unmount lets the volume cleanly re-mount. This is new behavior — legacy hid eject on macOS entirely.
- `UnsupportedVDriveEjector` — `IsSupported` false; `Eject` returns a failed `VDriveEjectResult`, never throws. Windows (lock–dismount–eject sequence, spec 03 §5.3) and Linux are later issues; the interface is designed for them now.
- `VDriveEject.CreateForCurrentPlatform()` — macOS → real ejector, everything else → unsupported.

## Load-bearing invariants

1. **Latin1 in, Latin1 out.** Any UTF-8 in this layer corrupts bytes > 0x7F and breaks byte-exact round-trips. Never "fix" the encoding.
2. **The write-refusal rule is a safety feature** (spec 03 §5.2): it prevents scattering files onto arbitrary volumes when a path is wrong. `allowCreate` is per-call and deliberate.
3. **Device facts live in the catalog only.** Labels, marker/version/settings paths, layout folders all come from `DeviceDefinition`; this layer contains zero device-specific strings.
4. **Scanners never throw on bad candidates** — an unreadable volume is somebody else's mount, not an error.

## Deliberately not here

- **No file-content parsing** — version-file text is parsed by the firmware module ([firmware.md](firmware.md)); layouts, macros, and settings values are the parser issues (specs 04, 06, 08).
- **No legacy "running from the v-Drive" mode** (spec 03 §3.2 step 1) — dropped by decision on issue #6; discovery always scans mounts.
- **No `app_settings.txt` handling** (03 §6) — the app-settings module decides its local fallback location later.
- **No UI states/dialogs** — error strings, demo-mode UX, and the troubleshoot flow (03 §3.5, spec 10) belong to the app layer.
