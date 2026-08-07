# `docs/app/` — the app's module documentation

**Agent-first**: read the doc for a module instead of its source, and dip into source only for
particulars. This file is the router — find the doc here, then read that doc, not this one.
Nothing here restates a module's content; if you find yourself updating a description below
because behavior changed, the change belongs in the module's own doc.

Every doc opens the same way: a one-line statement of what the module is, then a table of
namespaces and entry points. Three section headings recur and are worth knowing about:

- **`## Load-bearing invariants`** — the rules the module's tests enforce. Read these before
  changing anything in it; they are the things that break quietly.
- **`## Deliberately not here`** — concerns that were consciously placed elsewhere, with the
  reason. Check this before adding a responsibility to a module.
- **`## Spec strings and deliberate deviations`** — where the UI's text came from, and every
  place the app knowingly departs from `specs/` or `docs/design/`, with why.

## Which doc answers which question

| If you need to know… | Read |
|---|---|
| Which project a type belongs in, what references what, build/test/CI commands | [solution-structure.md](solution-structure.md) |
| What devices exist, what each can do, key tokens, layer geometry, where a key sits | [domain-data.md](domain-data.md) |
| The editable runtime state — layers, keys, remaps, macros, validation | [keyboard-model.md](keyboard-model.md) |
| Finding the v-Drive, reading/writing its files, ejecting it | [vdrive.md](vdrive.md) |
| Parsing or writing `layouts/layoutN.txt` — remaps, tap-and-hold, macros, dialects | [layout-files.md](layout-files.md) |
| Parsing or writing `lighting/ledN.txt`; lighting modes, zones, colours | [lighting.md](lighting.md) |
| `kbd_settings.txt` / `app_settings.txt` — the **per-device** settings engine | [settings.md](settings.md) |
| Theme, motion budget, window geometry — the **per-user** store (not the above) | [host-preferences.md](host-preferences.md) |
| Firmware version parsing and which features a firmware gates | [firmware.md](firmware.md) |
| Loading, saving, importing or ejecting one numbered profile end to end | [profiles.md](profiles.md) |
| Turning real keypresses into assignments; what "swallowed" means | [keystroke-capture.md](keystroke-capture.md) |
| The window, dashboard, detection loop, notifications, Settings/Help screens, nav | [app-shell.md](app-shell.md) |
| The board picture, key inspector rail, macro library, Lighting/Settings tabs, shortcuts | [keyboard-editor.md](keyboard-editor.md) |
| The panels still modal over the editor (Search Keys, Export) and Import | [feature-dialogs.md](feature-dialogs.md) |
| The Savant Elite2 — `pedals.txt` and its pedal editor | [savant-elite.md](savant-elite.md) |
| Colour/type/geometry/motion tokens, control themes, icons, **and how to test UI work** | [design-system.md](design-system.md) |

## Module inventory

`KinesisEdit.Core` — UI-free domain:

| Namespace | Doc |
|---|---|
| `Devices`, `Keys`, `Geometry` (incl. `Geometry.Visual`) | [domain-data.md](domain-data.md) |
| `Model` | [keyboard-model.md](keyboard-model.md) |
| `VDrive` (`.Discovery`, `.Io`, `.Eject`) | [vdrive.md](vdrive.md) |
| `Layouts` | [layout-files.md](layout-files.md) |
| `Lighting` (incl. `Lighting.Preview`) | [lighting.md](lighting.md) |
| `Settings` | [settings.md](settings.md) |
| `Firmware` | [firmware.md](firmware.md) |
| `Profiles` | [profiles.md](profiles.md) |
| `Input` | [keystroke-capture.md](keystroke-capture.md) |
| `Transfer` | [feature-dialogs.md](feature-dialogs.md) |
| `SavantElite` | [savant-elite.md](savant-elite.md) |

`KinesisEdit` — the Avalonia app:

| Area | Doc |
|---|---|
| `Services`, shell view models, `Views` (shell, dashboard, Settings, Help) | [app-shell.md](app-shell.md) |
| `Services` host-preference store and its appliers | [host-preferences.md](host-preferences.md) |
| Editor view models and views, the key inspector rail, the tabs | [keyboard-editor.md](keyboard-editor.md) |
| `EditorOverlayHost` and the panels it hosts | [feature-dialogs.md](feature-dialogs.md) |
| The pedal editor | [savant-elite.md](savant-elite.md) |
| `Themes/`, `Styles/`, `Controls/`, `Assets/Fonts`, the headless test harness | [design-system.md](design-system.md) |

## Related references, not part of this folder

- `specs/` — the authoritative **domain** reference (legacy apps, devices, file formats). Start at
  [`specs/README.md`](../../specs/README.md). Docs here cite it by number and section; when they
  disagree, `specs/` wins on domain facts.
- `docs/design/` — the authoritative **design** reference for anything visual or user-facing, and a
  delivered artifact: read it, don't rewrite it. Start at [`docs/design/README.md`](../design/README.md).
  What the app actually does — including every deliberate deviation — is recorded here instead.
- `docs/guides/Coding Conventions.md` — how the code is written.

## Adding a doc

A new module gets a doc here, added to both tables above. Keep it token-efficient: enough for an
agent to work in the module without opening its source, not a tutorial. Follow the existing shape —
purpose line, entry-point table, then the recurring sections above as they apply.
