# KinesisEdit — Avalonia redesign mockups, curated extract

## How to read this

This distills [`KinesisEdit.dc.html`](KinesisEdit.dc.html), a pan/zoom canvas of inline-styled HTML mockups for a redesign of the KinesisEdit desktop app. Read it instead of the HTML; open the HTML in a browser only when you need pixel-level reference for the screen you are building. The design language and implementation notes that govern all of it live in [`handoff.md`](handoff.md).

The mockups were produced in two passes: **turn 1** (`1a`–`1l`) established the design language and drew the main screens; **turn 2** (`2a`, `2b`, `2d`–`2i`, no `2c`) closes gaps turn 1 asserted but never drew. Turn 2 sits physically first in the HTML but is ordered second here; per the designer, "nothing below revises 1a–1l, it extends them." Three mockups are not screens at all but design law — **1a** (Foundations), **2b** (Foundations II) and **1l** (Open decisions) — and are transcribed near-verbatim below. Quoted strings are exact UI copy from the file. Nothing here is inferred; where the designer wrote explanatory prose beside a mockup, it is reproduced rather than paraphrased.

## Index

| id | one-line description |
| --- | --- |
| 1a | Foundations: surface ramps (dark + light hex), status vocabulary, type scale, key-state badges, spacing, elevation |
| 1b | Dashboard, populated, dark, mixed device states (connected / cannot access / scanning / not detected / web-tool-only) |
| 1c | The same dashboard in light mode — the light half of the pair |
| 1d | Dashboard empty state: nothing detected, device picker, per-device connection steps, demo launch |
| 1e | Layout/Remap editor — Freestyle Edge RGB, all key states, key inspector + token picker |
| 1f | Editor shell — Advantage 360, 5 layers, Demo Mode bar, leave-with-unsaved modal |
| 1g | Editor shell at the 720×480 minimum — TKO, tabs collapse, layer switcher survives |
| 1h | Lighting for Advantage 360's six indicator LEDs (function table, no per-key color) |
| 1i | Macros — slot-based (FS Edge RGB) vs the 360's flat library, plus capture mode |
| 1j | Settings — Advantage2 on 2MB hardware (nothing writable) + app/notification preferences |
| 1k | Shared components: toast, message box, blocking loading state |
| 1l | Open design decisions the designer made unilaterally, with reasoning |
| 2a | Layout tab in light mode — the light half of the editor pair |
| 2b | Foundations II: focus rings, keyboard shortcut grammar, icon system, refresh motion + motion budget |
| 2d | Savant Elite 2 — pedal gets its own visual language, accessory jack strip, no layers/lighting tabs |
| 2e | Advantage2 QWERTY/Dvorak variants + the two card states turn 1 skipped |
| 2f | Lighting — the mode is previewed on the board itself, mode rail with per-mode parameters |
| 2g | TKO edge lighting — 33 zones drawn as an outline around the dimmed board |
| 2h | Key inspector — the Tap & hold, Multi-modifier and Locked-key panels turn 1 only tabbed to |
| 2i | Macros edited in place inside the key inspector; the Macros tab becomes a library |

## Document-level statements

- Turn 1 header: "KinesisEdit — A precision instrument for keyboards that can't talk back."
- Turn 1 framing note: "Windows are drawn at the shipped default of **1000×680** unless labelled otherwise. Dark is the primary showcase; 1c proves the light pair. Design language: single accent for 'you changed this', hue-coded key-state badges, mono for anything that is literally a value in a config file, and a 4px grid throughout."
- Turn 2 header: "Turn 2 · closing the spec gaps — The parts turn 1 asserted but never drew." Order of turn 2 follows the gap list: "light pair for the editor, focus + icon foundations, the pedal, Advantage2 variants, lighting visualised on the board itself, TKO edge zones, and the inspector's remaining panels — macros included, edited in place."
- "Decisions this turn" (closing block, sits after 2i): "Nothing ejects implicitly: Home returns to the dashboard, Save writes files, and Eject is its own button on the device card — the one place the user asks for it on purpose. Firmware update checking is gone from the dashboard, the editor and the notification preferences. Macros are edited inside the key inspector and carry names, so the Macros tab is a library for renaming and reusing them rather than a second editor. Lighting modes are previewed on the board itself, with the mode rail beside it. Minimum-window treatment is dropped for now — say the word if you want it revisited."

---

## 1a — Foundations — tokens, type, status vocabulary, key-state badges

**This is spec, not a screen. Transcribed near-verbatim.**

**Surface ramp — dark** (named token → hex):

- `canvas` `0F1214`
- `panel` `16191C`
- `bar` `1C2024`
- `raised` `23272C`
- `line` `2C3136`
- `line·hi` `3A4046`

**Surface ramp — light** (same six names):

- `canvas` `F4F5F6`
- `panel` `FFFFFF`
- `bar` `FAFBFB`
- `raised` `EEF0F1`
- `line` `DDE1E3`
- `line·hi` `C9CFD2`

**Status vocabulary** — four states, each a hue plus a fixed meaning:

- "v-Drive OK" `4FBF8B` — "connected · writable"
- "v-Drive Error" `E4685E` — "gone · unwritable"
- "Demo Mode" `B58CF6` — "nothing is written"
- "Advisory" `DDA94E` — "over limit · never blocks"

**Type scale — Plex Sans / Plex Mono** (size/weight → use):

- 24 / 600 — device name (sample: "Advantage 360")
- 18 / 600 — "Screen titles"
- 15 / 500 — "Card and panel headings"
- 13 / 400 — "Body copy and controls"
- 11 / 400 — "Secondary / helper text"
- 11 mono — "Section label"
- 12 mono — config values (sample: `[kp-lshft]`, `>{del}`, `250ms`)

Rule, verbatim: "Mono is reserved for values that exist verbatim in the config file — tokens, delays, drive paths, counters. Sans for everything the app says in its own voice."

**Key states — "one badge, one hue, no legend needed twice"** (badge glyph → state → badge form):

- `F` → Factory → "default"
- `Esc` → Remapped → "bar"
- `M1` → Macro → "dot"
- `⌥` → Tap-and-hold → "corner"
- `⌃⌥` → Multi-modifier → "360 only"
- `⚙` → Locked → "hatched"
- `A` → Selected → "ring"
- `?` → Has advisory → "warning"

**Spacing:** `4 · 8 · 12 · 16 · 24 · 32`; "radius 4 key / 6 panel / 10 window".

**Elevation:**

- "flat: 1px line only"
- "popover: +0 8px 24px /.5"
- "modal: +0 24px 60px /.6"

---

## 1b — Dashboard — populated, mixed states · dark · 1000×680

- Shell chrome: app mark "K" + wordmark "KinesisEdit"; nav items "Home", "Settings", "Help"; status area shows "refreshed 0.4s ago" beside a "v-Drive OK" chip.
- Content header: "Devices" with subtitle "3 of 7 known devices present · list updates itself" and a "Scan all" button.
- Card — **Advantage 360**: "Split contoured · 5 layers · 6 indicator LEDs", status "Connected", path `/Volumes/ADV360`; actions "Configure", "Scan for v-Drive", "⏏ Eject".
- Card — **TKO**: "60% gaming · 2 layers · per-key + edge RGB", status "Cannot Access" with a `!` mark and the explanation "Drive TKO is visible but not writable. Another app may have a file open, or the volume mounted read-only."; actions "Configure", "Retry access", "⏏ Eject".
- Card — **Freestyle Edge RGB**: "Split flat · 2 layers · per-key RGB", status "Scanning for v-Drive…"; actions "Configure", "Scanning".
- Card — **Savant Elite 2**: "3-button foot pedal · accessory jack", status "Pedal not detected"; actions "Demo Mode", "Scan for v-Drive".
- Card — **Advantage 360 Professional** (out of scope, marked `↗`): "Configured in Kinesis' web tool — KinesisEdit doesn't edit this board." with a single action "Open web tool ↗".
- Every card exposes Eject as an explicit per-card button; there is no implicit eject anywhere in the dashboard.

---

## 1c — Dashboard — the light half of the pair (same screen, OS in light mode)

- Identical structure and copy to 1b, rendered on the light surface ramp — this mockup exists to prove the pair, not to introduce new UI.
- Chrome: "K / KinesisEdit", nav "Home · Settings · Help", "refreshed 1.2s ago", "v-Drive OK" chip.
- Header unchanged: "Devices", "3 of 7 known devices present · list updates itself", "Scan all".
- Advantage 360 card: "Split contoured · 5 layers · 6 indicator LEDs", "Connected", `/Volumes/ADV360`, "Configure", "Scan for v-Drive", "⏏ Eject".
- TKO card: "60% gaming · 2 layers · per-key + edge RGB", "Cannot Access", `!`, "Drive TKO is visible but not writable.", "Configure", "Retry access".
- The view is cropped after two cards — the light treatment is the subject, not the full list.

---

## 1d — Dashboard — nothing detected (device picker + demo launch)

- Status chip in the chrome reads "v-Drive Error" while nothing is present.
- Headline: "Keyboard not detected". Body, verbatim: "KinesisEdit is watching for a v-Drive and will pick one up the moment it appears — no need to press anything. Meanwhile, pick your device for connection steps, or work without hardware."
- Left panel: "Which device do you have?" listing "Advantage2" (tagged "default"), "Advantage 360", "Freestyle Edge", "Freestyle Pro", "Freestyle Edge RGB", "TKO", "Savant Elite 2 (pedal)". Helper: "Your pick drives the steps at right and which board Demo Mode opens."
- Right panel is device-specific: "Get an Advantage2 into a detectable state — 3 steps".
- Step 1: "Plug the keyboard into this computer with its USB cable."
- Step 2: "On the keyboard, hold Progrm and tap F1 to mount the v-Drive."
- Step 3: "A drive named ADVANTAGE2 appears. This screen will replace itself with your device card automatically."
- Live reassurance line: "Still watching · rescanned 8 times since you opened this window".
- Actions: "Launch Advantage2 in Demo Mode" (primary, names the picked device), "Scan now", "Troubleshooting tips ↗".

---

## 1e — Layout / Remap — Freestyle Edge RGB, all key states, inspector + token picker · 1000×680

- Window title "Freestyle Edge RGB — KinesisEdit"; editor bar carries "⏏", "Home", device name "Freestyle Edge RGB", drive path `/Volumes/FS_EDGE`, "Save", and a "v-Drive OK" chip.
- Tab row: "Layout", "Macros", "Lighting", "Settings". Layer switcher: "Layer" with "Top" / "Fn".
- Advisory strip above the board, verbatim: "3 keys carry advisory notes on this layer — tap-and-hold count is 11 of 10. Files from older firmware can exceed today's limits; nothing is blocked." with a "Review 3" action.
- The board is drawn as two split halves with real legends including secondary labels (`1!`, `2@`, `-_`, `=+`) and function-row device hotkeys: "F1 mute, F2 vol−, F3 vol+, F4 ▶, F5 ⏮, F6 ⏭, F7 status, F8 vdrv, F9 speed, F10 nkro, F11 game, F12 reset"; also "Pause insert", "Del scr lk", and the indicator row "☾ ① ② ③ ④ ⑤ ⑥ ⑦ ⑧ Fn ☼".
- Legend strip under the board with live counts: "Remapped 3", "Macro 2", "Tap-and-hold 11", "Locked 1"; plus "Copy key…" and "Reset layer".
- Key inspector (right): header "Left half · [d] position", showing "factory [d] · now [esc]" and the exclusivity sentence "This key does one thing". Mode tabs: "Remap", "Tap & hold", "Macro", "Multi-mod", with the note "Picking another replaces the current assignment. Multi-modifier is Advantage 360 only."
- Token picker: search field `⌕` containing typed text "esc" with a blinking caret and a match counter "18/1204"; a "● Record" button beside it; filter chips "All, Letters, Nav, Media, Mouse, Hotkeys, Recent".
- Results list: `[esc]` "Escape" with hint "↵ assign"; group header "Navigation · 3"; `[escape]` "alias of [esc]"; `[kp-esc]` "Keypad layer Esc"; `[hk-esc]` "Device hotkey".
- Inline advisory in the picker: "Advisory: Esc already exists on the top-left position of this layer. Duplicates are allowed." Inspector footer: "Revert key", "Copy to…".

---

## 1f — Editor shell — Advantage 360, 5 layers, Demo Mode bar + leave-with-unsaved modal · 1000×680

- Window title "Advantage 360 (Demo) — KinesisEdit"; bar shows "←", "Home", "Advantage 360", "Save", and a "Demo Mode" chip.
- Demo banner, verbatim: "Demo Mode — no keyboard attached. Nothing you change here is written anywhere." with actions "Export layout to file…" and "Connect a device".
- Tabs "Layout / Macros / Lighting / Settings"; layer switcher shows five layers — "Base", "Keypad", "Fn1", "Fn2", "Fn3" — annotated with the shortcut "⌥1–5".
- The board is the contoured split with two wells; keys carry state badges inline (`⚙` locked, `⌃⌥` multi-modifier, `Esc` remap bar, `M3` macro).
- Both halves are labelled "left indicators" and "right indicators" for the LED clusters.
- Modal — "KinesisEdit", `!`, title "Save changes before leaving?"; body verbatim: "You've edited 7 keys across 2 layers. Saving writes the layout files to the v-Drive. Eject when you're done — the keyboard reloads on eject, and only you decide when that happens."
- Modal has a suppression checkbox: "Don't ask again — always save on leaving", and three buttons "Cancel", "Discard", "Save".

---

## 1g — Editor shell at the 720×480 floor — TKO, tabs collapse, layer switcher survives

- Demonstrates the minimum window size, 720×480; window title "TKO — KinesisEdit".
- The bar compresses: "Home", "TKO", "Save", and the status chip shortens from "v-Drive OK" to just "OK".
- Tab row still shows "Layout / Macros / Lighting / Settings" but collapsed; the layer switcher survives the squeeze with "Top", "Fn", "Edge".
- Board status line: "3 keys selected · tripartite space · edge strip outlined".
- Lighting side panel at this width: "Mode" with dropdown "Wave ▾", "Speed 6 / 9", "Direction" with only "←" and "→" available, and the note "Wave supports left/right only."
- Paint section: "Paint selection · 3 keys" with a "+" swatch adder, the rule "Hatched swatch = no color (off), not black.", and actions "Select all" / "Clear".
- Note: the closing "Decisions this turn" block says minimum-window treatment is "dropped for now", so 1g is the only place the 720×480 floor is drawn.

---

## 1h — Lighting — Advantage 360's six indicator LEDs (content area, 1000px wide)

- Panel title "Indicator LEDs" with the framing sentence: "Six fixed LEDs, three per half. Each one reports a function — there are no per-key colors on this board."
- Actions: "Preview on device".
- Left side is the "Physical position" diagram, rows "L1 L2 L3" and "R1 R2 R3", with the linking rule: "Click an LED here or a row at right — they're the same selection."
- Right side is a table with columns "LED / Function / Color".
- Rows: "L1 — Caps Lock — `4FBF8B` — on while engaged"; "L2 — Active profile — `5B9DF9` — blinks profile number"; "L3 — Unassigned — — — stays dark".
- "R1 — Active layer — 5 colors → — one per layer", expanded inline to the five layer names "Base, Keypad, Fn1, Fn2, Fn3".
- "R2 — Battery level — `DDA94E` — Bluetooth boards only"; "R3 — Unassigned — — — stays dark".
- Footer enumerates the domain: "Available functions: Caps Lock · Num Lock · Scroll Lock · active profile · active layer · NKRO status · battery level."

---

## 1i — Macros — slot-based (left) and the 360's flat library (right), plus capture mode

- Left: slot-based model for the Freestyle Edge RGB — header "Macros on F3 · Freestyle Edge RGB", subtitle "3 of 5 slots used · 1 active", with a "Pick another key" action.
- "Slot 1 — Sign-off block": `{lctrl}{a} {del} Best,{enter}{enter}Jamie{enter}`, badged "ACTIVE".
- "Slot 2 — Build & run": `{lctrl}{lshift}{b} {f5}`, action "Make active".
- "Slot 3 — Full test suite": `{lctrl}{grave} npm run test:all{enter} …` with the over-budget advisory "512 of 500 characters — over the device budget. Saved as-is."
- "Slot 4 — empty" with a "+" and "Record a macro"; two meters at the bottom: "layout keystroke budget 5 140 / 7 200" and "this macro 512 / 500".
- Right: "Macro library · Advantage 360 · one flat list per profile", subtitle "24 macros · trigger + layer set per macro", action "New macro"; search field `⌕` "Search name, trigger, or contents…" and a filter "All layers ▾".
- Table columns: "Macro / Trigger / Layer / Length". Rows: "Sign-off block — `Best,{enter}{enter}Jamie` — `⇧ + [f3]` — Fn2 — 34"; "Build & run — `{lctrl}{lshift}{b} {f5}` — `[f4]` — Base — 12"; "Em dash — `{lalt}{kp0}{kp1}{kp5}{kp1}` — `[minus]` — Keypad — 5"; "Deploy sequence — 6 co-trigger modifiers — legacy budget is 4 — `⌃⌥⇧ + [d]` — Fn3 — 88".
- Capture mode overlay: "Recording keystrokes — your typing goes into this macro, not into the app" with a "Stop" button, live step stream `{lctrl}↓ {a} {lctrl}↑ {del} {lshift tap} ▌`.
- Capture rules, verbatim: "Arrows = press/release. A bare modifier records as tap. Search and shortcuts are suspended until you stop." Footer hint: "Esc to stop".

---

## 1j — Settings — Advantage2 on 2MB hardware (nothing writable) + app/notification prefs

- Banner: "This Advantage2 has 2 MB firmware — device settings can't be written to it", explained as: "The board reports 2MB. Its settings file is read-only in firmware, so the controls below show what the keyboard is doing but can't change it. Remaps, macros, and layers all still save normally." Link: "Which board do I have? ↗".
- Section "Written to the keyboard" — device settings, each shown with its current value and a "read-only" marker.
- "Startup profile — Which layout the board loads on power-up — 1 (read-only)".
- "Macro playback speed — Default for macros without their own speed — 6 (read-only)".
- "Key-click tone — off"; "Toggle tone — on".
- "Open the v-Drive at startup — An app preference, not a board setting — this one still applies" (i.e. app prefs remain live even when device settings are frozen).
- Capability-gating rule, verbatim: "Game mode, backlight mode, and status-report speed don't exist on this device, so they aren't shown at all — only settings the board actually has appear here."
- Section "App & notifications — stored per device", checkbox list: ✓ "Warn before leaving with unsaved changes"; ✓ "Confirm before resetting a layer"; ☐ "Show the 'keystrokes captured' summary after recording"; ✓ "Confirm before switching keyboard variant"; ☐ "Explain advisory warnings in full instead of one line"; "+7 more".
- Also on this page: "Your custom swatches — reused in every color picker".
- Demo caveat: "In Demo Mode these preferences are readable but never written — toggles snap back when the session ends."

---

## 1k — Shared components — toast, message box, loading state

- Toast spec: "Toast · bottom-right · 5s".
- Success toast: title "Saved", body "Written to the v-Drive. Eject from the dashboard when you want the keyboard to reload them.", with an "×" dismiss.
- Advisory toast variant: "Saved with 3 advisories" — "Everything was written. Review the warnings".
- Blocking loading state: "Loading · blocking" with the message "Loading Advantage 360…".
- Message box spec: "Message box · YesNoCancel + suppress" — chrome shows "KinesisEdit" and a "?" glyph.
- Sample message box: title "Copy macros too?", body "You're copying F3 onto F8. F3 carries 3 macro slots."
- Suppression checkbox: "Don't ask this again".
- Three buttons, ordered: "Cancel", "Key data only", "Include macros" — i.e. the two affirmative choices are labelled by outcome, not Yes/No.

---

## 1l — Open decisions I made — flag any you'd rather flip

**This is rationale, not a screen. Transcribed near-verbatim; the designer made these calls unilaterally and invites reversal.**

**Unsaved changes** — "Blocking Save / Discard / Cancel on Home, with a 'don't ask again' opt-out. The Save button is amber whenever the session is dirty, so the modal is rarely a surprise."

**Leaving a session** — "Home just goes home — it never ejects. Ejecting is its own deliberate action on the dashboard card, so nothing is released behind the user's back. With unsaved edits, Home asks once inline."

**Advisories** — "Amber, never red, and always in two places: a dot on the exact key or row, and one calm summary strip per tab. Nothing is ever clamped or refused."

**Live refresh** — "Cards keep a fixed height and stable order; only the status line, accent rail, and secondary button swap on a 2s tick. Nothing reflows, so selection and scroll never move."

---

## 2a — Layout tab — the light half of the editor pair · 1000×680

- Same Layout screen as 1e rendered on the light ramp; window title "Freestyle Edge RGB — KinesisEdit", bar with "Home", "Freestyle Edge RGB", `/Volumes/FS_EDGE`, "Save", "v-Drive OK".
- Tabs "Layout / Macros / Lighting / Settings"; "Layer" switcher "Top" / "Fn".
- Advisory strip: "3 keys carry advisory notes on this layer — tap-and-hold count is 11 of 10." with "Review 3".
- Legend under the board now includes advisory as a counted state: "Remapped 3", "Macro 2", "Tap-and-hold 11", "Advisory 3", "Locked 1".
- Inspector header "Left half · [d] position", assignment shown as "factory [d] · now [esc]", exclusivity sentence tightened to "This key does one thing — picking another replaces it."
- Mode tabs "Remap / Tap & hold / Macro / Multi-mod"; search "esc" with counter "18/1204" and "● Record"; filter chips reduced at this width to "All, Letters, Nav, Media, Recent".
- Result rows `[esc]` "Escape · ↵ assign", group "Navigation · 3", `[escape]` "alias of [esc]", `[kp-esc]` "Keypad layer Esc", `[hk-esc]` "Device hotkey"; footer "Revert key", "Copy to…".
- Light-theme law, verbatim: "Light is not an inversion: surfaces climb toward white as they come forward (canvas → panel → key), where dark climbs away from black. Status hues darken for contrast on light (OK `#35A26D`, advisory `#C08A21`, error `#B0453C`) while keeping the same hue identity."

---

## 2b — Foundations II — focus & keyboard affordances, icon system, refresh motion

**This is spec, not a screen. Transcribed near-verbatim.**

**Focus — "one ring, three surfaces."** Specimens show `rest` vs `:focus-visible` on a `key` and on a `chip`. Rule, verbatim: "Ring is 1px accent border + 3px 28% halo, never an outline offset — it has to read on a 26px key without eating its neighbour. Focus is always visible when it exists; mouse clicks suppress it, arrow/Tab summon it. Selection (2px accent ring, filled) and focus can coexist on the same key and must stay distinguishable."

**Keyboard grammar** (shortcut → behavior, verbatim):

- `↑↓←→` — "move key selection across the physical grid, not tab order"
- `⌥1–5` — "jump to layer n — shown inline on the layer switcher"
- `⌘F` — "focus the token search from anywhere in the editor"
- `⌘S / ⌘W` — "save · return to the dashboard"
- `Esc` — "leaves capture mode first, closes the inspector second"

**Icon style** — "16px grid, 1.5px stroke, square caps, geometry only." Three icon families:

- "device silhouettes · plan view, true proportion" — `contoured`, `split flat`, `60% TKO`, `pedal`
- "state & action marks" — `connected`, `not detected`, `!` `cannot access`, `scanning`, `eject`
- "lighting-mode marks — motion drawn, never named twice (full set in 2f)"

**"The 2s refresh must not move anything"**, verbatim: "Cards are keyed by drive identity and never reordered by a refresh — a newly detected device animates in at the end of the list, height-only, 160 ms ease-out. Nothing else re-lays out. Status changes cross-fade the chip's fill over 220 ms; text swaps are instant. Scroll offset and key selection are held by identity, not index." Illustrated as "v-Drive OK → 220 ms cross-fade → v-Drive Error".

Second refresh rule, verbatim: "A refresh that arrives while a card's own button is under the cursor or focused is deferred until focus leaves — the list never steals a click."

**Motion budget** (every animation in the app, with its duration):

- "state cross-fade — 220 ms"
- "list insert (height) — 160 ms ease-out"
- "tab / layer swap — 0 ms — instant, no slide"
- "popover in — 120 ms fade + 2px rise"
- "modal in — 140 ms fade, scrim 40%"
- "toast in / out — 180 ms · dwell 5 s"
- "recording pulse — 1.4 s ease-in-out loop"
- "respects reduce-motion — fades only, no rise"

Rationale for the zero-duration layer swap, verbatim: "Layer switching is deliberately unanimated — the spec says it will be used constantly, and 200 ms × 200 switches is the difference between an instrument and a website."

---

## 2d — Savant Elite 2 — the pedal has its own visual language · 1000×680

- Window title "Savant Elite 2 — KinesisEdit"; bar "Home", "Savant Elite 2", `/Volumes/PEDAL`, "Save", "v-Drive OK".
- Tab row is reduced to three: "Assignments", "Macros", "Settings" — no Layout, no Lighting.
- Where the layer switcher would be: "No layers — one assignment surface".
- The three pedals are drawn as pedals, labelled "left" → `[ctrl]+[c]`, "middle" → "macro · 'build & run'", "right" → `[ctrl]+[v]`.
- Accessory jack drawn as a separate strip: "Accessory jack — 4 positions" with "A1 `[f13]`", "A2 `[f14]`", "A3 unset", "A4 unset".
- Rationale, verbatim: "Four accessory positions read as a wired strip off the pedal body, not as four more keys in a grid — the physical relationship is the whole point."
- Inspector: "Middle pedal", assignment "Build & run", "Macro · 14 keystrokes", with the exclusivity rule "A pedal position holds one of these — same exclusivity as a key."
- Inspector tabs reuse the key vocabulary: "Remap", "Tap & hold", "Macro".
- Macro "Steps" list shown inline: "01 `[lctrl]` ▼ hold", "02 `[b]` tap", "03 `[lctrl]` ▲ release"; actions "● Record", "Edit steps".
- Closing rule, verbatim: "The pedal has no lighting, no layers and no per-key grid — the editor drops those tabs entirely rather than showing them empty."

---

## 2e — Advantage2 — QWERTY / Dvorak variants, plus the two missing card states

- Header "Advantage2", `/Volumes/ADVANTAGE2 · 4MB`.
- New control: "Variant" toggle with "QWERTY" / "Dvorak", sitting beside the "Layer" switcher ("Top" / "Keypad").
- Variant rationale, verbatim: "Variant is a property of the file set, not a view toggle — switching it rewrites which factory legends the board reports, so it sits beside the layer switcher but reads as a heavier commitment (confirm on switch when the layout already has remaps)."
- Board is drawn as two concave wells, labelled "left well · concave, 6×4 + thumb cluster" and "right well · Dvorak legends shown" — the right half displays Dvorak legends (`F G C R L /`, `D H T N S -`, `B M W V Z`) while the left shows `' , . P Y` etc.
- Second half of the mockup: "Card states turn 1 skipped".
- **Not Detected** card — "Advantage2", "Split contoured · 2 layers", status "Not Detected", with the note "Known device, no drive mounted. Idle and quiet — no red, no spinner. This is the resting state, not an error."; actions "Demo Mode", "Scan for v-Drive".
- **Scanning** card — "TKO", "60% gaming · per-key + edge RGB", status "Scanning for v-Drive…", with the note "Transient state while a manual rescan is in flight. The card keeps its size and its buttons keep their positions — only the status line and a 3px indeterminate bar change, so a 2s refresh can't shift the layout under the cursor."; actions "Configure", "Cancel".

---

## 2f — Lighting — the mode is rendered on the board, not described in a dropdown · 1000×680

- Editor shell with "Lighting" tab active; switcher relabelled "Lighting layer" with "Top" / "Fn".
- Board header: "Wave · live preview" — "the board animates the selected mode at its real speed and direction".
- Paint state under the board: "Paint · 2 keys selected", with the swatch rule "hatched = off, not black" and actions "Select all", "Clear".
- Interaction between paint and effect, verbatim: "Wave ignores painted colors, so the paint layer is shown at 40% under the effect — the colors are still on file. Solid, Reactive, Ripple and Starlight render the paint directly."
- Mode rail beside the board, headed "Mode — click to preview on the board"; each row names the mode and the parameters it accepts.
- Full mode list with parameters: "Wave — spd · L/R"; "Solid — color"; "Breathe — color · spd"; "Spectrum — spd · L/R"; "Reactive — 2 colors · spd"; "Ripple — 2 colors · spd"; "Fireball — spd · L/U"; "Starlight — 2 colors · spd"; "Rebound — color · spd · L/R"; "Loop — color · spd"; "Pulse — color · spd"; "Rain — color · spd · U/D"; "Off — —"; "Pitch black — —".
- Parameter controls below: "Color `57C4D8`", "Speed 6 / 9", "Direction" with `← → ↑ ↓`.
- Direction rule, verbatim: "Directions a mode can't use stay in place, struck through — the row never changes shape as you move down the list."

---

## 2g — TKO edge lighting — 33 zones as an outline, not a grid · 1000×680

- Editor shell for the TKO with "Lighting" active; window title "TKO — KinesisEdit", `/Volumes/TKO`, "Save", "v-Drive OK".
- New switcher named "Surface" rather than layer: "Keys · Top", "Keys · Fn", "Edge strip".
- The edge strip is drawn as an outline wrapping the board, annotated "left 9 / bottom 15 / right 9 = 33 addressable zones".
- Rationale and selection rules, verbatim: "The keys stay on screen but dimmed — the strip is only comprehensible in relation to the board it wraps. Zones select individually, by run (shift-click), or by side; the two unlit zones show the same hatch as an unpainted key."
- Mode panel headed "Edge mode" with the scoping note "8 edge-only effects — a separate set from the key modes in 2f."
- Edge mode list: "Sweep", "Solid", "Breathe", "Spectrum", "Chase", "Mirror", "Pulse", "Off".
- Parameters: "Speed 4 / 9" and "Direction" with rotational arrows `↻ ↺` (not the L/R/U/D set used for key modes).
- Selection panel: "Selection · zone L6" with color `6F3BE2` and a "Clear" action.
- Bulk selectors: "Left 9", "Bottom 15", "Right 9", "All 33".

---

## 2h — Key inspector — the three panels turn 1 only tabbed to

Three inspector states drawn side by side; tab strip is always "Remap / Tap & hold / Macro / Multi-mod".

**Tap & hold panel** — header "Right half · [j] position", active tab "Tap & hold".

- "Tap — a quick press sends" → `[j]`, with a "● Record" button.
- "Hold — past the delay it sends" → `[lctrl]`, with its own "● Record" button.
- Capture rule, verbatim: "A bare modifier is recordable as a hold — tap-alone and held-in-combo are captured as different things."
- "Delay" slider showing "250 ms", range min `1` to max `999`, labelled "default 250 · this device".
- Amber advisory block: "Advisory · 11 of 10 tap-and-holds" — "The layout is one over the device budget. It still saves; the board keeps the first ten it reads."

**Multi-modifier panel** — header "Advantage 360 · left thumb", active tab "Multi-mod".

- Scoping rule, verbatim: "Eleven fixed combinations — a closed set from the firmware, not a modifier builder. Elsewhere in the app this tab isn't drawn at all."
- Combination grid as drawn: `⌃⇧`, `⌃⌥`, `⌃⌥⇧`, `⌃⌘`, `⌥⇧`, `⌥⌘`, `⇧⌘`, `⌃⇧⌘`, `⌥⇧⌘`, `⌃⌥⌘`, `⌃⌥⇧⌘`, plus the selected `⌃⌥⇧`.
- Cost note: "Sends all three as one code — not a macro, so it costs nothing from the keystroke budget."
- Exclusivity warning shown at switch time, verbatim: "Assigning this clears the remap that was on this key — the four modes are one slot, and the panel says so at the point of switching rather than after."

**Locked key panel** — header "Right half · SmartSet key", state "Locked position".

- Explanation, verbatim: "This key is the board's own configuration key. Its behaviour lives in firmware, not in the layout files, so nothing here can be written — including on a device where every other position is free."
- Tabs are individually disabled with reasons: "Remap — not writable", "Tap & hold — not writable", "Macro — not writable".
- Informational section "What it does on the board": "hold + F1…F12 device hotkeys", "hold + Esc mount the v-Drive", "hold + 1/2 switch profile".
- Actions "Copy from…" and "Copy to…", with the asymmetry rule: "A locked key can still be a copy source, never a target — so the inspector disables one direction, not the whole action."

---

## 2i — Macros edited in place — no separate screen, and named so they can be reused · 1000×680

- Layout tab of the Freestyle Edge RGB with a macro-carrying key selected; bar "Home", "Freestyle Edge RGB", "Save", "v-Drive OK"; tabs "Layout / Macros / Lighting / Settings"; "Layer" "Top" / "Fn".
- Board annotation: "carries a macro · 3 keys" and "selecting a key edits its macro right here — the Macros tab is a library, not the editor".
- Redefinition of the Macros tab, verbatim: "What the Macros tab is for now — Every macro has a name, so the tab lists them once: rename, see which keys and layers fire each one, duplicate, delete. Assigning a named macro to a second key is a pick from the inspector's own dropdown — the Advantage 360's flat list is the same view, minus the per-key slots."
- Inspector: "Right half · [f3] position", active tab "Macro"; the macro is picked by name from a dropdown — "Sign-off block ▾" — with the reuse note "Named, so it can be picked for another key from this same dropdown. Also on [f7] · Fn."
- Step editor headed "Steps" with the reorder affordance "drag ⠿ · ⌥↑↓" and a "● Record" button.
- Steps as drawn, each with a drag handle and an `×` delete: "01 `[lshift]` press", "02 `[b]` ⇧ held", "03 `[lshift]` release", "04 `[e]` tap", "05 `[s]` tap", "06 `[t]` tap", "07 `[enter]` tap · 80 ms", then "08 ＋ insert step".
- Live capture banner: "Recording into step 04 — your typing goes here, not into the app. Esc stops."
- Footer meters: "Playback speed 3 / 5", "this macro 128 / 500", "layout keystrokes 5 140 / 7 200".
