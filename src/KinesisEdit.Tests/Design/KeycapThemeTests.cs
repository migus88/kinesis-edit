using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Geometry.Visual;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The key cap's control theme — <c>Themes/ControlThemes/Keycap.axaml</c> — plus the LED strip
    /// and the two legends of <c>Controls/KeyCapView.axaml</c> that sit inside it.
    /// <para>
    /// The cap carries the densest state matrix in the app: five faces on one small square, four
    /// badges on its four corners, and several of each can be true at once. Avalonia ranks no
    /// selector, so "listening beats selected beats hover" is true only for as long as somebody
    /// keeps writing the <c>:not(...)</c> qualifiers — which is invisible to a resource test and is
    /// most of what follows. The rest is at the glass, because a halo that is configured and then
    /// clipped away, or a hatch that draws one stripe and stops, both look perfectly correct in the
    /// object graph.
    /// </para>
    /// </summary>
    public class KeycapThemeTests
    {
        /// <summary>The theme key <c>Styles/</c> will bridge <c>Button.keyCap</c> to.</summary>
        private const string ThemeKey = "KeyCapButton";

        private const double HostWidth = 200;

        private const double HostHeight = 160;

        /// <summary>Cap size for the rendering tests: larger than a 1U cap so a probe has room.</summary>
        private const double CapWidth = 44;

        private const double CapHeight = 40;

        /// <summary>
        /// A 1U cap at the size the board actually draws one — <c>KeycapPitchX</c> − <c>KeycapGap</c>
        /// by <c>KeycapPitchY</c> − <c>KeycapGap</c>, the handoff's 30x26. The probe size above is
        /// bigger on purpose, and that extra room is exactly what a test about the cap's VERTICAL
        /// BUDGET must not have: at 40px tall the caption's row absorbs the slack and every
        /// arrangement fits.
        /// </summary>
        private const double UnitCapWidth = 30;

        /// <inheritdoc cref="UnitCapWidth" />
        private const double UnitCapHeight = 26;

        /// <summary>How far outside the cap's own edge the focus halo is sampled; inside its 3px.</summary>
        private const int OutsideProbe = -2;

        /// <summary>
        /// How far inside the cap's edge the selection ring is sampled. The outer pixel column is
        /// the cap's own 1px border; column 1 is the first of the face, and the ring's 2px covers
        /// both.
        /// </summary>
        private const int InsideProbe = 1;

        /// <summary>
        /// Slack allowed when comparing an arranged legend against the height its text needs.
        /// Avalonia rounds a desired size up to the device pixel and a text layout does not, so the
        /// two differ by a fraction on every line; a legend that is genuinely cut loses whole
        /// pixels (2.7 of 11.7 in the defect this guards).
        /// </summary>
        private const double Tolerance = 0.01;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheKeyCapTheme_ResolvesInBothVariants(string variantName)
        {
            var variant = ToVariant(variantName);
            var theme = Assert.IsType<ControlTheme>(DesignTokens.Resolve(ThemeKey, variant));

            Assert.Equal(typeof(Button), theme.TargetType);
        }

        /// <summary>The cap's states and the face token each is supposed to end up painted with.</summary>
        public static TheoryData<string, string> CapStates()
        {
            return new TheoryData<string, string>
            {
                { "rest", "SurfaceRaisedBrush" },

                // A remapped cap is an ORDINARY RAISED CAP. It used to take the 14% accent fill;
                // mockups 1e/2a draw it wearing only its bar, and the bar is now the whole signal.
                { "modified", "SurfaceRaisedBrush" },

                { "hover", "SurfaceKeySelectedBrush" },
                { "selected", "SurfaceKeySelectedBrush" },
                { "listening", "StatusAdvisoryTintBrush" }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(CapStates))]
        public void TheCapStateMatrix_PaintsItsTokenInDark(string state, string expectedKey)
        {
            AssertFacePaints(state, expectedKey, ThemeVariant.Dark);
        }

        [AvaloniaTheory]
        [MemberData(nameof(CapStates))]
        public void TheCapStateMatrix_PaintsItsTokenInLight(string state, string expectedKey)
        {
            AssertFacePaints(state, expectedKey, ThemeVariant.Light);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AListeningCap_ThickensItsBorderToTheAdvisoryHue(string variantName)
        {
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "listening");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryBrush", variant), cap.BorderBrush);
            Assert.Equal(new Thickness(2), cap.BorderThickness);
        }

        /// <summary>
        /// The pinned precedence, state pair by state pair. Every one of these is a case where two
        /// classes are true at the same time and the winner used to be decided by which style sat
        /// lower in <c>Styles/Keyboard.axaml</c>.
        /// </summary>
        public static TheoryData<string[], string> CompetingStates()
        {
            return new TheoryData<string[], string>
            {
                // locked loses its face to hover, to selection and to listening...
                { ["locked", "selected"], "SurfaceKeySelectedBrush" },
                { ["locked", "listening"], "StatusAdvisoryTintBrush" },

                // ...and selection loses only to listening.
                { ["selected", "listening"], "StatusAdvisoryTintBrush" },

                // `.modified` is in this data only to prove it changes nothing: it left the ladder
                // when the remap became a bar, so every combination reads exactly as it would
                // without it. A face setter creeping back onto it fails here rather than at the
                // board, where "a remapped cap looks selected" is the shape the bug takes.
                { ["modified", "locked"], "SurfaceInsetBrush" },
                { ["modified", "selected"], "SurfaceKeySelectedBrush" },
                { ["modified", "listening"], "StatusAdvisoryTintBrush" },
                { ["modified", "locked", "selected", "listening"], "StatusAdvisoryTintBrush" }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(CompetingStates))]
        public void ThePinnedPrecedence_DecidesTheFace(string[] classes, string expectedKey)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                var cap = Cap(variant, classes);

                using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

                Assert.Equal(DesignTokens.Resolve(expectedKey, variant), cap.Background);
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void HoverOutranksLocked_ForTheFaceAndNotForTheHatch(string variantName)
        {
            // The one place the pinned order deliberately differs from the old matrix. Hover still
            // wins the face — the pointer has to answer on a locked cap like it does on any other —
            // but the hatch is not a mood, so it survives: "not yours to edit" cannot stop being
            // true because a pointer crossed it.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "locked");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            SetPseudoClasses(cap, ":pointerover");

            Assert.Equal(DesignTokens.Resolve("SurfaceKeySelectedBrush", variant), cap.Background);
            Assert.True(HatchOf(cap).IsVisible, "The locked hatch went away under the pointer.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ALockedCap_IsHatchedRatherThanFlatlyFilled(string variantName)
        {
            // "Hatching = not yours to edit" — so the fill has to be stripes, at the design's 45
            // degrees. Two samples a step apart along the diagonal that crosses the bands land on
            // opposite sides of one, which a flat fill of any colour cannot do.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "locked");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var frame = host.Capture();
            var origin = OriginOf(host, cap);
            var stripe = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", variant);
            var face = DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant);

            var first = FramePixels.At(frame, (int)origin.X + 6, (int)origin.Y + 6);
            var second = FramePixels.At(frame, (int)origin.X + 7, (int)origin.Y + 7);

            Assert.True(Distance(first, second) > 8, $"Two pixels across the 45 degree hatch both painted {first}.");

            // ...and the whole diagonal is a tile, not one band: both the stripe colour and the
            // inset face it is drawn on appear along it.
            var onStripe = new List<bool>();

            for (var step = 0; step < 12; step++)
            {
                var painted = FramePixels.At(frame, (int)origin.X + 4 + step, (int)origin.Y + 4 + step);

                onStripe.Add(Distance(painted, stripe) < Distance(painted, face));
            }

            Assert.Contains(true, onStripe);
            Assert.Contains(false, onStripe);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ALockedCap_TradesItsSolidBorderForADashedOne(string variantName)
        {
            // docs/design/handoff.md draws locked as "45 degree hatched fill + dashed border". A
            // Border cannot dash in Avalonia 11, so the dash is the hatch shape's own stroke and the
            // cap's border steps out of the way rather than doubling it with a solid edge.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "locked");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var border = Assert.IsAssignableFrom<ISolidColorBrush>(cap.BorderBrush);

            Assert.Equal(Colors.Transparent, border.Color);

            var hatch = HatchOf(cap);

            Assert.True(hatch.IsVisible, "The locked cap drew no hatch.");
            Assert.Equal(DesignTokens.Resolve("HatchBrush", variant), hatch.Fill);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), hatch.Stroke);
            Assert.NotNull(hatch.StrokeDashArray);
            Assert.NotEmpty(hatch.StrokeDashArray!);
        }

        [AvaloniaFact]
        public void ARestingCap_DrawsNeitherHatchNorRing()
        {
            var cap = Cap(ThemeVariant.Dark);

            using var host = ThemedHost.Show(cap, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.False(HatchOf(cap).IsVisible);
            Assert.Equal(0, RingOf(cap).BoxShadow.Count);
            Assert.Equal(0, RootOf(cap).BoxShadow.Count);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void KeyboardFocus_PaintsTheHalo(string variantName)
        {
            // Inherited from BaseButton and asserted again here, because the cap is the surface the
            // rule was written for: 1px accent border plus a 3px 28% halo, and a clip anywhere
            // between the cap and the window would erase the second half of it.
            var variant = ToVariant(variantName);
            var cap = Cap(variant);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), cap.BorderBrush);

            var halo = RootOf(cap).BoxShadow;

            Assert.Equal(1, halo.Count);
            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), halo[0].Color);
            Assert.Equal(3, halo[0].Spread);

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentFocusHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X + OutsideProbe, MidRow(host, cap)));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APointerPress_DoesNotPaintTheHalo(string variantName)
        {
            var variant = ToVariant(variantName);
            var cap = Cap(variant);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.True(cap.Focus(NavigationMethod.Pointer), "The cap refused pointer focus.");

            Assert.DoesNotContain(":focus-visible", cap.Classes);
            Assert.Equal(0, RootOf(cap).BoxShadow.Count);

            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X + OutsideProbe, MidRow(host, cap)));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASelectedCap_RingsItselfWithoutReachingItsNeighbour(string variantName)
        {
            // Selection's 2px ring is drawn INWARD from the cap's edge, which is what leaves the 4px
            // gap between caps free for the focus halo — and is the whole reason the two can be told
            // apart when they are both on.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "selected");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var ring = RingOf(cap).BoxShadow;

            Assert.Equal(1, ring.Count);
            Assert.Equal(DesignTokens.ResolveColor("AccentKeyHaloColor", variant), ring[0].Color);
            Assert.Equal(2, ring[0].Spread);
            Assert.Equal(0, ring[0].OffsetX);
            Assert.Equal(0, ring[0].OffsetY);

            var frame = host.Capture();
            var origin = OriginOf(host, cap);
            var row = MidRow(host, cap);

            // Nothing at all outside the cap...
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                FramePixels.At(frame, (int)origin.X + OutsideProbe, row));

            // ...and the ring inside it, over the selected face.
            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("AccentKeyHaloBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceKeySelectedBrush", variant)),
                FramePixels.At(frame, (int)origin.X + InsideProbe, row));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void SelectionAndFocus_AreBothVisibleAndTellEachOtherApart(string variantName)
        {
            // The acceptance criterion of the issue. Both states are spread-only accent shadows at
            // nearly the same alpha, so stacking them on one element would produce a single slightly
            // denser ring and nothing legible. They are concentric instead — selection inside the
            // edge, focus outside it — and this reads one pixel of each band to prove it: a cap that
            // is both differs from a selected one where focus draws, and from a focused one where
            // selection draws.
            var variant = ToVariant(variantName);
            var selected = Sample(variant, ["selected"], focused: false);
            var focused = Sample(variant, [], focused: true);
            var both = Sample(variant, ["selected"], focused: true);

            Assert.True(
                Distance(both.Outside, selected.Outside) > 8,
                $"Focus adds nothing outside a selected cap: both painted {both.Outside}.");

            Assert.True(
                Distance(both.Inside, focused.Inside) > 8,
                $"Selection adds nothing inside a focused cap: both painted {both.Inside}.");

            // And each band still matches the one it came from, which is what "distinguishable"
            // means here: neither state has swallowed the other's ring.
            AssertClose(focused.Outside, both.Outside);
            AssertClose(selected.Inside, both.Inside);

            // ...and one control the comparisons above cannot supply on their own. The focused-only
            // cap rests on SurfaceRaised while a selected one rests on SurfaceKeySelected, so the
            // inside probe of `focused` differs from `both` in the FACE as well as in the ring, and
            // "selection adds something inside" would hold even if the ring were never drawn. A
            // hovered cap wears the selected face with no selection ring at all, which isolates the
            // ring as the single variable.
            var faceOnly = Sample(variant, [], focused: true, hovered: true);

            Assert.True(
                Distance(both.Inside, faceOnly.Inside) > 8,
                "The selection ring paints nothing over a cap that already wears the selected face: "
                + $"both painted {both.Inside}.");

            // The other half of the isolation: with the ring the only difference, the two are still
            // the same OUTSIDE, so the inward ring provably stays out of the neighbour's 4px gap.
            AssertClose(faceOnly.Outside, both.Outside);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASelectedAndFocusedCap_KeepsBothRingsInTheObjectGraph(string variantName)
        {
            // The same claim one level down, where the reason it works is visible: two elements,
            // one shadow each, because a Border has a single BoxShadow property and two
            // {DynamicResource} entries cannot be composed into one BoxShadows value in XAML.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "selected");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");

            Assert.Equal(DesignTokens.Resolve("SurfaceKeySelectedBrush", variant), cap.Background);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), cap.BorderBrush);

            var halo = RootOf(cap).BoxShadow;
            var ring = RingOf(cap).BoxShadow;

            // Exactly one each: the failure this guards is the two collapsing onto one element,
            // where they would sum into a single denser band instead of a concentric pair.
            Assert.Equal(1, halo.Count);
            Assert.Equal(1, ring.Count);

            // The design draws them 3px at 28% outward and 2px at 30% inward, which is what keeps
            // them legible as two marks rather than as one thick one.
            Assert.Equal(3, halo[0].Spread);
            Assert.Equal(2, ring[0].Spread);
            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), halo[0].Color);
            Assert.Equal(DesignTokens.ResolveColor("AccentKeyHaloColor", variant), ring[0].Color);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapHasNoPressFace_BecauseTheSelectionThatFollowsIsTheAnswer(string variantName)
        {
            // Keycap.axaml: "`:pressed` is deliberately absent. A cap's press is answered by the
            // selection that follows it immediately, and Fluent's 75ms scale-down is gone with
            // Fluent's template, so there is nothing to suppress and nothing to add."
            //
            // The cap is the one theme in the app that states an absence outright, so it is the one
            // that most needs it pinned: nothing else distinguishes "we decided against a press
            // face" from "we forgot to write one", and the cap's matrix is dense enough that a
            // later hand would add one without noticing the decision.
            var variant = ToVariant(variantName);
            var cap = Cap(variant);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            SetPseudoClasses(cap, ":pointerover");

            var hovered = FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X + 6, MidRow(host, cap));

            SetPseudoClasses(cap, ":pressed");

            Assert.Equal(DesignTokens.Resolve("SurfaceKeySelectedBrush", variant), cap.Background);
            AssertClose(hovered, FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X + 6, MidRow(host, cap)));

            // Nothing moved anywhere else either: the press is not a border or a thickness in
            // disguise, which is what a 26px cap 4px from its neighbour could not afford.
            Assert.Equal(DesignTokens.Resolve("SurfaceBorderRaisedBrush", variant), cap.BorderBrush);
            Assert.Equal(new Thickness(1), cap.BorderThickness);
            Assert.Equal(0, RootOf(cap).BoxShadow.Count);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledCap_LeavesTheBoardsRaisedFaceEntirely(string variantName)
        {
            // The cap does not override BaseButton's disabled treatment, which the theme states in
            // a comment and nothing checked. It matters because the whole board is disabled at once
            // — the editor dims it while a profile is loading — and a cap that kept its raised face
            // would leave the picture looking live while nothing on it answered.
            //
            // A PLAIN cap. The three live states take the same face, for a reason that is not the
            // same one at all — see ADisabledCapInAnyLiveState_TakesTheSameDeadFace.
            var variant = ToVariant(variantName);
            var cap = Cap(variant);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            cap.IsEnabled = false;

            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), cap.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), cap.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), cap.Foreground);

            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X + 6, MidRow(host, cap)));
        }

        /// <summary>
        /// The states that paint a cap with a hue of its own, and can outlive it. `.modified` used
        /// to be one and is not any more — it paints no face at all, so there is nothing for
        /// `:disabled` to have to beat.
        /// </summary>
        public static TheoryData<string> LiveCapStates()
        {
            return new TheoryData<string> { "selected", "listening" };
        }

        [AvaloniaTheory]
        [MemberData(nameof(LiveCapStates))]
        public void ADisabledCapInAnyLiveState_TakesTheSameDeadFace(string state)
        {
            // Contract 5 from the other side, and the trap Pills.axaml's header calls easy to miss:
            // Avalonia walks BasedOn first, so BaseButton's `:disabled` face is applied BEFORE
            // anything this theme declares, and a class written without `:not(:disabled)` wins over
            // it. A disabled `.modified` cap kept the full accent fill and accent edge, a disabled
            // `.selected` one kept its accent ring, and a disabled `.listening` one went on wearing
            // the amber "press a key now" — each of them taking the dead label colour and nothing
            // else, on a board the editor has dimmed precisely because nothing on it will answer.
            foreach (var variant in DesignTokens.Variants)
            {
                var cap = Cap(variant, state);

                using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

                cap.IsEnabled = false;

                Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), cap.Background);
                Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), cap.BorderBrush);
                Assert.Equal(DesignTokens.Resolve("TextDisabledBrush", variant), cap.Foreground);

                // Listening's 2px edge goes with it — the thickness is part of the shout — and so
                // does selection's ring, which is drawn in the accent and has no business on a cap
                // that cannot be selected any further.
                Assert.Equal(new Thickness(1), cap.BorderThickness);
                Assert.Equal(0, RingOf(cap).BoxShadow.Count);

                AssertClose(
                    Composite(
                        DesignTokens.ResolveBrushColor("SurfaceInsetBrush", variant),
                        DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                    FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X + 6, MidRow(host, cap)));
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapThatIsListeningAndSelected_KeepsItsRingFlushWithTheEdge(string variantName)
        {
            // The ring is deliberately NOT qualified against `.listening` — the key that is
            // listening is the key that is selected, and dropping the ring as the cap starts
            // waiting for a keystroke would read as the selection having moved. But `Border#Ring`
            // sits inside Root's padding, and its `Margin="-1"` is measured against a 1px border:
            // listening doubles that, which walks the panel and the ring one pixel further in and
            // leaves the band floating inside the cap's edge instead of hugging it.
            //
            // Read as geometry first, because that is the claim exactly, and then at the glass —
            // where a listening cap that is also selected must differ from one that is not, in the
            // OUTERMOST pixel of its own edge. That pixel is the whole defect: the broken margin
            // still paints a band, one step in.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "selected", "listening");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var ring = RingOf(cap);
            var inset = ring.TranslatePoint(new Point(0, 0), cap)
                ?? throw new InvalidOperationException("The ring is not in the cap's visual tree.");

            // Two pixels in, exactly as on a cap with a 1px border, so the 2px spread lands on the
            // cap's outer 2px in both cases.
            Assert.Equal(2, inset.X);
            Assert.Equal(2, inset.Y);
            Assert.Equal(cap.Bounds.Width - 4, ring.Bounds.Width);
            Assert.Equal(1, ring.BoxShadow.Count);

            var edge = EdgePixel(host, cap);
            var unselected = Cap(variant, "listening");

            using var bare = ThemedHost.Show(unselected, variant, HostWidth, HostHeight);

            Assert.True(
                Distance(edge, EdgePixel(bare, unselected)) > 8,
                $"The selection ring reaches no further than a listening cap's border: both painted {edge}.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheFocusRing_OutranksEveryStateBorder(string variantName)
        {
            // Six states compete for this one property, and the focus ring has to win all six. It
            // does only because each of them is written `:not(:focus-visible)` — Avalonia ranks no
            // selector, and BaseButton's styles are applied before this theme's.
            var variant = ToVariant(variantName);

            foreach (var state in new[] { "locked", "selected", "listening" })
            {
                var cap = Cap(variant, state);

                using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

                SetPseudoClasses(cap, ":pointerover");

                Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");
                Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), cap.BorderBrush);
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnUnlitKey_OnALightingBoard_ShowsTheHatchRatherThanGoingDarkOrVanishing(string variantName)
        {
            // "Off is hatched, never black", because black is a colour a key can legitimately be
            // lit — but the rule is scoped to a board that is SHOWING lighting, which is what
            // ShowsLedStrip says. Given that, the hatch is the strip's own background and the
            // colour, when there is one, covers it.
            var variant = ToVariant(variantName);
            var view = KeyCap(overlay: null, showsLedStrip: true);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var strip = StripOf(view);

            Assert.True(strip.IsVisible, "The LED strip vanished when the key had no colour.");
            Assert.Equal(DesignTokens.Resolve("HatchBrush", variant), strip.Background);
            Assert.False(ColorPatchOf(strip).IsVisible, "An unlit key painted a colour patch.");

            var frame = host.Capture();
            var origin = OriginOf(host, strip);
            var stripe = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", variant);
            var painted = RowSamples(frame, origin);

            Assert.Contains(painted, sample => Distance(sample, stripe) <= 24);
            Assert.DoesNotContain(painted, sample => Distance(sample, Colors.Black) <= 24);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapOnABoardThatIsNotShowingLighting_DrawsNoStripAtAll(string variantName)
        {
            // The third state, and the regression that made it necessary: the hatch answers "this
            // LED is off", never "lighting is not on screen". On the Keys tab — where nobody asked
            // about lighting — roughly a hundred caps were each carrying a hatched bar.
            //
            // Read at the glass as well as in the graph, because a strip that is present and
            // painted with the cap's own face would satisfy the property and still be a row of
            // bars: the pixel where the strip WOULD be must be the cap's resting face exactly.
            var variant = ToVariant(variantName);
            var lit = KeyCap(overlay: null, showsLedStrip: true);

            using var litHost = ThemedHost.Show(lit, variant, HostWidth, HostHeight);

            var litFrame = litHost.Capture();
            var stripRow = OriginOf(litHost, StripOf(lit));
            var hatched = RowSamples(litFrame, stripRow);

            // The control: on a lighting board that row is a hatch, so it is not one colour.
            Assert.True(hatched.Distinct().Count() > 1, $"The hatch painted one flat {hatched[0]}.");

            var plain = KeyCap(overlay: null, showsLedStrip: false);

            using var plainHost = ThemedHost.Show(plain, variant, HostWidth, HostHeight);

            var plainFrame = plainHost.Capture();

            Assert.False(StripOf(plain).IsVisible, "The Keys tab drew an LED strip.");

            // The two caps are the same size in the same place, so the strip's row is the same row —
            // and on the Keys tab it is bare cap face from end to end, one colour, nothing over it.
            var bare = RowSamples(plainFrame, stripRow);
            var face = Assert.IsAssignableFrom<ISolidColorBrush>(ButtonOf(plain).Background);

            Assert.Single(bare.Distinct());
            AssertClose(
                Composite(face.Color, DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                bare[0]);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ALitKey_OnALightingBoard_CoversTheHatchWithItsColour(string variantName)
        {
            // The other half: the hatch means "off", so a key that has a colour must not show any
            // of it through.
            var variant = ToVariant(variantName);
            var view = KeyCap(overlay: "#FF0000", showsLedStrip: true);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var patch = ColorPatchOf(StripOf(view));

            Assert.True(patch.IsVisible, "A lit key drew no colour.");

            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(patch.Background);

            Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), brush.Color);
        }

        [AvaloniaFact]
        public void ALitKey_OnABoardThatIsNotShowingLighting_StillDrawsNothing()
        {
            // The two answers are independent, and this is the pair that proves it: a key can be
            // lit on a picture that shows no LED row at all. `HasColorOverlay` means "this key is
            // lit" and nothing more — the Keys tab keeps computing it, because it shares the cap
            // view models with the lighting board, and simply draws none of it.
            var view = KeyCap(overlay: "#FF0000", showsLedStrip: false);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, HostWidth, HostHeight);

            var strip = StripOf(view);

            Assert.False(strip.IsVisible, "A lit key put an LED strip on a board that shows none.");
            Assert.True(
                ((KeyboardKeyViewModel)view.DataContext!).HasColorOverlay,
                "The scene did not actually light the key.");
        }

        [AvaloniaFact]
        public void TheKeyCapTheme_NamesNoPartWithAFluentPrefix()
        {
            // Contract 2. A part called PART_ContentPresenter would put this template back on the
            // hook Styles/ used to reach into, and would quietly re-register the presenter with the
            // ContentControl — the one thing the whole layer exists to stop.
            var theme = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Themes/ControlThemes/Keycap.axaml"]);

            Assert.DoesNotContain("PART_", theme, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void TheKeyCapView_ReachesItsKeyboardViewFromTheButtonItself()
        {
            // The template names its presenter `Label`, so the ContentControl no longer registers it
            // and the cap's content hangs off the presenter in the logical tree. An
            // `{Binding $parent[KeyboardView]}` written INSIDE that content would stop resolving;
            // the cap's command binding is on the Button, which is why it does not.
            var view = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Controls/KeyCapView.axaml"]);
            var ancestorBinding = view.IndexOf("$parent[controls:KeyboardView]", StringComparison.Ordinal);

            Assert.True(ancestorBinding >= 0, "The cap no longer reaches its KeyboardView at all.");
            Assert.True(
                ancestorBinding < view.IndexOf("<Grid", StringComparison.Ordinal),
                "An ancestor binding moved into the cap's content, where the presenter no longer registers.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ARemappedCap_IsAnOrdinaryRaisedCapWearingOnlyItsBar(string variantName)
        {
            // The visible behaviour change of issue #91, and the one thing here that no amount of
            // "the badge is drawn" can stand in for. `.modified` used to paint a 14% accent fill, an
            // accent border AND lift the caption to the accent — three accent signals for one state
            // on a 26px cap, on top of the bar the design actually specifies. Mockups 1e/2a draw it
            // as a plain raised cap with a 2px bar along the bottom, and this asserts exactly that:
            // the face, the border and the label colour are the RESTING ones.
            var variant = ToVariant(variantName);
            var resting = Cap(variant);
            var remapped = BadgeCap(variant, "modified");

            using var restingHost = ThemedHost.Show(resting, variant, HostWidth, HostHeight);
            using var host = ThemedHost.Show(remapped, variant, HostWidth, HostHeight);

            Assert.Equal(resting.Background, remapped.Background);
            Assert.Equal(resting.BorderBrush, remapped.BorderBrush);
            Assert.Equal(resting.Foreground, remapped.Foreground);
            Assert.Equal(DesignTokens.Resolve("SurfaceRaisedBrush", variant), remapped.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceBorderRaisedBrush", variant), remapped.BorderBrush);

            // ...and the bar is what says it, in the accent, spanning the whole bottom edge.
            var bar = RemapBarOf(remapped);

            Assert.True(bar.IsVisible, "A remapped cap drew no bar — and now has nothing at all.");
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), bar.Background);

            var frame = host.Capture();
            var origin = OriginOf(host, remapped);

            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                FramePixels.At(frame, (int)origin.X + (int)(CapWidth / 2), (int)origin.Y + (int)CapHeight - 2));

            // The face at mid-height is untouched: the bar is a mark, not a tint over the cap.
            AssertClose(
                Composite(
                    DesignTokens.ResolveBrushColor("SurfaceRaisedBrush", variant),
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant)),
                FramePixels.At(frame, (int)origin.X + 6, MidRow(host, remapped)));
        }

        [AvaloniaFact]
        public void NoStyle_LiftsAModifiedCapsCaptionToTheAccent()
        {
            // The third of the three signals, and the only one that lived outside the theme: a
            // `:is(TextBlock).keyCapText.modified` rule in Styles/Keyboard.axaml. It is read from
            // the source because a caption rule that is merely never MATCHED — the cap no longer
            // writing `Classes.modified` on the TextBlock — would leave the style sitting there
            // waiting for the next hand to re-point it.
            var styles = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Styles/Keyboard.axaml"]);

            Assert.DoesNotContain("keyCapText.modified", styles, StringComparison.Ordinal);

            // ...and the cap no longer writes the class onto its caption either. `Classes.modified`
            // survives on the BUTTON, which is what the remap bar's selector reads; the content is
            // everything from the Grid onwards.
            var view = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Controls/KeyCapView.axaml"]);
            var content = view[view.IndexOf("<Grid", StringComparison.Ordinal)..];

            Assert.DoesNotContain("Classes.modified", content, StringComparison.Ordinal);
            Assert.Contains("Classes.modified", view, StringComparison.Ordinal);
        }

        /// <summary>
        /// The four badges: the class that shows one, the part it is drawn by, the brush role it
        /// takes and the size Themes/Geometry.axaml gives it.
        /// </summary>
        public static TheoryData<string, string, string, double, double> Badges()
        {
            return new TheoryData<string, string, string, double, double>
            {
                { "modified", "RemapBar", "AccentBrush", double.NaN, 2 },
                { "macro", "MacroDot", "BadgeMacroBrush", 5, 5 },
                { "tapHold", "TapHoldMark", "BadgeTapHoldBrush", 6, 6 },
                { "advisory", "AdvisoryBar", "StatusAdvisoryStrongBrush", 12, 3 }
            };
        }

        [AvaloniaTheory]
        [MemberData(nameof(Badges))]
        public void EachBadge_IsItsOwnPartAtItsOwnSizeInItsOwnHue(
            string className,
            string partName,
            string brushKey,
            double width,
            double height)
        {
            foreach (var variant in DesignTokens.Variants)
            {
                var cap = BadgeCap(variant, className);

                using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

                var badge = BadgeOf(cap, partName);

                Assert.True(badge.IsVisible, $"'{className}' drew no {partName}.");
                Assert.Equal(DesignTokens.Resolve(brushKey, variant), PaintOf(badge));
                Assert.Equal(height, badge.Bounds.Height);

                // The remap bar is the one badge with no width of its own: it spans the whole
                // bottom edge, flush with the border rather than inset like the other three.
                if (double.IsNaN(width))
                {
                    Assert.Equal(CapWidth - 2, badge.Bounds.Width);
                }
                else
                {
                    Assert.Equal(width, badge.Bounds.Width);
                }
            }
        }

        [AvaloniaTheory]
        [MemberData(nameof(Badges))]
        public void EachBadge_IsDrawnWhereItsCornerIs(
            string className,
            string partName,
            string brushKey,
            double width,
            double height)
        {
            // The mockups inset a badge 2px from the INSIDE of the cap's 1px border. The template's
            // Panel already sits exactly there — Root's border plus its 2px padding — so an
            // alignment is the whole placement, and the arithmetic below is what proves the padding
            // and the inset have not drifted apart. The remap bar is flush instead, which is why it
            // carries the `Margin="-2"` that cancels that padding.
            _ = brushKey;

            var variant = ThemeVariant.Dark;
            var cap = BadgeCap(variant, className);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var box = BoxOf(cap, BadgeOf(cap, partName));

            if (partName == "RemapBar")
            {
                Assert.Equal(new Rect(1, CapHeight - 3, CapWidth - 2, 2), box);

                return;
            }

            var right = partName == "MacroDot" || partName == "TapHoldMark" || partName == "AdvisoryBar";

            Assert.True(right, "Every inset badge is right-aligned; the design puts none on the left.");
            Assert.Equal(CapWidth - 3 - width, box.X);
            Assert.Equal(partName == "TapHoldMark" ? CapHeight - 3 - height : 3, box.Y);
        }

        [AvaloniaTheory]
        [MemberData(nameof(Badges))]
        public void EachBadge_IsDrawnOnlyForItsOwnClass(
            string className,
            string partName,
            string brushKey,
            double width,
            double height)
        {
            _ = brushKey;
            _ = width;
            _ = height;

            // Every OTHER badge class, plus a bare cap: one mark must not answer for another, which
            // is the whole of "one badge, one hue, no legend needed twice" (mockups 1a).
            var variant = ThemeVariant.Dark;

            foreach (var other in new[] { "", "modified", "macro", "tapHold", "advisory" })
            {
                if (other == className)
                {
                    continue;
                }

                var cap = other.Length == 0 ? BadgeCap(variant) : BadgeCap(variant, other);

                using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

                Assert.False(
                    BadgeOf(cap, partName).IsVisible,
                    $"'{(other.Length == 0 ? "rest" : other)}' drew the {partName}.");
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AMacroKeyThatAlsoCarriesAnAdvisory_StepsItsDotLeftOfTheBar(string variantName)
        {
            // The collision the design does not answer: both marks are specified "top-right". The
            // bar keeps the corner — it is the state asking for attention — and the dot insets by
            // BadgeAdvisoryBarWidth + BadgeGap. Stacked, one would simply be invisible.
            var variant = ToVariant(variantName);
            var macroOnly = BadgeCap(variant, "macro");
            var both = BadgeCap(variant, "macro", "advisory");

            using var soloHost = ThemedHost.Show(macroOnly, variant, HostWidth, HostHeight);
            using var host = ThemedHost.Show(both, variant, HostWidth, HostHeight);

            var solo = BoxOf(macroOnly, BadgeOf(macroOnly, "MacroDot"));
            var dot = BoxOf(both, BadgeOf(both, "MacroDot"));
            var bar = BoxOf(both, BadgeOf(both, "AdvisoryBar"));

            // The bar did not move, the dot did, and by exactly one bar plus one gap.
            Assert.Equal(CapWidth - 3 - 12, bar.X);
            Assert.Equal(solo.X - 14, dot.X);
            Assert.Equal(solo.Y, dot.Y);

            // ...which is the same claim stated as the thing that matters: they do not touch.
            Assert.True(
                dot.Right + 2 <= bar.X,
                $"The macro dot ({dot}) is not a full BadgeGap clear of the advisory bar ({bar}).");

            // And both are still painted, which the geometry alone cannot promise.
            var frame = host.Capture();
            var origin = OriginOf(host, both);

            AssertClose(
                DesignTokens.ResolveBrushColor("BadgeMacroBrush", variant),
                FramePixels.At(frame, (int)(origin.X + dot.X + 2), (int)(origin.Y + dot.Y + 2)));
            AssertClose(
                DesignTokens.ResolveBrushColor("StatusAdvisoryStrongBrush", variant),
                FramePixels.At(frame, (int)(origin.X + bar.X + 6), (int)(origin.Y + bar.Y + 1)));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapInEveryStateAtOnce_ShowsAllFiveMarksWithoutOverlap(string variantName)
        {
            // Remapped + macro + tap-and-hold + advisory + locked, all true. Four badges on four
            // corners plus the hatch under them: the acceptance criterion, and the case where the
            // corners actually compete.
            var variant = ToVariant(variantName);
            var cap = BadgeCap(variant, "modified", "macro", "tapHold", "advisory", "locked");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var hatch = HatchOf(cap);
            var remap = BoxOf(cap, BadgeOf(cap, "RemapBar"));
            var dot = BoxOf(cap, BadgeOf(cap, "MacroDot"));
            var tapHold = BoxOf(cap, BadgeOf(cap, "TapHoldMark"));
            var advisory = BoxOf(cap, BadgeOf(cap, "AdvisoryBar"));

            Assert.True(hatch.IsVisible, "The locked hatch went away under four badges.");

            // Pairwise. The tap-and-hold triangle and the remap bar share the bottom-right and are
            // allowed to touch — the triangle's base sits on the bar's top edge — so that pair is
            // asserted as "abuts" rather than as "apart"; nothing else may meet at all.
            Assert.False(dot.Intersects(advisory), $"The macro dot {dot} overlaps the advisory bar {advisory}.");
            Assert.False(dot.Intersects(remap), $"The macro dot {dot} overlaps the remap bar {remap}.");
            Assert.False(dot.Intersects(tapHold), $"The macro dot {dot} overlaps the tap-and-hold mark {tapHold}.");
            Assert.False(advisory.Intersects(remap), $"The advisory bar {advisory} overlaps the remap bar {remap}.");
            Assert.False(advisory.Intersects(tapHold), $"The advisory bar {advisory} overlaps the tap-and-hold mark {tapHold}.");
            Assert.True(tapHold.Bottom <= remap.Y, $"The tap-and-hold mark {tapHold} runs into the remap bar {remap}.");

            // And every one of the four hues actually reaches the glass, over the hatch.
            var frame = host.Capture();
            var origin = OriginOf(host, cap);

            AssertClose(
                DesignTokens.ResolveBrushColor("BadgeMacroBrush", variant),
                FramePixels.At(frame, (int)(origin.X + dot.X + 2), (int)(origin.Y + dot.Y + 2)));
            AssertClose(
                DesignTokens.ResolveBrushColor("StatusAdvisoryStrongBrush", variant),
                FramePixels.At(frame, (int)(origin.X + advisory.X + 6), (int)(origin.Y + advisory.Y + 1)));
            AssertClose(
                DesignTokens.ResolveBrushColor("AccentBrush", variant),
                FramePixels.At(frame, (int)(origin.X + remap.X + 8), (int)(origin.Y + remap.Y + 1)));

            // The triangle's right angle is the corner, so the pixel one step in from it on the
            // diagonal is inside the ink while the opposite corner of its box is not.
            AssertClose(
                DesignTokens.ResolveBrushColor("BadgeTapHoldBrush", variant),
                FramePixels.At(frame, (int)(origin.X + tapHold.Right - 2), (int)(origin.Y + tapHold.Bottom - 2)));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ASelectedCapThatIsAlsoRemapped_KeepsBothTheRingAndTheBar(string variantName)
        {
            // The one place a badge and a ring share a pixel: the selection ring paints the cap's
            // outer 2px inward and the remap bar sits in the 2px above the border. They are the
            // same accent, so this cannot be read at the glass — it is read as "both are still
            // configured", which is the failure that would actually happen (a `:not(.selected)`
            // creeping onto the bar, or the ring being dropped on a remapped cap).
            var variant = ToVariant(variantName);
            var cap = BadgeCap(variant, "modified", "selected");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");

            Assert.Equal(1, RingOf(cap).BoxShadow.Count);
            Assert.Equal(1, RootOf(cap).BoxShadow.Count);
            Assert.True(RemapBarOf(cap).IsVisible, "The bar went away under a selection ring.");
            Assert.Equal(DesignTokens.Resolve("SurfaceKeySelectedBrush", variant), cap.Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ABadgeIsAFact_AndOutlivesEveryFaceStateAndTheDisabledBoard(string variantName)
        {
            // Same rule as the locked hatch, and for the same reason: "this key carries a macro"
            // cannot stop being true because a pointer crossed it or because a profile is loading.
            // None of the four is qualified against the ladder or against `:disabled`.
            var variant = ToVariant(variantName);

            foreach (var state in new[] { "locked", "selected", "listening" })
            {
                var cap = BadgeCap(variant, "modified", "macro", "tapHold", "advisory", state);

                using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

                SetPseudoClasses(cap, ":pointerover");

                AssertEveryBadgeVisible(cap, $"with .{state} and the pointer over it");

                cap.IsEnabled = false;

                AssertEveryBadgeVisible(cap, $"with .{state} on a disabled board");
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapOnAPictureThatShowsNoStateBadges_DrawsNoneOfTheFour(string variantName)
        {
            // The Lighting tab's answer. `.stateBadges` is the picture's, not the key's: both tabs
            // render the same cap view models, so nothing on the DataContext could separate them —
            // the same reasoning, and the same shape, as the LED strip's ShowsLedStrip.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "modified", "macro", "tapHold", "advisory", "locked");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            foreach (var partName in new[] { "RemapBar", "MacroDot", "TapHoldMark", "AdvisoryBar" })
            {
                Assert.False(
                    BadgeOf(cap, partName).IsVisible,
                    $"The {partName} was drawn on a picture that shows no state badges.");
            }

            // Locked is NOT one of the four: it says the position cannot be edited at all, which is
            // as true on a lighting board as anywhere else.
            Assert.True(HatchOf(cap).IsVisible, "The locked hatch went with the state badges.");
        }

        [AvaloniaTheory]
        [InlineData("Dark", false)]
        [InlineData("Dark", true)]
        [InlineData("Light", false)]
        [InlineData("Light", true)]
        public async Task NoLegendOnTheWholeBoard_IsCutShortByTheBoxItIsDrawnIn(
            string variantName,
            bool lighting)
        {
            // THE ONE THAT WOULD HAVE CAUGHT IT. Every assertion this file makes about a cap was
            // true while the board was unreadable: `Esc` read `Fsc`, `Space` read `Snace`, `Q` read
            // `O`, and the second line of `Caps\nLock` was gone altogether. The reason none of them
            // saw it is that they all asked the cap what it was configured to do, and a cropped
            // legend is configured perfectly — it is simply drawn into a box smaller than the text
            // needs.
            //
            // `:is(TextBlock).keyCapText` used to set `LineHeight="9"` on a 9px font and
            // `.keyCapSubText` `LineHeight="7"` on a 7px one, justified as the CSS `line-height: 1`
            // the mockups set. THE TWO ARE NOT THE SAME THING. CSS removes leading and lets glyphs
            // spill out of the line box; Avalonia SIZES the line box, pins the baseline at the
            // font's own ascent below its top, and draws nothing past `nLines x LineHeight`. IBM
            // Plex Sans is 1.3em tall and its baseline alone sits 1.025em down, so a 9px box on a
            // 9px line cut ABOVE the baseline.
            //
            // So this asserts the invariant rather than the value: EVERY LEGEND THE BOARD DRAWS IS
            // ARRANGED AT LEAST AS TALL AS THE SAME TEXT NEEDS AT ITS FONT'S NATURAL METRICS. That
            // catches a line height under the font's box, a grid row that squeezes a legend, a
            // caption that grew a line the cap has no room for, and a new device whose legends are
            // longer than this one's — none of which any per-property assertion can see.
            //
            // It runs on the REAL board, in both variants, on both pictures the editor draws: the
            // Layout board (whose caps carry the silkscreen sub-legends and the nine `\n` captions)
            // and the Lighting board (whose caps carry the LED strip instead). A per-key mode has
            // to be picked for the second — the mode rail's selection decides whether a board is
            // drawn at all, and `Disable` draws none.
            var variant = ToVariant(variantName);

            using var factory = new ViewSceneFactory();
            var editor = await factory.CreateEditorAsync().ConfigureAwait(true);

            if (lighting)
            {
                editor.SelectedTab = EditorTab.Lighting;
                editor.Lighting.SelectModeCommand.Execute(
                    editor.Lighting.Modes.Single(mode => mode.Mode == LightingMode.Freestyle));
            }

            var view = await factory.CreateAsync("KinesisEdit.Views.KeyboardEditorView").ConfigureAwait(true);

            view.DataContext = editor;

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var caps = view.GetVisualDescendants().OfType<KeyCapView>().ToList();

            Assert.NotEmpty(caps);

            var checkedLegends = 0;

            foreach (var legend in caps.SelectMany(cap => cap.GetVisualDescendants().OfType<TextBlock>()))
            {
                // IsEffectivelyVisible, not IsVisible: the sub-legend's own binding only answers
                // "does this position print anything", and the panel around it is what withholds
                // the legend on a lighting board. A legend hidden by its parent is arranged at zero
                // and would fail the comparison below for a reason that is not a crop.
                if (!legend.IsEffectivelyVisible || string.IsNullOrEmpty(legend.Text))
                {
                    continue;
                }

                checkedLegends++;

                Assert.True(
                    legend.Bounds.Height + Tolerance >= NaturalHeight(legend),
                    $"'{legend.Text.Replace("\n", "\\n")}' is drawn in {legend.Bounds.Height:F2}px "
                    + $"but needs {NaturalHeight(legend):F2}px at {legend.FontSize}px — the bottom is cut off.");
            }

            // A board that drew no legends at all would pass the loop above vacuously. The Edge RGB
            // carries 95 positions and the sub-legends on top of them.
            Assert.True(checkedLegends >= 95, $"Only {checkedLegends} legends were drawn; the board is not on screen.");

            // ...and the two pictures are NOT the same: the sub-legend is not drawn on a lighting
            // board, because it and the LED strip want the same band under the caption. Without
            // this the loop would pass on a Lighting board that had simply stopped rendering.
            Assert.Equal(lighting ? 95 : 95 + 35, checkedLegends);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheCapsLegend_TakesTheDesignsNineFourHundredKeycapStep(string variantName)
        {
            // Declared in Themes/Typography.axaml since issue #85 and unused until now, because the
            // board was drawn at up to 44px a cap and the type did not scale with it. It does now —
            // KeyboardPanel lays the board out at mock scale and BoardScaleHost grows the picture as
            // one — so 9 is the authored size rather than the drawn one.
            var variant = ToVariant(variantName);
            var cap = Cap(variant);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("FontSizeKeycapLabel", variant), cap.FontSize);
            Assert.Equal(9d, cap.FontSize);
            Assert.Equal(FontWeight.Normal, cap.FontWeight);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapWithASilkscreenSubLegend_DrawsItSmallAndDimBelowTheCaption(string variantName)
        {
            // The shifted character or the device hotkey the board PRINTS ('!', 'mute', 'scr lk'),
            // stacked UNDER the caption and centred on it — mockup 1e's own column (`F1` over
            // `mute`, `align-items: center`). The order is the hierarchy: what the key does now
            // leads, what the board printed on it follows. It used to be drawn above, which
            // inverted that.
            var variant = ToVariant(variantName);
            var view = KeyCap(overlay: null, showsLedStrip: false, legend: "1", secondaryLegend: "!", atUnitSize: true);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var caption = CaptionOf(view);
            var sub = SubLegendOf(view);

            Assert.True(sub.IsVisible, "A cap with a silkscreen sub-legend drew none.");
            Assert.Equal("!", sub.Text);
            Assert.Equal(DesignTokens.Resolve("TextSecondaryBrush", variant), sub.Foreground);
            Assert.True(sub.FontSize < caption.FontSize, "The sub-legend is not a step under the caption.");

            var subBox = BoxOf(view, sub);
            var captionBox = BoxOf(view, caption);

            Assert.True(subBox.Y > captionBox.Y, "The sub-legend is drawn above the caption.");
            Assert.InRange(subBox.Center.X, (UnitCapWidth / 2) - 1, (UnitCapWidth / 2) + 1);

            // The pair is FLUSH: the silkscreen's line box opens exactly where the caption's ends.
            // That is the whole of the `Margin="0,-2,0,0"` on `.keyCapSubText` — the legend's row is
            // 2px shorter than its own box, so it hangs up into the leading the caption's last line
            // does not use, and the pair costs 20 of the cap's 24 rather than 22. A positive gap
            // here would mean the room has been given back; a negative one, that the two collide.
            Assert.InRange(subBox.Y - captionBox.Bottom, 0, 1);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapCarryingBothASilkscreenAndABadge_KeepsItsCaptionOutFromUnderTheMark(string variantName)
        {
            // The collision the stack order creates and the shared leading resolves. With the
            // caption leading, a cap that also prints a silkscreen has 20 of its 24 rows spoken for
            // and the caption sits near the top — where the advisory bar and the macro dot live
            // (2..5px inside the cap's border). The badges are drawn last and opaque, so an
            // overlap here is not a clip that a metrics test would catch: it is a mark painted
            // across the top of the legend it is meant to annotate.
            var variant = ToVariant(variantName);
            var view = KeyCap(overlay: null, showsLedStrip: false, legend: "1", secondaryLegend: "!", atUnitSize: true);
            var model = (KeyboardKeyViewModel)view.DataContext!;

            model.HasAdvisory = true;

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var caption = CaptionOf(view);
            var bar = BoxOf(view, BadgeOf(ButtonOf(view), "AdvisoryBar"));

            Assert.True(bar.Height > 0, "The advisory bar was not drawn, so this proves nothing.");

            // Against the caption's CAP HEIGHT rather than its line box: the box opens with the
            // font's leading, and it is the ink the mark must not land on. Plex Sans puts a capital
            // 0.698em below the top of the line and the baseline 1.025em below it.
            var inkTop = BoxOf(view, caption).Y + ((1.025 - 0.698) * caption.FontSize);

            Assert.True(
                bar.Bottom <= inkTop + 0.5,
                $"The advisory bar ends at {bar.Bottom:F2} and the caption's glyphs start at {inkTop:F2}.");
        }

        [AvaloniaFact]
        public void ACapWhosePrintSaysNothingNew_KeepsItsCaptionCentred()
        {
            // Most positions carry no sub-legend at all — the print is what the caption already
            // says — so the row has to collapse rather than reserve space nobody asked for.
            var withSub = KeyCap(overlay: null, showsLedStrip: false, legend: "1", secondaryLegend: "!", atUnitSize: true);
            var without = KeyCap(overlay: null, showsLedStrip: false, legend: "1", secondaryLegend: null, atUnitSize: true);

            using var subHost = ThemedHost.Show(withSub, ThemeVariant.Dark, HostWidth, HostHeight);
            using var host = ThemedHost.Show(without, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.False(SubLegendOf(without).IsVisible, "A cap with no silkscreen drew an empty legend row.");

            var centred = BoxOf(without, CaptionOf(without));

            Assert.InRange(centred.Center.Y, (UnitCapHeight / 2) - 1, (UnitCapHeight / 2) + 1);

            // The sub-legend now takes its room BELOW the caption, so a cap that carries one draws
            // its caption HIGHER than a cap that does not. If the row failed to collapse the two
            // would sit at the same height and the legend would be drawn over the caption.
            Assert.True(
                BoxOf(withSub, CaptionOf(withSub)).Center.Y < centred.Center.Y,
                "The sub-legend took no room, so it is drawn over the caption.");
        }

        [AvaloniaFact]
        public void TheCapView_WritesTheKeysFourBadgeClassesAndThePicturesSwitch()
        {
            // The two answers, kept apart. The four classes say what the KEY is and are written
            // whatever the surface is; `stateBadges` says whether this SURFACE draws them, and is
            // the only thing ShowsStateBadge moves.
            var view = KeyCap(overlay: null, showsLedStrip: false, legend: "1", secondaryLegend: null);
            var key = ((KeyboardKeyViewModel)view.DataContext!).Key;

            key.ApplyRemap(TestLayouts.Gen1Key("z"));
            key.SetMacro(1, new Macro());
            key.ApplyTapAndHold(TestLayouts.Gen1Key("a"), TestLayouts.Gen1Key("b"), 250);
            ((KeyboardKeyViewModel)view.DataContext!).RefreshFromModel();
            ((KeyboardKeyViewModel)view.DataContext!).HasAdvisory = true;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, HostWidth, HostHeight);

            var cap = ButtonOf(view);

            Assert.Contains("modified", cap.Classes);
            Assert.Contains("macro", cap.Classes);
            Assert.Contains("tapHold", cap.Classes);
            Assert.Contains("advisory", cap.Classes);
            Assert.Contains("stateBadges", cap.Classes);

            AssertEveryBadgeVisible(cap, "on a picture that shows state badges");

            view.ShowsStateBadge = false;

            Assert.DoesNotContain("stateBadges", cap.Classes);

            // The key's own four classes are untouched — only the picture changed its mind.
            Assert.Contains("modified", cap.Classes);
            Assert.Contains("macro", cap.Classes);
            Assert.Contains("tapHold", cap.Classes);
            Assert.Contains("advisory", cap.Classes);

            foreach (var partName in new[] { "RemapBar", "MacroDot", "TapHoldMark", "AdvisoryBar" })
            {
                Assert.False(BadgeOf(cap, partName).IsVisible, $"{partName} survived ShowsStateBadge = false.");
            }
        }

        [AvaloniaFact]
        public void TurningTheStateBadgesOff_LeavesTheLedStripAlone()
        {
            // The two picture-level switches are independent: the Lighting tab turns the badges off
            // and the strip on, and nothing about either answer may reach the other.
            var view = KeyCap(overlay: "#FF0000", showsLedStrip: true, legend: "1", secondaryLegend: null);

            view.ShowsStateBadge = false;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, HostWidth, HostHeight);

            var strip = StripOf(view);

            Assert.True(strip.IsVisible, "Turning the badges off took the LED strip with them.");
            Assert.True(ColorPatchOf(strip).IsVisible, "A lit key lost its colour with the badges.");
            Assert.True(CaptionOf(view).IsVisible, "Turning the badges off took the legend with them.");
        }

        /// <summary>Fails unless all four badges are drawn on <paramref name="cap"/>.</summary>
        private static void AssertEveryBadgeVisible(Button cap, string context)
        {
            foreach (var partName in new[] { "RemapBar", "MacroDot", "TapHoldMark", "AdvisoryBar" })
            {
                Assert.True(BadgeOf(cap, partName).IsVisible, $"The {partName} went away {context}.");
            }
        }

        /// <summary>One pixel of each concentric band, for one combination of states.</summary>
        private readonly record struct RingSample(Color Inside, Color Outside);

        private static RingSample Sample(ThemeVariant variant, string[] classes, bool focused, bool hovered = false)
        {
            var cap = Cap(variant, classes);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            if (focused)
            {
                Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");
            }

            // Hover paints the selected face and no ring, which is the only way to hold the face
            // constant while the selection ring is the variable under test.
            if (hovered)
            {
                SetPseudoClasses(cap, ":pointerover");
            }

            var frame = host.Capture();
            var origin = OriginOf(host, cap);
            var row = MidRow(host, cap);

            return new RingSample(
                FramePixels.At(frame, (int)origin.X + InsideProbe, row),
                FramePixels.At(frame, (int)origin.X + OutsideProbe, row));
        }

        private static void AssertFacePaints(string state, string expectedKey, ThemeVariant variant)
        {
            var cap = Cap(variant, state == "rest" || state == "hover" ? [] : [state]);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            // After Show, not before: Button re-syncs `:pressed` from IsPressed when its template is
            // applied, and the same pass would drop a pseudo-class raised beforehand.
            if (state == "hover")
            {
                SetPseudoClasses(cap, ":pointerover");
            }

            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant);
            var expected = Composite(DesignTokens.ResolveBrushColor(expectedKey, variant), canvas);
            var frame = host.Capture();

            AssertClose(expected, FramePixels.At(frame, frame.PixelSize.Width / 2, frame.PixelSize.Height / 2));
        }

        /// <summary>A bare cap wearing <paramref name="classes"/>, on the key cap's own theme.</summary>
        private static Button Cap(ThemeVariant variant, params string[] classes)
        {
            var cap = new Button
            {
                // The bridge from `Button.keyCap` to this theme lives in Styles/ and is somebody
                // else's file; naming the theme directly keeps these tests about the theme.
                Theme = (ControlTheme)DesignTokens.Resolve(ThemeKey, variant),
                Width = CapWidth,
                Height = CapHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            foreach (var className in classes)
            {
                cap.Classes.Add(className);
            }

            return cap;
        }

        /// <summary>
        /// A bare cap on a picture that <b>does</b> draw state badges — <c>stateBadges</c> plus
        /// <paramref name="classes"/>. Every badge selector is qualified by that class, so a test
        /// about a badge has to write it; <see cref="Cap"/> deliberately does not, which is what
        /// makes it the "lighting picture" case.
        /// </summary>
        private static Button BadgeCap(ThemeVariant variant, params string[] classes)
        {
            return Cap(variant, [.. classes, "stateBadges"]);
        }

        /// <summary>
        /// A real <see cref="KeyCapView"/> over a cap view model carrying <paramref name="overlay"/>,
        /// on a picture that is or is not showing lighting. The second argument is the surface's
        /// answer, not the key's: <see cref="KeyboardView"/> hands it down, and a cap hosted alone
        /// takes the property's own default of false.
        /// </summary>
        private static KeyCapView KeyCap(
            string? overlay,
            bool showsLedStrip,
            string? legend = null,
            string? secondaryLegend = null,
            bool atUnitSize = false)
        {
            var key = TestLayouts.CreateLayout("esc").Layers[0].Keys[0];
            var visual = new KeyVisual(key.Index, 0, 0, legend: legend, secondaryLegend: secondaryLegend);
            var model = new KeyboardKeyViewModel(key, visual, TokenDialect.Gen1)
            {
                ColorOverlayHex = overlay
            };

            return new KeyCapView
            {
                DataContext = model,
                ShowsLedStrip = showsLedStrip,
                Width = atUnitSize ? UnitCapWidth : CapWidth,
                Height = atUnitSize ? UnitCapHeight : CapHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static Border RootOf(Button cap)
        {
            return NamedPart<Border>(cap, "Root");
        }

        private static Border RingOf(Button cap)
        {
            return NamedPart<Border>(cap, "Ring");
        }

        private static Rectangle HatchOf(Button cap)
        {
            return NamedPart<Rectangle>(cap, "Locked");
        }

        private static Border RemapBarOf(Button cap)
        {
            return NamedPart<Border>(cap, "RemapBar");
        }

        /// <summary>One of the four badge parts by name, whatever shape it happens to be.</summary>
        private static Control BadgeOf(Button cap, string partName)
        {
            return cap.GetVisualDescendants().OfType<Control>().Single(part => part.Name == partName);
        }

        /// <summary>The brush a badge paints with, whether it is a <c>Border</c> or a <c>Shape</c>.</summary>
        private static IBrush? PaintOf(Control badge)
        {
            return badge switch
            {
                Border border => border.Background,
                Shape shape => shape.Fill,
                _ => throw new InvalidOperationException($"{badge.Name} is neither a Border nor a Shape.")
            };
        }

        /// <summary>
        /// What <paramref name="legend"/>'s text needs at its own font's <b>natural</b> metrics —
        /// the same typeface, size, weight and wrapping, laid out across the width it was actually
        /// given, and with no line height imposed on it.
        /// <para>
        /// Measured through a fresh <see cref="TextLayout"/> rather than by re-measuring the
        /// control, because the control's own <c>DesiredSize</c> already carries whatever
        /// <c>LineHeight</c> a style put on it: a cropped legend desires exactly the box that is
        /// cropping it, which is why the defect this guards survived the suite.
        /// </para>
        /// </summary>
        private static double NaturalHeight(TextBlock legend)
        {
            return new TextLayout(
                legend.Text,
                new Typeface(legend.FontFamily, legend.FontStyle, legend.FontWeight, legend.FontStretch),
                legend.FontSize,
                legend.Foreground,
                textWrapping: legend.TextWrapping,
                maxWidth: legend.Bounds.Width).Height;
        }

        /// <summary>Where <paramref name="part"/> sits in <paramref name="root"/>'s own coordinates.</summary>
        private static Rect BoxOf(Visual root, Visual part)
        {
            var origin = part.TranslatePoint(new Point(0, 0), root)
                ?? throw new InvalidOperationException("The part is not in that control's visual tree.");

            return new Rect(origin, part.Bounds.Size);
        }

        private static T NamedPart<T>(Button cap, string name) where T : Visual
        {
            return cap.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);
        }

        /// <summary>A run of pixels along the LED strip's own row, left to right.</summary>
        private static IReadOnlyList<Color> RowSamples(WriteableBitmap frame, Point stripOrigin)
        {
            var samples = new List<Color>();

            for (var offset = 2; offset < 16; offset++)
            {
                samples.Add(FramePixels.At(frame, (int)stripOrigin.X + offset, (int)stripOrigin.Y + 1));
            }

            return samples;
        }

        private static Button ButtonOf(KeyCapView view)
        {
            return view.GetVisualDescendants().OfType<Button>().Single();
        }

        /// <summary>
        /// The cap's LED strip: the 3px band under the caption. Nameless, which is how it is told
        /// apart from the advisory badge — that bar is 3px tall too, and it is a template PART,
        /// which is exactly the distinction that matters here.
        /// </summary>
        private static Border StripOf(KeyCapView view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Height == 3 && border.Name is null);
        }

        /// <summary>The cap's own caption — what the position reads right now.</summary>
        private static TextBlock CaptionOf(KeyCapView view)
        {
            return TextOf(view, "keyCapText");
        }

        /// <summary>The silkscreen sub-legend: what the board prints, whatever the key now does.</summary>
        private static TextBlock SubLegendOf(KeyCapView view)
        {
            return TextOf(view, "keyCapSubText");
        }

        private static TextBlock TextOf(KeyCapView view, string className)
        {
            return view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(text => text.Classes.Contains(className));
        }

        /// <summary>The colour patch that covers the strip's hatch when the key is lit.</summary>
        private static Border ColorPatchOf(Border strip)
        {
            return Assert.IsType<Border>(strip.Child);
        }

        /// <summary>
        /// The colour of the cap's outermost pixel column, at mid-height: on its own border, and
        /// the one column an inward ring either reaches or does not.
        /// </summary>
        private static Color EdgePixel(ThemedHost host, Button cap)
        {
            return FramePixels.At(host.Capture(), (int)OriginOf(host, cap).X, MidRow(host, cap));
        }

        private static Point OriginOf(ThemedHost host, Visual control)
        {
            return control.TranslatePoint(new Point(0, 0), host.Window)
                ?? throw new InvalidOperationException("The control is not in the window's visual tree.");
        }

        private static int MidRow(ThemedHost host, Control control)
        {
            var probe = control.TranslatePoint(new Point(0, control.Bounds.Height / 2), host.Window)
                ?? throw new InvalidOperationException("The control is not in the window's visual tree.");

            return (int)probe.Y;
        }

        /// <summary>
        /// Raises pseudo-classes by hand; the headless session has no pointer, so <c>:pointerover</c>
        /// is set through the same interface Avalonia's own input code uses.
        /// </summary>
        private static void SetPseudoClasses(Button cap, params string[] pseudoClasses)
        {
            var classes = (IPseudoClasses)cap.Classes;

            foreach (var pseudoClass in pseudoClasses)
            {
                classes.Set(pseudoClass, true);
            }
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>Source-over composite of a possibly translucent colour onto an opaque one.</summary>
        private static Color Composite(Color source, Color background)
        {
            var alpha = source.A / 255d;

            return Color.FromRgb(
                (byte)Math.Round((source.R * alpha) + (background.R * (1 - alpha))),
                (byte)Math.Round((source.G * alpha) + (background.G * (1 - alpha))),
                (byte)Math.Round((source.B * alpha) + (background.B * (1 - alpha))));
        }

        /// <summary>Channel-sum distance, so a rounding difference is not a failure.</summary>
        private static int Distance(Color left, Color right)
        {
            return Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);
        }

        private static void AssertClose(Color expected, Color actual)
        {
            Assert.True(Distance(expected, actual) <= 3, $"Expected about {expected}, painted {actual}.");
        }
    }
}
