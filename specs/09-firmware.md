# 09 — Firmware: Version Detection, Feature Gating, and the Update Flow

Scope: how each app determines the keyboard/lighting firmware version from the v-Drive, the exact minimum-version gates for features, the "Check for Updates" dialog flow (including the online version endpoint), and app self-update behavior.

## 1. Version files on the device

| Device | File | Line prefixes parsed (case-insensitive) |
|---|---|---|
| FS Edge / FS Pro / Adv2 | `firmware/version.txt` (Adv2: `active/version.txt`) | `model name`, `firmware version` |
| RGB / TKO | `firmware/version.txt` | `model name`, `kbd firmware`, `led firmware` |
| Advantage 360 | `settings/settings.txt` (shared with keyboard settings) | `model`, `kbd_fw_r` (no LED version) |
| Savant Elite 2 | `active/version.txt` | free-text; sample: `Firmware version is 1.0.44` + a date line |

Example version files as shipped on real devices:

- RGB:
  ```
  Model name: FS Edge RGB
  KBD Firmware: 1.0.1709.us (4MB), 03/08/2019
  LED Firmware: 1.0.521
  LED Bootloader: 255.255
  ```
  (The `LED Bootloader` line is never parsed.)
- FS Edge: `Model name: FS Edge` / `Firmware version: 1.0.340.us (2MB), 09/26/2016`.

### 1.1 Parsing

Two readers share the same parsing logic:

- One reads the version file from the app's active base path and is used by the main forms as the connection test; a missing file produces the error `'Version.txt file not found'`.
- The other reads from the device root and returns a firmware-info record (fields initialized to empty string / `-1`); it is used by the firmware dialog.

Line handling: lowercase the line, prefix-match, take the remainder after the prefix plus one separator character (this skips the `:` or the `_r` suffix separator), then trim. Versions are parsed from the first three dot-separated numeric tokens into major/minor/revision (a non-numeric token parses as 0), so `1.0.1709.us (4MB), 03/08/2019` parses as `1 / 0 / 1709` and trailing text is ignored.

Adv2 special case: while scanning `version.txt`, if any line contains the literal marker `4MB`, settings editing is enabled; the app starts with it disabled, so boards whose version file carries the `2MB` marker cannot edit settings from the app.

### 1.2 Comparison

Version comparisons (equal / bigger-or-equal / smaller) are proper lexicographic comparisons on major → minor → revision. The same comparison is used for keyboard firmware, lighting firmware, and app versions.

## 2. Minimum-firmware feature gates (all)

Every gate also passes in demo mode.

| App | Version requirement | Gated feature |
|---|---|---|
| FS Edge/Pro | KBD ≥ 1.0.340 | 100 macros instead of 24; custom + random macro delays. Refusal dialog: `'To utilize custom or random delays, please download and install the latest firmware.'` |
| FS Edge/Pro | KBD ≥ 1.0.480 | Hyper/Meh multimodifiers; Tap and Hold. Refusal dialogs: `'To utilize Multimodifiers…'`, `'To utilize Tap and Hold Actions…'` |
| Advantage 2 | KBD ≥ 1.0.516 | Hyper/Meh; Tap and Hold |
| RGB | KBD ≥ 1.0.1 | Tap and Hold; Hyper/Meh multimodifiers |
| RGB | KBD ≥ 1.0.121 **and** LED ≥ 1.0.58 | Ripple and Fireball lighting effects |
| RGB | LED ≥ 1.0.44 | Lighting layer switch (Fn-layer lighting) and per-layer base color panel |
| RGB | LED = 1.0.44 or LED = 1.0.58 (exact match) | One-time "Expansion Pack" dialog after an LED-firmware update, offered only if no led file contains the `fn ` prefix yet: pack 1 mirrors the current lighting to the Fn layer; pack 2 loads 9 preset lighting files |
| TKO | KBD = 1.0.0 (exact match) | Startup warning: `'Attention macro users: Update your firmware now for full functionality.'` with an "Upgrade Firmware" button |
| Advantage 360 | KBD ≥ 1.0.69 | Macro selection buttons inside the Tap & Hold editor |

"Upgrade Firmware" buttons open the per-device support pages:

- FS Pro: `https://kinesis-ergo.com/support/freestyle-pro/#firmware-updates`
- FS Edge: `https://gaming.kinesis-ergo.com/fs-edge-support/#firmware`
- Adv2: `https://kinesis-ergo.com/support/advantage2/#firmware-updates`
- RGB: `https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#firmware`
- TKO: `https://gaming.kinesis-ergo.com/tko-support/#firmware`
- Adv360: `https://kinesis-ergo.com/support/kb360/#firmware-updates`

## 3. The "Check for Updates" dialog

Used by RGB, TKO, and Adv360 (opened from the main form's Firmware button or the master dashboard's "Check for Updates"). The dialog requires a connected device.

UI: title `Check for Updates`; three rows labeled `Keyboard Firmware :`, `Lighting Firmware :`, `SmartSet App :`, each with a button initially captioned `Checking for update...`. For Adv360 the Lighting row is hidden and the form shrinks.

Flow, step by step (kicked off by a timer when the form is shown):

1. Read local versions from the device's version file. If the keyboard version comes back empty → all three buttons show `Error reading firmware file` and nothing else happens.
2. Read the app's own version from its embedded executable version resource (four numeric components, formatted `major.minor.revision.build`), then parsed like any other version (first three dot-separated numeric tokens).
3. HTTP GET the published-versions endpoint (a plain GET using the platform's native HTTP facility):
   - Gaming master: `https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions`
   - Office master: `https://kinesis-ergo.com/wp-json/ksv/v1/get_versions`

   Expected response shape: `{"keyboard_ver":"1.0.0","lighting_ver":"1.0.0","app_ver":"2.0.20", "mac_app_ver":"2.1.3"}`. If firmware-debug mode is enabled (presence of a file named `debug_firm.on` in the drive root), the raw JSON is shown in a dialog.
4. Parse the JSON and pick keys per device:

   | Device | Keyboard key | Lighting key |
   |---|---|---|
   | TKO | `tko_keyboard_version` | `tko_lighting_version` |
   | RGB | `keyboard_ver` | `lighting_ver` |
   | Adv360 | `kb360_version` | `kb360_version` (same value; row hidden) |

   App key: Windows gaming `app_ver`, Windows office `pc_app_version`; macOS gaming `mac_app_ver`, macOS office `mac_app_version`.
5. For each of keyboard / lighting / app: if the local version is smaller than the remote one, the button becomes `Update Now` in the accent color with a hand cursor; otherwise `No update available`. An empty remote value → `Error fetching keyboard firmware` / `Error fetching lighting firmware` / `Error fetching app version`.
6. Any exception (no internet, bad JSON): all buttons show `Check connection` and a dialog `'Error accessing internet or firmware website: <message>'` appears.
7. Clicking an `Update Now` button does **not** transfer firmware; it opens the support website: keyboard/lighting → `<help URL>#firmware` (Adv360: its firmware-updates URL above); app → `<help URL>#smartset-app`. The user downloads and installs manually.

### 3.1 `update.upd` mechanism

The keyboard firmware applies a file named `update.upd` found in the device's `firmware` folder. The legacy app included a hands-free flow targeting this mechanism (download a firmware ZIP into `<device root>/firmware/`, unzip it, replace any existing `update.upd` with the extracted file, then report `'Ready to update!'`) but never enabled it; all updates are delivered through the support-site links above.

## 4. App self-update

There is no self-updating mechanism. The app's own version participates only in the dialog above (row `SmartSet App :`), where it is compared against the endpoint's app key and linked to the `#smartset-app` section of the support site. The FS Edge/Pro, Adv2, and Savant Elite apps have no update dialog at all — only the firmware-gate dialogs of §2 that link to the support pages.
