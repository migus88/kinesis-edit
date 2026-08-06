# Savant Elite2 pedal file (SavantElite) and its editor

The pedal-file engine of `KinesisEdit.Core` plus the app-layer editor over it: the engine parses and serializes the Savant Elite2's single config file `active/pedals.txt` — seven inputs, each carrying one key/mouse action or one macro — per spec 12 §4. Consumes the key-token registry ([domain-data.md](domain-data.md)) in the Legacy dialect, the `Macro`/`Keystroke` model ([keyboard-model.md](keyboard-model.md)), and `IVDriveFileService` ([vdrive.md](vdrive.md)) for all file access. It does **not** touch `KeyboardLayout` (the SE2 has no geometry and no layers — `KeyboardLayout.Create` throws for it), is not a `LayoutDialect` ([layout-files.md](layout-files.md) parses none of this), has no lighting or settings path (both capabilities are `None`), and never ejects.

| Namespace | Entry point | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.SavantElite` | `PedalFileParser.Parse(lines)` | Lines → `PedalParseResult` (configuration + invalid lines + owned source lines) | 12 §4.1, §4.2 |
| `KinesisEdit.Core.SavantElite` | `PedalFileSerializer.Serialize(config)` / `.Merge(config, sourceLines)` / `.SerializeInput(input)` | Model → the 7-line fresh file / the in-place rewrite / one line | 12 §4.3, §4.6 |
| `KinesisEdit.Core.SavantElite` | `PedalFileService(IVDriveFileService)` | `GetPedalFilePath`, `Load`, `Save` against a `VDriveLocation` | 12 §4.6, §5 |
| `KinesisEdit.Core.SavantElite` | `PedalConfiguration`, `PedalInput`, `PedalInputId`, `PedalInputMode`, `PedalInputs` | The 7 inputs, their mode/action/macro, tokens + display names | 12 §1, §4.2, §5 |
| `KinesisEdit.Core.SavantElite` | `PedalTokenMap` | Token ↔ key, plus the 4 §4.4 spellings the key table cannot express | 12 §4.4; 05 §3.5, §3.14 |
| `KinesisEdit.Core.SavantElite` | `PedalInvalidLine`, `PedalLineSegment`, `PedalSourceLine` | Tracked lines with validity spans; per-line ownership for the save merge | 12 §4.2, §4.6 |
| `KinesisEdit.Core.SavantElite` | `PedalActionDescriber` | Assignment display text + double-click recognition | 12 §4.5, §5, §6 |
| `KinesisEdit.Core.SavantElite` | `PedalEditSession` | One in-progress edit of one input: mode, entry buffer, sticky Win modifier, display items, commit | 12 §5, §6 |
| `KinesisEdit.Core.SavantElite` | `PedalSpecialActions` (+ `PedalSpecialAction`, `PedalSpecialActionGroup`, `PedalSpecialActionModes`, `PedalSpecialActionKind`) | The Special Actions menu as domain data | 12 §6 |
| `KinesisEdit.Core.SavantElite` | `PedalSpecialActionResult`, `PedalSpecialActionFailure` | Whether a menu item applied, and why it did not | 12 §6 |
| `KinesisEdit.ViewModels` | `SavantElitePedalViewModel` (+ `PedalInputRowViewModel`, `PedalInvalidLineViewModel`, `PedalLoadState`) | The editor's view model: seven rows, the entry box, invalid lines, load state, Save | 12 §5 |
| `KinesisEdit.ViewModels` | `PedalSpecialActionGroupViewModel`, `PedalSpecialActionViewModel`, `PedalEntryText` | The §6 menu as rows; the entry box's `␣` rule | 12 §5, §6 |
| `KinesisEdit.Views` | `SavantElitePedalView` | The XAML for it | 12 §5 |
| `KinesisEdit.Converters` | `MenuItemToggleTypeConverter` | `IsCheckable` → a `MenuItem`'s `ToggleType`, so only §6's Win entry is a check box | 12 §6 |

## Model

`PedalConfiguration` holds exactly seven `PedalInput`s in `PedalInputs.All` order — `PedalInputs` derives both the order and the file tokens from `KeyRegistry.PedalPositionTokens` (05 §3.14), so there is no second list; `PedalInputId` is the enum over them (1-based, `None = 0`) and `GetDisplayName` supplies the §5 row captions (`Left Pedal`… `Jack 4`). A `PedalInput` is a mutable POCO: `Mode` (`None`/`Single`/`Macro`), `Action` (`KeyDefinition?`), `Macro` (`Macro?`), kept consistent by `SetSingleAction`/`SetMacro`/`Clear` — a null payload means unassigned. `Macro` is the shared model type used unchanged, including its `Keystroke.DiffPressRelease` flag; speed/repeat are never written for the pedal. `Clone()` on both types is §5 step 1's Cancel backup, and is **used by nothing** — `PedalEditSession` owns a private copy until Done (invariant 1 of the editing layer), so the editor needs no snapshot to restore.

## Parsing (12 §4.1, §4.2)

- Lines are lowercased per character and right-trimmed (offsets keep mapping onto the verbatim original for segment tracking). Only the *line* is trimmed — a trailing `{ }` group ends with `}`, so its inner space survives.
- A line is a pedal line only when it **starts** with `[input]` or `{input}` naming one of the seven tokens; the bracket style selects the mode for the whole line. Anything else — the factory `*` instruction block, its ASCII module diagram, blank lines, a line that merely *contains* `[lpedal]` — is preserved verbatim and carries `PedalInputId.None` in `SourceLines`.
- Single mode reads exactly one `[token]`; nothing after `>` leaves the input unassigned (the `[lpedal]>` line of a fresh file).
- Macro mode iterates the `{...}` groups: a leading `-` is key-down, `+` key-up, **first character only** (`{-.}`, `{-vol-}`, `{+vol+}`, `{-intl-\}`). A non-modifier down/up pair for the same key collapses into **one** keystroke — the factory `{-x}{+x}` spelling and the app's bare `{x}` produce identical models. A dangling `{+x}`, or a `{-x}` never closed, is still one keystroke: nothing is dropped.
- Modifier state machine (identical to `Layouts.MacroValueReader`, 06 §2.2): `{-mod}` joins the active set (deduped by key code), plain keys record the held set, `{+mod}` leaves it; a modifier pressed and immediately released, an up with no matching down, and a still-held never-used modifier at line end all become plain keystrokes of that modifier.
- A line whose every segment is valid is applied; otherwise it lands in `InvalidLines` (1-based number, owning input, verbatim text, segments tiling the text) and the input keeps its previous state. Duplicates apply in file order, so **last wins** — including when the same input appears in both bracket forms.

## Serializing (12 §4.3, §4.6)

`SerializeInput` writes `[input]>[token]` (empty after `>` when unassigned) or `{input}>` plus the macro groups. `PedalMacroWriter` mirrors `MacroKeystrokeRenderer`'s modifier diffing (06 §3) — modifiers first, each normal key as one `{token}` group, consecutive Shift-only keys sharing one `{-shift}…{+shift}` wrapper, still-held modifiers closed after the last keystroke, `DiffPressRelease` written as `{-key}{ }{+key}` inside its wrap, and the `SingleEvent` speed/delay keys always as one bare `{speedN}`/`{dNNN}` group. It cannot call the renderer directly because two pedal tokens differ from the registry's Legacy column (see invariant 3 and 8).

Two save shapes, per §4.6:

- **`Serialize`** — exactly seven lines in `PedalInputs.All` order, for a file that does not exist yet.
- **`Merge`** — every source line whose owner is an input is regenerated from the model **at its own index** (in whichever bracket form the model now needs, so a single↔macro flip keeps its slot); every other line is copied verbatim. A line is *added* only for an input the file never mentioned that the model now assigns (`IsAssigned`), and it is inserted right after the last pedal-owned line (at the end when the file has no pedal line at all), in `PedalInputs.All` order — see invariant 11. Duplicate lines for one input are each rewritten with that input's current text, so the file stays self-consistent under the firmware's last-wins rule.

"Copied verbatim" is the line **content**: `IVDriveFileService.WriteAllLines` re-terminates every line with the platform newline (spec 03 §5.2 "native platform line endings are produced"), so a CRLF device file saved on macOS comes back LF-terminated throughout. Nothing else about a preserved line changes.

`PedalFileService` binds this to a drive: `GetPedalFilePath` builds `<root>/active/pedals.txt` from the catalog's `LayoutFileScheme` (`Kind = PedalFile`) and throws for any other device; `Load` throws `FileNotFoundException` (§5 step 8's "Create a new file?" prompt is the UI's call); `Save` re-reads the file, merges, and writes — falling back to `WriteAllLines(allowCreate: true)` with the seven-line form when **the re-read** reports `FileNotFoundException`. The catch is scoped to the read alone, unlike `SettingsService.SaveAppSettings`, whose read and write share one `UpdateSettingsFile` call: `WriteAllLines` raises the same exception when the file vanishes mid-save (the drive is unmounted, the pedal is flipped to play mode), and answering that with the create path would replace a drive that still held the factory block with seven bare lines. A lost file at write time propagates instead.

## Tokens (12 §4.4)

Resolution is Legacy dialect → `PedalTokenMap.ExtraTokenCodes` → any dialect (read tolerantly). The extras are the four §4.4 spellings the key table cannot express:

| Token | Resolves to | Why |
|---|---|---|
| `" "` (bare space) | space bar (32) | The save value is a single space character; `KeyRegistry.FindByToken` rejects whitespace |
| `125` / `500` | `d125` / `d500` (10007/10008) | Firmware 1.0.44 field files use the bare-number form |
| `win` | Left Win | §4.4 lists only `win`; the key table spells the same keys `lwin`/`rwin` (05 §3.5) |

Writing is the registry's canonical casing (`F1`–`F24` capitalised, everything else lowercase) with two pedal overrides: either Win key writes `win`, and the space bar writes `space` in single mode but the bare space in macro mode (producing the literal `{ }`). Everything else in the §4.4 catalog — including `shutdn`, which parses fine and is simply never offered in a menu — resolves straight through the Legacy dialect; `Integration/PedalTokenIntegrationTests` pins the whole catalog.

## Display text (12 §4.5, §5, §6)

`PedalActionDescriber` is pure and presentation-neutral (plain strings; which items §5 paints red is the UI's decision). `Describe(input)` renders a single action as `[x]` and a macro as its items concatenated — an unmodified item as its bare file token (so `{-shift}{t}{+shift}{h}{a}{n}{k}{ }…` reads back as `{shift+t}hank you.`), a modified one as `{mod+key}`, and a `DiffPressRelease` key with a trailing `{ }`. `DescribeMacroItems` returns the same per item. `IsLeftMouseDoubleClick` / `IsLeftMouseDoubleClickAt` recognise the `lmouse` + 125 ms + `lmouse` triple and `LeftMouseDoubleClickText` is the `{lmouse-dblclick}` caption; both field spellings (`{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}` and `{lmouse}{d125}{lmouse}`) qualify because they parse into the same three keystrokes.

## Editing (12 §5, §6)

Two UI-free pieces: `PedalEditSession` is the §5 edit box as a model — one in-progress edit of one input — and `PedalSpecialActions` is the §6 menu as data. The app layer supplies keystroke capture ([keystroke-capture.md](keystroke-capture.md)), the buttons and the menu chrome; neither type knows about either.

| Entry point | Does | §5/§6 step |
|---|---|---|
| `PedalEditSession.BeginFor(input)` | Opens an edit with the input's current programming loaded (macro deep-cloned) | step 1 (Configure) |
| `.Mode`, `.SetMode(mode)` | Single Action / Multiple Actions; `PedalInputMode.None` throws | step 2 |
| `.AddKeystroke(key, modifiers)` | One captured keypress | step 3 |
| `.Apply(action)`, `.CanApply(action)` | A Special Actions pick → `PedalSpecialActionResult` | step 4, §6 |
| `.Backspace()`, `.Clear()` | Remove the last display item / empty the entry | step 5 |
| `.DisplayItems`, `.IsEmpty`, `.SingleAction`, `.Macro`, `.HoldWinModifier` | The entry as the memo shows it and as the model holds it | display conventions |
| `.ApplyTo(input)` | Commits the entry — **Cancel is simply not calling this** | step 6 (Done) |
| `.DiffersFrom(input)` | Whether committing would change the input — the `*Modified` label | step 6 |
| `PedalSpecialActions.Groups`, `.Find(id)` | The five §6 sections in menu order; lookup by stable kebab-case id (`left-mouse-double-click`) | §6 |
| `PedalSpecialAction.Modes`, `.Kind`, `.CreateKeystrokes()`, `.SupportsMode(mode)` | One menu item: where it is offered, what it does, the keys it emits | §6 |

The catalog is MOUSE ACTIONS → EDITING TOOLS → MEDIA CONTROLS → COMMONLY USED SHORTCUTS → PEDAL RESPONSE, items in §6 order — including `Windows Combination (Win + …)`, which §6 lists under COMMONLY USED SHORTCUTS (after `Calculator`, before `Cmd + Tab`), not under PEDAL RESPONSE, whose last item is `Different Press & Release`. `PedalSpecialActionsTests` pins the caption list of each section against the spec table. `Modes` is `Single`, `Macro`, or both; `Kind` is `Keystrokes` (content), `DifferentPressRelease` or `ToggleWinModifier` (editor state, no content). Every key resolves through `PedalTokenMap` at type load and a catalog whose shape is wrong (a keystroke item that emits nothing, a single-mode item that emits more than one key, a duplicate id) throws there too — a registry regression fails loudly instead of silently emitting an empty macro.

Two deliberate departures from the legacy menu, both because a pedal is programmed on one machine and used on another while the file tokens are identical cross-platform (§4.4): **no OS gating** — §6's "Windows only"/"macOS only" items are all present, and the ones that differ per platform appear twice with the platform in the caption (`Cut (Ctrl + X)` *and* `Cut (Cmd + X)`) — and **`shutdn` is not in the catalog**, per §4.4's "defined but not exposed in the menu". Cmd is `MacroModifiers.LeftWin`, which writes the `win` token.

Load-bearing invariants of the editing layer:

1. **The session never touches the `PedalInput` until `ApplyTo`.** `BeginFor` deep-clones the macro in and `ApplyTo` clones it back out, so Cancel needs no backup (unlike §5 step 1, which snapshots the key list) and a committed macro cannot be mutated by later session edits. `ApplyTo` throws `ArgumentException` for a different input id; an empty entry calls `input.Clear()`. **A commit that changes nothing does nothing**: `ApplyTo` returns early when `!DiffersFrom(input)`, so Done on an untouched entry cannot even flip the input's mode.
2. **"Fires nothing" is one state, whichever mode holds it.** `DiffersFrom` compares an empty entry against `input.IsAssigned`, not against `Mode`, because `{jack1}>` — macro mode, empty macro — is a legitimate file line that programs nothing, exactly like an unassigned input. Without that, opening such a row and pressing Done badged it `Modified` and rewrote the line as `[jack1]>` on the next save.
3. **A real mode switch clears the entry** (§5 step 2) and drops the held Win modifier; switching to the mode already in effect is a **no-op that keeps the entry**, so re-clicking a latched button is harmless.
4. **`BeginFor` loads the existing entry** instead of defaulting to Single Action as §5 step 1 did — the legacy behaviour threw an existing macro away the moment Configure was pressed. Unassigned inputs still open as an empty single-action entry; the mode is one click away.
5. **Single mode drops modifiers.** `AddKeystroke(key, modifiers)` in `PedalInputMode.Single` replaces the entry with the bare key: a single-action line is `[input]>[token]`, one token, nowhere to put a modifier string (§4.3).
6. **Display items are grouped greedily left-to-right**, by the same scan `PedalActionDescriber.DescribeMacroItems` uses (`PedalDisplayItems`, internal). Backspace removes one *item*, so a trailing left-mouse double click goes in one press (three keystrokes) — but `lmouse,d125,lmouse,d125,lmouse` is a triple plus two singles, not two triples, and a backwards scan would disagree with the memo.
7. **"Different Press & Release" is refused rather than approximated.** It toggles the flag on the last keystroke and fails (entry untouched) with `PedalSpecialActionFailure.EmptyEntry` when the macro is empty, `ModeNotSupported` when the mode would have to switch first (which would clear the entry, leaving nothing to flag), and `ModifierKeystroke` when the last keystroke is a modifier key — `{-lshift}{ }{+lshift}` reloads as a Shift-modified space under the `{ }` rule below (invariant 1 of the file format), so the flag would vanish on the next load. The three are distinct because the user's fix differs: type something, switch mode, or nothing at all. **A bare `{ }` marker is never appended**, whatever §6's "otherwise a standalone `{ }` marker is appended" says: that shape is a space on reload.
8. **`Apply` mutates nothing when it refuses**, and `CanApply` is the same predicate without mutating — the mode switch happens only after the item is known to be applicable, so a refused item cannot destroy the entry it was going to modify.
9. **The sticky Win modifier rides on captured keystrokes only.** §6 latches it onto "subsequently entered keystrokes", which is `AddKeystroke` and nothing else: a Special Actions item already carries the modifiers §6 gives it, and overlaying Win on those rewrites the item into something else — `{-win}{lmouse}{d125}{lmouse}{+win}` is no longer the §4.5 double click (three display items, three Backspaces) and wraps a *pause* in a Win press/release, which plays back as a bare Win tap. `AddKeystroke` skips a key that is itself a modifier, which `Keystroke.Modifiers` would normalize away anyway — the code says so rather than relying on it.
10. **`CreateKeystrokes()` hands out fresh clones every call.** `Keystroke` is mutable and the catalog is a shared static, so the prototypes are never handed to a caller and the session may append them as they are.

## The editor in the shell (12 §5, §6)

`SavantElitePedalViewModel` + `SavantElitePedalView` are the SE2's editor slot in the app shell ([app-shell.md](app-shell.md)). Wiring is two lines: `App.BuildServices` constructs one stateless `PedalFileService` over the shared `VDriveFileService` and hands it to `EditorViewModelFactory`, which builds this view model instead of `EditorPlaceholderViewModel` when `device.DeviceId == DeviceId.SavantElite2` and hands it the same shared keystroke-capture service and `INotificationService` the keyboard editor gets (the factory is the only place any device is named; the shell's `Editor` is typed `DeviceEditorViewModel` — see [`app-shell.md`](app-shell.md)). Session lifetime, the loading caption, Home and the v-Drive indicator are unchanged; the only thing the shell learns about the pedal is that its `ConfirmCloseAsync` can refuse to be closed.

What it shows: the device name, the demo badge and Save; **always seven rows** in `PedalInputs.All` order (`PedalInputRowViewModel`: caption from `PedalInputs.GetDisplayName`, `Single action`/`Macro` caption, assignment text from `PedalActionDescriber.Describe`, `IsAssigned` for muting, plus the `IsEditing`/`IsModified` flags and an `Edit` button); every `PedalInvalidLine` as line number + owning input + verbatim text; and one status note. Long macro text wraps rather than clipping. Modern styling only — the legacy two-panel form and its yellow edit box are not reproduced; the entry box is inline under the row it programs.

The lifecycle mirrors `KeyboardEditorViewModel` ([keyboard-editor.md](keyboard-editor.md)) exactly: the constructor touches no file and subscribes to `KeystrokeCaptured` for the editor's lifetime, `LoadAsync` does the blocking read on the thread pool against `IsLoading` (idempotent, total, fired and forgotten by the shell), `IsBusy` covers the save — `ShowLoading` inside the `try` and `IsBusy = false` **before** `HideLoading()` in the `finally`, because a failure in either overlay call would otherwise strand the flag, and a stranded `IsBusy` here disables Save, every entry command *and* (through `ConfirmCloseAsync`) Home, i.e. traps the user in the editor — and `Dispose` unsubscribes and stops capture.

| Surface | Does |
|---|---|
| `BeginEditCommand(row)` | Opens `PedalEditSession.BeginFor(row.Input)` on that row and calls `_capture.Start()` |
| `SetSingleModeCommand` / `SetMacroModeCommand` | §5 step 2; a real switch clears the entry |
| `EntryItems`, `EntryText`, `HasEntry`, `EditMode`, `IsSingleMode`/`IsMacroMode`, `HoldWinModifier` | The entry box, re-read from the session after every change |
| `BackspaceCommand`, `ClearEntryCommand` | §5 step 5 |
| `SpecialActionGroups` + `ApplySpecialActionCommand` | The §6 menu; a refusal sets `EntryMessage` and changes nothing |
| `DoneCommand` / `CancelEditCommand` | §5 step 6 — commit (marking the row `IsModified` when it differs) or discard |
| `SaveCommand`, `HasUnsavedChanges`, `CanEdit` | `PedalFileService.Save` on the thread pool, then a toast; **no eject** |
| `ConfirmCloseAsync` | The unsaved-changes gate the shell awaits before Home or another device |

Load-bearing decisions of this layer:

1. **The entry box is not a `TextBox`.** Focus in one auto-suspends the capture service ([keystroke-capture.md](keystroke-capture.md)), so a real text box would record nothing; it is a bordered, read-only `SelectableTextBlock`.
2. **Every message box is wrapped in `Suspend()`/`Resume()`** (the private `ShowMessageBoxAsync` does it for all of them). Capture swallows every key of the focused window while it runs, the dialog's own Enter and Escape included — this is the first consumer of that API. A box that could not be shown returns null and is treated as "the user did not answer".
3. **Picking another row mid-edit asks first** (§5 step 1's `Key modification in progress, apply changes?`): Yes commits, No discards, Cancel keeps the entry open. Re-clicking the row already being edited is a no-op — Done and Cancel are its exits.
4. **`CanEdit` is `Loaded`, `FileMissing` or `DemoMode`** — everything the load can end in except `LoadFailed` (a file nobody could see is not rewritten from the model) and the pre-load `None`, where there is nothing on screen to edit yet. Saving is a narrower question, and it is asked in two parts: `CanEverSave()` is the *capability* — a non-demo session with a `Location` and a `Loaded`/`FileMissing` load — and `CanSave()` adds the transient `HasUnsavedChanges && !IsBusy && !IsLoading`. So demo mode edits but never writes (03 §3.5) and a missing file is created by the save (§4.6's file-absent path).
5. **`ConfirmCloseAsync` asks the question it can answer.** With a writable drive it is §5 step 9's Yes/No/Cancel (Yes saves and returns false if the save failed); when saving is impossible **at all** — demo mode, an unreadable file, i.e. `!CanEverSave()` — it degrades to a Yes/No *discard* question, because a "save" the app cannot perform would trap the user in the editor. **A save in flight refuses outright** (`IsBusy` → false, before any dialog) **but not silently** — it raises the `Saving` / `Please wait for the save to finish.` toast: the top bar is outside the editor's disabled panel and the loading overlay takes no clicks, so Home *is* reachable mid-save (the shell gates it on its own `IsBusy`, not the editor's), and a refusal with no dialog and no toast is a live button that does nothing. Leaving would dispose this editor and eject the volume while `WriteAllLines` is still running — while telling the user their changes "cannot be saved", which is exactly wrong. The write is short; the navigation works the moment it finishes.
6. **`PedalEntryText` is the entry box's only display rule**: §5's `␣` (U+2423) for a space, applied to the box and **not** to the row memos, which keep the raw file tokens.
7. **A refused Special Action is an inline line, not a dialog.** `Apply` mutates nothing when it refuses, so there is nothing to undo and nothing to confirm.
8. **The Special Actions menu carries its own command.** `PedalSpecialActionViewModel.ApplyCommand` is the editor's command handed down, because the menu lives in a flyout whose popup is outside the view's tree and cannot reach it with an ancestor binding.
9. **The menu is a `MenuFlyout` of real `MenuItem`s**, five top-level items (the §6 sections) with their actions underneath, built from `SpecialActionGroups` through two `ControlTheme`s (`ItemContainerTheme`). A `MenuItem` closes the menu when it is invoked; the plain buttons it replaced left a tall popup sitting over Backspace / Clear / Cancel / Done after every pick. **`ToggleType` is bound per item** (`IsCheckable` through `MenuItemToggleTypeConverter`: `CheckBox` for §6's one checkbox item, `None` for the rest) and `IsChecked` follows the session. It has to be per item: Avalonia's menu interaction handler check-marks *any* leaf item whose `ToggleType` is `CheckBox` the moment it is clicked, writing through `SetCurrentValue`, and the one-way `IsChecked` binding never takes that back — the view model's value does not change, so it raises nothing to override with. The Fluent theme keys its check glyph off the `MenuItem`'s own `:checked` pseudo-class, not off the view model, and the `MenuFlyout`'s popup keeps its containers between openings, so a blanket `ToggleType="CheckBox"` left a permanent mark on every action that had ever been picked. `IsEnabled` is `CanApply`; an item belonging to the other mode is muted, not hidden, because picking it still works (§6 switches the mode).
10. **`HoldWinModifier` re-evaluates the commands itself.** Clear is offered for a latched modifier over an empty entry, and latching it changes nothing else the command could be re-evaluated from — `EntryItems` hands back the same empty list, which `SetProperty` sees as unchanged.

`PedalLoadState` is the whole load state machine, and the view colors the note from it (no brushes in the view model, per app-shell invariant 6):

| State | When | Note |
|---|---|---|
| `None` | Before `LoadAsync` runs — the rows exist, but nothing has been read yet, so the editor is not editable either | none |
| `Loaded` | The file parsed | none, or `EmptyFileMessage` when the file programs nothing |
| `DemoMode` | `Device.IsDemoMode` or no `Location` — **the filesystem is never touched**, not even for a present-but-unwritable drive | `DemoModeMessage` (amber) |
| `FileMissing` | `FileNotFoundException`/`DirectoryNotFoundException` — an expected state for a pedal that was never programmed, not an error (§5 step 8's "Create a new file?" is a *save*-time prompt) | `MissingFileMessage` (amber) |
| `LoadFailed` | Any other I/O failure | `LoadFailureMessagePrefix` + the exception message (red) |

Every state still renders the seven rows — they exist before the load, so the view is never blank and never flashes. All wording is `const` on the view model and pinned by `KinesisEdit.Tests/ViewModels/SavantElitePedalViewModelTests` (what the file says) and `SavantElitePedalViewModelEditingTests` (what programming it does).

## Load-bearing invariants

1. **`{ }` is a split marker in exactly one shape; everywhere else it is the space bar** (12 §4.4, §6). Inside an open, not-yet-split key-down (`{-k}{ }{+k}`) it sets that keystroke's `DiffPressRelease`. **Every other `{ }` is the space key (code 32)**, held modifiers included — `{-ctrl}{a}{ }{+ctrl}` is Ctrl+`a` then Ctrl+space, and a second marker on the same key-down (`{-a}{ }{ }`) is `a`(split) + space. Why: §6 writes the split as `{-key}{ }{+key}` only "when the key has modifiers, *otherwise* a standalone `{ }` marker is appended", so a bare `{ }` while modifiers are held is a shape the legacy app never writes, while §4.4 says the legacy loader "resolves `{ }` to the space key". Reading a modifier-shielded `{ }` as a split destroyed real data — it is exactly what `PedalMacroWriter` emits for a space under held modifiers. Accepted consequence: a legacy standalone marker after an *unmodified* key loads as a space rather than a split flag; §4.4 sanctions that resolution, and it is the only reading under which the module round-trips its own output losslessly (`PedalRoundTripTests.RoundTrip_WithAModifierShieldedSpace_KeepsTheSpaceKeystroke`).
2. **`{125}`/`{500}` are read, `{d125}`/`{d500}` are written** (12 §4.4). A firmware-1.0.44 field file upgrades on the first load→save cycle; the legacy app could not round-trip it at all.
3. **The space asymmetry is real** (12 §4.4). Single mode writes `[space]`, macro mode writes a literal `{ }`. This is why the pedal has its own macro writer instead of calling `MacroKeystrokeRenderer`.
4. **Direction-prefix stripping is the first character only** (12 §4.2). `{-.}` is a press of `.`, `{-vol-}` of `vol-`, `{+vol+}` a release of `vol+`, `{-intl-\}` a press of `intl-\` — the factory file literally contains `{-.}{+.}`.
5. **Duplicate lines for one input: last one wins** (12 §4.1 load order; the Legacy remap rule of 04 §2.1). This holds across bracket forms too — a `[lpedal]` line after a `{lpedal}` line makes the input single.
6. **`shutdn` parses like any other token** (12 §4.4). It is defined and never offered in a menu; suppressing it is a UI decision, not a parse rule.
7. **No non-US virtual-key canonicalization** (12 §4.6 mentions converting virtual keys to US-English equivalents on save). That is Windows-only legacy behaviour with no macOS analogue; file tokens are already canonical US spellings, so nothing is translated here.
8. **No eject** (12 §5 step 7). A save ends with "NOW CHANGE YOUR SE2 TO 'PLAY MODE' TO IMPLEMENT CHANGES" — the firmware reloads on the physical mode switch or a replug, so `VDrive.Eject` is never involved.
9. **Only lines that *start* with an input name are ours.** Everything else survives a save with its content verbatim (line terminators normalized, see above), including the `*` block, the ASCII diagram, blank lines, and lines that mention a pedal token further along.
10. **`KeyboardLayout` is never built for the SE2.** No geometry, no layers, no remap-vs-macro distinction (12 §7) — `PedalConfiguration` is the whole model.
11. **A save never grows a file that programs nothing new** (12 §4.6 "rewrites *only* the lines whose prefix matches a pedal token"; §1 "your device will only have some of these inputs"). Loading a five-input file and saving it unchanged yields the same five lines — an unassigned input the file never mentioned gets no line, and an input that *is* assigned gets one inserted next to the pedal block rather than appended after the factory diagram. Only the file-absent path writes all seven lines.

## Deliberately not here

- **No unsaved-changes question on window close** (§5 step 9). `ConfirmCloseAsync` guards the two *navigations* the shell owns — Home and opening another device — but quitting the app does not go through it: `MainWindowViewModel.Dispose` calls `CloseEditor()` directly, which disposes the editor and drops its unsaved edits without asking. Closing the window with pending changes therefore loses them silently. Wiring the window's closing event to `ConfirmCloseAsync` (and cancelling the close on `false`) is shell work no issue owns yet.
- **No Save As and no Open-another-file** (§5 step 8). The editor loads and saves the pedal's own `active/pedals.txt` and nothing else: a file picker over a v-Drive that holds exactly one config file is a legacy-app affordance, not a requirement.
- **No `Pedal Connection Lost` dialog** (12 §3, 03 §3.5). A drive that disappears mid-session already flips the shell's indicator to `v-Drive Error` and a save against it fails with the exception's own message; the dedicated dialog is shared save/load-time behaviour, still owned by no issue in this module.
- **No pedal illustration.** §5's left panel drew the hardware; the seven rows carry the same information and every product has a different subset of them (§1).
- **No detection or troubleshoot dialogs** (12 §3) — v-Drive discovery already covers the SE2 through the catalog ([vdrive.md](vdrive.md)) and the shell's `NoDeviceViewModel` supplies the `Pedal not detected` empty state; the pedal view only reports demo mode as a note.
- **No firmware surface** (12 §7) — the SE2 has no firmware-update UI and its `version.txt` is parsed by the firmware module ([firmware.md](firmware.md)) for the About box only.
- **No `Keep` flag on invalid lines** — unlike layout files, a broken pedal line's slot is rewritten from the model on save, so there is nothing to opt into keeping.
