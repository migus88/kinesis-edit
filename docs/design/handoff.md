# Handoff: KinesisEdit — Kinesis keyboard configuration app

**Target: Avalonia UI on .NET 10** (explicit requirement — see "Target stack").

## Overview
KinesisEdit is a desktop configuration app for Kinesis programmable input devices. Its core premise: these keyboards **can't talk back** — each device mounts a **v-Drive** (a USB mass-storage volume containing plain-text config files). The app discovers mounted v-Drives by polling, parses the files, lets the user edit layouts / macros / lighting / settings, and writes the files back. Nothing is live; state is inferred from the filesystem, and the keyboard only reloads when the user ejects.

Devices covered by the design:
- **Freestyle Edge RGB** — split, 2 layers (Top/Fn), per-key RGB
- **Advantage 360** — split contoured, 5 layers, 6 indicator LEDs, flat macro library
- **Advantage2** — 2 MB hardware (device settings read-only), QWERTY/Dvorak variants, fixed multi-modifier set
- **TKO** — 60% board, 33 edge lighting zones (Left 9 / Bottom 15 / Right 9)
- **Savant Elite 2** — foot pedal (own visual language: pedals + jack ports, no key grid)

## About the design files
The files in this bundle are **design references created in HTML** — high-fidelity prototypes showing intended look and behavior, **not production code to copy**. The task is to **recreate these designs in Avalonia (XAML + C#)** using idiomatic Avalonia patterns. Do not port the HTML/CSS or its DOM structure.

- `KinesisEdit.dc.html` — open in a browser. It is a pan/zoom canvas of labeled mockups. Each mockup has an id badge (`1a`–`1l`, `2a`–`2i`; there is no `2c`). Turn 2 sits at the top, turn 1 below. Turn 2 **extends** turn 1, it does not revise it.
- `support.js` — mockup runtime required for the HTML to render; irrelevant to implementation.

### Mockup index
- **1a** Foundations — tokens, type, status vocabulary, key-state badges
- **1b** Dashboard, populated, dark, 1000×680
- **1c** Dashboard, light theme pair
- **1d** Dashboard, nothing detected (device picker + demo launch)
- **1e** Layout/Remap — Freestyle Edge RGB, all key states, inspector + token picker
- **1f** Editor shell — Advantage 360, 5 layers, Demo Mode bar + leave-with-unsaved modal
- **1g** Editor shell at the 720×480 minimum — tabs collapse, layer switcher survives
- **1h** Lighting — Advantage 360's six indicator LEDs
- **1i** Macros — slot-based (Edge) vs flat library (360), capture mode
- **1j** Settings — Advantage2 read-only device settings + app/notification prefs
- **1k** Shared components — toast, message box, loading state
- **1l** Open design decisions (rationale — read before changing behavior)
- **2a** Layout tab in light theme
- **2b** Foundations II — focus rings, keyboard grammar, icon system, refresh motion budget
- **2d** Savant Elite 2 pedal editor
- **2e** Advantage2 QWERTY/Dvorak variants + resting/error card states
- **2f** Lighting — mode rendered on the board (Freestyle Edge RGB)
- **2g** TKO edge lighting — 33 zones as an outline
- **2h** Key inspector — Remap / Tap & hold / Multi-mod panels in detail
- **2i** Macros edited in place from the key inspector; Macros tab as a library

## Fidelity
**High-fidelity.** Colors, spacing, type sizes, and copy are final and intentional. Recreate pixel-perfectly in DPI-independent logical units (the mock's px = Avalonia's device-independent units). The mock's drawn window chrome (traffic lights / title strip) is illustrative — use native window decorations per platform.

## Target stack
- .NET 10, Avalonia UI 11.x (latest stable), single codebase for Windows / macOS / Linux.
- MVVM with `CommunityToolkit.Mvvm` (source-generated observable properties / commands). Compiled bindings (`x:DataType`) everywhere.
- Theming via `ResourceDictionary.ThemeDictionaries` with `ThemeVariant.Dark` (primary showcase) and `ThemeVariant.Light` (1c/2a prove the pair). Follow the OS theme by default.
- Custom controls only where standard ones can't stretch: the keyboard canvas, keycap, and TKO edge-zone outline. Everything else is restyled standard controls.

## Design tokens

### Surfaces (dark / light)
| Token | Dark | Light | Use |
|---|---|---|---|
| canvas | `#0F1214` | `#F4F5F6` | window background |
| panel | `#16191C` | `#FFFFFF` | cards, toolbar |
| inset | `#131619` | `#F4F5F6` | tab strip bg, inspector rail bg |
| bar | `#1C2024` | `#FAFBFB` | title strip, secondary buttons, list rows |
| raised | `#23272C` | `#EEF0F1` | key faces, active nav pill |
| line | `#2C3136` | `#DDE1E3` | default borders/dividers |
| line-hi | `#3A4046` | `#C9CFD2` | hover/emphasized borders, kbd chips |
| border-raised | `#31363B` | `#C9CFD2` | button & key borders |

Extra darks seen in art: `#262B30` (device thumbnail body), `#2A2F35` (selected key face). Light extreme: `#B2B9BD`.

### Text (dark / light)
| Role | Dark | Light |
|---|---|---|
| primary | `#E8EBED` | `#14181B` |
| secondary | `#C6CCD1` | `#4A5158` |
| body-muted | `#98A1A8` | `#4A5158` |
| muted | `#7B858C` | `#6C757C` |
| faint | `#6C757C` | `#8A9298` |
| disabled/hint | `#545C62` | `#8A9298` |

### Accent & status
- **Accent** `#5B9DF9` — means exactly two things: *selection/focus* and *"you changed this"*. On-accent text: `#0F1214` (dark theme), `#FFFFFF` (light theme). Link hover `#8FBCFB`. Selection fill `rgba(91,157,249,0.14)`; selected-row inset ring `#3E6FA8`.
- **OK green** `#4FBF8B` — pill text on dark `#7ED3AC`; light theme: dot `#35A26D`, text `#1D7A52`. Tints: bg `rgba(79,191,139,0.10–0.14)`, border `rgba(…,0.35–0.4)`.
- **Advisory amber** `#DDA94E` — strong `#C08A21`; light-theme text `#7A5A11`; prose-on-tint `#C9B48A`/`#D9BE86`. Amber is the *only* warning color — the app never shows red for advisories.
- **Error red** `#E4685E` — text-on-tint `#EE9C94`/`#F09A92`. Also the Record ● color.
- **Badge hues**: macro dot `#C77DD8`, tap-and-hold triangle `#35A26D`, lighting-preview cyan `#57C4D8`, LED purple `#B58CF6`.

### Typography
IBM Plex Sans (400/500/600, 700 only for the tiny logo "K") + IBM Plex Mono (400/500/600). **Mono for anything that is literally a value in a config file**: tokens `[esc]`, mount paths `/Volumes/FS_EDGE`, hex values, ms counts, timestamps.

Scale (size/weight): 18/600 page title · 15/500 card & inspector titles · 14/500 modal title · 13/600 toolbar device name · 12/500 buttons, tabs, nav · 12/400 modal body · 11/400 body, meta, pill labels (500) · 10–11 mono values · 10/600 mono section labels, UPPERCASE, letter-spacing 0.10–0.14em · 9/400 keycap labels at mock scale. Line-height 1 for single-line UI text, 1.4–1.6 for prose.

### Geometry
- **4px grid** throughout. Radii: panels/cards 8–9, buttons/controls 5, keycaps 4, kbd chips 3, pills 999. Padding rhythms: cards 14, inspector sections 12–14, buttons 8×13, tabs 0×13.
- Chrome heights: toolbar **46**, tab bar **38**, advisory strip ~30, mock title strip 30.
- Inspector rail: **268px** wide on Layout, **300px** on the macro-editing variant.
- Window: default **1000×680**, minimum **720×480** (at the floor: tab labels collapse, the layer switcher survives — see 1g).
- Keycaps at mock scale: 30×26 (1u), gap 4; wide keys 66 (space). Scale the whole board up with available space; keep proportions.

### Focus, selection, key badges
- **Focus ring** = 1px accent border + `box-shadow 0 0 0 3px rgba(91,157,249,0.28)`. Never an offset outline. Mouse clicks suppress it; Tab/arrows summon it (`:focus-visible` semantics).
- **Selection** on a keycap = filled face + 1px accent border + 2px halo `rgba(91,157,249,0.3)`. Focus and selection can coexist and must stay distinguishable.
- **Keycap badges** (the state vocabulary, all drawn on the cap):
  - Remapped: 2px accent bar across the bottom edge
  - Macro: 5px `#C77DD8` dot, top-right
  - Tap-and-hold: `#35A26D` right-triangle, bottom-right corner (~6px)
  - Advisory: 12×3px `#C08A21` rounded bar, top-right
  - Locked (e.g. SmartSet key): 45° hatched fill + dashed border — hatching = "not yours to edit"; also used for "LED off" (off is hatched, never black)
- Every board view carries a **legend row** beneath it counting each state (see 1e/2a).

### Elevation & motion
- Modal shadow `0 24px 60px rgba(0,0,0,0.6)`; scrim `rgba(8,10,12,0.62)`.
- **Motion budget (2b — treat as spec):** status cross-fade 220ms · list insert height-only 160ms ease-out · tab/layer swap **0ms, instant** · popover 120ms fade + 2px rise · modal 140ms fade · toast 180ms in/out, 5s dwell · recording pulse 1.4s ease-in-out loop · reduce-motion → fades only, no rise. Layer switching is deliberately unanimated.

## Screens

### 1. Dashboard / Home (1b, 1c, 1d, 2e)
- App bar (46px): 18px accent "K" logo tile + "KinesisEdit"; nav pills Home / Settings / Help; right side: mono `refreshed 0.4s ago` + global status pill (`v-Drive OK`, green).
- Header row: "Devices" (18/600) + subtitle "3 of 7 known devices present · list updates itself"; right: `Scan all` secondary button.
- **Device cards**, 2-column grid, gap 12. Card = panel bg, 1px line border, radius 8, padding 14, with a **2px status rail** flush on the left edge. Anatomy: 92×56 device art (abstract key-grid drawing, not a photo) · name (15/500) · meta line ("Split contoured · 5 layers · 6 indicator LEDs") · status dot + label + mono mount path.
- Card states (each fixed height, buttons swap):
  - **Connected · writable** — green rail, `Configure` (accent primary) + `Scan for v-Drive` + `Eject` (green-tinted, right-aligned)
  - **Resting** (known device, no drive mounted) — no rail, dim art, "idle and quiet — no red, no spinner"; `Demo Mode` + `Scan for v-Drive`
  - **Error** (drive present, cannot access) — red rail + red status text
  - Variants row (2e): Advantage2 offers QWERTY/Dvorak variant selection on the card.
- **Empty state (1d)**: device picker (all 7 known models with silhouettes) + demo launch.
- Light theme (1c) is the same screen re-tokened — nothing moves.

### 2. Editor shell (1f, 1g, 2a, 2i)
- Toolbar (46px): `Home` bordered button · divider · device name (13/600) + mono mount path · right: **Save** (accent; turns **amber** whenever the session is dirty) + status pill.
- Tab bar (38px, inset bg): Layout · Macros · Lighting · Settings — active tab = 500 weight + `inset 0 -2px 0 #5B9DF9` underline. Right: mono "LAYER" label + segmented layer switcher (Top/Fn for Edge; 1–5 for Advantage 360, with ⌥n hints inline).
- **Advisory strip** (amber tint, one per tab max): dot + one calm sentence ("3 keys carry advisory notes on this layer — tap-and-hold count is 11 of 10.") + `Review 3` bordered button.
- **Demo Mode bar** (1f) when editing without hardware.
- **Unsaved-changes modal** (1f): "Save changes before leaving?" · body with mono counts ("You've edited `7 keys` across `2 layers`…") · checkbox "Don't ask again — always save on leaving" · buttons Cancel (neutral) / Discard (red-tinted) / Save (accent). 420px wide.
- **Home never ejects** (1l): leaving a session releases nothing; Eject is its own deliberate dashboard action.

### 3. Layout tab (1e dark, 2a light)
- Content = keyboard canvas (centered, both split halves as separate panels with 26px gutter) + legend row + **key inspector rail (268px, right)**.
- Clicking a key selects it and opens the inspector; the inspector header names the position in mono ("Right half · [j] position").
- Inspector **action tabs** (2×2 grid): Remap · Tap & hold · Macro · Multi-mod. Multi-mod renders **only** on devices whose firmware has it; elsewhere the tab is not drawn at all. Unavailable-but-relevant panels show dashed/hint styling.
- Footer (always): advisory note (e.g. duplicate-key notice — "Duplicates are allowed") + `Revert key` / `Copy to…` buttons.

### 4. Key inspector panels (2h)
- **Remap**: current token field + **token picker** — search (⌘F focuses it from anywhere), category chips (All / Letters / Nav / Media / Mouse / Hotkeys), "Recent" group, rows = mono token `[esc]` + friendly name, selected row shows `↵ assign`. Aliases listed (`[escape]` alias of `[esc]`).
- **Tap & hold**: "Tap — a quick press sends" field + red `● Record`; "Hold — past the delay it sends" field + `● Record` (a bare modifier is recordable as a hold); **Delay slider 1–999ms, default 250** with mono readout; budget advisory ("11 of 10 — it still saves; the board keeps the first ten it reads").
- **Macro** (2i): macros are **edited in place here** — named macro, steps, record. The Macros *tab* is a library, not the editor. Assigning an existing named macro to another key = dropdown pick in this panel.
- **Multi-mod**: a closed set of **11 fixed modifier combinations** from the firmware (⌃⇧, ⌃⌥, ⌃⌥⇧, ⌃⌘, ⌥⇧, ⌥⌘, ⇧⌘, ⌃⇧⌘, ⌥⇧⌘, ⌃⌥⌘, ⌃⌥⇧⌘) rendered as a pick-one grid — not a modifier builder.

### 5. Macros tab (1i, 2i)
A **library**: every macro has a name; the tab lists each once — rename, duplicate, delete, and see which keys/layers fire it. Freestyle Edge shows per-key **slots**; Advantage 360 shows the same view as a flat list (no slots). **Capture mode** records keystrokes with the 1.4s recording pulse; Esc leaves capture mode first.

### 6. Lighting tab (2f, 2g, 1h)
- The active mode is **rendered on the board itself**, never merely named in a dropdown. Effects list on the right rail — click an effect to preview it on the board. Controls: color swatch + mono hex, speed as segmented bars (accent-filled), direction toggle.
- Per-key painting (Edge RGB): Select all / Clear; **off keys are hatched, not black**.
- **TKO (2g)**: 33 zones drawn as an **edge outline** around the board (not a grid); zone group chips `Left 9` `Bottom 15` `Right 9` `All 33`; per-zone color, e.g. swatch `#6F3BE2` + `Clear`.
- **Advantage 360 (1h)**: no RGB matrix — six **indicator LEDs**, each individually configured.

### 7. Settings tab (1j)
Device settings + app prefs in one place. On Advantage2 (2 MB hardware) the device section is **read-only** — shown, explained, nothing writable, no error styling. App prefs include notifications and the "don't ask again" save behavior.

### 8. Savant Elite 2 (2d)
The pedal gets its own visual language: three pedal shapes + jack ports drawn to proportion (no key grid), each an assignable slot; dashed outline = unassigned. Same shell, same inspector grammar.

### 9. Shared components (1k, 2b)
Toast (180ms, 5s dwell), message box, loading state (spinner = 14px circle, 1.5px stroke, one transparent quadrant, 1.1s linear spin). Icon system: **16px grid, 1.5px stroke, square caps, geometry only** — device silhouettes in plan view at true proportion; state marks (connected ◉, not-detected dashed ○, cannot-access ! circle, scanning spinner, eject ⏏ as triangle over bar). Lighting-mode marks draw the motion (solid, breathing, spectrum, wave bars, rings, off) rather than naming it.

## Interactions & behavior
- **Keyboard grammar (2b, spec):** ↑↓←→ move key selection across the *physical grid* (not tab order) · ⌥1–5 jump to layer n · ⌘F focus token search · ⌘S save · ⌘W return to dashboard · Esc leaves capture mode first, closes the inspector second. Map ⌘→Ctrl and ⌥→Alt on Windows/Linux.
- **The 2s refresh must not move anything (2b, spec):** cards are keyed by drive identity and never reordered by a refresh; a newly detected device animates in *at the end* (height-only, 160ms); status pill changes cross-fade 220ms, text swaps are instant; a refresh that lands while a card's control is hovered/focused is **deferred until focus leaves**; scroll offset and selection are held by identity, not index.
- **Dirty tracking:** any edit marks the session dirty → Save turns amber; leaving via Home asks once (modal) unless opted out; Save writes the files to the v-Drive; the keyboard only reloads on Eject.
- **Advisories never block:** amber, never red; nothing is clamped or refused (over-budget layouts still save — the board truncates). Always shown twice: a mark on the exact key/row + one summary strip per tab.
- **Demo Mode:** full editor against bundled fixture files, no hardware.
- **Recording:** red ● buttons; while capturing, pulse animation; Esc exits capture.

## State management (suggested shape)
- `DeviceMonitorService` — 2s `DispatcherTimer` poll of mounted volumes matching known v-Drive signatures; emits keyed diffs (never a rebuilt list).
- `VDriveService` — parse/serialize the plain-text layout, macro, and lighting files; atomic writes (write-temp-then-rename); expose mount path.
- `DeviceCapabilities` per model — layer count, lighting kind (per-key / edge-zones / indicator-LEDs / none), macro model (slots vs flat), multi-mod support, budgets (e.g. max 10 tap-and-holds), writable-settings flag. **All screens render from this record** — no per-device screens.
- `LayoutSession` (one per open editor) — per-layer bindings, macros, lighting; dirty flag; per-key revert; advisory computation.
- ViewModels: `DashboardViewModel`, `EditorViewModel` (owns tab VMs + `KeyInspectorViewModel`), plus `AppSettings` (persist "don't ask again", theme follow).
- `DemoDeviceProvider` — fixture-backed implementations of the same interfaces.

## Avalonia implementation notes
- Put every token above in `ThemeDictionaries` (Dark/Light) as `Color` + `SolidColorBrush` resources; name them by role (`SurfaceCanvas`, `TextPrimary`, `AccentBrush`, `StatusOk`…). Never hardcode a hex in a view.
- Ship IBM Plex Sans + IBM Plex Mono (SIL OFL) as embedded `avares://` fonts with `FontFamily` fallbacks.
- Focus halo: Avalonia `BoxShadow` supports spread — `0 0 0 3 #475B9DF9` + accent `BorderBrush` on `:focus-visible`-equivalent (use `FocusAdorner`-free custom styling; suppress on pointer press).
- **Keyboard canvas**: an `ItemsControl` over key models with a custom panel (or `Canvas` + bound `Canvas.Left/Top`); geometry loaded from a per-device JSON (key units on the 4px grid: x, y, w, h, half, label). `KeycapControl` = `TemplatedControl` drawing face, border, label, and the four badge marks; scale the whole board uniformly to fit the content area.
- Hatched fills (locked keys, LED-off): `VisualBrush`/`DrawingBrush` tile, 45°, 4px pitch.
- Segmented controls (layer switcher, speed bars): restyled `ListBox` with horizontal `StackPanel`, or `RadioButton` group styled as segments.
- TKO edge zones: custom control rendering 33 hit-testable segments along a rounded-rect path.
- Window: `Width=1000 Height=680 MinWidth=720 MinHeight=480`; native chrome; title "‹Device› — KinesisEdit" while editing.
- Tab/layer switches: no `Transitions` on those containers (spec: instant). Respect OS reduce-motion where detectable.
- Cross-platform paths: mount detection differs per OS (`/Volumes/*` on macOS, drive letters on Windows, `/media`/`/run/media` on Linux) — keep it behind `DeviceMonitorService`.

## Assets
- **No raster assets.** Device art and icons are simple vector geometry per the 2b icon spec (16px grid, 1.5px stroke). Draw them as Avalonia `PathGeometry`/`StreamGeometry` resources.
- Fonts: IBM Plex Sans, IBM Plex Mono — Google Fonts / GitHub (OFL license permits embedding).

## Files
- `KinesisEdit.dc.html` — all mockups (open in a browser; ids `1a`–`1l`, `2a`–`2i`, no `2c`)
- `support.js` — mockup runtime dependency (reference only)
