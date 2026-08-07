using System.Reflection;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry.Visual;
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
            new GlyphDeferral(
                "Views/KeyboardEditorView.axaml",
                0x2715,
                "✕ on the macro step's remove button; the view is rebuilt by a later redesign issue."),
            new GlyphDeferral(
                "Views/MacroDelayOverlayView.axaml",
                0x25B2,
                "▲ on the delay stepper; the overlay is rebuilt by a later redesign issue."),
            new GlyphDeferral(
                "Views/MacroDelayOverlayView.axaml",
                0x25BC,
                "▼ on the delay stepper; the overlay is rebuilt by a later redesign issue."),
            new GlyphDeferral(
                "Views/SavantElitePedalView.axaml",
                0x25BE,
                "▾ on the Special Actions button; the pedal view is rebuilt by a later redesign issue.")
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
        [InlineData("WidthInspectorRailWide", 300)]
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
                authored => authored.Site == "ViewModels/WebToolCardViewModel.cs"
                    && authored.Member == nameof(WebToolCardViewModel.OpenWebToolActionCaption)
                    && authored.Text.Contains('↗'));

            // Private, and deliberately so: display text is as often a private constant as a public
            // one, and a sweep that read only the public surface would miss half the chrome.
            Assert.Contains(
                constants,
                authored => authored.Site == "Input/CapturedKeystrokeView.cs"
                    && authored.Member == "MissingToken"
                    && authored.Text.Contains('—'));

            Assert.Contains(
                attributes,
                authored => authored.Site == "Views/KeyboardEditorView.axaml" && authored.Text.Contains('—'));

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

            // The deferred `✕` is forgiven in its own view and nowhere else.
            var deferred = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/KeyboardEditorView.axaml", "Content", "✕"), deferred);
            Assert.Empty(deferred);

            var elsewhere = new SortedSet<string>(StringComparer.Ordinal);
            CollectUncoveredChromeRunes(new AuthoredText("Views/Elsewhere.axaml", "Content", "✕"), elsewhere);
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

            foreach (var type in typeof(WebToolCardViewModel).Assembly.GetTypes())
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
                if (System.Text.Rune.IsWhiteSpace(rune) || IsCovered(rune) || IsDeferred(authored.Site, rune))
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
        /// </summary>
        private static bool IsCovered(System.Text.Rune rune)
        {
            foreach (var familyKey in new[] { "FontSans", "FontMono" })
            {
                var family = (FontFamily)DesignTokens.Resolve(familyKey, ThemeVariant.Dark);

                foreach (var weight in new[] { FontWeight.Normal, FontWeight.Medium, FontWeight.SemiBold })
                {
                    if (!FontManager.Current.TryGetGlyphTypeface(new Typeface(family, FontStyle.Normal, weight), out var typeface))
                    {
                        return false;
                    }

                    if (!typeface.TryGetGlyph((uint)rune.Value, out _))
                    {
                        return false;
                    }
                }
            }

            return true;
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
    }
}
