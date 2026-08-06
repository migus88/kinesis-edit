# Design system (Themes, Styles, Assets/Fonts, motion, icons, the headless test harness)

The implementation of `docs/design/` inside `KinesisEdit`: the colour/shape/type/motion tokens as Avalonia resources, the drawn marks (state icons, device silhouettes, lighting-mode marks) and the leaf control that paints them, the shared style layer built on them, the two embedded IBM Plex families, the reduce-motion switch, and the headless UI test harness that guards all of it. `docs/design/` states the laws; this doc is how they are spelled in the app, plus every place the implementation deliberately differs and why.

**Read this before touching any view, style or user-facing string** — together with `docs/design/README.md`, which is the design source and must never be rewritten to match the code.

| File | Kind | Holds |
|---|---|---|
| `Themes/Tokens.axaml` | `ResourceDictionary` + `ThemeDictionaries` | The colour roles. 92 keys per variant, `Dark` and `Light` identical key sets. |
| `Themes/Geometry.axaml` | `ResourceDictionary` | Radii, paddings, the spacing scale, chrome heights, rails, icon/spinner/hatch metrics. Theme-independent. |
| `Themes/Typography.axaml` | **`Styles`** | The two `FontFamily` resources, the `FontSize*`/line-height/tracking doubles, and the type-scale classes. |
| `Themes/Motion.axaml` | `ResourceDictionary` | The motion budget: named `TimeSpan`s plus a `…Full`/`…Reduced` `Transitions` pair per motion. |
| `Themes/Icons.axaml` | `ResourceDictionary` | 10 state/action/chrome marks as `StreamGeometry`. Colourless, theme-independent. |
| `Themes/DeviceArt.axaml` | `ResourceDictionary` | 7 device silhouettes, one per programmable board, all in one 92×56 box. |
| `Themes/LightingMarks.axaml` | `ResourceDictionary` | 16 lighting-mode marks, one per `LightingMode` member. |
| `Themes/Controls.axaml` | `ResourceDictionary` + `ThemeDictionaries` | Alias map putting FluentTheme's own control keys onto our roles. 267 keys per variant + 2 shape aliases. |
| `Controls/Icon.cs` | C# | The leaf control that draws one mark: the fit, the pen, the caps. No colour, no motion. |
| `Converters/GeometryResources.cs`, `DeviceArtConverter`, `LightingModeMarkConverter` | C# | Enum → geometry key → resolved `Geometry`, for the two families that are driven by data. |
| `Styles/Text.axaml` | `Styles` | Text *roles* (colour and emphasis) layered over the type scale. |
| `Styles/Surfaces.axaml` | `Styles` | Window, cards, overlays, scrim, popovers, toasts, the status chip, framed lists and hairline rows, the device-art frame, the logo tile. |
| `Styles/Buttons.axaml` | `Styles` | `primaryAction`, `rowButton`, `zoneButton`, `ListBoxItem`, `ProgressBar`. |
| `Styles/Keyboard.axaml` | `Styles` | The board, the key cap and its four states, the recording pulse, the layer pill. |
| `Styles/Editor.axaml` | `Styles` | Editor tab strip, action fields, lighting mode menu and its mode marks, colour swatches. |
| `Styles/Icons.axaml` | `Styles` | The two `Icon` classes: `.deviceArt` (the shared box) and `.spinner` (box + rotation). |
| `Assets/Fonts/` | `AvaloniaResource` | IBM Plex Sans 400/500/600/700, IBM Plex Mono 400/500/600, `LICENSE.txt` (SIL OFL). |
| `Services/IMotionSettings`, `MotionResourceBinder`, `IReduceMotionDetector` | C# | Reduce-motion detection and the alias binding it drives. |

**There are two files named `Icons.axaml`** — `Themes/Icons.axaml` holds the geometry, `Styles/Icons.axaml` holds the two style classes. They are different kinds of file (`ResourceDictionary` vs `Styles`) merged in different places, so a change made in the wrong one resolves to nothing. `IconCatalogTests.BothIconFiles_AreVisibleToTheseGuards` asserts the test harness reads both and that they differ.

`App.axaml` is **composition only** — 83 lines: data templates, converters, `FluentTheme`, the ColorPicker style include, the seven `ResourceInclude`s and the seven `StyleInclude`s. It defines no colour, no size and no style, and it must stay that way.

## How a view consumes it

1. **Name a role, never a value.** `{DynamicResource SurfacePanelBrush}`, never `#16191C`. A hex literal in any file outside `Themes/` is a bug and fails `ResourceReferenceTests.NoView_HardcodesAHexColour`. (The lighting picker's swatches are the one legitimate colour-from-data case and go through `HexColorToBrushConverter`, not through markup.)
2. **`DynamicResource`, not `StaticResource`, for anything theme-dependent.** The app follows the OS variant (`RequestedThemeVariant="Default"`), and a `StaticResource` snapshots one variant at load.
3. **Every colour role is a pair.** `<Role>Color` is a `Color`, `<Role>Brush` the `SolidColorBrush` built from it. Use the brush for `Background`/`BorderBrush`/`Foreground`; the `Color` half exists for `GradientStop`, animation, and for the Fluent keys that are typed `Color`.
4. **Every key exists in both variants.** Asserted, not assumed — see `TokenCompletenessTests`.
5. **Type = scale class + role class.** `Classes="meta muted"` composes the 11/400 sans step from `Themes/Typography.axaml` with the muted foreground from `Styles/Text.axaml`. The scale never sets a colour; the role never sets a size (except `.badge` and `.rowCaption`, which are single-purpose).

Two laws that decide which token you are allowed to reach for:

- **The mono law.** *"Mono is reserved for values that exist verbatim in the config file — tokens, delays, drive paths, counters. Sans for everything the app says in its own voice."* If the string is not something a user could find by opening the file on the v-Drive, it is sans.
- **The accent law.** The accent means **exactly two things**: *selection/focus*, and *"you changed this"*. It is not a decoration, not a brand colour, and not "the important one". `Button.primaryAction`, the selected layer pill, the selected tab underline, a modified key cap and the `.badge` tag are the sanctioned uses.

## Colour token registry — `Themes/Tokens.axaml`

44 roles (each a `Color`+`Brush` pair) plus 4 `BoxShadows` = 92 keys per variant.

### Surfaces — the elevation ramp

Dark climbs *away* from black as content comes forward; light climbs *toward* white. Light is not an inversion.

| Role | Dark | Light | Use |
|---|---|---|---|
| `SurfaceCanvas` | `#0F1214` | `#F4F5F6` | Window background |
| `SurfacePanel` | `#16191C` | `#FFFFFF` | Cards, toolbar, overlay cards, action fields |
| `SurfaceInset` | `#131619` | `#F4F5F6` | Tab-strip and inspector-rail backgrounds, the board, fields, disabled faces |
| `SurfaceBar` | `#1C2024` | `#FAFBFB` | Title strip, secondary buttons at rest, list rows |
| `SurfaceRaised` | `#23272C` | `#EEF0F1` | Key faces, active nav pill, button hover |
| `SurfaceLine` | `#2C3136` | `#DDE1E3` | Default borders and dividers |
| `SurfaceLineHigh` | `#3A4046` | `#C9CFD2` | Hover/emphasised borders, kbd chips |
| `SurfaceBorderRaised` | `#31363B` | `#C9CFD2` | Button and key borders |
| `SurfaceThumbnail` | `#262B30` | `#EEF0F1` | Device-thumbnail body |
| `SurfaceKeySelected` | `#2A2F35` | `#E4E7E9` | Selected/hovered key face, hovered row |

Light `SurfaceInset` deliberately equals `SurfaceCanvas`: an inset rail reads as inset against the *panel* beside it, not against the window.

### Text — six steps

| Role | Dark | Light |
|---|---|---|
| `TextPrimary` | `#E8EBED` | `#14181B` |
| `TextSecondary` | `#C6CCD1` | `#4A5158` |
| `TextBodyMuted` | `#98A1A8` | `#4A5158` |
| `TextMuted` | `#7B858C` | `#6C757C` |
| `TextFaint` | `#6C757C` | `#8A9298` |
| `TextDisabled` | `#545C62` | `#8A9298` |

Light collapses two pairs (`Secondary` == `BodyMuted`, `Faint` == `Disabled`) because the light ramp has less headroom. That is the design's own value table, not a transcription slip; all six keys stay so every call site resolves in both variants.

### Accent

| Role | Dark | Light | Use |
|---|---|---|---|
| `Accent` | `#5B9DF9` | `#5B9DF9` | Selection, focus, "you changed this" |
| `AccentText` | `#0F1214` | `#FFFFFF` | The colour that sits **on** an accent fill (not accent-coloured text) |
| `AccentLinkHover` | `#8FBCFB` | `#8FBCFB` | Link hover; dark accent-button hover fill |
| `AccentSelectionFill` | `#245B9DF9` | `#245B9DF9` | Translucent selection fill (14 % accent) |
| `AccentSelectedRing` | `#3E6FA8` | `#3E6FA8` | Selected-row inset ring; dark press / light hover accent fill |
| `AccentFocusHalo` | `#475B9DF9` | `#475B9DF9` | 28 % accent, the focus ring's spread colour |
| `AccentKeyHalo` | `#4D5B9DF9` | `#4D5B9DF9` | 30 % accent, the selected-keycap halo |

### Status — four states, fixed meanings

| Role | Dark | Light |
|---|---|---|
| `StatusOk` | `#4FBF8B` | `#35A26D` |
| `StatusOkText` | `#7ED3AC` | `#1D7A52` |
| `StatusOkTint` | `#1F4FBF8B` | `#1F4FBF8B` |
| `StatusOkTintBorder` | `#614FBF8B` | `#6135A26D` |
| `StatusAdvisory` | `#DDA94E` | `#DDA94E` |
| `StatusAdvisoryStrong` | `#C08A21` | `#C08A21` |
| `StatusAdvisoryText` | `#C9B48A` | `#7A5A11` |
| `StatusAdvisoryTint` | `#1FDDA94E` | `#1FDDA94E` |
| `StatusAdvisoryTintBorder` | `#61C08A21` | `#61C08A21` |
| `StatusError` | `#E4685E` | `#B0453C` |
| `StatusErrorText` | `#EE9C94` | `#B0453C` |
| `StatusErrorTint` | `#1FE4685E` | `#1FB0453C` |
| `StatusErrorTintBorder` | `#61E4685E` | `#61B0453C` |
| `StatusDemo` | `#B58CF6` | `#7A4FD0` |
| `StatusDemoTint` | `#1FB58CF6` | `#1F7A4FD0` |
| `StatusDemoTintBorder` | `#61B58CF6` | `#617A4FD0` |

### Badge hues — variant-independent

They sit on a key cap, not on the window, so they do not flip.

| Role | Value | Use |
|---|---|---|
| `BadgeMacro` | `#C77DD8` | Macro dot, top-right of a cap (5 px) |
| `BadgeTapHold` | `#35A26D` | Tap-and-hold right-triangle, bottom-right (~6 px) |
| `BadgeLightingPreview` | `#57C4D8` | Lighting-preview cyan |
| `BadgeLed` | `#B58CF6` | LED purple |

`BadgeLed` and dark `StatusDemo` are the same colour under two role names, deliberately: neither call site should have to know about the other. They part company on light, where the *status* hue darkens for contrast and the badge does not.

Remaining keycap badge geometry the design specifies (not yet drawn — see "Known gaps"): remapped = 2 px accent bar on the bottom edge; advisory = 12×3 px `StatusAdvisoryStrong` rounded bar top-right; locked = 45° hatched fill + dashed border.

### Elevation and scrim

| Role | Value (both variants) | Use |
|---|---|---|
| `Scrim` | `#9E080A0C` | Modal scrim, `rgba(8,10,12,0.62)` |
| `ShadowPopover` | `0 8 24 0 #80000000` | `Border.overlayCard` |
| `ShadowModal` | `0 24 60 0 #99000000` | `Border.overlayCard.modalCard` |
| `ShadowFocusHalo` | `0 0 0 3 #475B9DF9` | Focus ring — spread only, never an offset outline |
| `ShadowKeyHalo` | `0 0 0 2 #4D5B9DF9` | Selected key cap |

**Flat elevation is a 1 px `SurfaceLine` border and no shadow at all.** Only a surface that genuinely floats casts one, and then it casts `ShadowPopover` or `ShadowModal` — never an invented shadow. The shadows are the same in both variants: dimming a light window uses the same near-black at the same alpha.

## The four-status vocabulary

`ViewModels/StatusSeverity` is the single enum; views map it to classes through `EnumMatchConverter`. View models never expose brushes.

| `StatusSeverity` | Hue | Means, and only this | Chip class | Text class |
|---|---|---|---|---|
| `Ok` | green | Connected and writable | `.ok` | `.statusOk` |
| `Error` | red | Gone or unwritable | `.error` | `.statusError` |
| `Demo` | purple | Nothing is written — demo mode | `.demo` | `.statusDemo` |
| `Warning` | amber | An advisory: over a limit. Never blocks. | `.warning` | `.statusWarning` |
| `Unknown` | — | Nothing known yet; renders unfilled | (none) | (none) |

- **Amber is the only warning colour.** An advisory is never red. Red is reserved for genuine failure — and for the macro Record dot.
- **Demo mode is not a warning.** It is a different thing the app is doing, not a degraded version of working, which is why it gets its own hue and its own enum member rather than borrowing amber.
- The three `…Text` roles exist because text on a tint needs to be a step further from the tint than the fill colour is. Demo has no `StatusDemoText` — the design source names none — so it labels its chip with the fill colour. Measured: `StatusDemo` on `StatusDemoTint` is 5.1–6.1:1 on dark and **4.2–4.6:1 on light**, depending on the surface behind the chip, where the three roles that do have a text token reach 4.7–5.9:1. The light end sits on the AA boundary rather than clear of it; adding a `StatusDemoText` is the fix if demo mode ever moves onto a lighter surface.

## Geometry — `Themes/Geometry.axaml`

Everything on a 4 px grid. Theme-independent, so declared outside `ThemeDictionaries` and resolvable under either variant.

| Key(s) | Value | Notes |
|---|---|---|
| `RadiusPanel` / `RadiusControl` / `RadiusKeycap` / `RadiusChip` / `RadiusPill` | 8 / 5 / 4 / 3 / 999 | Panels & cards / buttons & fields / key caps / kbd chips / pills |
| `PaddingCard` | `14` | Cards, overlay cards, entry panels |
| `PaddingInspectorSection` | `12` | Inspector sections, the board's inner padding |
| `PaddingButton` | `13,8` | Buttons, action fields, zone/mode/colour-slot buttons |
| `PaddingTab` | `13,0` | Tab strip |
| `Space4` … `Space32` | 4 / 8 / 12 / 16 / 24 / 32 | The whole spacing scale. Nothing may use a gap outside it. |
| `HeightToolbar` / `HeightTabBar` / `HeightAdvisoryStrip` | 46 / 38 / 30 | Fixed: a long device name must not push the board down |
| `WidthInspectorRail` / `WidthInspectorRailWide` | 268 / 300 | The wide one is the macro-editing variant |
| `GutterSplit` | 26 | Between the two half-panels of a split board |
| `CardGridGap` / `WidthCardStatusRail` | 12 / 2 | Dashboard card grid and its flush-left status rail |
| `IconSize` / `IconStrokeThickness` | 16 / 1.5 | The icon law's grid and pen — see "Icons and device art". Never raster |
| `IconSizeDialog` | 24 | A dialog's type mark, drawn larger than a rail icon: it is the one thing that says what kind of interruption this is. The only sanctioned departure from `IconSize` |
| `SpinnerSize` / `SpinnerStrokeThickness` | 14 / 1.5 | One quadrant transparent, 1.1 s linear spin |
| `HatchPitch` / `HatchAngle` | 4 / 45 | Locked keys and LED-off fills |

**Avalonia's two-value `Thickness` is `horizontal,vertical`** — the reverse of the CSS shorthand the handoff is written in. The handoff's "buttons 8×13" is therefore `13,8` here.

Window size (1000×680 default, 720×480 minimum) lives in `MainWindow.axaml`, not here; `ThemedHost` in the tests mirrors it.

## Typography — `Themes/Typography.axaml`

This file is a **`<Styles>`, not a `<ResourceDictionary>`** — a resource dictionary cannot host `<Style>` elements. It is therefore included under `Application.Styles`; its `<Styles.Resources>` still resolve app-wide.

Families (both under SIL OFL, embedded — see "Fonts"):

- `FontSans` → `avares://KinesisEdit/Assets/Fonts#IBM Plex Sans` + system fallbacks
- `FontMono` → `avares://KinesisEdit/Assets/Fonts#IBM Plex Mono` + system fallbacks

`:is(Window)` sets `FontSans`, so the whole tree inherits sans and only a mono style opts out.

| Class | Resource | Size/weight | Use |
|---|---|---|---|
| `.deviceHeadline` | `FontSizeDeviceHeadline` | 24/600 sans | Device name on an editor's own header |
| `.pageTitle` | `FontSizePageTitle` | 18/600 sans | One per screen |
| `.cardTitle` | `FontSizeCardTitle` | 15/500 sans | Card and inspector-section titles |
| `.modalTitle` | `FontSizeModalTitle` | 14/500 sans | Overlay panel titles |
| `.toolbarDevice` | `FontSizeToolbarDevice` | 13/600 sans | Device name in the toolbar |
| `.control` | `FontSizeControl` | 12/500 sans | Buttons, tabs, nav, counters |
| `.modalBody` | `FontSizeModalBody` | 12/400 sans | Modal prose (`LineHeightModalBody` 18) |
| `.body` | `FontSizeBody` | 11/400 sans | Prose (`LineHeightBody` 16.5) |
| `.meta` | `FontSizeMeta` | 11/400 sans | Single-line captions and secondary lines |
| `.pill` | `FontSizeMeta` | 11/500 sans | The label inside a status pill |
| `.monoValue` | `FontSizeMonoValue` | 11/400 mono | A value that exists verbatim in a config file |
| `.monoValueSmall` | `FontSizeMonoValueSmall` | 10/400 mono | The same, somewhere tighter |
| `.sectionLabel` | `FontSizeSectionLabel` | 10/600 mono, +1.2 px tracking | Caption over a group of controls |
| `.logoMark` | `FontSizeLogoMark` | 10/700 mono | The app mark's "K" in the app bar, and nothing else |
| `.keycapLabel` | `FontSizeKeycapLabel` | 9/400 sans | Key legend at mock scale — **defined but unused, see "Known gaps"** |

`TextBox.monoValue` repeats the family/size setters, because a `TextBox` is not a `TextBlock` and the `:is(TextBlock)` selectors cannot reach it.

Two implementation notes that are not free choices:

- **No style sets `LineHeight` to the font size.** The design's "line-height 1" is the CSS meaning — *add no leading*, glyphs may overflow — but Avalonia clips to the line box, so a literal `LineHeight == FontSize` cuts the descenders off every label in the app. Single-line styles set no `LineHeight` at all and take the font's own metrics (Plex: 1.3, no line gap), which is the honest equivalent. Only the two prose styles set it.
- **`LetterSpacing` is in pixels, not em.** The one tracking rule in the design (0.10–0.14 em on the 10 px uppercase section labels) is pinned to 0.12 em → `LetterSpacingSectionLabel` = 1.2. Avalonia has no text-transform either, so section-label text is authored uppercase at the call site.

`FontSizeLogoMark` (10) is not a step of the scale: it is the "K" of the app mark, alone in the app at weight 700 and alone in being mono type that is **not** a value out of a config file. The handoff grants the weight to exactly this glyph (`handoff.md:86`, "700 only for the tiny logo 'K'") and draws it at 10 px inside an 18 px accent tile (`Border.logoTile`). It stays type rather than becoming a geometry — a wordmark is not one of the three icon families — and it does not borrow `FontSizeMonoValueSmall`, which means "a config value in a tight place".

## Icons and device art — `Themes/Icons.axaml`, `DeviceArt.axaml`, `LightingMarks.axaml`, `Controls/Icon.cs`

The law, verbatim (`handoff.md:162`): **"16px grid, 1.5px stroke, square caps, geometry only."** Nothing in the app is a raster icon, an icon font or a Latin-1 character standing in for one.

### The three families

| Dictionary | Keys | Naming law | Enumerated by |
|---|---|---|---|
| `Themes/Icons.axaml` | 10 | `Icon<Name>` | Nothing — the set is fixed and listed in `IconCatalogTests` |
| `Themes/DeviceArt.axaml` | 7 | `DeviceArt<DeviceId>` | `DeviceCatalog.All.Where(IsProgrammable)` |
| `Themes/LightingMarks.axaml` | 16 | `LightingMark<LightingMode>` | every `LightingMode` member |

All three are plain `ResourceDictionary`s with **no `ThemeDictionaries`** — a shape does not change when the OS flips — merged in `App.axaml` after `Motion.axaml` and before `Controls.axaml`. **They carry no colour and must never carry one**; `IconCatalogTests.NoMarkDictionary_DeclaresAColour` scans the markup for hex and named colours. The call site names the token brush.

- **`Themes/Icons.axaml`** — `IconConnected` (the ring) + `IconConnectedCore` (the dot: two `Icon`s in one cell, because one geometry cannot be both stroked and filled), `IconNotDetected` (the same ring, dashed **by the pen**), `IconCannotAccess`, `IconWarning`, `IconError`, `IconInformation`, `IconConfirmation`, `IconEject` (fill), `IconScanning`. The seven ring marks are built on the same circle — centre (8,8), radius 7 — and `IconEject` is centred on it too, so a chip's mark sits on the same baseline whichever state it is in. The circle is written out per key rather than shared through an alias, so one mark can diverge later without disturbing the others. `IconEject` and `IconConnectedCore` are the only fill-only marks. `IconScanning` is the one mark **not** on the 16 px grid: it is authored in a **14×14** box centred on (7,7), because `SpinnerSize` is 14 and the rotation has to turn about the arc's own centre.
- **`Themes/DeviceArt.axaml`** — one plan-view drawing per programmable board, every one authored in the same **92×56** box with the board centred in it, so in those coordinates the seven are at true proportion *to each other* (`IconGridTests.EveryDeviceArt_IsCentredInTheSharedBox`). Each path opens with `F0` (EvenOdd), so the key rects punch out of the body when the drawing is filled. The two catalog entries that are not programmable — the never-shipped CROSSFIRE keypad and the web-configured Advantage 360 Professional — deliberately have **no art**, and the frame that would hold it is hidden rather than drawn empty.
- **`Themes/LightingMarks.axaml`** — one mark per mode, drawing the *motion* rather than the name. Three are **filled** (`Freestyle`, `Monochrome`, `Breathe`); the other 13 are **stroked**. `LightingModeMarkConverter.IsFilled` is the single source of that split.

### The authoring-box rule — why `Icon` fits the box, not the ink

`Icon` scales its geometry by its `SourceBox` (the coordinate box the mark was drawn in), **never** by `Geometry.Bounds`. This is the whole point of having a grid:

- The marks are deliberately inset from their box by different amounts (`IconConnectedCore` is 5.6 units wide; the rings span 1–15). Fitting each one's ink would blow every mark out to the same extent and destroy both the shared centre and the relative weights the set was drawn with.
- `IconScanning`'s ink is lop-sided on purpose — the right quadrant is missing — so ink-fitting would re-centre the arc and the spinner would **wobble** as it turned. `IconGridTests.TheScanningMark_IsLopsided_WhichIsWhyEveryMarkDeclaresItsBox` asserts that lop-sidedness so the rule stays load-bearing.
- A pedal and a contoured board share one 92×56 box; fitting each to its own ink would make them the same width.

Fitting is **uniform and centred**, so a non-square `SourceBox` is letterboxed rather than stretched, and `MeasureOverride` reports the letterboxed rectangle rather than a square with dead margins. A `SourceBox` with no extent falls back to the geometry's bounds — the deliberate escape hatch for a mark with no box of its own.

**The pen does not scale with the geometry.** `StrokeThickness` is a thickness at the *rendered* size: the pen is built at `StrokeThickness / scale` inside the fit transform, so 1.5 measures 1.5 whether the mark is drawn at 16 px or at 92. `StrokeDashArray` inherits that for free, because Avalonia's dash lengths are multiples of the pen width.

### The `Icon` API

`KinesisEdit.Controls.Icon`, a leaf `Control` — no template, no children, no motion of its own.

| Property | Default | Notes |
|---|---|---|
| `Data` | null | The `Geometry`. Null draws nothing and measures zero. |
| `Stroke` | null | Outline brush. Bind with `{DynamicResource}`. |
| `Fill` | null | Fill brush. |
| `StrokeThickness` | 1.5 (`Icon.DefaultStrokeThickness`) | Device-independent at the rendered size. |
| `Size` | 16 (`Icon.DefaultSize`) | Edge of the square the box is fitted into. |
| `SourceBox` | `0,0,16,16` | The authoring box. |
| `StrokeDashArray` | null | In multiples of `StrokeThickness`. |

Square caps and mitre joins are **fixed by the control** (`Icon.StrokeLineCap` / `StrokeLineJoin`) — the law, not a call-site choice. Several marks (the dots of `!`, `i`, `?`) are 0.6-long segments that rely on the square cap to read as dots at all.

`Size`/`StrokeThickness` defaults are written out in C# *and* in `Themes/Geometry.axaml`, because a styled property's default is resolved before any resource dictionary is in scope. `IconRenderTests.TheControlsDefaults_AreTheGeometryTokens` is the only thing stopping the two from drifting.

**An `Icon` with neither brush draws nothing, deliberately.** There is no base `Icon` style and no default colour: the design names no default icon colour, and inventing one would hide a call site that forgot to name a role.

### Call-site patterns

A stroke mark:

```xml
<controls:Icon Data="{DynamicResource IconCannotAccess}"
               Stroke="{DynamicResource StatusErrorTextBrush}" />
```

A fill mark:

```xml
<controls:Icon Data="{DynamicResource IconEject}"
               Fill="{DynamicResource TextSecondaryBrush}" />
```

The connected mark is **two `Icon`s in one cell** — one geometry cannot be a stroked ring *and* a filled dot:

```xml
<Panel Width="{DynamicResource IconSize}" Height="{DynamicResource IconSize}">
  <controls:Icon Data="{DynamicResource IconConnected}"     Stroke="{DynamicResource StatusOkTextBrush}" />
  <controls:Icon Data="{DynamicResource IconConnectedCore}" Fill="{DynamicResource StatusOkTextBrush}" />
</Panel>
```

Marks are drawn at `IconSize` everywhere except a dialog's type mark, which is drawn at `IconSizeDialog` (24) — `MessageBoxWindow` sets it on both the cell and each `Icon`. Do not reach for an arbitrary size: a mark that is neither 16 nor 24 is a design decision that belongs in this doc first.

The dashed ring — the dash is a **pen** property, so "not detected" is the plain circle plus a dash array, not a second geometry:

```xml
<controls:Icon Data="{DynamicResource IconNotDetected}"
               Stroke="{DynamicResource StatusErrorTextBrush}"
               StrokeDashArray="3,2" />
```

The spinner — the class carries the geometry, the 14×14 box and the rotation, so only the colour is left to name:

```xml
<controls:Icon Classes="spinner" Stroke="{DynamicResource AccentBrush}" />
```

Device art — the class carries the shared box; `Size` stays at the call site because the same drawing is used at two sizes:

```xml
<controls:Icon Classes="deviceArt" Size="92"
               Data="{Binding DeviceId, Converter={StaticResource DeviceArt}}"
               Fill="{DynamicResource SurfaceThumbnailBrush}" />
```

**A fixed set of states is enumerated in markup, not resolved by a converter.** Put every state's `Icon` in one `Panel` and toggle `IsVisible` through `EnumMatchConverter`: the cell then has a fixed width whichever state is showing, and each state names its own token — which a converter cannot do, because it can only swap the geometry, not the pen or the brush.

### Enum → geometry, and the guard blind spot it creates

Two families are driven by *data* rather than by a fixed set of states, so they go through converters (registered in `App.axaml` as `DeviceArt` and `LightingModeMark`), both resolving through `GeometryResources.Find`, which returns null for a missing key rather than throwing:

- **`DeviceArtConverter`** — `DeviceId` → `DeviceArt<Id>`. The mapping is a `switch` spelled out per member, so a device added to the catalog without a drawing is a visible gap here rather than a key that silently fails to resolve.
- **`LightingModeMarkConverter`** — `LightingMode` → `LightingMark<Mode>`, with a **`ConverterParameter` of `"Stroke"` or `"Fill"`**. It answers only the half the mode is drawn in and returns null for the other, so the call site puts two `Icon`s in one cell and exactly one of them draws. Stroking *and* filling would turn every ring in the set into a disc.

`MessageBoxIcon` is **not** converted — its four marks differ in colour as well as in shape, so `MessageBoxWindow` enumerates them in markup.

> **Blind spot:** a resource key looked up from C# is invisible to `ResourceReferenceTests` / `AuthoredXaml`, which only read markup. Nothing in the XAML names `DeviceArtAdvantage360` or `LightingMarkRipple`; a typo in a converter's key, or a mark deleted from a dictionary, would resolve to null at runtime and simply draw nothing. **`IconCoverageTests` is what guards those keys instead** — it enumerates the domain (the device catalog, the `LightingMode` enum, `MessageBoxIcon`) and resolves each key through the live application, in both directions: every member must have a mark, and every mark must name a member.

### Adding a new mark

1. Author the geometry in the right dictionary, inside a 16×16 box (or the family's box), origin top-left, y down. Keep the *painted* result — stroke included — inside the box.
2. Name it `Icon…` / `DeviceArt<DeviceId>` / `LightingMark<Mode>`; the prefix is enforced (`EveryDeclaredMark_CarriesItsFamilysPrefix`).
3. No colour, ever.
4. Draw it at the call site with an `Icon`, naming a token brush.

What fails if you skip a step: a mark whose paint leaves its box → `IconGridTests.EveryMark_PaintsInsideItsAuthoredBox` / `…KeepsItsStrokeInsideItsBoxToo`; a mark authored at the wrong scale or emptied → `EveryMark_FillsEnoughOfItsBoxToRead`; a duplicate of an existing shape → `NoTwoMarks_ShareTheSamePathData_ExceptTheNamedPairs`; a device or lighting mode without a mark, or a mark naming neither → `IconCoverageTests`; a dictionary written but never merged into `App.axaml` → `EveryMarkDictionary_IsMergedIntoTheApplicationsResources` (Avalonia does not complain about a `{DynamicResource}` naming a key nobody defined — it draws an icon-shaped hole).

**Two pairs are deliberately the same path data** and are named exemptions in `IconGridTests._sharedShapes`, each with its reason: `IconCannotAccess` == `IconWarning` (both a `!` in a circle, separated by colour — red for failure, amber for advisory) and `IconConnected` == `IconNotDetected` (the shared ring, separated by the pen). A mirror test, `TheDeliberatelySharedShapes_AreStillShared`, fails if either pair diverges, so a stale exemption cannot hide the next real collision.

### The spinner

`Styles/Icons.axaml` `.spinner`: `IconScanning` in its own 14×14 `SourceBox`, `Size` = `SpinnerSize` (14), `StrokeThickness` = `SpinnerStrokeThickness` (1.5), `RenderTransformOrigin` at the centre, and a `Style.Animations` loop rotating 0→360° over **1.1 s, `LinearEasing`, `IterationCount="Infinite"`**.

- The duration is **written out, not bound** — an `Animation` is not part of the logical tree, so a `DynamicResource` on it resolves to nothing and silently yields a zero-length loop. This is the same rule the recording pulse follows. `DurationSpinnerRotation` in `Themes/Motion.axaml` is the budget's record of the number, and `SpinnerMotionTests` is what keeps the two from drifting apart.
- It has **no `…Full`/`…Reduced` pair and no `MotionResourceBinder` alias**: it is a loop, not a property change to interpolate.
- **It keeps spinning under reduce-motion, deliberately.** The spinner is not decoration — it is the only affordance saying the app is still working, and a frozen one reads as a hung app, which is a worse outcome for the very user reduce-motion exists to protect. The reduced budget drops rises, slides and height changes; a status affordance that carries information is not in that category. `SpinnerMotionTests.TheSpinner_UnderReduceMotion_KeepsSpinning` pins that.

### Where the marks are used

| View | Marks |
|---|---|
| `MainWindow` | The logo tile + wordmark; the status chip's `IconConnected`+`Core` / `IconWarning` / `IconCannotAccess` |
| `DeviceCardView` | The 92×56 art frame (`Border.deviceArtFrame`, hidden when the device has no art); the status chip's state mark; `IconEject` beside the Eject caption |
| `NoDeviceView` | The seven silhouettes in the device picker, stroked at `Size="33"` / `StrokeThickness="0.6"` |
| `LoadingView` | `.spinner` (it replaced an indeterminate `ProgressBar`) |
| `MessageBoxWindow` | `IconInformation` / `IconConfirmation` / `IconWarning` / `IconError` |
| `LightingTabView` | The mode rail's marks, through `Icon.modeMark` (+`.filled`, +`.selected`) in `Styles/Editor.axaml` |

An `Icon` paints from a brush property, not from `TextElement.Foreground`, so it cannot inherit a row's colour the way a caption does — `Icon.modeMark` is where the rail's rest/selected pair is spelled instead of at the call site.

## The style layer — `Styles/`

Split by concern rather than one dumping ground. Order in `App.axaml` matters only between `Typography` and these: a role style may override a scale step, never the reverse.

| File | Classes |
|---|---|
| `Text.axaml` | `.muted`, `.sectionLabel` (colour half), `.badge`, `.rowCaption`, `TextBox.monoValue`, `.statusOk/.statusWarning/.statusError/.statusDemo`, `.logoMark` (colour half) |
| `Surfaces.axaml` | `:is(Window)`, `Border.card`, `Border.deviceCard`, `Border.deviceArtFrame`, `Border.logoTile`, `Border.overlayCard` (+`.modalCard`), `Border.overlayScrim` (+`.open`), `ContentControl.overlayHost` (+`.open`), `ContentControl.popover` (+`.open`), `FlyoutPresenter`, `MenuFlyoutPresenter`, `Border.toast` (+`.shown`), `Border.statusChip` (+`.ok/.warning/.error/.demo`), `Border.listFrame`, `Border.listRow`, `Border.entryPanel`, `Border.entryBox` |
| `Buttons.axaml` | `Button.primaryAction`, `Button.rowButton` (+`.selected`), `Button.zoneButton`, `ListBoxItem`, `ProgressBar` |
| `Keyboard.axaml` | `Border.keyboardBoard`, `Button.keyCap` (+`.modified/.locked/.selected/.listening`), `.keyCapText` (+`.modified/.locked`), the recording pulse, `Button.layerTab` (+`.selected`) |
| `Editor.axaml` | `Button.editorTab` (+`.selected`), `Border.actionField` (+`.armed`), `Button.modeOption` (+`.selected`), `Icon.modeMark` (+`.filled`, +`.selected`), `Button.colorSwatch`, `Border.colorSwatchFill`, `Button.colorSlot` (+`.selected`) |
| `Icons.axaml` | `Icon.deviceArt` (the 92×56 box), `Icon.spinner` (the 14×14 box + the rotation) |

Four conventions that recur and are easy to get wrong:

1. **`:is(TextBlock)`, never `TextBlock`.** An Avalonia type selector matches the exact type; several of these roles render on `SelectableTextBlock` (unparsed file lines, the pedal entry box), which a bare selector silently skips.
2. **Paint a button through `/template/ ContentPresenter#PART_ContentPresenter`.** Fluent's `:pointerover`/`:pressed`/`:disabled` setters target that presenter, and a `Background` set on the `Button` itself loses to them.
3. **Declare states in increasing precedence.** Later styles win, so a listening key always reads as listening whatever else it also is.
4. **Animate through a class, not through `IsVisible`.** `IsVisible="False"` collapses a control instantly and gives a transition nothing to run on; `.open`/`.shown` re-evaluates the `Opacity` setter and the transition plays. `Border.listRow`'s separator is a *top* border on every row but the first (`ContentPresenter:nth-child(1) > Border.listRow` clears it) — a bottom border would leave a trailing hairline under the last row.

## The Fluent control-alias layer — `Themes/Controls.axaml`

**What it does.** The app classes what it draws itself, but a plain `Button`, `ComboBox`, `TextBox`, `CheckBox`, `RadioButton`, `Slider`, `ToggleSwitch`, `Expander`, `ScrollBar`, flyout, menu or tooltip is drawn by `FluentTheme` from *Fluent's* palette — a different family of greys and a different blue. The result reads as two design systems in one window. Fluent's control themes do not hardcode those colours: every one is a `{DynamicResource}` against a named key (`ButtonBackground`, `ComboBoxBorderBrush`, `SliderTrackFill`, …) declared in the theme's own resources. This file redeclares those keys against our roles.

**Why it must live in `Application.Resources`.** Avalonia consults `Application.Resources` **before** `Application.Styles`, which is where `FluentTheme`'s originals live. Move this file into `Styles/` and Fluent wins every lookup — the aliases resolve, nothing throws, and the controls simply stay grey. That is exactly the silent failure mode the whole file exists to close.

**Three rules it follows.**

- It is an *alias map*, not a palette: every entry is `<StaticResource x:Key="FluentKey" ResourceKey="Role…" />`. There is no colour value anywhere in it, so a role changes in one place. `ControlPaletteTests.EveryControlKey_PaintsADesignToken` enforces this.
- It is scoped to the control types the app actually puts on screen; re-skinning all of Fluent would be a much larger surface with no user in it.
- Both variants carry the same keys (`DarkAndLight_RedeclareTheSameControlKeys`).

**The neutral ramp**, shared by everything that reads as a surface:

| State | Face | Border | Text |
|---|---|---|---|
| rest | `SurfaceBar` | `SurfaceBorderRaised` | `TextPrimary` |
| pointerover | `SurfaceRaised` | `SurfaceLineHigh` | `TextPrimary` |
| pressed | `SurfaceLine` | `SurfaceLineHigh` | `TextPrimary` |
| disabled | `SurfaceInset` | `SurfaceLine` | `TextDisabled` |

A *field* (`TextBox`, `ComboBox`) rests on `SurfaceInset` instead — it is a hole in the panel, not a thing on top of it — and takes the accent on its border when focused.

**The accent ramp is the one place the variants diverge.** `AccentText` flips (near-black in dark, white in light), so a state that brightens the fill is legible in dark and illegible in light. Each variant therefore moves the fill *away* from its own label colour: dark brightens on hover (`AccentLinkHover`) and deepens on press (`AccentSelectedRing`); light deepens on hover (`AccentSelectedRing`) and lifts back to base accent on press. Disabled leaves the accent family entirely (`SurfaceInset` + `TextDisabled`) — a Save with nothing to save must not look like a Save that works.

**Two shape aliases** sit outside the variant dictionaries because a radius does not change with the theme: `ControlCornerRadius` → `RadiusControl`, `OverlayCornerRadius` → `RadiusPanel`.

**Extending it for a control type not yet covered:**

1. Find the keys the control theme actually reads — `Avalonia.Themes.Fluent`'s `Controls/<Type>.xaml`, or Avalonia DevTools on a live instance.
2. Add `<StaticResource x:Key="<FluentKey>" ResourceKey="<Role>Brush" />` to **both** variant dictionaries. Never a literal.
3. **Check the type.** Fluent types a scatter of keys as `Color` rather than `SolidColorBrush` (`ExpanderHeaderBackground`, `ScrollBarThumbBackgroundColor`, `SystemAccentColor`, …). Aliasing one of those to a `…Brush` role leaves the control drawing *nothing*. Point it at the `…Color` half and add it to `ControlPaletteTests.ColorAliases`.
4. Add one representative key to `ControlPaletteTests.SharedAliases` (or `ThePaletteCovers_EveryControlTypeTheAppPutsOnScreen`) so the family cannot be dropped wholesale later.

Fluent exposes no key for a few things; those are set as ordinary styles in `Styles/Buttons.axaml` instead — `ListBoxItem` padding, `ProgressBar`'s bar and trough. `ListBoxItem` selection/hover comes from the shared `SystemControlHighlightList*` keys, which is why those are mapped.

## Motion budget — `Themes/Motion.axaml`

A budget, not a suggestion: nothing may animate longer than the table says, and **nothing may animate that is not in the table**.

| Motion | Duration key | Full set | Reduced set |
|---|---|---|---|
| Status cross-fade | `DurationStatusCrossFade` 220 ms | Background + BorderBrush `BrushTransition` | identical (a fade survives) |
| List insert | `DurationListInsert` 160 ms `CubicEaseOut` | `Height` `DoubleTransition` | **empty** |
| **Tab / layer swap** | `DurationTabSwap` **0 ms** | *(none — see below)* | *(none)* |
| Popover in | `DurationPopoverIn` 120 ms | `Opacity` + `RenderTransform` (2 px rise) | `Opacity` only |
| Modal in | `DurationModalIn` 140 ms | `Opacity` | `Opacity` |
| Scrim | `DurationModalIn` 140 ms | `Opacity` | `Opacity` |
| Toast in/out | `DurationToast` 180 ms, dwell `DurationToastDwell` 5 s | `Opacity` + `RenderTransform` | `Opacity` only |
| Recording pulse | `DurationRecordingPulse` 1.4 s `SineEaseInOut`, infinite | a `Style.Animations` opacity loop | same (a slow opacity cycle *is* a fade) |
| Spinner rotation | `DurationSpinnerRotation` 1.1 s `LinearEasing`, infinite | a `Style.Animations` rotation loop | **same — deliberate, see "Icons and device art"** |

### Why the tab/layer swap is zero, verbatim

> "Layer switching is deliberately unanimated — the spec says it will be used constantly, and 200 ms × 200 switches is the difference between an instrument and a website."

Three consequences:

1. **`DurationTabSwap` exists only so the zero is documented.** There is no `TabSwapTransitions` resource and there must never be one — a zero-duration transition still costs a frame of bookkeeping per switch. `MotionBudgetTests.NoTabSwapTransitionsResource_Exists` asserts the absence.
2. **Declaring no `Transitions` is not the same as having none.** Fluent's `Button` control theme ships a **75 ms `RenderTransform` press animation** that every `Button` in the app inherits — including the layer pills and the section tabs. `Button.layerTab` and `Button.editorTab` therefore set `Transitions="{x:Null}"` explicitly. Without that the layer switch animates after all, and because it is a *movement* it would run under reduce-motion too.
3. **No container in the app owns a page transition.** Neither the section tabs nor the layer switch is a `TabControl`: both are `Button`s in an `ItemsControl` over a `Panel` whose children toggle `IsVisible`. Avalonia 11's `TabControl` has no `PageTransition` property either; `Carousel` is the only container that does and the app uses none. `MotionBudgetTests.NoAuthoredMarkup_DeclaresAPageTransition` keeps it that way.

### Reduce-motion, and how a style consumes it

Motion.axaml declares each motion **twice** — `<Name>TransitionsFull` and `<Name>TransitionsReduced` — and never declares the bare `<Name>Transitions` key. Views and styles bind only the bare alias:

```xml
<Setter Property="Transitions" Value="{DynamicResource PopoverTransitions}" />
```

`MotionResourceBinder.Apply(application, motionSettings)` writes that bare key into `Application.Resources` itself, aliased to the Full or Reduced set. An entry a resource dictionary owns outranks anything in its merged dictionaries, so the alias always wins; and because the consumers bind *dynamically*, calling `Apply` again re-points every one of them live. The six aliases are listed in `MotionResourceBinder.Aliases` — adding a motion means adding both resources to `Motion.axaml` **and** one row there.

Detection:

- `IMotionSettings.ReduceMotion` is the single switch. `MotionSettings` asks an `IReduceMotionDetector` **once, in its constructor**, and remembers the answer; nothing re-reads the OS while the app runs.
- `ReduceMotionDetector.CreateForCurrentPlatform()` returns `MacOsReduceMotionDetector` on macOS (the primary platform) and `UnsupportedReduceMotionDetector` — always "don't know" — elsewhere. Avalonia 11.3 exposes no reduce-motion API of its own.
- The macOS reader shells out to `defaults read com.apple.universalaccess reduceMotion` through Core's `IProcessRunner`, mirroring `VDriveEject.CreateForCurrentPlatform`. A non-zero exit means the user never touched the switch, which is *not* a failure — it reports "unknown".
- Unknown, and any exception from the detector, resolve to **motion on**. An accessibility lookup is never allowed to matter more than starting up.
- `App.OnFrameworkInitializationCompleted` resolves it and calls `MotionResourceBinder.Apply` **before any window exists**, because the alias resources are what the views' transitions bind to. The `ReduceMotion` setter is public so a later preference screen or another platform's detector can flip it — re-run `Apply` afterwards, the aliases are not recomputed on their own.

The reduced set is **fades only: no rise, no slide, no height animation**. `ListInsertTransitionsReduced` is an *empty* `Transitions` rather than a missing resource — assigning empty is how a control ends up with no animation while the alias stays resolvable; dropping the resource would leave the setter unset.

**Two motions are not transitions**: the recording pulse (a `Style.Animations` opacity loop on `Button.keyCap.listening, :is(TextBlock).recording`) and the spinner's rotation (`Icon.spinner`, `Styles/Icons.axaml`). Both durations are **written out rather than bound**, because an `Animation` is not part of the logical tree and a `DynamicResource` on it would resolve to nothing and silently give a zero-length loop; `DurationRecordingPulse` and `DurationSpinnerRotation` are the budget's record of the two numbers. Neither has a `…Full`/`…Reduced` pair or a `MotionResourceBinder` alias — there is no property change to interpolate — and neither is stopped under reduce-motion: a slow opacity cycle *is* a fade, and a frozen spinner reads as a hung app.

## Fonts

IBM Plex Sans 400/500/600/700 and IBM Plex Mono 400/500/600 are vendored under `src/KinesisEdit/Assets/Fonts/` and shipped as `<AvaloniaResource Include="Assets/Fonts/**" />`, so the app never depends on what the machine has installed. `LICENSE.txt` (SIL Open Font License 1.1, which permits embedding) travels with them.

`Avalonia.Fonts.Inter` and `.WithInterFont()` are **removed** — `Program.BuildAvaloniaApp` registers no font package at all. Weight 700 is carried only for the tiny logo "K"; the scale itself uses 400/500/600.

**The name-ID-16 gotcha.** IBM Plex's TTFs carry legacy family names — "IBM Plex Sans Medm", "IBM Plex Sans SmBld" — in name ID 1, which would fragment the family into one entry per weight and make `FontWeight="SemiBold"` resolve to nothing. Avalonia's embedded font collection groups faces by the **typographic** family name (name ID 16), which is "IBM Plex Sans" for every weight, so a single `avares://…/Assets/Fonts#IBM Plex Sans` family key carries them all. `ShapeAndTypeTokenTests.PlexSans_CarriesEveryWeightTheScaleUses` asserts Normal/Medium/SemiBold each resolve to a real glyph typeface, and `Family_LoadsRatherThanFallingBackToASystemFont` asserts the embedded collection registered at all rather than silently falling back to the OS default.

---

# Testing UI work in this repo

There *is* automated coverage for the UI now, it runs with **no display**, and it is where every design-system regression is caught. Use it; do not re-derive it.

## The harness

`src/KinesisEdit.Tests` references `Avalonia.Headless.XUnit` and `Avalonia.Skia`, both pinned to the app's Avalonia version (11.3.12 — a headless platform out of step with the framework it boots will not load).

`Headless/TestAppBuilder.cs` carries the assembly attribute:

```csharp
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

public static AppBuilder BuildAvaloniaApp() =>
    AppBuilder.Configure<App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
```

- It boots the **real `App`** — its `App.axaml`, and therefore the actual theme dictionaries, style includes, embedded fonts and `ViewLocator`. A test exercises the composition the app ships, not a stub that merges the same files. `App.OnFrameworkInitializationCompleted` builds no window here (it only wires the shell under `IClassicDesktopStyleApplicationLifetime`), but it *does* run `MotionResourceBinder.Apply`, so the motion aliases are the app's own.
- Only the platform underneath differs: `AvaloniaHeadlessPlatform` replaces the windowing system. `dotnet test src/KinesisEdit.sln` therefore runs the whole UI suite on a machine with no display and no window server — which is what CI is. `HeadlessHarnessTests.TheSession_RunsOnTheHeadlessWindowingPlatform` fails if `Avalonia.Native`/`Avalonia.X11`/`Avalonia.Win32` ever loads.
- Write UI tests with `[AvaloniaFact]` / `[AvaloniaTheory]` (not `[Fact]`/`[Theory]`) — they run the body on a real Avalonia UI thread.
- `Headless/ThemedHost.cs` shows one control under a chosen `ThemeVariant`, pinning the variant **on the window** rather than on the application so a dark host and a light host can coexist in one session without leaking global state into the next test. It runs `Dispatcher.UIThread.RunJobs()` after `Show()`, because `Show()` only *queues* the first layout pass.
- `Design/ViewSceneFactory.cs` builds every view over a realistic view model using the shell tests' own fakes, so no scene touches a drive, a settings file or the OS. A view whose `DataContext` is null renders an empty shell and proves nothing — `ViewRenderSmokeTests.EveryView_GetsADataContext_OrDeclaresItNeedsNone` guards that.
- **`ViewSceneFactory.ViewTypes()` discovers a `Window` or a `UserControl`** — that is, a *screen*: a whole surface the app hosts and hands a view model. It used to be "every `Control` that is not a `Panel`", which also swept up the **leaf controls drawn in code** under `Controls/` (`KeyboardPanel`, `Icon`). Those are not screens: they have no view model, a scene for them would be a fabrication rather than a realistic one, and each would need a permanent exemption on `EveryView_GetsADataContext_OrDeclaresItNeedsNone`. They are covered by their own targeted render tests instead, which can drive the properties a leaf actually has (`Controls/KeyboardPanelTests`, `Design/IconRenderTests`). The narrowing dropped nothing that was discovered before — every type the old filter found is a `Window` or a `UserControl`, and `TheViewCatalog_CoversEveryScreenTheAppCanShow` holds the count and the named screens to that.

## Rendered-frame capture — the key technique

Drawing is deliberately **not** stubbed: `UseHeadlessDrawing = false` plus `.UseSkia()` means the headless window rasterises real pixels offscreen. `HeadlessWindowExtensions.CaptureRenderedFrame()` returns them as a `WriteableBitmap`, which can be saved to a PNG and looked at.

This needs **no display and no macOS screen-recording permission** — nothing is captured from the screen; Skia renders into a buffer. It is the fastest way to answer "what does this actually look like?" while working on a view, and it is far more reliable than launching the app.

Two mechanics that cost real time to discover:

1. **Transitions run on wall-clock time, and ticking the render timer does not advance them.** `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` reports the *stopwatch* elapsed time, so calling it in a loop renders the same instant over and over. A surface whose `.open` class was just set is still at `Opacity="0"` and the PNG comes back blank. **Wait ~300 ms of real time (a `Task.Delay`) before capturing anything that fades in**, then tick and capture. `ThemedHost.Capture()` retries the tick five times, which covers the "first tick was spent on layout" case but cannot substitute for real elapsed time.
2. **Turn `UseLayoutRounding` off in layout-arithmetic tests.** In the app, arranged caps are snapped to device pixels, which is right on screen and useless in a test: it rounds a 0.6 px half-gap to 1 and hides every off-by-a-fraction. `KeyboardPanelTests` sets `UseLayoutRounding = false` on both the panel and its children.

**Frame capture is a diagnostic, not a suite.** Run it ad hoc, look at the PNG, delete it. **There is deliberately no golden-image comparison anywhere in the tests, and none should be added** — pixel baselines break on a font-rasteriser update, an Avalonia patch release, a different CI runner, and a one-pixel layout change that nobody minds, and the resulting churn buries the regressions that matter. The suite asserts on *resolved values and named pixels* instead (see `FramePixels` below), which is stable.

## What each suite guards

| Suite | Guards | The regression it catches |
|---|---|---|
| `Headless/HeadlessHarnessTests` | The harness's own claims | The session stops booting the real `App`; a native windowing backend creeps in (CI breaks); drawing gets stubbed and every render test becomes vacuous |
| `Design/TokenCompletenessTests` | `Tokens.axaml` structure | A role added to one variant only; a `Color` without its `Brush`; a `Brush` painting a different colour than its `Color`; a merge that dropped half the file |
| `Design/TokenValueTests` | Every role's exact hex in both variants | A role that exists with the wrong value — which repaints the app just as thoroughly as one that is missing |
| `Design/ShapeAndTypeTokenTests` | Geometry + type resources, and the fonts | A radius/padding/height drifting off the handoff; the embedded families failing to register and the whole app rendering in the OS default face; a Plex weight not resolving |
| `Design/ResourceReferenceTests` | **Every resource key any XAML file looks up resolves in both variants**, and no view holds a hex | See below — this is the important one |
| `Design/ViewResolutionTests` | `ViewLocator` name-matching | A renamed or moved view model silently degrading to the diagnostic `TextBlock`; a stale exclusion-list entry |
| `Design/ViewRenderSmokeTests` | Every view, under both variants, loads + styles + draws | A broken `StaticResource`, an unparseable brush or geometry, a `DataTemplate` naming a moved type, a selector referring to a template part that no longer exists, a setter whose value cannot coerce |
| `Design/ControlPaletteTests` | `Controls.axaml` | A Fluent key left unmapped — it still resolves, to Fluent's own grey, so nothing throws and nothing looks obviously wrong; a `Color`-typed key aliased to a brush role, which makes the control draw nothing |
| `Design/ControlRenderTests` | The palette **at the glass** | A resource that resolves correctly but never reaches the control, because a Fluent state setter targeting `PART_ContentPresenter` outranks a `Background` on the control itself |
| `Design/MotionBudgetTests` | The budget | A duration off the table; a `TabSwapTransitions` resource appearing; a layer/tab container acquiring a `Transitions`; a page transition; movement leaking into the reduced set; `MotionResourceBinder` failing to re-point an alias |
| `Design/IconCatalogTests` | The three geometry dictionaries' structure | A dictionary written but never merged into `App.axaml` — every key then resolves to nothing and draws an icon-shaped hole; a key without its family's prefix; a duplicate key; a colour creeping into a shape file; a mark dropped from the fixed state/action set; the two `Icons.axaml` files being read as one |
| `Design/IconCoverageTests` | Coverage, driven by the domain | **The blind spot no markup guard can see**: a key looked up from C# rather than from XAML. A device added to the catalog with no art; a new `LightingMode` with no mark; art for a device the app cannot configure (which would promise an editor that does not exist); a mark naming nothing |
| `Design/IconGridTests` | The icon law, made enforceable | A mark whose paint or stroke leaves its authored box; a mark emptied or authored at the wrong scale; a silhouette off-centre in the shared 92×56 box; the scanning arc losing the lop-sidedness that makes the `SourceBox` rule load-bearing; two marks drawn identically; a named shared-shape exemption going stale |
| `Design/IconRenderTests` | `Icon` at the glass | A mark fitted to its ink instead of its box; a stroke-only mark drawn filled; a silhouette stretched instead of letterboxed; a mark not centred in a slot larger than its fit; the pen scaling with the geometry so a large icon reads as a fat one; `Icon`'s C# defaults drifting from `IconSize`/`IconStrokeThickness` |
| `Design/SpinnerMotionTests` | The one icon that moves | The style's written-out duration drifting from `DurationSpinnerRotation`; the loop stopping short of a full turn, easing, or not repeating; the class losing the scanning mark or its 14×14 box; the rotation origin leaving the centre (the arc then orbits); the spinner acquiring a `…Full`/`…Reduced` pair; reduce-motion freezing it |
| `Controls/KeyboardPanelTests` | The board's unit→pixel arithmetic | Scale taken from the wrong axis; an infinite/NaN/≤0 constraint contributing; the `NaturalUnitSize` fallback; asymmetric gaps; a negative cap width; the invalid-board path failing to arrange children |

### Why `ResourceReferenceTests` exists

**A missing `{DynamicResource}` key fails silently at runtime.** Avalonia resolves it to nothing at all, leaves the property at its default, and renders a control that merely looks a little wrong. No exception, no log line — a rendering test cannot see it. (A broken `{StaticResource}`, by contrast, throws while the XAML loads and is caught by the render smoke.)

The only way to catch it is to read the markup and check every key. The app compiles its XAML, so there is no readable copy in the built assembly; `KinesisEdit.Tests.csproj` therefore embeds the app's `.axaml` files as `EmbeddedResource` under `LinkBase="Xaml"`, and `Design/AuthoredXaml.cs` reads them back. It strips XML comments first — these files quote the markup they explain — and scans three forms: `{DynamicResource X}`, `{StaticResource X}`, and the element form `<StaticResource ResourceKey="X" />` that is the whole of `Controls.axaml`.

**If you add a XAML file, it is picked up automatically.** If you add a *new form* of resource reference, extend `AuthoredXaml.ResourceKeysIn` or it silently escapes the guard.

### Asserting a control actually paints a token — `FramePixels`

`Headless/FramePixels.At(frame, x, y)` reads one pixel out of a captured frame, handling both `Bgra8888` and `Rgba8888`. It is the one assertion that reaches all the way through: a token, resolved under a variant, applied by a style, drawn by Skia, read back as the colour the handoff names.

```csharp
using var host = ThemedHost.Show(button, ThemeVariant.Light, 200, 100);
var frame = host.Capture();
Assert.Equal(
    DesignTokens.ResolveBrushColor("AccentBrush", ThemeVariant.Light),
    FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
```

Use it when the question is "does this control *end up* painted with that role", not "does that role resolve" — the two differ whenever a control theme's state setters are in play. Keep the sample point somewhere flat (the middle of a filled face, a corner of the window), never on a border, a glyph or an anti-aliased edge.

`Design/DesignTokens.cs` is the resolver helper. It reaches the tokens two ways on purpose: `DeclaredKeys` loads `Tokens.axaml` directly, which is the only way to see what a variant *declares* (the resource system can only answer about a key it is handed), and `TryResolve` asks the live `Application`, which is what a view does — so a token that is declared but never merged still fails. `ControlPalette.cs` does the same for `Controls.axaml`.

## Adding a screen: the test checklist

1. Nothing extra is needed for `ResourceReferenceTests`, `ViewResolutionTests` or `ViewRenderSmokeTests` — all three discover types by reflection and pick a new view up automatically. But `ViewSceneFactory` must learn to build a realistic view model for it, or `EveryView_GetsADataContext_OrDeclaresItNeedsNone` will fail (which is the point).
2. Bump the count tripwires if you add a batch of views (`TheViewCatalog_CoversEveryScreenTheAppCanShow`, `EveryViewOfTheApp_IsVisibleToThisGuard`).
3. A view model rendered by an explicit `DataTemplate` rather than by the locator goes on `ViewResolutionTests._excluded` **with the reason**; `EveryExclusion_StatesWhy` and `TheExclusionList_HasNoStaleEntries` police that list.
4. New token → `TokenValueTests` row. New motion → `Motion.axaml` (both flavours) + `MotionResourceBinder.Aliases` + a `MotionBudgetTests` row. New Fluent control family → `Controls.axaml` in both variants + `ControlPaletteTests`. New mark → the right geometry dictionary, under its family's prefix; the `Icon*` suites pick it up by reflection, except a new *state/action* mark, which also goes on `IconCatalogTests._stateAndActionMarks`.

---

# Deviations from `docs/design/` and source conflicts

Per the repo's documentation rule, deliberate deviations are recorded here, not in the handoff. `docs/design/` is a delivered artifact.

| Conflict / gap in the source | Resolution shipped | Why |
|---|---|---|
| Scrim: `2b` says "scrim 40 %", the handoff's elevation line says `rgba(8,10,12,0.62)` | **0.62** (`#9E080A0C`) | The elevation line is the explicit token; `2b`'s 40 % reads as shorthand for the fade in the mock |
| Type scale: `1a`'s specimen (body 13/400, mono values 12, mono labels 11) vs the handoff (body 11/400, mono 10–11, labels 10/600) | **Handoff wins** | It is derived from the drawn screens; `1a` is a type specimen board. `1a`'s 24/600 step is kept anyway as `FontSizeDeviceHeadline` |
| Radii: `1a` says "radius 4 key / 6 panel / 10 window" vs the handoff's 8–9 panel / 5 control / 4 keycap / 3 chip / 999 pill | **Handoff wins** (panels pinned to 8 of the 8–9 range) | Matches the drawn art and the issue body |
| Light error `#B0453C` appears only in mockup `2a`, not in the handoff's token tables | Shipped as light `StatusError`/`StatusErrorText` | The acceptance criterion "every token in handoff § Design tokens" is a *subset* of what ships, not a ceiling |
| OK tint background given as a range `0.10–0.14`; tint border as `0.35–0.4` | Pinned to **0.12** (`0x1F`) and **0.38** (`0x61`) — the midpoints | A token must be one value. Applied to all four status ramps for consistency |
| Advisory prose-on-tint `#C9B48A`/`#D9BE86` and error text-on-tint `#EE9C94`/`#F09A92` are **unlabelled pairs** | Read as dark/light. The light members are replaced by `2a`'s darker values (`#7A5A11`, `#B0453C`) | Both members of each pair are too light to read on a light background; `2a`'s law is that status hues darken on light. **This is an inference, not a stated value** |
| No light value exists anywhere for the demo purple | Light `StatusDemo` **derived** as `#7A4FD0` | Follows `2a`'s law — darken the hue for contrast, keep the hue identity. A derivation, not a transcription |
| `#B58CF6` is both "LED purple" (handoff badge hues) and "Demo Mode" (`1a`) | Two role tokens, one colour: `BadgeLed` and `StatusDemo` | Neither call site has to know about the other. They diverge on light, where only the status hue darkens |
| Light theme collapses `secondary` == `body-muted` (`#4A5158`) and `faint` == `disabled/hint` (`#8A9298`) | Kept as **six distinct keys** | Not an error in the source. Six keys keeps the completeness test symmetric and every call site variant-agnostic |
| Reduce-motion: the design says only "fades only, no rise"; issue #85 adds "no height animation" | Implemented as the **issue** states — the reduced list insert is an empty `Transitions` | A deliberate extension beyond the design source, recorded here |
| The handoff prescribes role naming with four illustrative examples and no registry, and is inconsistent about the `Brush` suffix | Normalised to paired `<Role>Color` / `<Role>Brush` | Leaves `AccentBrush` intact and regularises everything else. The registry above is this repo's, not the handoff's |
| The handoff's "line-height 1 for single-line UI text" | No `LineHeight` set on single-line styles | CSS's line-height 1 lets glyphs overflow; Avalonia clips to the line box, so a literal 1 would cut descenders off every label. Font metrics are the honest equivalent |
| The handoff's button padding "8×13" (CSS order) | `PaddingButton` = `13,8` | Avalonia's two-value `Thickness` is horizontal,vertical |
| Section-label tracking "0.10–0.14 em" | Pinned to 0.12 em → `LetterSpacingSectionLabel` = 1.2 px | Avalonia's `LetterSpacing` is in pixels, and the labels are 10 px |
| The dev-only `KeystrokeCaptureSpikeWindow` was proposed as exempt from the design system | **No exemption.** It uses `SurfaceLineBrush` and `RadiusControl` like everything else | It follows the OS theme like every other window, and there was no cost to doing it right. See [keystroke-capture.md](keystroke-capture.md) |

## Deviations in the icon and device-art system

The framing for the device-art rows: `handoff.md:123` asks for "all 7 known models with silhouettes", but the icon specimen in mockup `2b` draws only **four archetypes** — `contoured`, `split flat`, `60% TKO`, `pedal`. Seven boards had to be drawn from four drawings plus the hardware and the specs, and the discriminators had to be invented rather than transcribed.

| Conflict / gap in the source | Resolution shipped | Why |
|---|---|---|
| `2b` draws `contoured` as **two detached rects**, shared by both contoured boards; `mockups.md:324` labels the Advantage2 "Split contoured" | Advantage2 is drawn as **one connected body** — two wells joined by a central bridge | The hardware is one piece. The 360's two fully detached halves are the primary discriminator between the pair, and reusing the split silhouette for both would leave the two most similar boards indistinguishable |
| Nothing in `docs/design/` draws an Advantage2 | Its **front recess** and a **12-tab function strip** are invented from the real hardware | `specs/05-key-model.md` §4.5 gives it an 18-position function row (indices 0–17, including the locked Keypad and Program keys) that the 360 (§4.7, whose base layer starts at the number row) lacks entirely. 18 tabs would be sub-pixel in a 92-wide box, so the strip is abstracted to 12 |
| Real contoured wells are 6×4 plus thumb clusters (`mockups.md:322`) | Key grids abstracted to **4×4**; thumb clusters not drawn | This is what the mock's own 360 card art does: two 34×40 halves, each a 4×4 grid. At 92×56 a 6×4 grid plus clusters is texture, not information |
| The Freestyle Pro | Drawn with a **narrower left half and no macro column** | That is the physical hardware. **Caveat:** the repo's own domain model contradicts it — `FreestyleGeometry.cs:81` builds the Pro from `BuildEdgeTopKeys`, giving it the Edge's `hk1`–`hk10` column, because the legacy app shipped one binary for both. The card art follows the hardware, so the Pro's card will not match its future board picture (#39) |
| Freestyle Edge vs Freestyle Edge RGB | **One shared body**, differing only by the `hk0` cap at the top of the macro column | They are the same physical board; `specs/05-key-model.md` §4.2 says the RGB is "the same 95-position shape as FS Edge but index 0 is the extra `hk0` key", and `FreestyleEdgeRgbVisual.cs:75-84` draws exactly that. Honestly the weakest-differentiated pair of the seven |
| The Savant Elite 2's two drawings disagree: `2b`'s silhouette bottom-aligns its three pedals (`align-items: flex-end`), the dashboard card art centres them | **Bottom-aligned** | One geometry is used at both sizes and can only do one. Pedals rest on a floor |
| `2b`'s contoured silhouette puts the large radius on the **outer** top+bottom corners; the 92×56 card art puts it on the **inner top** corner | Follows the **card art** | The card art is the drawing at the size the art frame actually renders; the specimen is a 15×20 thumbnail of an idea |
| `handoff.md:123` asserts silhouettes in the empty-state device picker; mockup `1d` draws none — it lists the seven by name only | **Silhouettes shipped** | The handoff is the explicit instruction and `1d` is the older screen. The picker is exactly where a silhouette earns its place: the user is choosing which board they own |
| `2f` (and `2b`) draw the lighting-mode marks as **filled colour and gradient swatches** — a rainbow block for Spectrum, a two-stop gradient for Breathe | **Stroked/filled geometry**, painted with a token | A baked gradient cannot inherit a theme token, cannot recolour for the selected row, and reads as a colour *sample* rather than as a motion. `2b`'s own geometry archetypes are followed instead and the token carries the colour |
| `2f` lists **14** modes, omitting `Freestyle` and `Frozen Wave` | **All 16** `LightingMode` members are marked | The picker renders from the enum; a missing mark is a hole in the rail. `Disabled` is the menu's "Disable" row and `PitchBlack` the reserved `[black]` token |
| `2b`'s wave archetype is **"wave bars"** | Wave is drawn as a **curve with a leading arrow** | The bar register is already spent three times over — Spectrum's bands, Rain's columns, Disable's bar — so a fourth set of bars would be the least distinguishable mark on the rail. The curve also buys the Frozen Wave pair below |
| Frozen Wave has no drawing; the obvious reading is "wave + stop mark" | The **wave sampled into five fixed stripes**, no arrow | `specs/07-lighting.md` §2.3 makes it a *static* per-LED rainbow (`[frozenwave_edge]`, a bare token plus per-LED colour lines), not a paused animation |
| Loop has no drawing | An **open circular arrow** (315°), not a closed circuit | `specs/07-lighting.md` §3 gives it a direction and a speed — "sweeps in the chosen direction". Kept open so its silhouette cannot be mistaken for Monochrome's disc or Breathe's ring |
| `specs/07-lighting.md` §3 says Breathe **and** Pulse "fade the whole board in and out" — the same sentence for both | Breathe = concentric **circles** (a key breathing), Pulse = concentric **rounded rects** (the board breathing) | The real difference is in the file grammar: Pulse writes only `[pulse]>[spdN]`, while Breathe carries per-key colours. Key-shaped vs board-shaped is that difference, drawn |
| The design reserves hatching for "off, **not** black" (`handoff.md:105`, `mockups.md:333`) | `Disabled` keeps `2b`'s plain bar; **PitchBlack is a slashed board outline**, not hatched | Hatching the mode literally named Pitch Black would say the opposite of what the hatch means. The slash keeps that register free, and at rail size a 4 px hatch pitch fills in solid anyway |
| Ripple is `2b`'s "rings" archetype | **Two arcs and an origin dot**, not three rings | Three evenly spaced arcs at 1.5 px inside a 16 px box leave under a pixel between them and smear at rail size (confirmed by frame capture). Full concentric circles would also collide with Breathe's ring |
| The app mark "K" | Stays **700-weight mono type**, not geometry | `handoff.md:86`: "700 only for the tiny logo 'K'". It is a wordmark, not an icon, and sits outside the three icon families |
| The mock draws the spinner four different ways: 14 px/1.5/1.1 s, 12 px/1.5/1.1 s, 10 px/1.5/0.7 s and 20 px/2 px/0.75 s | Normalised to **14 px / 1.5 px / 1.1 s** | That is the one the handoff states in prose (`handoff.md:162`) and the one the icon specimen draws. `SpinnerSize`/`SpinnerStrokeThickness` are the single source |
| Reduce-motion says "fades only, no rise, no height animation" | **The spinner keeps spinning** | It is an essential status affordance, not decoration: a frozen spinner reads as a hung app, which is a worse outcome for the very user reduce-motion exists to protect. Recorded here because it is the one exception to the reduced budget |
| The icon law's pen is 1.5 px | The empty-state picker's silhouettes stroke at **0.6** | At `Size="33"` the key rects are under two pixels across; 1.5 welds the grid into a slab and even 1.0 closes it up. 0.6 leaves the bodies reading as outlines in both variants (checked by rendering the seven at 0.5/0.6/0.75/1.0 through the headless harness) |
| `handoff.md:117` says "92×56 device art" without saying whether that is the frame or the art | **92×56 is the art box**; `Border.deviceArtFrame` adds 6 padding + 1 border and so renders **106×70** outer | The mock's own CSS agrees: its art frame is `width: 92px; height: 56px; padding: 6px; border: 1px` with no `box-sizing: border-box`, so 92×56 is the content area there too. Treating it as the outer box would shrink every board about 25 % below the scale the mock draws |
| The state-mark family is connected / not-detected / cannot-access / scanning | **Demo mode gets no state mark** | The state marks describe what the app found on a drive; demo mode is the *absence* of a drive state. Inventing a mark for it would put a fourth family in a set of three. Its chip still carries its own hue and label |
| `IconWarning` == `IconCannotAccess` and `IconConnected` == `IconNotDetected` are byte-identical path data | Kept as **four separate keys**, with the pairs named as exemptions in `IconGridTests` | The design draws each pair as one shape separated by pen and colour (amber advisory vs red failure; solid vs dashed ring). Separate keys mean a call site names the thing it means, so a later divergence in one cannot silently restyle the other — and the mirror test fails if a pair *stops* being shared, so the exemption cannot go stale |

## Known gaps handed to later issues

The design system is complete; the *screens* built on it are not. Everything below is a deliberate deferral, not an oversight — the redesign epic ([#57](https://github.com/migus88/kinesis-edit/issues/57)) owns them.

- **The dashboard is still a `WrapPanel` of fixed 320 px cards.** The handoff specifies a 2-column grid at gap 12, with a 2 px status rail flush against each card's left edge. `CardGridGap` and `WidthCardStatusRail` are defined and unused.
- **`Configure` is not an accent primary.** The card's primary action is a plain button.
- **The editor's dirty-state Save treatment is unimplemented.** The handoff says Save "turns amber whenever the session is dirty"; today it is a static `Button.primaryAction`.
- **Light `Button.primaryAction` at rest measures 2.75:1** (white `AccentText` on the `#5B9DF9` accent fill), below WCAG AA for normal text. This is the handoff's own Accent/AccentText pairing shipped faithfully; the alternative — accent as a *foreground* on a neutral face — is worse (2.4–2.8:1) and was the defect this replaced. Dark is fine at 6.84:1. Raising light means changing a design token and belongs with the designer, not in a style file.
- **Light `StatusDemo` on its own tint is 4.2–4.6:1** — the AA boundary. See the status table above.
- **`FontSizeKeycapLabel` (9) is defined but no cap uses it.** The design quotes 9/400 "at mock scale", where a 1U cap is 30×26; `KeyboardPanel` draws a cap at up to 44 px and the legend does not yet scale with the board, so 9 px would be a regression against the drawn art rather than a match for it. `Button.keyCap` takes the 11/400 body step until the cap renderer scales its own type. `.keycapLabel` waits for that.
- **No keycap badges are drawn yet** — the remapped bar, macro dot, tap-and-hold triangle, advisory bar and locked hatching all have tokens and metrics (`BadgeMacro`, `BadgeTapHold`, `HatchPitch`, `HatchAngle`) and no renderer.
- **Four call sites still draw a glyph rather than a mark**, deliberately: the `▲▼` steppers in `TapAndHoldOverlayView` and `MacroDelayOverlayView`, the `✕` and `•` in `KeyboardEditorView`, and the `▾` in `SavantElitePedalView`. Their views are rewritten by later redesign issues, and drawing a mark for a control that is about to be replaced would be work thrown away twice. (Keycap legends — `KeyRegistry.GlyphText`, `PedalSpecialActions` — are **not** in this list: they are domain data, not icons, and stay type forever.)
- **`Border.statusChip` has no `.unknown`.** `StatusSeverity.Unknown` renders an unfilled chip, which is what the transitional state should look like.
- **Flyout/menu popover transitions are bounded but not driven.** Avalonia raises no open/closed pseudo-class on a `FlyoutPresenter`, so the budget currently only bounds property changes the presenter makes of its own accord.

## Load-bearing invariants

1. **No colour value outside `Themes/`.** Enforced by test.
2. **Both theme variants carry identical key sets** — in `Tokens.axaml` *and* in `Controls.axaml`. The app follows the OS theme; a key present in one variant is a colour that disappears on half the machines.
3. **`App.axaml` stays composition only.**
4. **`Controls.axaml` stays in `Application.Resources`.** Moving it to `Application.Styles` silently un-does the whole control palette.
5. **Tab and layer containers carry no `Transitions` at all** — and must clear Fluent's inherited one explicitly.
6. **Views and styles bind the bare motion alias**, never `…Full`/`…Reduced` directly.
7. **View models expose enums and strings, never brushes.** `EnumMatchConverter` maps them to style classes, which keeps the view models toolkit-free *and* lets `DynamicResource` re-resolve when the OS theme flips at runtime.
8. **The three geometry dictionaries carry no colour** — even though they live in `Themes/`, which is the one folder allowed to hold hex. A shape is a shape; the call site names the token it is painted with, or the icon freezes at one theme's colour.
9. **An `Icon` fits its authoring box, never its ink.** Ink-fitting destroys the shared centre of the state marks, the relative proportion of the seven boards, and the spinner's rotation centre.

## Deliberately not here

- **No design-token generator, no build step.** The tokens are hand-authored XAML; the value tests are the safety net.
- **No golden images.** See above — a deliberate refusal, not a missing feature.
- **No app-level theme picker.** `RequestedThemeVariant="Default"` follows the OS. Pinning a variant is a preference a later issue may add; `ThemedHost` already proves both variants work.
- **No animation framework.** Six `Transitions` pairs and one `Style.Animations` loop are the whole motion surface, and the budget says that is all there may be.
