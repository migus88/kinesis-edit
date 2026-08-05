# Layout files (Layouts)

The layout-file engine of `KinesisEdit.Core`: parses and serializes `layouts/layoutN.txt` (Advantage2: `qwerty.txt`/`dvorak.txt`) — remaps, tap-and-hold, multi-modifiers, and macros — for the four in-scope dialects of spec 04 §4.1. Pure lines-in/lines-out: no filesystem access (encoding/newlines are `VDriveFileService`'s job, [vdrive.md](vdrive.md)), no firmware state (the dialect comes from device identity alone), no UI. Builds on the keyboard model ([keyboard-model.md](keyboard-model.md)) via its tolerant load paths and on the domain data ([domain-data.md](domain-data.md)) for tokens, geometry, and the persisted-count capabilities.

| Namespace | Entry point | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Layouts` | `LayoutFileParser(DeviceId, LayoutVariant).Parse(lines)` | Lines → fresh `KeyboardLayout` + tracked lines (`LayoutParseResult`) | 04 §4.2; 06 §2 |
| `KinesisEdit.Core.Layouts` | `LayoutFileSerializer.Serialize(layout[, trackedLines])` | Model → lines, full regeneration + kept lines | 04 §4.3; 06 §3 |
| `KinesisEdit.Core.Layouts` | `LayoutDialect`, `LayoutDialectResolver` | Device → dialect (finer than `TokenDialect`) | 04 §4.1 |
| `KinesisEdit.Core.Layouts` | `LayoutInvalidLine`, `LayoutLineSegment` | Tracked unapplied lines: verbatim text, validity spans, `Keep` | 04 §5 |
| `KinesisEdit.Core.Layouts` | `Advantage2KeypadExceptions` | Exception token list + explicit token→code map (10043–10070, `kp-insert`→VK_INSERT) | 05 §5.4; 04 §3.2 |

## Dialects

`TokenDialect` cannot tell FS from RGB/TKO or Adv2 from the SE2 pedal, hence `LayoutDialect`:

| `LayoutDialect` | Devices | Tokens | Layer encoding | Macro slots written | Co-triggers written | Speed/repeat written |
|---|---|---|---|---|---|---|
| `Freestyle` | FS Edge, FS Pro | Gen1 | `fn ` line prefix | 1–3 | 1 | `{sN}`/`{xN}` only when ≥ 1 |
| `Gen1Rgb` | Edge RGB, TKO | Gen1 | `fn ` line prefix | 1–5 | 4 | always, incl. `{s0}`/`{x0}` |
| `Gen2` | Advantage360 | Gen2 | `<base>`…`<function3>` headers | flat list | 4 | always (values are clamped 1–9 on load) |
| `Advantage2` | Advantage2 | Legacy | `kp-` token prefix + exception list | 1–3 | 3 | `{speed1}`–`{speed9}`, **no repeat token** |

Persisted slot/co-trigger counts and speed/repeat ranges come from `MacroCapability` (`PersistedSlotsPerKey`, `PersistedCoTriggersPerMacro`, `Speed`/`Repeat`, `ClampsOutOfRangeValues`) — never hard-coded here.

## Parsing (04 §4.2, 06 §2)

- Fresh `KeyboardLayout` per `Parse` — the file fully replaces the model. Lines are lowercased per character and right-trimmed (case-insensitive; offsets keep mapping onto the verbatim original); blank lines ignored; no comment syntax.
- Rule detection: split on the **first** `>`; config side with `[`/`]` = single-key, `{`/`}` = macro; `[t&h` in the value = tap-and-hold; a value that is one of the 11 codes = multi-modifier — on `Gen1Rgb`/`Gen2` only, older dialects treat the code as an unknown output (invalid line). A `Gen1Rgb` file carrying one round-trips byte-exact yet `Validate()` still reports `MultiModifiersNotSupported`: the *feature* is Adv360-only per the adjudication in [domain-data.md](domain-data.md) (04 §2.3's "RGB-family serializer" is shared-parser lineage, not device scope), so the advisory is intended, like `EditOnLockedKey`. FS/Adv2 additionally require the line to start with `[` or `{`; `fn ` is Gen1-family syntax only.
- Token resolution is dialect-first, then any-dialect fallback (read tolerantly): accepts documented older spellings — Adv2 `kp/`/`kp*`/`kp-`/`kp+` (05 §3.6 value spellings = the Gen1 tokens of the same keys), Legacy `escape` in a Gen1 file, the Legacy-only generic modifiers inside Gen1 values (06 §7.3). Writing always emits the current dialect's canonical casing.
- Rules land via the model's **tolerant paths** (`ApplyRemap`/`ApplyTapAndHold`/`ApplyMultiModifiers`/`SetMacro`): conflicting rules are both stored, device limits are `Validate()`'s job. Duplicates: last wins; remap-to-original clears (04 §2.1).
- Adv2 layer routing per token, in order: exception-list match (before prefix stripping — `kp-insert` is in the list; `kp-` alone is the keypad-minus value spelling, not an empty prefix) → keypad value-spelling set → contains `kp-` (strip first occurrence) → else top layer. Exception tokens resolve through the explicit 05 §5.4 code map; the layer lookup falls back to a position/trigger-token scan because the geometry resolves those tokens by registry first-match (e.g. `kp0` → VK Numpad0, not 10056).
- Tap-and-hold delay: `Gen1Rgb`/`Gen2` mark a missing/negative/non-numeric delay invalid; FS/Adv2 substitute the 250 ms default (04 §2.2).
- Macro value side: leading `{sN}`/`{xN}` (Adv2: `{speedN}`, no repeat) are honored only before the first keystroke and are not keystrokes; out-of-range values: `Gen1Rgb` ignores (default applies), FS/Adv2 substitute the device default, Gen2 clamps into 1–9. Later `{s2}` etc. resolve as the 05 §3.12 pseudo-keys. Delay tokens are keystrokes; on `Gen1Rgb`, `d125`/`d500` resolve to the *generated* codes 10085+N (05 §3.12), elsewhere to the legacy 10007/10008 rows.
- Modifier tracking (06 §2.2): `{-mod}` joins the active set (deduped by key code), plain keys record the held set, `{+mod}` leaves it; an up with no matching down — or before the modifier shielded anything — becomes a single tap, as does a bare modifier token and a still-held never-used modifier at line end (nothing is dropped). Gen2 `{-k}`/`{+k}` on non-modifiers become explicit `KeyDirection` events.
- Triggers: non-Gen2 lines match `FindByTriggerKeyCode` on the addressed layer and fill the key's **first empty** slot (a sixth line on one trigger is tracked, not silently dropped); Gen2 matches by **original key across all layers**, takes the layer from the current header (unknown `<...>` header = invalid line, layer unchanged), and appends to the flat list. **All** co-triggers a line carries are stored — truncation to the persisted count happens only on save.

## Invalid lines (04 §5)

Every line that cannot be applied is tracked on **all** dialects (the legacy FS/Adv2 apps didn't track — the never-drop-silently rule wins): `LineNumber` (1-based), `LayerIndex` (the routing verdict), verbatim `Text` (original casing, any `fn ` prefix), `Segments` tiling the text with per-segment validity (for the future red-segment dialog), and `Keep` defaulting to **false** — unkept lines vanish on the first save (04 §5.2). Structural failures with no span of their own (missing t&h group, all five macro slots full) yield a tracked line whose segments are all valid.

## Serialization (04 §4.3, 06 §3)

Full regeneration; nothing survives but kept lines. Per layer in list order (Gen2 emits every header, even for empty layers): per key in index order *tap-and-hold else multi-modifier else remap* (multi-modifier lines only on `Gen1Rgb`/`Gen2`), then the key's persisted macro slots; Gen2 instead appends the flat list's macros of that layer after the keys; then that layer's kept lines verbatim. Macro lines wrap `MacroKeystrokeRenderer.RenderKeystrokes` (the 06 §3 diffing lives there once); `MacroLineWriter` adds the per-dialect trigger/co-trigger groups (Adv2 keypad-layer macros get `kp-` prefixes except exception tokens) and the speed/repeat variants of the table above. Empty macros and tokenless keys (Adv2 Keypad/Program) write nothing.

## Load-bearing invariants

1. **Never enforce, never drop.** Limits are `Validate()`'s job; unapplied lines are tracked, keepable, and re-emitted verbatim.
2. **Read tolerantly, write the current dialect.** Cross-dialect token fallback on read; canonical single-dialect tokens with registry casing on write.
3. **Canonical files round-trip byte-equal** (pinned against every 04 §6 / 06 §7 worked example); legacy inputs normalize (casing, RGB inserting `{s5}`, alias → save spelling) rather than fail.
4. **The exception list is checked before `kp-` stripping**, and its codes come from the explicit 05 §5.4 map — plain registry lookup gives the wrong rows for `numlk`, `kp0`…
5. **Dialect from device identity only** — no firmware probing (spec 06 ties `d125`/`d500` to the app generation, not firmware).

## Deliberately not here

- **No SE2 pedal dialect** (spec 12) — the pedal file is not a layout dialect: `LayoutDialectResolver` returns `None`, the parser refuses the device, and `active/pedals.txt` has its own engine in [savant-elite.md](savant-elite.md).
- **No lighting lines** (04 §3.4) — `led*.txt` belongs to [lighting.md](lighting.md); layout files never carry edge-lighting lines.
- **No save orchestration** — file naming/numbering, profile pairing, Adv360 factory-profile write protection, and drive I/O are issue #11's layer.
- **No invalid-line dialog** (04 §5.2) — surfacing tracked lines and their red segments is the editor UI, issue #16; this module only carries the data.
- **No firmware gating** (09 §2) — e.g. the FS 24→100 macro-count gate is validation/UI territory.
