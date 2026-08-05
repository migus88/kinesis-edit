# Savant Elite2 pedal file (SavantElite) and its read-only view

The pedal-file engine of `KinesisEdit.Core` plus the app-layer view over it: the engine parses and serializes the Savant Elite2's single config file `active/pedals.txt` — seven inputs, each carrying one key/mouse action or one macro — per spec 12 §4. Consumes the key-token registry ([domain-data.md](domain-data.md)) in the Legacy dialect, the `Macro`/`Keystroke` model ([keyboard-model.md](keyboard-model.md)), and `IVDriveFileService` ([vdrive.md](vdrive.md)) for all file access. It does **not** touch `KeyboardLayout` (the SE2 has no geometry and no layers — `KeyboardLayout.Create` throws for it), is not a `LayoutDialect` ([layout-files.md](layout-files.md) parses none of this), has no lighting or settings path (both capabilities are `None`), and never ejects.

| Namespace | Entry point | Does | Owning spec |
|---|---|---|---|
| `KinesisEdit.Core.SavantElite` | `PedalFileParser.Parse(lines)` | Lines → `PedalParseResult` (configuration + invalid lines + owned source lines) | 12 §4.1, §4.2 |
| `KinesisEdit.Core.SavantElite` | `PedalFileSerializer.Serialize(config)` / `.Merge(config, sourceLines)` / `.SerializeInput(input)` | Model → the 7-line fresh file / the in-place rewrite / one line | 12 §4.3, §4.6 |
| `KinesisEdit.Core.SavantElite` | `PedalFileService(IVDriveFileService)` | `GetPedalFilePath`, `Load`, `Save` against a `VDriveLocation` | 12 §4.6, §5 |
| `KinesisEdit.Core.SavantElite` | `PedalConfiguration`, `PedalInput`, `PedalInputId`, `PedalInputMode`, `PedalInputs` | The 7 inputs, their mode/action/macro, tokens + display names | 12 §1, §4.2, §5 |
| `KinesisEdit.Core.SavantElite` | `PedalTokenMap` | Token ↔ key, plus the 4 §4.4 spellings the key table cannot express | 12 §4.4; 05 §3.5, §3.14 |
| `KinesisEdit.Core.SavantElite` | `PedalInvalidLine`, `PedalLineSegment`, `PedalSourceLine` | Tracked lines with validity spans; per-line ownership for the save merge | 12 §4.2, §4.6 |
| `KinesisEdit.Core.SavantElite` | `PedalActionDescriber` | Assignment display text + double-click recognition | 12 §4.5, §5, §6 |
| `KinesisEdit.ViewModels` | `SavantElitePedalViewModel` (+ `PedalInputRowViewModel`, `PedalInvalidLineViewModel`, `PedalLoadState`) | The read-only view model: seven rows, invalid lines, load state | 12 §5 |
| `KinesisEdit.Views` | `SavantElitePedalView` | The XAML for it | 12 §5 |

## Model

`PedalConfiguration` holds exactly seven `PedalInput`s in `PedalInputs.All` order — `PedalInputs` derives both the order and the file tokens from `KeyRegistry.PedalPositionTokens` (05 §3.14), so there is no second list; `PedalInputId` is the enum over them (1-based, `None = 0`) and `GetDisplayName` supplies the §5 row captions (`Left Pedal`… `Jack 4`). A `PedalInput` is a mutable POCO: `Mode` (`None`/`Single`/`Macro`), `Action` (`KeyDefinition?`), `Macro` (`Macro?`), kept consistent by `SetSingleAction`/`SetMacro`/`Clear` — a null payload means unassigned. `Macro` is the shared model type used unchanged, including its `Keystroke.DiffPressRelease` flag; speed/repeat are never written for the pedal. `Clone()` on both types is the editor's Cancel backup (§5 step 1).

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

## Read-only view in the shell (12 §5)

`SavantElitePedalViewModel` + `SavantElitePedalView` are the SE2's editor slot in the app shell ([app-shell.md](app-shell.md)) — currently **read-only**: it loads the file once in its constructor and shows it. Wiring is two lines: `App.BuildServices` constructs one stateless `PedalFileService` over the shared `VDriveFileService`, and `MainWindowViewModel.OpenDevice` builds this view model instead of `EditorPlaceholderViewModel` when `device.DeviceId == DeviceId.SavantElite2` (the only device branch in the shell; `Editor` is typed `DeviceEditorViewModel`, the small base carrying `Device`/`DeviceName`/`IsDemoMode`). Session lifetime, the loading caption, Home and the v-Drive indicator are unchanged — nothing about the pedal is special to the shell beyond which view model it constructs.

What it shows: the device name and the demo badge; **always seven rows** in `PedalInputs.All` order (`PedalInputRowViewModel`: caption from `PedalInputs.GetDisplayName`, `Single action`/`Macro` caption, assignment text from `PedalActionDescriber.Describe`, and `IsAssigned` for muting); every `PedalInvalidLine` as line number + owning input + verbatim text; and one status note. Long macro text wraps rather than clipping. Modern styling only — the legacy two-panel form, the yellow edit box, the `Configure` buttons and the `*Modified` labels of §5 are deliberately not reproduced.

`PedalLoadState` is the whole state machine, and the view colors the note from it (no brushes in the view model, per app-shell invariant 6):

| State | When | Note |
|---|---|---|
| `Loaded` | The file parsed | none, or `EmptyFileMessage` when the file programs nothing |
| `DemoMode` | `Device.IsDemoMode` or no `Location` — **the filesystem is never touched**, not even for a present-but-unwritable drive | `DemoModeMessage` (amber) |
| `FileMissing` | `FileNotFoundException`/`DirectoryNotFoundException` — an expected state for a pedal that was never programmed, not an error (§5 step 8's "Create a new file?" is a *save*-time prompt) | `MissingFileMessage` (amber) |
| `LoadFailed` | Any other I/O failure | `LoadFailureMessagePrefix` + the exception message (red) |

Every state still renders the seven rows, so the view is never blank. All wording is `const` on the view model and pinned by `KinesisEdit.Tests/ViewModels/SavantElitePedalViewModelTests`.

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

- **No editing — the view reads, it never writes.** The capture wiring (§5 step 3), the Single Action / Multiple Actions modes with their Backspace/Clear/Done/Cancel semantics, the Special Actions menu of §6, the `*Modified` markers, and the Save / Save As / Open-another-file flows of §5 step 8 are all still missing; issue #18 stays open for them. `PedalFileService.Save`, `PedalConfiguration.Clone` and `PedalActionDescriber.DescribeMacroItems` exist for that work and are currently called by nothing in the app.
- **No detection or troubleshoot dialogs** (12 §3) — v-Drive discovery already covers the SE2 through the catalog ([vdrive.md](vdrive.md)) and the shell's `NoDeviceViewModel` supplies the `Pedal not detected` empty state; the pedal view only reports demo mode as a note, and the `Pedal Connection Lost` dialog is save/load-time behaviour that arrives with editing.
- **No firmware surface** (12 §7) — the SE2 has no firmware-update UI and its `version.txt` is parsed by the firmware module ([firmware.md](firmware.md)) for the About box only.
- **No `Keep` flag on invalid lines** — unlike layout files, a broken pedal line's slot is rewritten from the model on save, so there is nothing to opt into keeping.
