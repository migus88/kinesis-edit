# Keystroke capture (Positions, Recorder, Session, Capture Service)

Turns real keypresses into key-table entries for remap and macro recording: physical positions in, `KeyDefinition`s out, with the key event consumed before any control in the window sees it. All decisions live in pure state machines in `KinesisEdit.Core.Input`; the app project holds only a thin `TopLevel` adapter. It is not a global/system-wide hotkey listener, and it does not encode modifiers for files — capture stops at the in-memory keystroke.

Note what "swallowed" means here: the OS has *already* delivered the event to the focused window. Setting `e.Handled` stops the in-app routing (and, backend-dependent, the derived `TextInput` event), so the keystroke never reaches a control or produces text. It does not unhook the OS, and system-reserved combinations never arrive at all.

| Namespace | Entry point | Encodes | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.Input` | `PhysicalKeyCode` + `PhysicalKeyMap` | Physical key position → key-table entry | spec 05 §3.3, §3.5, §3.6, §3.7 |
| `KinesisEdit.Core.Input` | `KeystrokeRecorder` (+ internal `HeldKeySet`) | The capture state machine (swallow/emit rules) | spec 10 §Keyboard-input capture; spec 12 §Programming a pedal step 3 |
| `KinesisEdit.Core.Input` | `KeystrokeCaptureSession` | Started/suspended gating around the recorder | spec 10 §Keyboard-input capture |
| `KinesisEdit.Core.Input` | `IKeystrokeCaptureService` | The abstraction editors code against | spec 10 §Keyboard-input capture |
| `KinesisEdit.Input` (app) | `AvaloniaKeystrokeCaptureService` | Tunnel-phase key preview on one `TopLevel` | spec 10 §Keyboard-input capture; spec 11 §Tap-and-Hold |

## Positions — `PhysicalKeyCode`, `PhysicalKeyMap`

- `PhysicalKeyCode` — toolkit-neutral enum of 126 physical positions + `None`. Member names are the W3C `KeyboardEvent.code` names verbatim (which is also what `Avalonia.Input.PhysicalKey` uses); numeric values are this project's own. Only positions the key registry genuinely cannot express are left undeclared (`IntlRo`, `IntlYen`, browser/power/`Lang*` keys); those pass through to the OS.
- `PhysicalKeyMap.Resolve(PhysicalKeyCode)` → `KeyDefinition?` via `KeyRegistry.FindByCode`, so first-match semantics apply (spec 05 §7); `null` for `None` and uncovered positions. `IsModifier(...)` is true for exactly the eight left/right modifier positions.
- Spec-mandated specials: the eight modifiers resolve to their **dedicated** codes 160–165 / 91 / 92 (spec 05 §3.5), never the generic Shift/Ctrl/Alt codes; keypad Enter resolves to internal code **10000**, never main Enter's 13 (spec 10; spec 12 §Programming a pedal step 3); Print Screen → 44, `IntlBackslash` → 226.
- Media/volume positions are declared and map to spec 05 §3.7: `mute` 173, `vol-` 174, `vol+` 175, `next` 176, `prev` 177, `stop` 178, `play` 179, `ejct` 11150. Without them a user pressing Mute to record it would just mute the machine.

## State machine — `KeystrokeRecorder`

`Handle(PhysicalKeyCode, KeyTransition)` → `KeystrokeCaptureResult` (`ShouldSwallow` + optional `Keystroke`); `Reset()` clears held state; `HeldModifiers` snapshots it. It tracks **every** held position (not only modifiers) in the internal `HeldKeySet`, together with a per-position "consumed" flag. Rules, all from spec 10 §Keyboard-input capture unless noted:

- A non-modifier key-down emits the key together with the currently held modifiers, and is swallowed.
- **Auto-repeat never duplicates anything**: a key-down for a position already held is swallowed and emits nothing. The toolkit exposes no repeat flag, so this is recorder-side.
- Print Screen is recognised **only on key-up**; its key-down is swallowed and snapshots the held modifiers, which the key-up then replays — releasing a modifier before Print Screen comes up does not lose it.
- A modifier tapped alone emits on key-up (spec 12 §Programming a pedal step 3). It does **not** emit if it took part in a combination, nor if it was pressed while any other key was already held — an abandoned `Ctrl+Shift` chord emits nothing. Re-pressing a modifier clears its consumed flag, so it can be tapped alone afterwards.
- An unresolvable position passes through untouched on both transitions — but its key-down still consumes the held modifiers, because it is a real physical key press.
- A key-up for a position that is not currently held (a key held before capture started, or one released after `Reset()`) passes through untouched and emits nothing.

`CapturedKeystroke` (record) carries `Key`, the originating `PhysicalKey`, and `HeldModifiers` — deduplicated by key code and **always empty when `Key` is itself a modifier** (spec 05 §5.1), enforced by the type so no caller can build an invalid keystroke.

## Gating — `KeystrokeCaptureSession`

The UI-free owner of *when* the recorder runs, so the rule is testable instead of living in the adapter.

- `Start`/`Stop`/`Suspend`/`Resume` + `SetTextInputFocused(bool)`; `IsCapturing`, `IsSuspended`, `IsExplicitlySuspended`, `IsTextInputFocused`, `HeldModifiers`; events `KeystrokeCaptured` and `StateChanged`.
- `Handle(...)` returns `PassThrough` — nothing captured, nothing swallowed — whenever the session is not capturing or is suspended; otherwise it delegates to the recorder and raises `KeystrokeCaptured` for each completed keystroke.
- **`IsSuspended` is authoritative and computed**: explicitly suspended **or** text input focused. The two sources are independent and neither clears the other; both reset the recorder so nothing leaks into the next recording.
- `Stop` resets the recorder **and** clears an explicit suspension — `Suspend(); Stop(); Start();` must not come back silently deaf. `Resume` on a stopped session only clears the suspension; it never starts capture.
- `StateChanged` fires only when `IsCapturing`/`IsSuspended` actually move, so a UI can refresh its status without polling.

## Adapter — `AvaloniaKeystrokeCaptureService`

- Implements `IKeystrokeCaptureService` by delegating every question to a `KeystrokeCaptureSession`; it re-exposes `StateChanged`. `Dispose` stops and detaches, idempotently.
- Attaches `KeyDownEvent`/`KeyUpEvent` on the `TopLevel` in the **tunnel** phase with `handledEventsToo: true` (both are registered `Tunnel, Bubble`). Tunnel is what makes the window see and consume a key before any focused control — the Avalonia equivalent of spec 10's macOS "main form previews all key events … and consumes the event". `handledEventsToo` is purely defensive: the `TopLevel` is the first node on the tunnel route, so nothing can have handled the event yet.
- Per event: translate `e.PhysicalKey`, push the current focus verdict into the session, feed it, set `e.Handled` **iff** `ShouldSwallow`. No rules of its own.
- Its only platform judgement is "does focus sit in a `TextBox`" (visual-ancestor walk), pushed into the session via `SetTextInputFocused` — before each key event and from `GotFocusEvent`/`LostFocusEvent` handlers on the `TopLevel`, so status updates the moment focus moves. `SuspendOnTextInputFocus` (default `true`) turns that auto-suspend off, which is what lets the harness demonstrate swallowing.
- `AvaloniaPhysicalKeyBridge` builds `PhysicalKey` → `PhysicalKeyCode` in one cached, case-sensitive `Enum.TryParse` pass by member name. All 126 declared Core names match Avalonia 11.3.12 exactly, so zero aliases are needed. A future rename gets an alias here, never a rename in Core.

## The consumer — the keyboard editor

`DeviceEditorViewModel` is the first real consumer ([keyboard-editor.md](keyboard-editor.md)): it subscribes to `KeystrokeCaptured` for the editor's lifetime, calls `Start()` **only** when a key enters listening state, applies the captured `CapturedKeystroke.Key` through `KeyboardKey.Remap`, and calls `Stop()` on the keystroke, on cancel, on a layer switch and on `Dispose`. One instance is built lazily by `App` over the shell window and shared by every editor; the shell disposes it after the editor.

- **The editor's own Escape is a casualty of "swallow everything", by design.** While a key listens, Escape resolves like any other position and becomes the assignment — a keyboard must be able to carry an Escape remap — so the editor cancels by pointer instead. See [keyboard-editor.md](keyboard-editor.md) for the full rule and the tunnel-order reason its Escape handler is a safety net rather than the path.
- **Suspension has no consumer yet.** Nothing calls `Suspend`/`Resume`: the editor has no text-entry dialog, and the adapter's text-input auto-suspend covers the only case that exists. Macro recording (#15) is what makes the explicit calls matter.

## Permissions and platform reach (spike findings)

- Avalonia 11.3.12's `KeyEventArgs.PhysicalKey` supplies the left/right modifier distinction natively, so there is **no native interop, no `CGEventTap`, no macOS Accessibility (TCC) permission, and no app-bundle identity work**.
- This beats the legacy app: spec 10 records that the legacy macOS path had to normalize generic modifiers to their left variants because "macOS does not report a generic-modifier distinction usable here". This app distinguishes them for real.
- One implementation covers macOS, Windows and Linux. The legacy Windows `WH_KEYBOARD` low-level hook is unnecessary — it was thread-local and active only while the app had focus, i.e. never a global capture either.
- **Limitation:** capture is focused-window only. OS-reserved combinations (macOS Cmd+Tab, Cmd+Q, Cmd+Space; Windows Win+L, Ctrl+Alt+Del) are consumed by the OS before the app sees them and can be neither captured nor swallowed. The legacy apps had the same limitation; escalating to a `CGEventTap` would require the Accessibility permission and is deliberately out of scope.
- **Verified status.** Guarded by tests: the Core state machine and the capture/suspend gating (`KinesisEdit.Core.Tests/Input/`). Guarded by the build only: that the solution compiles and that every compiled binding in the harness resolves. **Checked once by hand and guarded by nothing**: that the app launches; the by-name alignment of `PhysicalKeyCode` with Avalonia's `PhysicalKey`; that `KeyDownEvent`/`KeyUpEvent` support `Tunnel`; and that `GotFocusEvent`/`LostFocusEvent` are `Bubble`-only, which is why the focus handlers register `Tunnel | Bubble`. If Avalonia renames a `PhysicalKey` member, no test fails — that position silently becomes `None` and falls through to the OS. Still needs a human at the keyboard: that keys are really swallowed, that left vs right modifiers report distinctly from real hardware, and that focus-based pass-through works. `KinesisEdit.Views.KeystrokeCaptureSpikeWindow` is the issue-#12 harness for exactly that: start/stop, manual Suspend/Resume, live status, a per-keystroke log (code + Legacy/Gen1/Gen2 tokens + held modifiers), and a text box that is a pass-through demo with auto-suspend on and a **swallow probe** with it off (typing produces no text while the log fills). It is kept out of the shipped UI and opens as the main window only on demand: `dotnet run --project src/KinesisEdit -- --keystroke-spike` (no flag → the normal app shell). It builds its own capture service and touches no other app service.

## Load-bearing invariants

1. **All decisions in Core.** The adapter only translates, judges focus, and marshals; anything with a rule in it belongs in `KeystrokeRecorder` or `KeystrokeCaptureSession`, which are unit-tested. This is what keeps the untested UI layer defensible.
2. **Physical position, never character.** Capture reads `PhysicalKey`, so the recorded key is layout-independent and left/right modifiers stay distinct (spec 10).
3. **Swallow only what is understood.** `ShouldSwallow` is false for unresolvable positions *and* for a key-up the recorder never saw go down; the app must never eat a key it did not record.
4. **Suspension is total.** While suspended (explicitly or by text focus) nothing is captured and nothing is swallowed (spec 10), and `IsSuspended` reports both sources.
5. **Keypad Enter ≠ main Enter.** Code 10000 vs 13 — a distinction the whole capture chain must preserve (spec 10; spec 12).

## Deliberately not here

- **No modifier-string encoding.** The spec 05 §5.1 two-character form (`'LS'`, `'RC'`, `'S '` — the trailing space is load-bearing) is a file-format concern for the parser/serializer work; capture emits `KeyDefinition`s.
- **No routing.** Deciding whether a captured key becomes a remap, a macro step, or a Tap-and-Hold action (spec 10 §Routing; spec 11 §Tap-and-Hold) is the editor UIs' job — the remap leg is in [keyboard-editor.md](keyboard-editor.md); the other two are issue #15.
- **No global capture.** See the limitation above — focused-window only, by design.
- **No pedal specifics.** Spec 12's single-action vs macro edit modes are pedal-editor behaviour built on top of this service.
