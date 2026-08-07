# Host preferences (per-user, per-machine)

The app's **host-side** preference layer: the things that belong to the person at this computer rather than to any keyboard — the theme, the motion budget, the shell window's last size and position, and how wide they dragged the editor's key inspector rail. A toolkit-free model and store, a tolerant JSON file engine, a platform path seam, and two thin bridging services that make a preference visible in the running app.

| Namespace | Entry point(s) | Encodes / Does |
|---|---|---|
| `KinesisEdit.Services` | `HostPreferences`, `WindowGeometry`, `AppThemePreference`, `MotionPreference` | The model. Toolkit-free, value-equal, defaults = "follow the OS, remember no window, leave the rail as authored" |
| `KinesisEdit.Services` | `IHostPreferencesStore`, `JsonHostPreferencesStore` | The one in-memory copy: `Current`, `Update(Func<…>)`, `Changed` |
| `KinesisEdit.Services` | `HostPreferencesParser`, `HostPreferencesSerializer`, `HostPreferencesJsonNames` | Tolerant read / canonical write of the JSON file |
| `KinesisEdit.Services` | `IHostPreferencesPathProvider`, `HostPreferencesPathProvider` | Where the file lives, per platform — and the seam that keeps tests out of it |
| `KinesisEdit.Services` | `ThemeApplier` | `AppThemePreference` → Avalonia `ThemeVariant`, onto the `Application` |
| `KinesisEdit.Services` | `MotionPreferenceApplier` | `MotionPreference` + the OS answer → `IMotionSettings.ReduceMotion`, then re-runs `MotionResourceBinder` |

## This is *not* `app_settings.txt` — read this before touching either

The app has **two** preference stores and they are easy to confuse. They are not variants of one thing; they answer different questions and live on different disks.

| | **Host preferences** (this doc) | **App preferences** (`app_settings.txt`) |
|---|---|---|
| Type | `IHostPreferencesStore` / `JsonHostPreferencesStore` | `IAppPreferencesStore` / `VDriveAppPreferencesStore` |
| Model | `HostPreferences` (in `KinesisEdit.Services`) | `AppSettings` (in `KinesisEdit.Core.Settings`) |
| File | `<per-user config>/KinesisEdit/preferences.json` | `settings/app_settings.txt` **on the keyboard's v-Drive** |
| Scope | **Per user, per machine** | **Per device** — travels with the board |
| Format | JSON | The spec 08 §3 `key=value` text format, shared with the legacy Pascal app |
| Carries | Theme, motion, window geometry, inspector-rail width | The 17 notification/display preferences and the 12 `cust_color_N` swatches |
| Written in demo mode | Yes | **No** (spec 08 §3) |
| Docs | here | [settings.md](settings.md) |
| Spec | none — net-new, issue [#96](https://github.com/migus88/kinesis-edit/issues/96) | specs/08-settings.md §3 |

The Settings screen's own section label says it: *"App & notifications — stored per device"* (mockup 1j). Anything in **this** store is stored per *user*.

**Why they cannot be merged.** A host preference has to be readable with no keyboard attached — the window opens before any drive is scanned, and the theme must already be right on the first frame. A device preference has to follow the board to whatever machine it is plugged into. Neither file can do the other's job. So: do not extend `IAppPreferencesStore` to carry a theme, do not put a hide-flag in `HostPreferences`, and do not add a second reader or writer to either file (settings.md invariant 8 is unchanged and now has a sibling rule).

## The model

- **`AppThemePreference`** — `FollowSystem` (0) / `Light` / `Dark`.
- **`MotionPreference`** — `FollowSystem` (0) / `AlwaysReduce` / `NeverReduce`.
- **`WindowGeometry`** — `Width`, `Height` (DIPs, matching `Window.Width`/`Height`), optional `X`, `Y` (screen pixels, matching `Window.Position`), `IsMaximized`.
- **`HostPreferences`** — `Theme`, `Motion`, `Window` (nullable), `InspectorRailWidth` (nullable `double`). `HostPreferences.Default` is `FollowSystem`, `FollowSystem`, no geometry, no rail width — i.e. exactly the app as it behaved before there was a choice.

`FollowSystem` is the **zero value of both enums** on purpose: a `default(T)`, an unset field and a fresh record all have to land on the option that changes nothing.

`HostPreferences` is a `record` and equality is by value, including the geometry. That is not decoration — it is what lets the store detect a no-op write (below).

**Negative `X`/`Y` are legal.** A monitor placed left of or above the primary one has negative coordinates; rejecting them would drag the window back onto the main screen at every launch.

**`InspectorRailWidth` is the keyboard editor's key inspector rail, in DIPs, and `null` means "as authored".** Issue [#119](https://github.com/migus88/kinesis-edit/issues/119) made the rail drag-adjustable; the width it is dragged to is a fact about this person's screen and their taste, not about any board, so it is stored here rather than in `app_settings.txt` — and it has to be right on the first frame of an editor opened over a keyboard that has never been plugged into this machine.

**The rail's band lives on `HostPreferences` and is written twice, on purpose.** `MinimumInspectorRailWidth` (240), `DefaultInspectorRailWidth` (268) and `MaximumInspectorRailWidth` (520) are plain C# constants, and `ClampInspectorRailWidth(width)` is the one rule that applies them — a non-finite width is not a width at all and yields the default, because `Math.Clamp` propagates NaN and a NaN on a column definition takes the whole tab's measure pass with it. The **same** three numbers are geometry tokens (`WidthInspectorRailMin` / `WidthInspectorRail` / `WidthInspectorRailMax`) so the splitter can bound the drag, but a view model may not read Avalonia resources ([app-shell.md](app-shell.md) invariant 8) — hence two copies, pinned to each other by `KeyboardEditorViewModelTests.TheRailsWidths_AreWrittenTwice_AndMustAgree`.

**The size range lives in `WindowGeometry` and nowhere else.** `WindowGeometry.TryCreate` returns `null` for a width or height that is non-finite, ≤ 0 or beyond `MaximumExtent` (32000) — and null means *no stored geometry*, which is the fresh-install state, not a window that cannot be shown. `MinimumExtent` is 1, deliberately **not** the shell's 720×480 floor: this rejects garbage, it does not restate a minimum size that belongs to the window.

## The store — `IHostPreferencesStore`, `JsonHostPreferencesStore`

Same shape as `IAppPreferencesStore`, because it solves the same problem: several consumers (the Settings screen, the window's geometry handler, the two appliers) over one file, and a second reader would show the others stale state. `Current` / `Update(Func<HostPreferences, HostPreferences>)` / `Changed`.

- **The file is read once, on first access, and then lives in memory.** Not re-read per property, never held open.
- **`Update` takes a function, not a value.** Two callers write different fields of the same record — the Settings screen writes the theme while the window's close handler writes the geometry — and a set-the-whole-record API would let the second clobber the first with the copy it read a moment earlier. A mutation always sees the newest state. A mutation returning null throws (that is a caller bug, not an I/O failure).
- **A mutation that returns an equal record does nothing at all** — no write, no `Changed`. The Settings screen binds to `Changed`, and a no-op re-entering every binding is a loop waiting to happen. This is what the record's value equality buys.
- **`Changed` is raised after `Current` has moved**, and outside the lock.
- **Nothing throws for a filesystem problem.** A failed read yields `HostPreferences.Default`; a failed write is dropped *and the in-memory value still moves*, so the app agrees with what the user just did even when the disk refused. Both guards are deliberately broad and deliberately silent — a preference is never worth a failed launch or a broken screen.
- **Serialization happens outside the write guard**, on purpose: a defect in the serializer is a bug, not an I/O failure, and swallowing it with one would hide it forever.

## The file — `HostPreferencesParser`, `HostPreferencesSerializer`

`System.Text.Json`, no new package. Written indented, enum values **by name** (a number would tie the file to the enum's declaration order forever), property names in `HostPreferencesJsonNames`.

```json
{
  "theme": "Dark",
  "motion": "AlwaysReduce",
  "window": { "width": 1000, "height": 680, "x": -1440, "y": 25, "maximized": false },
  "inspectorRailWidth": 412
}
```

**Read tolerantly, write the current form** — the same rule the on-device file engines follow, for the same reason. `HostPreferencesParser.Parse` has no input that makes it throw:

| Input | Result |
|---|---|
| null / empty / whitespace (no file yet) | defaults |
| unparseable JSON, trailing comma, binary | defaults |
| root is an array/string/number/null | defaults |
| unknown keys | ignored; the modelled keys still apply |
| wrong-typed value (`"theme": 3`) | that field only falls back |
| unknown enum name (`"theme": "Chartreuse"`, `"7"`) | that field only falls back |
| `window` not an object, or missing width/height | no stored geometry |
| width/height out of range or non-finite | no stored geometry |
| one bad coordinate | that coordinate is dropped, the geometry survives |
| non-boolean `maximized` | false |
| `inspectorRailWidth` missing, or not a number (`"320"`, `true`, `null`, `[]`, `{}`) | no stored width — the rail opens as authored |
| `inspectorRailWidth` non-finite, ≤ 0, or > `WindowGeometry.MaximumExtent` | no stored width |
| `inspectorRailWidth` outside 240…520 (`9999`, `100`) | **clamped** into the band |

**A rail width is clamped on load; a nonsensical one is not stored.** The distinction is the point: `9999` is a legible request for the widest rail, so it becomes 520 rather than stranding the rail off screen, while `-320`, `0` and `1e30` are not widths at all and clamping them would invent a preference the user never expressed. The upper cut-off is `WindowGeometry.MaximumExtent`, which is already this module's line between "a size" and garbage.

A null `InspectorRailWidth` writes **no** key, exactly as a null `Window` writes no object; a non-finite one — only a hand-built record can carry it — is dropped rather than thrown over, for the same `Utf8JsonWriter` reason.

**It reads field by field rather than calling `JsonSerializer.Deserialize<T>`** — the serializer throws on a wrong-typed value and takes the whole file with it, so one hand-edited line would cost the user their theme *and* their window position. Property names and enum names both match case-insensitively.

A null `Window` writes **no** `window` object rather than `"window": null`; the serializer also omits a geometry that is not `IsUsable`, because `Utf8JsonWriter` throws on NaN and a preference save may not throw.

## The path seam — `IHostPreferencesPathProvider`

`<configuration root>/KinesisEdit/preferences.json`, where the root is:

| Platform | Root |
|---|---|
| macOS (primary) | `~/Library/Application Support` |
| Windows | `%APPDATA%` |
| Linux | `$XDG_CONFIG_HOME`, else `~/.config` |

The roots are written out rather than taken from `Environment.SpecialFolder.ApplicationData` everywhere, because .NET maps that folder to `~/.config` on macOS — right for Linux, wrong for the primary platform. With no home directory at all (a stripped container, a service account) it falls back to `AppContext.BaseDirectory`, mirroring the "next to the executable" fallback specs/03 §6 describes for the legacy app: a relative path would follow the working directory and move between launches.

**The seam exists so the test suite can never write to the real user config directory, and that is asserted, not assumed** (`HostPreferencesPathProviderTests`):

- `JsonHostPreferencesStore` **resolves no path of its own** — there is deliberately no `CreateForCurrentPlatform()` on it. `HostPreferencesPathProvider.CreateForCurrentPlatform()` is the only thing in the app that names the real location, and the composition root is the only caller. A reflection test fails if a constructor stops requiring a provider or a static factory returning a store appears.
- `TemporaryHostPreferences` (the test helper every file-backed store in the suite is built through) asserts *in its own constructor* that its root is under the temp directory and is not the real path.
- `TheSuite_NeverWrites_ToTheRealUserConfigurationDirectory` snapshots the real file's existence and last-write time, exercises a full write through the temporary store, and asserts neither moved.

## Applying a preference — `ThemeApplier`, `MotionPreferenceApplier`

The model and the store are toolkit-free (app-shell.md invariant 8); these two are the bridging services that touch Avalonia, in the same folder and the same shape as `MotionResourceBinder`. In both, the *decision* is a pure static (`ToThemeVariant`, `Resolve`) and only `Apply` touches application state. `HostPreferencesToolkitFreedomTests` asserts both halves — that the model/store/parser/path types reference no Avalonia type, and that these two do.

**`ThemeApplier.Apply(application, preference)`** sets `Application.RequestedThemeVariant`:

| Preference | `ThemeVariant` |
|---|---|
| `FollowSystem` | `ThemeVariant.Default` |
| `Light` | `ThemeVariant.Light` |
| `Dark` | `ThemeVariant.Dark` |

`Default` is what `App.axaml` declares, so assigning it is how the app goes *back* to following the OS after a forced light or dark. Every colour in the app is a role token resolved with `DynamicResource` and both variants are declared ([design-system.md](design-system.md)), so this re-resolves the whole palette live.

**`MotionPreferenceApplier.Apply(application, motionSettings, preference)`** resolves the preference onto `IMotionSettings.ReduceMotion` **and re-runs `MotionResourceBinder.Apply`**. The second half is not optional: the motion aliases are entries `MotionResourceBinder` writes into `Application.Resources` and they are **not** recomputed on their own, so flipping `ReduceMotion` alone changes nothing anyone can see. Views bind the aliases with `DynamicResource`, so a screen already on display re-points with them.

### `IMotionSettings.SystemReduceMotion` — why it was added

`MotionSettings` asks `IReduceMotionDetector` **once, in its constructor**, and nothing re-reads the OS while the app runs ([design-system.md](design-system.md)). But `MotionPreference.FollowSystem` has to be able to go *back* to the OS answer after the user has overridden it — and the first override would have destroyed the only copy of it.

So `MotionSettings` now remembers the answer twice: `ReduceMotion` is the live switch every style reads and anything may write, and **`SystemReduceMotion` is the OS's answer, frozen and never overwritten**. `MotionPreferenceApplier.Resolve(preference, systemReduceMotion)` is the whole rule:

| Preference | Result |
|---|---|
| `AlwaysReduce` | `true` |
| `NeverReduce` | `false` |
| `FollowSystem` (and anything unrecognised) | `systemReduceMotion` |

Nothing re-reads the OS to answer this, which matters because the macOS detector shells out to a process.

## Load-bearing invariants

1. **Host preferences and `app_settings.txt` are two stores over two files with two scopes, and neither may grow into the other.** The table at the top is the whole rule. A preference about a *keyboard* goes on the drive; a preference about the *person or the machine* goes here.
2. **`JsonHostPreferencesStore` resolves no path of its own.** The only route to the real per-user directory is `HostPreferencesPathProvider.CreateForCurrentPlatform()`, called by the composition root. Adding a convenience factory on the store puts the developer's own preferences file one careless call away from every test in the suite — and there is a test that fails if one appears.
3. **`Update` takes a function, and an equal result is a no-op.** Do not "simplify" it to a value setter (two writers would clobber each other) and do not make it raise `Changed` unconditionally (the Settings screen binds to it).
4. **No filesystem failure escapes the store, in either direction.** A read that fails means defaults; a write that fails is dropped while the in-memory value still moves. A preference is never worth taking the app down for.
5. **Anything that changes `IMotionSettings.ReduceMotion` must re-run `MotionResourceBinder.Apply`.** Use `MotionPreferenceApplier`; do not set the flag by hand. The aliases are not recomputed on their own, so a flag set without the re-bind is a preference that silently does nothing.
6. **`SystemReduceMotion` is never written.** It is the record of what the OS said, and it is the only thing that makes `FollowSystem` reversible. Overwriting it, or "simplifying" it away in favour of re-asking the detector, breaks the preference and puts a process launch behind a radio button.
7. **The model stays toolkit-free.** A `ThemeVariant` on `HostPreferences` would be one line and would make the store untestable without a UI runtime, and would put the file's schema at the mercy of a toolkit type's name. `ThemeApplier` and `MotionPreferenceApplier` are the only Avalonia-aware types in this module (invariant 8).

## Deliberately not here

- **No composition-root wiring, no Settings screen, no window-geometry capture, no rail — *in this module*.** All four now exist; they simply are not here, because this module is the store, the file and the two appliers. Who builds them is `App.axaml.cs` (inside the desktop-lifetime branch, before any window: it reads `Current` once and runs both appliers, so the first frame already wears them), who binds to them is `SettingsScreenViewModel`, and who reads the live window's size is `MainWindow.RestoreGeometry`/`PersistGeometry` — all three described in [app-shell.md](app-shell.md). **`InspectorRailWidth`'s one reader and one writer is `KeyboardEditorViewModel`**, which the composition root reaches through `EditorViewModelFactory`'s optional `IHostPreferencesStore`: it reads `Current` once in its constructor (nothing else writes the width, so a `Changed` subscription could only tell it what it just did) and writes through `Update` on every real change of `InspectorRailWidth`. It is **not** debounced and does not need to be — an unchanged width returns before it writes, which is what turns a continuous drag into one write at its commit. The rail itself is [keyboard-editor.md](keyboard-editor.md)'s.
- **No migration or schema version.** The file is net-new and the tolerant read is the migration strategy: an unknown key is ignored and a missing one is a default, so an older or newer file is always readable.
- **No file watching.** One process owns this file; the store does not notice a hand edit made while the app is running, and picks it up on the next launch.
- **No atomic write.** `File.WriteAllText`, not write-temp-then-rename. A truncated preferences file degrades to defaults on the next read rather than corrupting anything, which is the same cost as the failure the rename would have prevented.
- **No per-device override of a host preference** (e.g. "dark theme, but only for this board"). Nothing in the mockups asks for it, and it would need both stores to agree on a precedence rule.
