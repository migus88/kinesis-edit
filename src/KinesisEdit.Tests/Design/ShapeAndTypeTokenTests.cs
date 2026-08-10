using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The other two token files: <c>Themes/Geometry.axaml</c> and the resources of
    /// <c>Themes/Typography.axaml</c>. Neither lives in a theme dictionary — a corner radius does
    /// not change when the OS flips — so the guard here is that they resolve in <b>both</b>
    /// variants anyway, which is what a view relying on them needs.
    /// <para>
    /// It also guards <b>what the embedded families can actually print</b>: every authored keycap
    /// legend, and every display string the chrome authors as text — a constant in the app
    /// assembly or a literal <c>Text=</c>/<c>Content=</c> in its markup.
    /// </para>
    /// </summary>
    public partial class ShapeAndTypeTokenTests
    {
        /// <summary>The app assembly's root namespace, which its folders mirror.</summary>
        private const string AppNamespacePrefix = "KinesisEdit.";

        /// <summary>
        /// The three call sites that still print a chrome glyph as text on purpose
        /// (docs/app/design-system.md § "Known gaps handed to later issues"). Each is scoped to the
        /// one file it was deferred in: the same character anywhere else, or a new uncovered
        /// character in these files, still fails the gate — and
        /// <see cref="EveryDeferredChromeGlyph_IsStillNeededByItsView"/> fails when a site is fixed
        /// and its entry left behind.
        /// </summary>
        private static readonly IReadOnlyList<GlyphDeferral> ChromeGlyphDeferrals =
        [
            // Issue #93 retired three of the four: the macro step's remove button left
            // KeyboardEditorView for the rail and spells its `×` U+00D7, which both families
            // carry, and MacroDelayOverlayView is gone entirely — its steppers were absorbed into
            // the step row and caption `+`/`-`. One deferral is left.
            new GlyphDeferral(
                "Views/SavantElitePedalView.axaml",
                0x25BE,
                "▾ on the Special Actions button; the pedal view is rebuilt by a later redesign issue.")
        ];

        /// <summary>
        /// The files allowed to author a <b>key-symbol</b> character — a mark out of
        /// <c>FontKeySymbols</c>, the third embedded family, which no IBM Plex face carries.
        /// <para>
        /// This is <b>not</b> a deferral: the string is not waiting to become a geometry, it is
        /// drawn in a face that can print it (<c>.keySymbol</c>, docs/app/design-system.md). It is
        /// narrow in both directions all the same — a rune is forgiven only in a listed file
        /// <em>and</em> only when the key-symbol family actually carries it, so a <c>☾</c> here
        /// still fails and a <c>⌘</c> in a sans caption anywhere else still fails.
        /// <see cref="EveryKeySymbolSite_StillAuthorsAMarkTheKeySymbolFamilyCarries"/> fails when a
        /// site stops authoring marks and its entry is left behind.
        /// </para>
        /// </summary>
        private static readonly IReadOnlyList<KeySymbolSite> KeySymbolSites =
        [
            new KeySymbolSite(
                "ViewModels/MacroModifierMarks.cs",
                "The 12 modifier marks of a macro step row (⇧ ⌃ ⌥ ⌘), drawn in the key-symbol face.")
        ];

        [AvaloniaTheory]
        // Radii: panels and cards 8, controls 5, keycaps 4, kbd chips 3, pills round.
        [InlineData("RadiusPanel", 8)]
        [InlineData("RadiusControl", 5)]
        [InlineData("RadiusKeycap", 4)]
        [InlineData("RadiusChip", 3)]
        [InlineData("RadiusPill", 999)]
        // The board's two half-panels are rounder than a cap and flatter than a card.
        [InlineData("RadiusBoardSection", 7)]
        public void Radius_InEachVariant_IsTheHandoffValue(string key, double expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(new CornerRadius(expected), (CornerRadius)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        // The 4px grid: the six-step spacing scale, the fixed chrome heights, the rails.
        [InlineData("Space4", 4)]
        [InlineData("Space8", 8)]
        [InlineData("Space12", 12)]
        [InlineData("Space16", 16)]
        [InlineData("Space24", 24)]
        [InlineData("Space32", 32)]
        [InlineData("HeightToolbar", 46)]
        [InlineData("HeightTabBar", 38)]
        [InlineData("HeightAdvisoryStrip", 30)]
        [InlineData("WidthInspectorRail", 268)]
        // 440 with issue #146 and 480 with issue #148, up from the handoff's 300: the redesigned
        // Macro panel draws a step row and BOTH rows of the compose bar as single lines, and #148's
        // second row measures 432 px with the panel's own chrome taking 50 more. It is still a floor
        // inside the band below, which KeyboardEditorViewModelTests pins to the C# constant.
        [InlineData("WidthInspectorRailWide", 480)]
        // The band the rail's drag seam moves between (issue #119). They are what the grid column's
        // own MinWidth/MaxWidth are set from, and KeyboardEditorViewModelTests pins each to the
        // HostPreferences constant that clamps the stored width — so a token moved here without its
        // sibling fails there rather than drifting. The top moved to 560 with the floor (#148): a
        // floor the seam can barely be dragged past is an override wearing a floor's name.
        [InlineData("WidthInspectorRailMin", 240)]
        [InlineData("WidthInspectorRailMax", 560)]
        [InlineData("GutterSplit", 26)]
        [InlineData("CardGridGap", 12)]
        [InlineData("WidthCardStatusRail", 2)]
        [InlineData("HeightDeviceCard", 212)]
        [InlineData("IconSize", 16)]
        [InlineData("IconStrokeThickness", 1.5)]
        [InlineData("IconSizeDialog", 24)]
        [InlineData("SpinnerSize", 14)]
        [InlineData("SpinnerStrokeThickness", 1.5)]
        [InlineData("HatchPitch", 4)]
        [InlineData("HatchAngle", 45)]
        // The keyboard canvas: the cell pitch the board is laid out on, and the badge vocabulary
        // drawn on a cap (handoff.md § "Focus, selection, key badges").
        [InlineData("KeycapPitchX", 34)]
        [InlineData("KeycapPitchY", 30)]
        [InlineData("KeycapGap", 4)]
        // How far the picture may be grown (issue #123). A deliberate deviation: the handoff caps
        // nothing, and an uncapped board was drawn 2238x631 at 2560 wide. BoardScaleHost itself
        // still defaults to no ceiling — this token, named once in KeyboardView.axaml, is the whole
        // of the policy. Doubled from 1.5 by issue #135: the first cap stopped the board growing
        // with most of a large window still empty.
        [InlineData("BoardScaleMax", 3.0)]
        // And the floor its row keeps. Fitting the board to its slot made every row sharing the
        // column a claim on it, and on the Lighting tab the wrapped zone buttons won: at 720x480
        // the picture came out four pixels tall.
        [InlineData("HeightBoardMin", 100)]
        [InlineData("BadgeRemapBarHeight", 2)]
        [InlineData("BadgeMacroDotSize", 5)]
        [InlineData("BadgeTapHoldSize", 6)]
        [InlineData("BadgeAdvisoryBarWidth", 12)]
        [InlineData("BadgeAdvisoryBarHeight", 3)]
        [InlineData("BadgeGap", 2)]
        public void Measure_InEachVariant_IsTheHandoffValue(string key, double expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(expected, (double)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        // Avalonia's two-value Thickness is horizontal,vertical — the reverse of the CSS shorthand
        // the handoff writes its "8,13" button padding in.
        [InlineData("PaddingCard", 14, 14)]
        [InlineData("PaddingInspectorSection", 12, 12)]
        [InlineData("PaddingButton", 13, 8)]
        [InlineData("PaddingTab", 13, 0)]
        [InlineData("PaddingBoardSection", 9, 9)]
        public void Padding_InEachVariant_IsTheHandoffValue(string key, double horizontal, double vertical)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(new Thickness(horizontal, vertical), (Thickness)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaFact]
        public void TheCardGridMargins_AreDerivedFromTheOneGap()
        {
            // `CardGridGap` is the number; the two Thicknesses are how it reaches a UniformGrid,
            // which has no spacing property and takes a Margin that no markup can compose out of an
            // x:Double. Three separate literals could drift apart in silence and leave the token
            // documented as the grid's gap while the grid used something else — so the derivation
            // is asserted rather than trusted.
            foreach (var variant in DesignTokens.Variants)
            {
                var gap = (double)DesignTokens.Resolve("CardGridGap", variant);

                Assert.Equal(
                    new Thickness(0, 0, gap, gap),
                    (Thickness)DesignTokens.Resolve("CardGridCellGap", variant));

                // The panel's negative of it: a negative margin inflates the slot a Layoutable is
                // arranged in, which is what removes the trailing gutter.
                Assert.Equal(
                    new Thickness(0, 0, -gap, -gap),
                    (Thickness)DesignTokens.Resolve("CardGridPanelBleed", variant));
            }
        }

        [AvaloniaFact]
        public void TheKeycapSize_IsThePitchMinusTheGap()
        {
            // The load-bearing arithmetic of the whole canvas. The handoff specifies the cap
            // ("30x26 (1u), gap 4"); the board is laid out on the pitch, and the cap is what is
            // left after the gap. Three literals that could drift apart in silence would leave the
            // board correct at one scale and wrong at every other, so the subtraction is asserted.
            foreach (var variant in DesignTokens.Variants)
            {
                var pitchX = (double)DesignTokens.Resolve("KeycapPitchX", variant);
                var pitchY = (double)DesignTokens.Resolve("KeycapPitchY", variant);
                var gap = (double)DesignTokens.Resolve("KeycapGap", variant);

                Assert.Equal(30, pitchX - gap);
                Assert.Equal(26, pitchY - gap);
            }
        }

        [AvaloniaTheory]
        // The type scale of handoff.md, in the order it lists it.
        [InlineData("FontSizeDeviceHeadline", 24)]
        [InlineData("FontSizePageTitle", 18)]
        [InlineData("FontSizeCardTitle", 15)]
        [InlineData("FontSizeModalTitle", 14)]
        [InlineData("FontSizeToolbarDevice", 13)]
        [InlineData("FontSizeControl", 12)]
        [InlineData("FontSizeModalBody", 12)]
        [InlineData("FontSizeBody", 11)]
        [InlineData("FontSizeMeta", 11)]
        [InlineData("FontSizeMonoValue", 11)]
        [InlineData("FontSizeMonoValueSmall", 10)]
        [InlineData("FontSizeSectionLabel", 10)]
        [InlineData("FontSizeKeycapLabel", 9)]
        // One step under it, and off the published scale: the physical secondary legend printed on
        // a cap. The handoff names no step for board silkscreen — mockup 1e draws it at 6.5 against
        // a 10 caption, this app 7 against 9 — so the number lives in Themes/ like every other type
        // step rather than as a literal in a style.
        [InlineData("FontSizeKeycapSubLegend", 7)]
        // Also off the scale, and for a reason the handoff could not have anticipated: the mockups
        // never draw a two-line cap (2a writes `Caps` and `Shift` on one line), while the domain
        // data spells nine of this board's captions in two ('Caps\nLock', specs/05-key-model.md
        // §1.1). Two 9px lines do not fit a 30x26 cap alongside the LED strip, so a caption that
        // carries its own break takes the step that does — still a full point over the silkscreen's
        // 7, which is what keeps the two apart.
        [InlineData("FontSizeKeycapLabelStacked", 8)]
        // 0.12em of tracking on the 10px uppercase section labels — the only tracking in the app,
        // and Avalonia's LetterSpacing is in pixels.
        [InlineData("LetterSpacingSectionLabel", 1.2)]
        public void TypeStep_InEachVariant_IsTheHandoffValue(string key, double expected)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(expected, (double)DesignTokens.Resolve(key, variant));
            }
        }

        [AvaloniaTheory]
        [InlineData("FontSans", "IBM Plex Sans")]
        [InlineData("FontMono", "IBM Plex Mono")]
        public void Family_IsTheEmbeddedIbmPlex(string key, string expected)
        {
            var family = Assert.IsType<FontFamily>(DesignTokens.Resolve(key, ThemeVariant.Dark));

            Assert.Equal(expected, family.Name);
            Assert.NotNull(family.Key);
            Assert.Contains("KinesisEdit/Assets/Fonts", family.Key!.ToString(), StringComparison.Ordinal);
        }

        [AvaloniaTheory]
        [InlineData("FontSans", "IBM Plex Sans")]
        [InlineData("FontMono", "IBM Plex Mono")]
        public void Family_LoadsRatherThanFallingBackToASystemFont(string key, string expected)
        {
            // The families are shipped in the assembly, so this must not depend on what the machine
            // has installed. If the embedded collection failed to register, the typeface would fall
            // back to the OS default and the whole app would render in the wrong face.
            var family = (FontFamily)DesignTokens.Resolve(key, ThemeVariant.Dark);

            Assert.True(
                FontManager.Current.TryGetGlyphTypeface(new Typeface(family), out var typeface),
                $"No glyph typeface for {expected}.");
            Assert.Equal(expected, typeface.FamilyName);
        }

        [AvaloniaTheory]
        [InlineData(FontWeight.Normal)]
        [InlineData(FontWeight.Medium)]
        [InlineData(FontWeight.SemiBold)]
        public void PlexSans_CarriesEveryWeightTheScaleUses(FontWeight weight)
        {
            var family = (FontFamily)DesignTokens.Resolve("FontSans", ThemeVariant.Dark);

            Assert.True(
                FontManager.Current.TryGetGlyphTypeface(new Typeface(family, FontStyle.Normal, weight), out var typeface),
                $"IBM Plex Sans has no {weight} face.");
            Assert.Equal(weight, typeface.Weight);
        }

        [AvaloniaFact]
        public void EveryAuthoredKeyLegend_HasAGlyphInBothEmbeddedFamilies()
        {
            // A keycap legend is domain text, not an icon, so it cannot be redrawn as geometry the
            // way mockup 1e's search glyph was (docs/app/design-system.md) — which makes the
            // embedded families' coverage a hard gate on what a board may print. IBM Plex is narrow
            // here: it carries no ☾ (U+263E), no ☼ (U+263C), no ①-⑧ (U+2460..2467), no ▶/⏮/⏭ and
            // no ⌫. A legend naming one of those would render as tofu on the board and no other
            // test would notice, because every assertion about the cap would still hold.
            var missing = new List<string>();

            foreach (var deviceId in Enum.GetValues<DeviceId>())
            {
                if (!VisualCatalog.TryGet(deviceId, out var visual))
                {
                    continue;
                }

                foreach (var key in visual.Keys)
                {
                    CollectUncoveredRunes(deviceId, key.Index, nameof(KeyVisual.Legend), key.Legend, missing);
                    CollectUncoveredRunes(deviceId, key.Index, nameof(KeyVisual.SecondaryLegend), key.SecondaryLegend, missing);
                }
            }

            Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
        }

        [AvaloniaFact]
        public void TheGlyphCoverageGuard_ActuallySeesAMissingGlyph()
        {
            // The guard above passes vacuously if the coverage probe answers "yes" to everything,
            // which is exactly what a wrong FontManager call would do. U+263E is the moon mockup 1e
            // prints on the Freestyle's first hotkey and is the reason that legend was substituted.
            Assert.False(IsCovered(new System.Text.Rune(0x263E)), "U+263E resolved — the probe is not reading the embedded families.");
            Assert.True(IsCovered(new System.Text.Rune('1')), "U+0031 did not resolve — the probe reads nothing at all.");
        }

        [AvaloniaFact]
        public void EveryAuthoredChromeString_HasAGlyphInBothEmbeddedFamilies()
        {
            // The same gate as the keycap one above, over the other half of the app's text: the
            // chrome. A legend is domain data; a chrome caption is ours, and we pick its characters
            // — which is exactly why an uncovered one slips in unnoticed. `⌥` in the layer
            // switcher's `⌥1` legend and `␣` in the pedal entry box both reached main this way,
            // and no rendering assertion could see either: Avalonia substitutes silently and the
            // control still measures, still lays out, still passes.
            var constants = AuthoredConstants();
            var attributes = AuthoredDisplayAttributes();
            var missing = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var authored in constants.Concat(attributes))
            {
                CollectUncoveredChromeRunes(authored, missing);
            }

            // Anti-vacuity. A sweep that silently finds nothing — a reflection filter that matches
            // no field, a regex that matches no attribute — passes this test while guarding
            // nothing at all, which this repo has been bitten by before. So the walk has to prove
            // it walked: a floor on each source (the app has ~400 constants and ~80 literal display
            // attributes today), and one named string of each kind that carries a non-ASCII
            // character, so the sweep cannot be quietly reduced to ASCII.
            Assert.True(constants.Count >= 250, $"Only {constants.Count} constants were reflected; the sweep found next to nothing.");
            Assert.True(attributes.Count >= 50, $"Only {attributes.Count} literal display attributes were read; the sweep found next to nothing.");

            Assert.Contains(
                constants,
                authored => authored.Site == "ViewModels/DeviceCardViewModel.cs"
                    && authored.Member == nameof(DeviceCardViewModel.ScanningStatusText)
                    && authored.Text.Contains('…'));

            // Private, and deliberately so: display text is as often a private constant as a public
            // one, and a sweep that read only the public surface would miss half the chrome.
            Assert.Contains(
                constants,
                authored => authored.Site == "Input/CapturedKeystrokeView.cs"
                    && authored.Member == "MissingToken"
                    && authored.Text.Contains('—'));

            // Anchored on the pedal view rather than the editor: issue #93 moved the editor's
            // macro markup into three views of its own, taking its non-ASCII display attributes
            // with it. `…` is covered by both families, so this proves the sweep reads a real
            // non-ASCII attribute — which the allowlisted `▾` in the same file could not.
            Assert.Contains(
                attributes,
                authored => authored.Site == "Views/SavantElitePedalView.axaml" && authored.Text.Contains('…'));

            Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
        }

        [AvaloniaFact]
        public void TheChromeGlyphWalk_ActuallyReportsAMissingGlyph()
        {
            // `TheGlyphCoverageGuard_ActuallySeesAMissingGlyph` proves the *probe*; this proves the
            // *walk* — that a string carrying an uncovered rune is reported by the collector the
            // sweep runs, and that the allowlist forgives one file rather than the whole app.
            var reported = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/Planted.axaml", "Text", "moon ☾"), reported);

            var line = Assert.Single(reported);
            Assert.Contains("U+263E", line, StringComparison.Ordinal);
            Assert.Contains("Views/Planted.axaml", line, StringComparison.Ordinal);

            var covered = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/Planted.axaml", "Text", "Save — now"), covered);
            Assert.Empty(covered);

            // The deferred `▾` is forgiven in its own view and nowhere else.
            var deferred = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/SavantElitePedalView.axaml", "Content", "▾"), deferred);
            Assert.Empty(deferred);

            var elsewhere = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/Elsewhere.axaml", "Content", "▾"), elsewhere);
            Assert.Single(elsewhere);
        }

        [AvaloniaFact]
        public void EveryDeferredChromeGlyph_IsStillNeededByItsView()
        {
            // An allowlist entry outlives the thing it excuses. Each of the three is a deferral,
            // not a permission: when its view is rebuilt and the glyph becomes a drawn mark, this
            // fails and the entry goes with it.
            var attributes = AuthoredDisplayAttributes();

            foreach (var deferral in ChromeGlyphDeferrals)
            {
                var rune = new System.Text.Rune(deferral.Codepoint);

                Assert.False(
                    IsCovered(rune),
                    $"U+{deferral.Codepoint:X4} '{rune}' is covered by both families — {deferral.Site} needs no deferral.");

                Assert.True(
                    attributes.Any(authored => authored.Site == deferral.Site && authored.Text.Contains(rune.ToString(), StringComparison.Ordinal)),
                    $"{deferral.Site} no longer prints U+{deferral.Codepoint:X4} '{rune}' — drop the deferral ({deferral.Reason}).");
            }
        }

        [AvaloniaFact]
        public void EveryKeySymbolSite_StillAuthorsAMarkTheKeySymbolFamilyCarries()
        {
            // The reverse check on the key-symbol exemption, the shape
            // `EveryDeferredChromeGlyph_IsStillNeededByItsView` uses: an exemption outlives the
            // thing it excuses. When a site stops authoring marks — the panel moves to a geometry,
            // the formatter is deleted — this fails and the entry goes with it.
            var authored = AuthoredConstants().Concat(AuthoredDisplayAttributes()).ToList();

            foreach (var site in KeySymbolSites)
            {
                var marks = authored
                    .Where(text => string.Equals(text.Site, site.Site, StringComparison.Ordinal))
                    .SelectMany(text => text.Text.EnumerateRunes())
                    .Where(rune => !IsCovered(rune) && KeySymbolGlyphCoverage.Instance.CanPrint(rune.ToString()))
                    .ToList();

                Assert.True(
                    marks.Count > 0,
                    $"{site.Site} authors no character that needs the key-symbol family — drop the exemption ({site.Reason}).");
            }
        }

        [AvaloniaFact]
        public void TheKeySymbolExemption_ReachesOnlyItsOwnFileAndOnlyItsOwnMarks()
        {
            // Both halves of the narrowing, proved the way the deferral list's are. A ⌘ is
            // forgiven in the formatter and nowhere else, and inside the formatter only a rune the
            // third family can actually draw is forgiven — a ☾ there still fails, which is what
            // stops the entry becoming a blanket permission for one file.
            var site = Assert.Single(KeySymbolSites).Site;

            var inItsOwnFile = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText(site, "WinMark", "⌘"), inItsOwnFile);
            Assert.Empty(inItsOwnFile);

            var elsewhere = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/Elsewhere.axaml", "Content", "⌘"), elsewhere);
            Assert.Single(elsewhere);

            var wrongGlyphInItsOwnFile = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText(site, "Planted", "moon ☾"), wrongGlyphInItsOwnFile);
            Assert.Single(wrongGlyphInItsOwnFile);
        }

        [AvaloniaFact]
        public void EveryResolvedKeyCaption_HasAGlyphInBothEmbeddedFamilies()
        {
            // The third glyph gate, and the one that closes docs/app/design-system.md's "live gap".
            // The keycap gate above walks the *silkscreen* — legends authored in the visual catalog
            // — but the caption a cap actually prints comes from the key table, through
            // `KeyCaption`, and a remapped cap, a macro step and a co-trigger all read it. All 17
            // of KeyRegistry's GlyphText values are in neither family, so before the capability
            // gate landed this walk failed 23 entries over.
            //
            // It gates the *resolved caption*, not `GlyphText` itself: an unprintable glyph is now
            // handled, so asserting the glyph column is covered would fail after the fix exactly as
            // it did before it. Resolving instead covers GlyphText, MacDisplayText, the per-dialect
            // display text and the token fallback in one walk.
            var missing = new SortedSet<string>(StringComparer.Ordinal);
            var resolved = 0;
            var glyphsDropped = 0;

            foreach (var entry in KeyRegistry.Entries)
            {
                foreach (var dialect in Enum.GetValues<TokenDialect>())
                {
                    foreach (var isMacOs in new[] { false, true })
                    {
                        var caption = KeyCaption.For(entry, dialect, isMacOs, EmbeddedFontGlyphCoverage.Instance);

                        resolved++;

                        if (entry.GlyphText.Length > 0 && !string.Equals(caption, entry.GlyphText, StringComparison.Ordinal))
                        {
                            glyphsDropped++;
                        }

                        CollectUncoveredCaptionRunes(entry, dialect, caption, missing);
                    }
                }
            }

            // Anti-vacuity, the shape the chrome gate uses: a floor proving the walk walked, and
            // proof that the branch this issue added actually fired. Without the second, the gate
            // passes trivially the day the probe starts answering "covered" for everything — which
            // is precisely the failure mode a wrong FontManager call produces.
            Assert.True(KeyRegistry.Entries.Count >= 1200, $"Only {KeyRegistry.Entries.Count} key-table entries were walked.");
            Assert.True(resolved >= 9000, $"Only {resolved} captions were resolved; the walk found next to nothing.");
            Assert.True(glyphsDropped > 0, "No entry fell off its glyph — the drop path never ran, so this gate proves nothing.");

            Assert.True(missing.Count == 0, string.Join(Environment.NewLine, missing));
        }

        [AvaloniaFact]
        public void TheResolvedCaptionWalk_ActuallyReportsAnUnprintableCaption()
        {
            // The walk's own probe, proved the way the chrome walk's is: a planted entry whose
            // caption carries an uncovered rune has to be reported, and a covered one must not be.
            var reported = new SortedSet<string>(StringComparer.Ordinal);
            var planted = new KeyDefinition
            {
                Code = 999_999,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                Gen1Token = "planted",
                DisplayText = "moon ☾"
            };

            var caption = KeyCaption.For(planted, TokenDialect.Gen1, isMacOs: false, EmbeddedFontGlyphCoverage.Instance);

            Assert.Equal("moon ☾", caption);

            CollectUncoveredCaptionRunes(planted, TokenDialect.Gen1, caption, reported);

            var line = Assert.Single(reported);
            Assert.Contains("U+263E", line, StringComparison.Ordinal);
            Assert.Contains("999999", line, StringComparison.Ordinal);

            var covered = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredCaptionRunes(planted with { DisplayText = "Moon" }, TokenDialect.Gen1, "Moon", covered);
            Assert.Empty(covered);
        }

        [AvaloniaFact]
        public void TheResolvedCaptionGate_WouldHaveFailedOnTheUnconditionalGlyphRule()
        {
            // What the gate is worth is what it catches, so the caught state is reproduced here:
            // a probe that answers "covered" to everything is exactly the rule this issue
            // replaced — `GlyphText` returned first and unconditionally — and under it the play
            // key captions as U+23EF, which no embedded face carries.
            var play = KeyRegistry.FindByCode(0xB3)
                ?? throw new InvalidOperationException("No key registered for the Play/Pause code.");

            var unconditional = KeyCaption.For(play, TokenDialect.Gen1, isMacOs: false, FakeGlyphCoverage.CoveringEverything);

            Assert.Equal(play.GlyphText, unconditional);

            var reported = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredCaptionRunes(play, TokenDialect.Gen1, unconditional, reported);

            var line = Assert.Single(reported);
            Assert.Contains("U+23EF", line, StringComparison.Ordinal);

            // And what it passes on now.
            Assert.Equal(
                "Play\nPause",
                KeyCaption.For(play, TokenDialect.Gen1, isMacOs: false, EmbeddedFontGlyphCoverage.Instance));
        }

        /// <summary>
        /// Reports every non-whitespace rune of one resolved caption that neither embedded family
        /// can print. There is no allowlist here on purpose: a keycap caption is domain data, so a
        /// gap is fixed by the caption rule (or by the key table), never excused.
        /// </summary>
        private static void CollectUncoveredCaptionRunes(
            KeyDefinition key,
            TokenDialect dialect,
            string caption,
            ICollection<string> missing)
        {
            foreach (var rune in caption.EnumerateRunes())
            {
                if (System.Text.Rune.IsWhiteSpace(rune) || IsCovered(rune))
                {
                    continue;
                }

                missing.Add(
                    $"Key {key.Code} ({dialect}) captions as '{caption}': U+{rune.Value:X4} '{rune}' is in neither embedded IBM Plex family.");
            }
        }

        /// <summary>
        /// Every <c>const string</c> / <c>const char</c> in the app assembly, public or not, with
        /// the file it was authored in. Constants only: the alternative — reading captions off
        /// static properties — means constructing view models, which runs their real logic and
        /// makes a font test depend on service wiring. Every non-ASCII caption in this app is
        /// already a constant, and a caption that is not is out of this sweep's reach (recorded in
        /// docs/app/design-system.md).
        /// </summary>
        private static IReadOnlyList<AuthoredText> AuthoredConstants()
        {
            var texts = new List<AuthoredText>();

            foreach (var type in typeof(DeviceCardViewModel).Assembly.GetTypes())
            {
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

                foreach (var field in type.GetFields(flags))
                {
                    if (!field.IsLiteral || field.IsInitOnly)
                    {
                        continue;
                    }

                    var text = field.GetRawConstantValue() switch
                    {
                        string value => value,
                        char value => value.ToString(),
                        _ => null
                    };

                    if (text is null)
                    {
                        continue;
                    }

                    texts.Add(new AuthoredText(SourceFileOf(type), field.Name, text));
                }
            }

            return texts;
        }

        /// <summary>
        /// Every literal <c>Text=</c> / <c>Content=</c> in the app's markup, read from the sources
        /// through <see cref="AuthoredXaml"/> — the app compiles its XAML away, so the markup
        /// itself is the only place a caption authored there can be seen.
        /// <para>
        /// Comments are stripped first, and that is load-bearing: these files quote the very markup
        /// they explain, so an explanation of a glyph reads exactly like the glyph. Bound values
        /// are skipped — a <c>{Binding}</c> or <c>{DynamicResource}</c> is a lookup, not text.
        /// </para>
        /// </summary>
        private static IReadOnlyList<AuthoredText> AuthoredDisplayAttributes()
        {
            var texts = new List<AuthoredText>();

            foreach (var file in AuthoredXaml.Files())
            {
                foreach (Match match in DisplayAttributePattern().Matches(AuthoredXaml.WithoutComments(file.Value)))
                {
                    var text = match.Groups["text"].Value;

                    if (text.StartsWith('{'))
                    {
                        continue;
                    }

                    texts.Add(new AuthoredText(file.Key, match.Groups["attribute"].Value, text));
                }
            }

            return texts;
        }

        /// <summary>
        /// A literal <c>Text="…"</c> or <c>Content="…"</c>. The lookbehind is what keeps
        /// <c>PlaceholderText</c> and the like from being read as <c>Text</c> and mis-attributed.
        /// </summary>
        [GeneratedRegex(@"(?<![\w.:])(?<attribute>Text|Content)=""(?<text>[^""]*)""")]
        private static partial Regex DisplayAttributePattern();

        private static void CollectUncoveredChromeRunes(AuthoredText authored, ICollection<string> missing)
        {
            foreach (var rune in authored.Text.EnumerateRunes())
            {
                if (System.Text.Rune.IsWhiteSpace(rune)
                    || IsCovered(rune)
                    || IsDeferred(authored.Site, rune)
                    || IsKeySymbol(authored.Site, rune))
                {
                    continue;
                }

                missing.Add(
                    $"{authored.Site} {authored.Member}: U+{rune.Value:X4} '{rune}' is in neither embedded IBM Plex family.");
            }
        }

        /// <summary>Whether <paramref name="site"/> is allowed to print <paramref name="rune"/> as text.</summary>
        private static bool IsDeferred(string site, System.Text.Rune rune)
        {
            return ChromeGlyphDeferrals.Any(
                deferral => deferral.Codepoint == rune.Value
                    && string.Equals(deferral.Site, site, StringComparison.Ordinal));
        }

        /// <summary>
        /// Whether <paramref name="rune"/> is a mark the key-symbol family carries, authored in a
        /// file that draws in that family. Both halves are required: the exemption is per file, and
        /// within a listed file it reaches only the runes the third family can actually print.
        /// </summary>
        private static bool IsKeySymbol(string site, System.Text.Rune rune)
        {
            return KeySymbolSites.Any(entry => string.Equals(entry.Site, site, StringComparison.Ordinal))
                && KeySymbolGlyphCoverage.Instance.CanPrint(rune.ToString());
        }

        /// <summary>
        /// The file <paramref name="type"/> was authored in. The app's folders mirror its
        /// namespaces, so the path is derivable — and a path is what an allowlist entry and a
        /// failure message can both be read against, whatever source the string came from.
        /// </summary>
        private static string SourceFileOf(Type type)
        {
            var declaring = type;

            while (declaring.DeclaringType is not null)
            {
                declaring = declaring.DeclaringType;
            }

            var name = declaring.Name;
            var generic = name.IndexOf('`');

            if (generic >= 0)
            {
                name = name[..generic];
            }

            var space = declaring.Namespace ?? string.Empty;
            var folder = space.StartsWith(AppNamespacePrefix, StringComparison.Ordinal)
                ? space[AppNamespacePrefix.Length..].Replace('.', '/') + "/"
                : string.Empty;

            return folder + name + ".cs";
        }

        private static void CollectUncoveredRunes(
            DeviceId deviceId,
            int keyIndex,
            string role,
            string? legend,
            List<string> missing)
        {
            if (legend is null)
            {
                return;
            }

            foreach (var rune in legend.EnumerateRunes())
            {
                if (System.Text.Rune.IsWhiteSpace(rune) || IsCovered(rune))
                {
                    continue;
                }

                missing.Add(
                    $"{deviceId} key {keyIndex} {role}: U+{rune.Value:X4} '{rune}' is in neither embedded IBM Plex family.");
            }
        }

        /// <summary>
        /// Whether <paramref name="rune"/> resolves to a real glyph in <b>both</b> embedded
        /// families, at every weight the type scale uses. A cap's legend can be set in either face,
        /// and a weight that lacks the glyph would substitute silently.
        /// <para>
        /// This delegates to the <b>production</b> probe rather than re-asking the font manager
        /// itself. The app now makes a rendering decision on the same question
        /// (<see cref="KeyCaption.For"/> drops an unprintable glyph, docs/app/keyboard-editor.md),
        /// and two implementations of "can we print this" would eventually disagree — with the
        /// gate passing while the screen drew tofu.
        /// </para>
        /// </summary>
        private static bool IsCovered(System.Text.Rune rune)
        {
            return EmbeddedFontGlyphCoverage.Instance.CanPrint(rune.ToString());
        }

        /// <summary>One authored display string, and where it was authored.</summary>
        /// <param name="Site">The file it lives in — <c>ViewModels/X.cs</c> or <c>Views/X.axaml</c>.</param>
        /// <param name="Member">The constant's name, or the attribute's.</param>
        /// <param name="Text">The string itself.</param>
        private sealed record AuthoredText(string Site, string Member, string Text);

        /// <summary>A glyph one named file may still print as text, and why it still may.</summary>
        /// <param name="Site">The one file the exemption applies to.</param>
        /// <param name="Codepoint">The character it exempts.</param>
        /// <param name="Reason">Why it is deferred rather than fixed.</param>
        private sealed record GlyphDeferral(string Site, int Codepoint, string Reason);

        /// <summary>One file that authors marks drawn in the key-symbol family, and what they are.</summary>
        /// <param name="Site">The one file the exemption applies to.</param>
        /// <param name="Reason">Which marks it authors, and where they are drawn.</param>
        private sealed record KeySymbolSite(string Site, string Reason);
    }
}
