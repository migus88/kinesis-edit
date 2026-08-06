# Design handoff — the KinesisEdit redesign

This directory holds the design handoff for the app's visual and interaction redesign, delivered as `Kinesis SmartSet Redesign.zip` on [issue #47](https://github.com/migus88/kinesis-edit/issues/47). It is the **authoritative design reference** for the redesign the same way `specs/` is the authoritative domain reference — the two answer different questions. `specs/` says what the files and devices *are*; this says what the app should *look and behave like*.

The redesign deliberately overrides parts of the current UI. The app up to this point mimics the legacy Pascal SmartSet apps closely; the intent here is a modern, easier app, so where a mockup contradicts today's behavior the mockup wins unless the epic says otherwise.

## Files

| File | What it is | When to read it |
|---|---|---|
| [`handoff.md`](handoff.md) | The handoff document, verbatim. Target stack, the full design-token tables, per-screen specs, interaction rules, suggested state shape, and Avalonia implementation notes. | First, and again whenever you need a token value, a measurement or a stack decision. |
| [`mockups.md`](mockups.md) | Agent-first distillation of all 20 mockups: what each screen shows, the exact UI copy, and the three rationale mockups transcribed near-verbatim. | Whenever you implement a screen — this is the per-screen brief. |
| [`KinesisEdit.dc.html`](KinesisEdit.dc.html) | The mockup canvas itself. Open in a browser: a pan/zoom board of labelled, high-fidelity mockups. | For pixel-level reference on the screen you are building. It is 369 KB of inline-styled HTML — do not read it as text; read `mockups.md`. |
| `support.js` | Runtime the HTML needs in order to render. | Never. It is not part of the design and nothing is implemented against it. |

## Reading order

1. `handoff.md` — "Overview", "Fidelity", "Target stack", "Design tokens".
2. `mockups.md` — the three law mockups first: **`1a`** (surface ramps, status vocabulary, type scale, key-state badges), **`2b`** (focus ring, keyboard grammar, icon system, refresh rules, motion budget), **`1l`** (the decisions the designer made unilaterally, with reasoning).
3. `mockups.md` — the entry for the screen you are building, then `handoff.md`'s matching "Screens" section.
4. The HTML canvas, for anything the words above leave ambiguous.

## Mockup ids

Ids are `1a`–`1l` (turn 1) and `2a`–`2i` (turn 2, **no `2c`**). Turn 2 extends turn 1; it never revises it.

| id | Subject |
|---|---|
| `1a` | Foundations — tokens, type scale, status vocabulary, key-state badges *(design law)* |
| `1b` | Dashboard, populated, dark |
| `1c` | Dashboard, light theme |
| `1d` | Dashboard, nothing detected — device picker and connection steps |
| `1e` | Layout/Remap — Freestyle Edge RGB, key inspector and token picker |
| `1f` | Editor shell — Advantage 360, Demo Mode bar, leave-with-unsaved modal |
| `1g` | Editor shell at the 720×480 floor *(explicitly dropped for now)* |
| `1h` | Lighting — the Advantage 360's six indicator LEDs |
| `1i` | Macros — slot-based vs. flat library, capture mode |
| `1j` | Settings — read-only device settings on a 2 MB Advantage2, plus app prefs |
| `1k` | Shared components — toast, message box, loading |
| `1l` | Open design decisions, with rationale *(design law)* |
| `2a` | Layout tab, light theme |
| `2b` | Foundations II — focus, keyboard grammar, icons, refresh, motion budget *(design law)* |
| `2d` | Savant Elite 2 — the pedal's own visual language |
| `2e` | Advantage2 QWERTY/Dvorak variants, plus resting and scanning card states |
| `2f` | Lighting — the mode rendered on the board |
| `2g` | TKO edge lighting — 33 zones as an outline |
| `2h` | Key inspector — Tap & hold, Multi-mod, Locked-key panels |
| `2i` | Macros edited in place; the Macros tab as a library |

## The laws that cut across every screen

Short version; the long version is in `mockups.md` under `1a`, `1l` and `2b`.

- **The mockups are references, not code.** Recreate them in idiomatic Avalonia (XAML + C#). Do not port HTML/CSS structure. Fidelity is high: colors, spacing, type sizes and copy are final. The mock's px is Avalonia's device-independent unit.
- **Never hardcode a hex in a view.** Every color is a named token in `ThemeDictionaries`, defined for both `Dark` and `Light`.
- **Light is not an inversion.** Dark surfaces climb away from black as they come forward; light surfaces climb toward white. Status hues darken on light while keeping their hue identity.
- **Mono type means "this is literally a value in a config file"** — key tokens, delays, drive paths, counters. Sans is the app speaking in its own voice.
- **One accent, two meanings only:** selection/focus, and "you changed this".
- **Advisories never block.** Amber, never red; nothing is clamped or refused; always shown twice — a mark on the exact key or row, and one calm summary strip per tab.
- **Capability-driven UI: absent features are not shown, not disabled.** A board without a feature does not render its control at all. The exception is a locked key, whose tabs render disabled with a reason.
- **Nothing ejects implicitly.** Home returns to the dashboard, Save writes files, Eject is its own deliberate action on the device card.
- **The 2 s refresh must not move anything.** Cards are keyed by drive identity, never reordered; a refresh landing while a card's control is hovered or focused is deferred until focus leaves.
- **The motion budget is fixed and small** — and tab/layer swaps are 0 ms on purpose.
- **Focus and selection are distinct and coexist:** focus is a 1 px accent border plus a 3 px 28 % halo, never an offset outline; selection is a filled face with a 2 px accent ring.

## Implementation

The redesign is tracked as its own epic with phased child issues; each child issue names the mockup ids it implements. See the [GitHub issues](https://github.com/migus88/kinesis-edit/issues).
