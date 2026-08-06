# Keyboard model (Model)

The runtime, editable state of one device profile: `KinesisEdit.Core.Model`. Plain mutable POCOs — no `INotifyPropertyChanged`, no I/O, no parsing. Built from the static domain data ([`domain-data.md`](domain-data.md)): a `DeviceDefinition` supplies every limit, a `DeviceGeometry` supplies every position, `KeyRegistry` resolves every token. Entry point: `KeyboardLayout`.

| Type | Models | Owning spec |
|---|---|---|
| `KeyboardLayout` | one profile: layers + the Gen2 flat macro list + counters + `Validate()` | 05 §1.4, §1.5; 06 §1; 04 §5.3 |
| `KeyboardLayer` | one layer: dense `Keys`, TKO `EdgeKeys`, four lookup paths | 05 §1.4, §1.5 |
| `KeyboardKey` | one physical position and everything the user puts on it | 05 §1.3, §5.3, §5.6, §5.7 |
| `KeyCopyScopes` | `[Flags]` scope of `KeyboardKey.CopyFrom`: `None`/`KeyData`/`Macros`/`All` | 05 §1.3 |
| `Macro` | ordered keystrokes + trigger metadata + co-triggers | 05 §1.2; 06 §1, §4, §5 |
| `Keystroke` | one key inside a macro + held modifiers + direction | 05 §1.1, §5.1, §5.2, §5.8 |
| `KeyDirection` | `None`/`Down`/`Up` — the `{-token}`/`{+token}` prefixes | 05 §1.1, §5.8; 06 §2.2 |
| `MacroKeystrokeRenderer` | macro → the 06 §3 line, its layer prefix, and the Adv360 length metric | 06 §3, §6 |
| `MacroLengthMetric` | which of 06 §6's two per-macro metrics a layout is measured in, and the measurement | 06 §6; 04 §5.3 |
| `ModelViolation` / `ModelViolationKind` | limit reports from `Validate()` | 04 §5.3; 06 §5, §6; 11 §11.1, §11.2 |
| `TapAndHoldPrecheck` / `TapAndHoldRefusal` | the four checks a UI runs *before* offering tap-and-hold, and their verbatim refusals | 11 §11.1 |
| `MacroDelayTokens` | the `dran` / `d001`..`d999` delay keys a macro editor inserts | 11 §11.3; 06 §2.2 |
| `MacroModifiers` / `MacroModifierCodes` | the two-char modifier codes and their key mapping | 05 §5.1 |
| `MultiModifierCodes` | the 11 accepted 4-char combos | 05 §5.7; 04 §2.3; 11 §11.2 |
| `KeyColor` | per-key LED colour (`readonly record struct`, R/G/B 0-255) | 05 §1.3; 07 §2.1, §4 |

## Two write paths

Every assignment on a `KeyboardKey` exists twice, and which one a caller picks is the whole contract of this module:

- **Editor paths** — `Remap`, `SetTapAndHold`, `TrySetMultiModifiers`, `AssignMacro`, `CopyFrom`. What a user action goes through. They **refuse** (return `false` / `0` / copy nothing) what the position cannot hold, per 05 §5.3, and they keep the three single-key rules mutually exclusive (below).
- **Tolerant load paths** — `ApplyRemap`, `ApplyTapAndHold`, `ApplyMultiModifiers`, `SetMacro`. What a parser goes through (04 §4.2: "rules are applied to the addressed key"). They **store** whatever the file carried and let `Validate()` report it, so nothing is dropped silently.

There is no third, implicit path: the `Macro1`..`Macro5` properties are **read-only**, so writing a slot always names `AssignMacro` (guarded) or `SetMacro` (tolerant).

Device *limits* (counts, budgets, ranges) are never enforced by either path — they are only ever reported.

### One rule per position

The three single-key rules of 04 §2 — remap, tap-and-hold, multi-modifier — share one slot on a position, because 04 §4.3 writes at most one line per key: *tap-and-hold, else multi-modifier, else remap*. A position holding two would silently lose the lower-precedence one on the next save, so **each editor path clears the other two**; 11 §11.1 states one leg outright ("a plain remap of the key likewise clears its tap-and-hold configuration") and 04 §2.3 makes remap and multi-modifier the same rule to begin with. The tolerant paths do not clear — a field file carrying two lines for one position keeps both, and `Validate()` reports `ConflictingSingleKeyRules`.

## `KeyboardLayout`

- `KeyboardLayout.Create(DeviceId, LayoutVariant = None)` resolves both catalogs; `new KeyboardLayout(DeviceDefinition, DeviceGeometry)` takes them directly (used to model a device whose capabilities differ). Devices without geometry (SE2, CROSSFIRE, Adv360 Pro) throw.
- `Device`, `Dialect` (Legacy = Adv2/SE2, Gen1 = FS/TKO, Gen2 = Adv360), `Variant`, `Layers`, `FindLayer(index)`.
- Macros: `UsesFlatMacroList` (Adv360 only, 06 §1) → `Macros`, `AddMacro` (throws on slot-based devices), `RemoveMacro` (by identity), `FindMacros(layerIndex, triggerKeyCode)`. `CreateMacro()` stamps the device's speed/repeat defaults.
- `EnumerateMacros()` yields the flat list **and then** every populated key slot, on every device, and never yields the same *instance* twice (identity is by reference — two equal-looking macros really are two macros). A well-formed model fills one store or the other — Gen2 appends every macro to the flat list (04 §4.2), slot-based families never fill it — but a tolerant load may produce both.
  - `MacroCount` and `TotalKeystrokes` are the **only** things derived from this traversal, so they and the two layout-budget checks that read them always agree. `Validate()`'s per-macro checks walk the slots and the flat list themselves and report a macro once per *place* it sits — one instance in two different key slots is measured twice, because it really is on two positions. (Between a slot and the flat list it is measured once; see Validation below.)
- Counters: `ModifiedKeyCount`, `MacroCount`, `TotalKeystrokes` (**1** per keystroke + **2** per attached modifier, 04 §5.3), `TapAndHoldCount`. All of them walk `Keys` only — TKO `EdgeKeys` are lighting zones and are excluded by design (see below).
- `Reset()` resets every key of every layer and empties the flat list. `Validate()` → `IReadOnlyList<ModelViolation>`.

## `KeyboardLayer`

`Index` (top 0 / bottom 1; Adv360 0..4), spec-literal `Name`, `LayerType` (QWERTY **0** / Dvorak **1**; on Adv360 it mirrors `Index`, 05 §1.4), `Keys` (dense 0..N−1, constructor throws on gaps), `EdgeKeys` (TKO's **33** lighting zones only).

Four lookups (05 §1.5), all returning `KeyboardKey?`: `FindByIndex`, `FindByOriginalKeyCode`, `FindByPositionKeyCode` (what remap lines address), `FindByTriggerKeyCode` (what macro triggers match). Plus `FindEdgeKeyByIndex`. `Reset()` clears every key.

**`EdgeKeys` are lighting zones, not typing keys.** Their lines live in `led*.txt` and never in a layout file (04 §3.4), and 05 §4.4 lists no remap or macro for them, so the model builds them locked and slotless (`CanEdit`/`CanAssignMacro`/`UsesMacroSlots` all **false**): every editor path refuses them, and a zone is meant to carry only its `KeyColor`. They are **outside every counter** — `ModifiedKeyCount`, `MacroCount`, `TotalKeystrokes`, `TapAndHoldCount` and `EnumerateMacros` walk `Keys` only. The tolerant load paths can still write to a zone, so `Validate()` **does** walk them (reporting `EditOnLockedKey` / `MacroOnRestrictedKey` with a message naming the zone); otherwise that state would be invisible on all 66 zones of a TKO layout.

## `KeyboardKey`

| Member | Rule |
|---|---|
| `OriginalKey` | factory default of the position |
| `PositionKey` | token naming the position; **defaults to `OriginalKey`**, differs only on Adv360 (05 §7.3) |
| `ModifiedKey` / `IsModified` | the remap |
| `TriggerKey` | `OriginalKey` for `fn1s` (**11167**) and `keyt` (**11166**), else `PositionKey` (05 §1.3) |
| `ModifiedOrOriginalKey` | `ModifiedKey` when modified — except those same two keys, which always report `OriginalKey` (05 §5.3) |
| `Index`, `CanEdit`, `CanAssignMacro` | copied from the geometry position |
| `SupportsMultiModifiers` | the **device's** `DeviceDefinition.SupportsMultiModifiers`, projected onto the key (Adv360 only) |
| `UsesMacroSlots` | whether the device stores macros in per-key slots at all (06 §1); **false** on Adv360 |
| `TapAction`/`HoldAction`/`TimingDelay`/`IsTapAndHold` | 05 §5.6 |
| `MultiModifiers` / `HasMultiModifiers` | raw 4-char code, canonical lowercase (05 §5.7) |
| `Macro1`..`Macro5` (**read-only**), `Macros` (read-only view), `GetMacro(slot)`, `ActiveMacroIndex` (**1** by default), `ActiveMacro`, `IsMacro`, `MacroCount` | 5 slots per key (05 §1.3, 06 §1) |
| `KeyColor` | nullable; **not** cleared by `Reset()` (05 §1.3 lists remap/tap-hold/multi-modifiers/macros only) |

Editor paths: `Remap(newKey)` sets the remap when the code **differs** from the original; a remap **to** the original clears it (04 §2.1). `SetTapAndHold(tap, hold, delay)` / `SetTapAndHold(tap, hold, TapAndHoldCapability)` (the capability overload applies the device default — **250** ms, **150** on Adv360). `TrySetMultiModifiers(code)` takes only the 11 codes, case-insensitively, **and only on a device that offers the feature** (11 §11.2 is "Advantage360 only"); a refused code changes nothing. Each of the three clears the other two on success ("One rule per position" above). All three return `false` on a locked position (`CanEdit == false`) for **every** argument, including a remap back to the key's own original — a position that accepts nothing must answer the same way whatever it is offered, and it never produces a layout line (05 §5.3).

`AssignMacro(macro)` is the guarded slot path: first empty slot, refused with `0` when the position rejects macros (06 §2.2), when all five are full, or when the device has no per-key slots (`UsesMacroSlots == false` — stamping a slot number on a flat-list macro would corrupt its `MacroIndex`, which is 0 there).

Tolerant load paths: `ApplyRemap`, `ApplyTapAndHold`, `ApplyMultiModifiers` (which still returns `false` for a code outside the 11 — that is a parse fact, not a device limit), and `SetMacro(slot, macro)` (stamps `MacroIndex`; checks neither `CanAssignMacro` nor `UsesMacroSlots`). None of them clears a sibling rule.

Clearing: `ClearRemap()`, `ClearTapAndHold()`, `ClearMultiModifiers()`, `ClearMacros()`, `Reset()` (all four, colour survives).

`CopyFrom(other, KeyCopyScopes)` is a user action (05 §1.3 "Copying between key positions"), so its **key-data** half refuses a locked target outright. Otherwise it **replaces** the target's key data with the source's — every field, so a stale rule on the target never survives — filtered by what the target's device can hold: on a device without multi-modifier support the target ends with no code at all. It therefore upholds "one rule per position" like the other editor paths and can never manufacture a conflict; it can only carry one the *source* already had (only a tolerant load can build such a source), which `Validate()` then reports. Its **macro** half deliberately stays unchecked: a macro on a `CanAssignMacro == false` position is already visible, because `Validate()` reports `MacroOnRestrictedKey` for it, whereas an unchecked key-data copy onto a locked position would have been silent.

**Watch out:** `Remap(key.OriginalKey)` — a no-op self-remap — returns `true` and is still "a plain remap", so it clears an existing tap-and-hold and multi-modifier. A UI "assign key" flow that re-applies a position's own default would therefore destroy its tap-and-hold; use `ClearRemap()` when the intent is only to drop the remap.

`fn1s`/`keyt` are remapped like any other position: 05 §5.3 says they "always act/report as their original layer-switch action even when 'modified'", so `IsModified` is set and `ModifiedKeyCount` counts them — only `ModifiedOrOriginalKey` ignores the remap. 04 §4.3 still writes the line and 04 §4.2 reads it back into the same state, so the round trip is faithful.

## `Macro` and `Keystroke`

- `Macro`: `Id` (fresh per `Clone()`), `TriggerKey` and `LayerIndex` (both **-1** = `UnassignedIndex` until assigned), `MacroIndex` (the slot on its key — **0** for flat-list macros, and the setter constrains nothing, which is why the validator reports the slot it *found* a macro in), `Speed`/`RepeatFrequency` (defaults from `MacroCapability.Speed`/`.Repeat`), `Keystrokes` + `Add/Insert/Remove/Clear`, `CoTriggers` + `AddCoTrigger`/`RemoveCoTriggerAt`/`ClearCoTriggers` (**4** legacy slots, more is reported not refused), `ContainsKeyCode`/`ContainsCoTrigger`, `IsEmpty`, `WeightedKeystrokeCount`, `Clone()`.
- `AddCoTrigger` does **not** de-duplicate: 06 §5 counts populated slots, and a repeated token in a field file is content that must survive a load/save round trip.
- `IsEquivalentTo` implements the 05 §1.2 comparison (same multi-key flag — constant `true` here — same count, per-item keystroke equality). Not an `Equals` override: macros are mutable, so reference identity stays their list identity.
- `CollidesWith` is the 06 §5 rule: both counts match **and** each set holds every co-trigger of the other, or both have zero. The containment is checked **both ways**, so the answer never depends on which macro is asked — a one-way test would call `[lctrl, lctrl]` and `[lctrl, lshft]` equal. `HasBalancedKeyDirections()` is the Gen2 up/down integrity rule (06 §5).
- `Keystroke`: `Key`, `Modifiers`, `UpDown`, `WriteDownUp` (**true**, false for `SingleEvent` speed/delay pseudo-keys), `DiffPressRelease` (**false**), `IsModifierKey`, `IsShifted`/`IsAltGr` (05 §5.2), `EffectiveDirection`, `ModifierCount`, `WeightedKeystrokeCount` = **1 + 2 × modifiers**, `FormatModifiers()`, `Clone()`, `IsEquivalentTo`.
- **Modifiers are never attached to a modifier key** (05 §5.1): assigning one, or switching `Key` to a modifier, clears `Modifiers`. Bits outside the §5.1 table are dropped on assignment, so two keystrokes that render alike also compare alike. `EffectiveDirection` applies `UpDown` only when the keystroke has no modifiers or is itself a modifier (05 §5.8) and `WriteDownUp` is true.
- `MacroModifierCodes` round-trips flags ↔ `"S "`/`"LS"`/… (the trailing space on generic codes is load-bearing, 05 §5.1) with `Format`/`TryParse`/`TryParseCode`/`GetCode`; `GetKeyCode` and `TryFromKeyCode` map keys and flags both ways, with both `W ` and `LW` resolving to Left Win. `Known` is the mask of defined flags. `Canonicalize` reduces a set to one flag per physical modifier key — always the first spelling of that key in §5.1 table order — so comparing canonical sets compares **held keys**: `Count` (and therefore `ModifierCount`) sees `W ,LW` as one modifier, and the renderer writes no transition when a keystroke spelling Win as `W ` is followed by one spelling it `LW`. `Format` stays on the raw set, so the file spelling round-trips.

## `MacroKeystrokeRenderer`

`Render(macro, dialect, layerPrefix = "")` emits 06 §3 in order: layer prefix → `{co-trigger}`… → `{trigger}` → `>` → `{sN}` → `{xN}` (both only when 0..9) → keystrokes. Modifier transitions come from **diffing the previous keystroke's held set against the current one**: released → `{+token}` first, newly held → `{-token}`; after the last keystroke every still-held modifier is closed with `{+token}`. Direction prefixes are `-` = down, `+` = up. `DiffPressRelease` keys write `{-token}{ }{+token}` (06 §7.6). A key with no file token in any dialect (05 §3.9, the Adv2 Program button) writes **nothing at all** — not the group and not its modifier transitions, which would otherwise inflate the length metric.

`RenderKeystrokes(macro, dialect)` is the keystroke section alone. `KeystrokeTextLength(macro, dialect)` is its length — the Adv360 metric (see below).

`LayerPrefixFor(dialect, layerIndex)` answers the prefix argument: `fn ` for layer 1 of the **Gen1** dialect and nothing anywhere else, because only that family puts the layer into the line itself — Legacy marks the keypad layer with `kp-` *inside* the first bracket and Gen2 with a `<...>` header line (04 §3.1–§3.3). Anything that shows a macro as "the line the file will carry" has to pass it, or it disagrees with `LayoutFileSerializer`, which shares the same `fn ` constant.

Token resolution falls back to the first dialect that names a key: the generic Shift/Ctrl/Alt entries exist only in the Legacy table (05 §3.5) yet appear inside Gen1 macro values (06 §7.3).

## Validation

`Validate()` reports, never refuses. Kinds: `MacroCountExceeded`, `MacroLengthExceeded`, `MacroKeystrokeBudgetExceeded`, `MacroCoTriggerLimitExceeded`, `MacroTriggerCollision`, `MacroOnRestrictedKey`, `MacroSpeedOutOfRange`, `MacroRepeatOutOfRange`, `EmptyMacro`, `UnbalancedMacroKeyDirection`, `ReservedMacroTriggerWithoutCoTrigger`, `TapAndHoldCountExceeded`, `TapAndHoldDelayOutOfRange`, `TapAndHoldNotSupported`, `EditOnLockedKey`, `MultiModifiersNotSupported`, `UnboundMacro`, `ConflictingSingleKeyRules`, `MacroOutsideFlatList`. Each `ModelViolation` carries `Kind`, `Message`, and where applicable `LayerIndex`/`KeyIndex`/`MacroIndex`/`Limit`/`ActualValue`.

**Order is fixed and repeatable**: layout-wide budgets, then per layer its keys in index order (permissions → tap-and-hold → macro slots in slot order) followed by its edge zones, then the flat list in list order. No hash container is enumerated, so two calls on an unchanged layout return identical lists.

- `MacroIndex` is the slot the validator **found** the macro in, not `Macro.MacroIndex` — that property has a public setter and may name a slot the key does not hold. It is null for flat-list macros.
- `LayerIndex` is null, never **-1**: the unassigned sentinel is not a layer, and a caller keying off it would look up a layer that cannot exist.
- A macro instance parked in both stores is measured **once** (the key walk wins); it still takes part in trigger grouping, because in the flat list it is on a trigger.
- `EditOnLockedKey` fires for a remap, tap-and-hold, or multi-modifier on a `CanEdit == false` position (05 §5.3, including the TKO edge zones); `MultiModifiersNotSupported` for a code on a device outside `SupportsMultiModifiers` (11 §11.2); `ConflictingSingleKeyRules` for a position holding more than one of the three rules of 04 §2; `MacroOutsideFlatList` for a macro in a key slot on a flat-list device (06 §1 — 04 §4.3 would never write it). All four are reachable only through the tolerant load paths.
- `UnboundMacro` fires for a Gen2 flat-list macro that names no trigger key or no layer (06 §1, and 06 §5's "a layer is selected"). Such a macro is **excluded** from trigger-collision grouping — it is not on a trigger yet, so it cannot duplicate one.
- Every limit comes from the device catalog. `MacroCountExceeded` compares against `MaxMacroCount` (the baseline); raising it via `GatedMaxMacroCount` needs firmware-gate evaluation, which is not this module's job. The Gen2-only checks (`EmptyMacro`, `UnbalancedMacroKeyDirection`, `ReservedMacroTriggerWithoutCoTrigger`) follow 06 §5.

## Editor pre-checks — `TapAndHoldPrecheck`, `MacroDelayTokens`

Two UI-free helpers that answer questions an editor would otherwise restate; both are pure functions over the model and carry the spec's wording/tokens as data, the same pattern `ModelViolation.Message` follows. Their consumer is the feature-dialog layer ([feature-dialogs.md](feature-dialogs.md)), which has the full rules.

- `TapAndHoldPrecheck.Evaluate(layout, layer, key)` → the **first** `TapAndHoldRefusal` of 11 §11.1 in the spec's order (same key on another layer → maximum reached → macro trigger → A-Z/0-9 on layer 0), `None` when the dialog may open; `MessageFor(refusal)` is the verbatim message. It reads `TapAndHoldCapability.MaxPerLayout`, `TapAndHoldCount`, both macro stores, and the key's **factory default** table (not its remap). Whether the device supports the feature at all (`TapAndHoldCapability.IsSupported`), and whether its firmware clears the gate, are separate questions the caller asks first — and does ([feature-dialogs.md](feature-dialogs.md)).

  **The maximum excludes the key being edited.** §11.1's wording caps how many tap-and-hold actions a profile *has*; re-opening the dialog on a key that already carries one rewrites that assignment rather than adding an eleventh, so `key.IsTapAndHold` takes one off the count. A literal reading would lock the last ten assignments of a full profile out of ever being edited again, which no recorded legacy behaviour asks for. A key carrying nothing yet is still refused.
- `MacroDelayTokens`: `RandomToken` `dran`, the 1–999 ms range, `BuildCustomToken(ms)` → `d` + three zero-padded digits, and `ResolveRandom`/`ResolveCustom` which look the key up **by token, never by code** — `dran` and the generated `d002` share code 10087 and `KeyRegistry.FindByCode` answers `dran` first (05 §7).

## Load-bearing invariants

1. **Limits are reported, never enforced.** The tolerant load paths store what a field file carries; `Validate()` describes what is out of bounds. Files written by older firmware may exceed today's limits and must still load. Only genuine programming errors (null args, slot/index out of range, flat-list misuse) throw. Position *permissions* (05 §5.3) and device *feature* scope (11 §11.2) are what the editor paths refuse.
2. **Layer content is immutable after construction.** `KeyboardLayer.Keys` is fixed; remaps, macros, and tap-hold live *on* the `KeyboardKey` objects, never by replacing list entries (05 §7.2). `KeyboardKey.Macros` is a read-only view over the slot array, so `SetMacro`'s slot stamping cannot be bypassed.
3. **Two length metrics, never conflated.**
   - **7200 layout budget** and the **300 per-macro cap** of every non-Gen2 family: `TotalKeystrokes` / `WeightedKeystrokeCount` = 1 per keystroke + 2 per attached modifier. 04 §5.3 defines keystroke accounting once for the whole document, so both use it.
   - **500 Adv360 per-macro cap**: `MacroKeystrokeRenderer.KeystrokeTextLength` — the length of the serialized **macro** text (06 §6), i.e. the value side of the §3 line. The trigger, the co-triggers, the layer prefix and the `{sN}`/`{xN}` markers are *not* in it.

   **`MacroLengthMetric` is the one place that picks between them**: `UsesSerializedTextLength(layout)` (= `layout.UsesFlatMacroList`), `Measure(macro, layout)` and `UnitFor(layout)`. `Validate()` reports the number `Measure` returns, so a UI budget readout built on the other metric would contradict the gate that stops the save — which is exactly why the choice is not left to each caller.
4. **Trigger identity ≠ position.** Macro lookups use `TriggerKey`, remaps use `PositionKey`; they differ wherever a position carries an explicit position token, and `fn1s`/`keyt` invert the rule back to `OriginalKey` (05 §1.3, §1.5).
5. **Indices are dense and stable.** 0..N−1 per layer, unique, and the same physical position keeps its index — and its `PositionKey` — across every layer of a device (05 §7.4).
6. **Copies, never shared instances.** Layer building and `CopyFrom`/`Clone` deep-copy macros; only the immutable `KeyDefinition` records are shared (05 §1.5).

## Deliberately not here

- **No file parsing, and no serialization above one macro line.** `MacroKeystrokeRenderer` writes the single 06 §3 macro line and exists so the model can measure the Adv360 cap; everything else belongs to the reader/writer module — remap/tap-and-hold/multi-modifier line syntax (04 §2), the Adv2 keypad-exception codes (05 §5.4) that disambiguate keypad-layer duplicates, the `fn `/`kp-`/`<header>` layer encodings, the Adv2 `{speedN}` and old-FS variants of the macro line, file assembly, and invalid-line tracking. That module is `KinesisEdit.Core.Layouts` (see [`layout-files.md`](layout-files.md)), which consumes the tolerant load paths above.
- **No firmware-gate evaluation** (spec 09 §2) — `GatedMaxMacroCount` and `TapAndHoldCapability.MinimumFirmware` are data the caller compares.
- **No UI or binding concerns** — no change notification, no undo buffers, no `IsNew` editing flags, no display/caption rendering (05 §5.2 is a UI concern). The one exception is `TapAndHoldPrecheck` (above): 11 §11.1's four checks are *model* questions ("does another layer already carry one?", "is this a macro trigger?") that every editor would otherwise re-derive, so they live here as a pure function — but nothing here refuses an assignment because of them. `SetTapAndHold` still stores whatever it is given.
- **No drive I/O**, no profile files, no lighting state beyond the per-key `KeyColor` slot.
