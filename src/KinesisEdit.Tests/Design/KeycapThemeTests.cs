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
using CommunityToolkit.Mvvm.Input;
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
    /// The key cap's control theme — <c>Themes/ControlThemes/Keycap.axaml</c> — plus the lit face
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
        /// How far inside the cap's edge the selection ring's <b>accent band</b> is sampled. The
        /// outer pixel column is the cap's own 1px border; column 1 is the first of the face, and
        /// the band covers both.
        /// </summary>
        private const int InsideProbe = 1;

        /// <summary>
        /// The third column in, where the ring's contrasting hairline is drawn — the half of the
        /// band that carries the signal when the key is lit a blue the accent cannot beat.
        /// </summary>
        private const int HairlineProbe = 2;

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
            // Selection's 3px ring is drawn INWARD from the cap's edge, which is what leaves the 4px
            // gap between caps free for the focus halo — and is the whole reason the two can be told
            // apart when they are both on. The band grew from 2px to 3px with issue #128 and stayed
            // inward, so this is the same claim it always was.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "selected");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var ring = RingOf(cap).BoxShadow;

            // TWO shadows, and the order is load-bearing: Skia draws outer box shadows in list
            // order, so the 1px hairline listed second lands ON TOP of the inner pixel of the 3px
            // accent spread rather than under it. Swap them and the ring is a plain accent band.
            Assert.Equal(2, ring.Count);
            Assert.Equal(DesignTokens.ResolveColor("AccentKeyRingColor", variant), ring[0].Color);
            Assert.Equal(3, ring[0].Spread);
            Assert.Equal(DesignTokens.ResolveColor("KeycapRingEdgeColor", variant), ring[1].Color);
            Assert.Equal(1, ring[1].Spread);
            Assert.Equal(0, ring[0].OffsetX);
            Assert.Equal(0, ring[0].OffsetY);
            Assert.Equal(0, ring[1].OffsetX);
            Assert.Equal(0, ring[1].OffsetY);

            var frame = host.Capture();
            var origin = OriginOf(host, cap);
            var row = MidRow(host, cap);

            // Nothing at all outside the cap...
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                FramePixels.At(frame, (int)origin.X + OutsideProbe, row));

            // ...the accent band inside it, OPAQUE — not a wash over the selected face, which is
            // exactly what stopped reading on a lit cap (#128)...
            AssertClose(
                DesignTokens.ResolveBrushColor("AccentKeyRingBrush", variant),
                FramePixels.At(frame, (int)origin.X + InsideProbe, row));

            // ...and the contrasting hairline one pixel further in.
            AssertClose(
                DesignTokens.ResolveBrushColor("KeycapRingEdgeBrush", variant),
                FramePixels.At(frame, (int)origin.X + HairlineProbe, row));
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

            // One halo, and the ring's own two: the failure this guards is the two STATES
            // collapsing onto one element, where they would sum into a single denser band instead
            // of a concentric pair. The ring's two are one state's two tones, not two states.
            Assert.Equal(1, halo.Count);
            Assert.Equal(2, ring.Count);

            // 3px at 28% outward, 3px opaque inward, which is what keeps them legible as two marks
            // rather than as one thick one — and they are now told apart by ALPHA as well as by
            // which side of the edge they fall on.
            Assert.Equal(3, halo[0].Spread);
            Assert.Equal(3, ring[0].Spread);
            Assert.Equal(DesignTokens.ResolveColor("AccentFocusHaloColor", variant), halo[0].Color);
            Assert.Equal(DesignTokens.ResolveColor("AccentKeyRingColor", variant), ring[0].Color);
            Assert.Equal(255, ring[0].Color.A);
            Assert.NotEqual(255, halo[0].Color.A);
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

            // Three pixels in, exactly as on a cap with a 1px border, so the 3px spread lands on
            // the cap's outer 3px in both cases.
            Assert.Equal(3, inset.X);
            Assert.Equal(3, inset.Y);
            Assert.Equal(cap.Bounds.Width - 6, ring.Bounds.Width);
            Assert.Equal(2, ring.BoxShadow.Count);

            // The PAINT ring moves with it, and for a reason that is not symmetry: the two boards
            // share their cap view models, so a listening cap can be paint-selected as well, and a
            // ring left behind by the thicker border would float on exactly the cap being pressed.
            var paintRing = PaintRingOf(cap);
            var paintInset = paintRing.TranslatePoint(new Point(0, 0), cap)
                ?? throw new InvalidOperationException("The paint ring is not in the cap's visual tree.");

            Assert.Equal(inset, paintInset);
            Assert.Equal(ring.Bounds, paintRing.Bounds);

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
            // ShowsLighting says. Given that, the hatch is the bottom of the three face layers and
            // the paint and the effect cover it when there are any.
            //
            // It is the cap's WHOLE FACE now, not the 3px strip it was until issue #94: a bar that
            // thin cannot read as a travelling wave, which is the thing the board exists to show.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var layers = FillLayersOf(view);

            Assert.True(FillOf(view).IsVisible, "The lit face vanished on a lighting board.");
            Assert.Equal(DesignTokens.Resolve("HatchBrush", variant), layers.Hatch.Background);
            Assert.False(layers.PaintUnder.IsVisible, "An unpainted key drew a paint layer.");
            Assert.False(layers.PaintOver.IsVisible, "An unpainted key drew a paint layer over the effect.");
            Assert.False(layers.Effect.IsVisible, "An unlit key drew an effect layer.");

            var frame = host.Capture();
            var stripe = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", variant);
            var painted = FaceSamples(host, view);

            Assert.Contains(painted, sample => Distance(sample, stripe) <= 24);
            Assert.DoesNotContain(painted, sample => Distance(sample, Colors.Black) <= 24);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapOnABoardThatIsNotShowingLighting_DrawsNoFillAtAll(string variantName)
        {
            // The third state, and the regression that made it necessary: the hatch answers "this
            // key is off", never "lighting is not on screen". On the Keys tab — where nobody asked
            // about lighting — roughly a hundred caps were each carrying a hatched bar; a hatched
            // FACE would be a hundred times worse.
            //
            // Read at the glass as well as in the graph, because a fill that is present and painted
            // with the cap's own face would satisfy the property and still be a texture: the cap
            // must be its resting face exactly, one flat colour end to end.
            var variant = ToVariant(variantName);
            var lit = KeyCap(showsLighting: true);

            using var litHost = ThemedHost.Show(lit, variant, HostWidth, HostHeight);

            var hatched = FaceSamples(litHost, lit);

            // The control: on a lighting board the face is a hatch, so it is not one colour.
            Assert.True(hatched.Distinct().Count() > 1, $"The hatch painted one flat {hatched[0]}.");

            var plain = KeyCap(showsLighting: false);

            using var plainHost = ThemedHost.Show(plain, variant, HostWidth, HostHeight);

            Assert.False(FillOf(plain).IsVisible, "The Keys tab drew a lit face.");

            var bare = FaceSamples(plainHost, plain);
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
            // The other half: the hatch means "off", so a key the effect is lighting must not show
            // any of it through. At full intensity the face is that colour and nothing else — read
            // across the whole width, so a fill that covered only part of the cap would fail. "That
            // colour" is the softened one since #124: the cap is handed the sampler's value and
            // paints the preview's, and this is the assertion that says the two are not the same.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, effect: "#FF0000");

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var effect = FillLayersOf(view).Effect;

            Assert.True(effect.IsVisible, "A lit key drew no colour.");
            Assert.Equal(1d, effect.Opacity);
            Assert.Equal(
                Softened("#FF0000"),
                Assert.IsAssignableFrom<ISolidColorBrush>(effect.Background).Color);
            Assert.NotEqual(Color.FromRgb(0xFF, 0x00, 0x00), Softened("#FF0000"));

            Assert.All(FaceSamples(host, view), sample => AssertClose(Softened("#FF0000"), sample));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APaintedKey_UnderAModeThatIgnoresPaint_IsDrawnAtFortyPercent(string variantName)
        {
            // Mockup 2f, verbatim: "Wave ignores painted colors, so the paint layer is shown at 40%
            // under the effect — the colors are still on file. Solid, Reactive, Ripple and Starlight
            // render the paint directly." The 40% is the VIEW MODEL's answer (PaintOpacity), so what
            // this asserts is that the cap actually draws at whatever it is handed, and that the
            // result at the glass really is a wash rather than the full colour.
            var variant = ToVariant(variantName);
            var ignored = KeyCap(showsLighting: true, paint: "#FF0000", paintOpacity: 0.4);
            var rendered = KeyCap(showsLighting: true, paint: "#FF0000");
            var unpainted = KeyCap(showsLighting: true);

            using var ignoredHost = ThemedHost.Show(ignored, variant, HostWidth, HostHeight);
            using var renderedHost = ThemedHost.Show(rendered, variant, HostWidth, HostHeight);
            using var unpaintedHost = ThemedHost.Show(unpainted, variant, HostWidth, HostHeight);

            Assert.Equal(0.4, FillLayersOf(ignored).Paint.Opacity);
            Assert.Equal(1d, FillLayersOf(rendered).Paint.Opacity);

            // ...and the dimmed one is the layer OVER the effect while the full one is the layer
            // under it. Which side is the whole of how the 40% survives — see the pair below.
            Assert.True(FillLayersOf(ignored).PaintOver.IsVisible);
            Assert.False(FillLayersOf(ignored).PaintUnder.IsVisible);
            Assert.True(FillLayersOf(rendered).PaintUnder.IsVisible);
            Assert.False(FillLayersOf(rendered).PaintOver.IsVisible);

            // The hatch under the paint makes any single pixel a coin toss, so the comparison is on
            // the mean across the cap's whole width — which is what the eye reads anyway.
            var washed = MeanFace(ignoredHost, ignored);
            var full = MeanFace(renderedHost, rendered);
            var bare = MeanFace(unpaintedHost, unpainted);

            AssertClose(Softened("#FF0000"), full);

            // The wash lies BETWEEN the unpainted cap and the full colour, which is what "40%"
            // means at the glass — it has left the hatch, and it has not arrived at the paint.
            //
            // Stated as distances rather than as a red channel on purpose. Until #124 the full face
            // was #FF0000, redder than any cap surface in either theme, so "washed.R < full.R" was
            // a fair reading of it; softened to #A41010 the full face is DARKER than the light
            // theme's own cap, and the wash of it is the redder of the two. The claim was never
            // about a channel — it is about where the wash sits between two faces — and said that
            // way it holds in both variants and survives the next re-tune of the tint.
            Assert.True(
                Distance(washed, bare) < Distance(full, bare),
                $"The 40% paint is as far from the bare cap as the full one ({washed}).");
            Assert.True(
                Distance(washed, bare) > Distance(full, bare) / 4,
                $"The 40% paint barely reached the cap ({washed}).");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APaintedKey_UnderAFullCoverageEffectThatIgnoresPaint_CompositesTheSixtyFortyBlend(
            string variantName)
        {
            // THE DEFECT THIS PAIR EXISTS FOR. Wave, Solid and Spectrum light EVERY key at intensity
            // 1.0, so a paint layer drawn under them is covered outright and the 40% mockup 2f
            // promises renders as 0% — every assertion about the layer's own opacity still passing.
            // The same layer composited over the effect is the blend the sentence describes.
            var variant = ToVariant(variantName);
            var painted = KeyCap(showsLighting: true, paint: "#0000FF", paintOpacity: 0.4, effect: "#00FF00");

            using var host = ThemedHost.Show(painted, variant, HostWidth, HostHeight);

            var layers = FillLayersOf(painted);

            // The effect keeps its own intensity: the paint over it is what takes the 40%.
            Assert.Equal(1d, layers.Effect.Opacity);
            Assert.Equal(0.4, layers.PaintOver.Opacity);
            Assert.True(layers.PaintOver.IsVisible);

            // effect·0.6 + paint·0.4 over a face the effect covers completely — read at the glass,
            // because the ORDER of the two layers is the whole of this fix and no property says it.
            // Both terms are the softened faces (#124): the blend is between what is DRAWN, and the
            // arithmetic is what this test is about.
            AssertClose(Blend(Softened("#00FF00"), Softened("#0000FF"), 0.4), MeanFace(host, painted));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnUnpaintedKey_UnderTheSameEffect_KeepsItAtFullStrength(string variantName)
        {
            // The other half of the rule, and the reason the blend is per-key rather than a wash
            // over the board: dimming a preview that has nothing to reveal would only weaken it.
            var variant = ToVariant(variantName);
            var bare = KeyCap(showsLighting: true, paintOpacity: 0.4, effect: "#00FF00");

            using var host = ThemedHost.Show(bare, variant, HostWidth, HostHeight);

            var layers = FillLayersOf(bare);

            Assert.Equal(1d, layers.Effect.Opacity);
            Assert.False(layers.PaintOver.IsVisible, "An unpainted key drew a paint layer over the effect.");
            Assert.False(layers.PaintUnder.IsVisible, "An unpainted key drew a paint layer under the effect.");

            AssertClose(Softened("#00FF00"), MeanFace(host, bare));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APaintedKey_UnderAPaintDirectMode_IsUnchanged(string variantName)
        {
            // Freestyle, Breathe and Frozen Wave hand the paint down at 1.0, and there the effect
            // layer already CARRIES the painted colour — it is the paint, modulated by the mode's
            // own animation. Moving that layer over the effect, or holding the effect back to 60%,
            // would flatten Breathe's pulse into a constant wash.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, paint: "#0000FF", effect: "#0000FF", effectIntensity: 0.5);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var layers = FillLayersOf(view);

            Assert.True(layers.PaintUnder.IsVisible);
            Assert.False(layers.PaintOver.IsVisible);
            Assert.Equal(1d, layers.PaintUnder.Opacity);
            Assert.Equal(0.5, layers.Effect.Opacity);

            // The paint under covers the hatch, so the face is that colour whatever the effect above
            // is doing — which is what makes a Breathe cap fade rather than dissolve into a texture.
            AssertClose(Softened("#0000FF"), MeanFace(host, view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnEffectAtPartialIntensity_LetsThePaintUnderItShowThrough(string variantName)
        {
            // The three layers are a stack, not a switch: paint on the hatch, effect over paint. A
            // key painted blue that a red effect is half-lighting reads as both, which is what makes
            // "the colors are still on file" visible while an effect runs over them.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, paint: "#0000FF", effect: "#FF0000", effectIntensity: 0.5);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var layers = FillLayersOf(view);

            Assert.True(layers.Paint.IsVisible);
            Assert.Equal(0.5, layers.Effect.Opacity);

            var mixed = MeanFace(host, view);

            Assert.True(mixed.R > 60, $"The effect did not reach the cap ({mixed}).");
            Assert.True(mixed.B > 60, $"The paint under the effect was covered outright ({mixed}).");
        }

        [AvaloniaFact]
        public void ALitKey_OnABoardThatIsNotShowingLighting_StillDrawsNothing()
        {
            // The two answers are independent, and this is the pair that proves it: a key can carry
            // paint on a picture that draws no lighting at all. The colour is a fact about the KEY —
            // the Keys tab keeps computing it, because it shares the cap view models with the
            // lighting board — and drawing it is a fact about the SURFACE.
            var view = KeyCap(showsLighting: false, paint: "#FF0000");

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.False(FillOf(view).IsVisible, "A painted key put a lit face on a board that shows none.");
            Assert.True(
                ((KeyboardKeyViewModel)view.DataContext!).HasPaintColor,
                "The scene did not actually paint the key.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ALockedCap_OnALightingBoard_KeepsItsOutlineOverTheColour(string variantName)
        {
            // The one collision the face introduced. `Rectangle#Locked` is drawn BEFORE the cap's
            // content in the template, so a lit locked key would have its hatch and its dashed edge
            // painted over by the colour and stop reading as locked at all. The theme answers it by
            // lifting the outline above the fill and dropping its hatch — the face is already
            // hatched under the colour — which leaves exactly the part the colour would have taken.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "locked", "lighting");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var hatch = HatchOf(cap);

            Assert.True(hatch.IsVisible, "The locked outline went away on a lighting board.");
            Assert.Equal(1, hatch.ZIndex);
            Assert.Null(hatch.Fill);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineHighBrush", variant), hatch.Stroke);
            Assert.NotEmpty(hatch.StrokeDashArray!);

            // ...and on the Layout board nothing moved: the hatch is the locked fill there.
            var layout = Cap(variant, "locked");

            using var layoutHost = ThemedHost.Show(layout, variant, HostWidth, HostHeight);

            Assert.Equal(0, HatchOf(layout).ZIndex);
            Assert.Equal(DesignTokens.Resolve("HatchBrush", variant), HatchOf(layout).Fill);
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

            Assert.Equal(2, RingOf(cap).BoxShadow.Count);
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
            // the same reasoning, and the same shape, as the lit face's ShowsLighting.
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
            // and the Lighting board (whose caps carry the mode on their FACES instead, and draw
            // no sub-legend at all). A per-key mode has to be picked for the second — the mode
            // rail's selection decides whether a board is drawn at all, and `Disable` draws none.
            //
            // The lighting board's budget got EASIER with issue #94 rather than harder: the colour
            // used to be a 3px strip in a row of its own, which left a stacked caption exactly 0px
            // of slack, and it is now the face — so both pictures have the same 24px for legends.
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
            // board, because the face under it is the key's own colour and a 7px silkscreen legend
            // over an arbitrary lit colour is not readable. Without
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
            var view = KeyCap(showsLighting: false, legend: "1", secondaryLegend: "!", atUnitSize: true);

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
            var view = KeyCap(showsLighting: false, legend: "1", secondaryLegend: "!", atUnitSize: true);
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
            var withSub = KeyCap(showsLighting: false, legend: "1", secondaryLegend: "!", atUnitSize: true);
            var without = KeyCap(showsLighting: false, legend: "1", secondaryLegend: null, atUnitSize: true);

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
            var view = KeyCap(showsLighting: false, legend: "1", secondaryLegend: null);
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
        public void TheCapView_WritesThePaintSelectionAsItsOwnClass()
        {
            // The two selections are separate all the way out to the view: `IsSelected` is the
            // inspector's single key and `IsLightingSelected` the lighting board's set, and one
            // must never write the other's class — the two boards share these view models, so a
            // paint selection that raised `.selected` would move the inspector.
            var view = KeyCap(showsLighting: true, paint: "#6F3BE2", legend: "1");
            var model = (KeyboardKeyViewModel)view.DataContext!;

            model.IsLightingSelected = true;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, HostWidth, HostHeight);

            var cap = Live(view);

            Assert.Contains("paintSelected", cap.Classes);
            Assert.DoesNotContain("selected", cap.Classes);
            Assert.Equal(2, PaintRingOf(cap).BoxShadow.Count);
            Assert.Equal(0, RingOf(cap).BoxShadow.Count);

            model.IsLightingSelected = false;

            Assert.DoesNotContain("paintSelected", cap.Classes);
            Assert.Equal(0, PaintRingOf(cap).BoxShadow.Count);
        }

        [AvaloniaFact]
        public void TurningTheStateBadgesOff_LeavesTheLitFaceAlone()
        {
            // The two picture-level switches are independent: the Lighting tab turns the badges off
            // and the colour on, and nothing about either answer may reach the other.
            var view = KeyCap(showsLighting: true, paint: "#FF0000", legend: "1", secondaryLegend: null);

            view.ShowsStateBadge = false;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, HostWidth, HostHeight);

            Assert.True(FillOf(view).IsVisible, "Turning the badges off took the lit face with them.");
            Assert.True(FillLayersOf(view).Paint.IsVisible, "A painted key lost its colour with the badges.");
            Assert.True(CaptionOf(view).IsVisible, "Turning the badges off took the legend with them.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APaintSelectedCap_WearsTheSelectionRingWithoutTakingTheOtherOne(string variantName)
        {
            // The Lighting board's own selection ("Paint · 2 keys selected", mockup 2f). It is a
            // DIFFERENT selection from `.selected` — that one is the single key the inspector rail
            // is talking about, this one is a set — so it owns its own ring part, and neither state
            // can move the other's.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "paintSelected");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            var ring = PaintRingOf(cap).BoxShadow;

            Assert.Equal(2, ring.Count);
            Assert.Equal(DesignTokens.ResolveColor("AccentKeyRingColor", variant), ring[0].Color);
            Assert.Equal(3, ring[0].Spread);
            Assert.Equal(DesignTokens.ResolveColor("KeycapRingEdgeColor", variant), ring[1].Color);
            Assert.Equal(1, ring[1].Spread);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), cap.BorderBrush);

            // The inspector's ring is untouched: this cap is not the selected key.
            Assert.Equal(0, RingOf(cap).BoxShadow.Count);

            // ...and it lands INSIDE the cap's own edge, like the ring it copies: the outermost
            // pixel column differs from a bare cap's, and the ring's bounds sit 3px in, which is
            // what keeps a 3px spread off the neighbour 4px away.
            var inset = PaintRingOf(cap).TranslatePoint(new Point(0, 0), cap)
                ?? throw new InvalidOperationException("The ring is not in the cap's visual tree.");

            Assert.Equal(3, inset.X);
            Assert.Equal(3, inset.Y);
            Assert.Equal(cap.Bounds.Width - 6, PaintRingOf(cap).Bounds.Width);

            var bare = Cap(variant);

            using var bareHost = ThemedHost.Show(bare, variant, HostWidth, HostHeight);

            Assert.True(
                Distance(EdgePixel(host, cap), EdgePixel(bareHost, bare)) > 8,
                "The paint selection reaches no further than an ordinary cap's border.");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ACapThatIsBothSelectedAndPaintSelected_ShowsBothAndStaysLegible(string variantName)
        {
            // Both classes can be true at once: the two tabs render the SAME cap view models, so
            // the key the inspector is on can also be one of the painted ones. Two states writing
            // one BoxShadow would work only while their values agreed, and `.listening` already
            // moves Ring's margin — hence two parts.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "selected", "paintSelected");

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.Equal(2, RingOf(cap).BoxShadow.Count);
            Assert.Equal(2, PaintRingOf(cap).BoxShadow.Count);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), cap.BorderBrush);

            // The two rings are concentric with each other rather than stacked outward, so the band
            // is the cap's own outer 3px and no wider than one selection's would be.
            Assert.Equal(RingOf(cap).Bounds, PaintRingOf(cap).Bounds);

            // Focus still owns the border and adds its halo outside both.
            Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");
            Assert.Equal(1, RootOf(cap).BoxShadow.Count);
            Assert.Equal(3, RootOf(cap).BoxShadow[0].Spread);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ADisabledPaintSelectedCap_LosesItsRingWithEverythingElse(string variantName)
        {
            // Contract 5 again: BaseButton's `:disabled` face is applied first, so the state carries
            // `:not(:disabled)` or a dead cap keeps a live accent ring while the board is loading.
            var variant = ToVariant(variantName);
            var cap = Cap(variant, "paintSelected");

            cap.IsEnabled = false;

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            Assert.Equal(0, PaintRingOf(cap).BoxShadow.Count);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), cap.BorderBrush);
        }

        /// <summary>
        /// The saturated colours a key can actually be lit, chosen so that each of the ring's two
        /// tones is the one carrying the signal in at least one row. <c>#FFFFFF</c> swallows the
        /// near-black hairline and the accent has to carry it; <c>#5B9DF9</c> is the accent's own
        /// hue and the hairline has to carry that one. A ring painted in a single colour — of any
        /// alpha, of any hue — fails one row or the other, which is the whole argument for two.
        /// </summary>
        public static TheoryData<string> LedFills()
        {
            return new TheoryData<string> { "#FF0000", "#00FF00", "#0000FF", "#FFFF00", "#FFFFFF", "#5B9DF9" };
        }

        [AvaloniaTheory]
        [MemberData(nameof(LedFills))]
        public void APaintSelectedCap_OverASaturatedLedColour_IsUnmistakable(string fill)
        {
            // THE DEFECT OF ISSUE #128, at the glass, on the surface it was reported on. Everything
            // the object graph could say about the paint selection was already true and asserted —
            // the class was written, the ring part carried a shadow, the border took the accent —
            // and the user still could not see that Q and S were selected, because the ring was
            // 30 % of blue laid over a face the led file had painted #A41010. Nothing but a
            // rendered pixel can tell the two apart, so this reads the band itself.
            foreach (var variant in DesignTokens.Variants)
            {
                AssertTheRingReadsOverTheFace(fill, variant, paintSelection: true);
            }
        }

        [AvaloniaTheory]
        [MemberData(nameof(LedFills))]
        public void ASelectedCap_OverASaturatedLedColour_IsUnmistakable(string fill)
        {
            // The same claim for the inspector's selection, and it is not a duplicate: the two
            // boards render the SAME cap view models, so `.selected` lands on a lit face whenever
            // the key the rail is talking about is one the Lighting tab is drawing. It owns its own
            // ring part, so nothing but a test holds the two in step.
            foreach (var variant in DesignTokens.Variants)
            {
                AssertTheRingReadsOverTheFace(fill, variant, paintSelection: false);
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheRingOnALitCap_StopsAtTheCapsOwnEdge(string variantName)
        {
            // The other half of "unmistakable": louder must not mean wider. The band is opaque now,
            // so a spread that reached outward would land squarely on the neighbouring cap's face
            // 4 px away rather than tinting the gap — and on the Lighting board that neighbour is
            // somebody's colour. Read on a lit cap because that is where the opaque ring lives.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, paint: "#FF0000");

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            Live(view);

            ((KeyboardKeyViewModel)view.DataContext!).IsLightingSelected = true;

            var frame = host.Capture();
            var origin = OriginOf(host, view);
            var row = FaceRow(host, view);

            for (var offset = -3; offset < 0; offset++)
            {
                AssertClose(
                    DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant),
                    FramePixels.At(frame, (int)origin.X + offset, row));
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark", "#FFFFFF", "KeycapLitLabelDarkBrush")]
        [InlineData("Dark", "#050505", "KeycapLitLabelLightBrush")]
        [InlineData("Light", "#FFFFFF", "KeycapLitLabelDarkBrush")]
        [InlineData("Light", "#050505", "KeycapLitLabelLightBrush")]
        public void ALitCapsCaption_TakesItsColourFromTheFillAndNotFromTheTheme(
            string variantName,
            string fill,
            string expectedKey)
        {
            // The face is DEVICE DATA and can be anything, so a caption drawn in a fixed text role
            // disappears on half the palette — under BOTH variants, which is why the two label roles
            // are variant-independent and why the choice cannot live in a theme dictionary. The
            // threshold is the luminance at which contrast against black equals contrast against
            // white; see Converters/LitLabelBrushConverter.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, paint: fill, legend: "1");

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve(expectedKey, variant), CaptionOf(view).Foreground);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnUnlitCapsCaption_KeepsTheOrdinaryTextRole(string variantName)
        {
            // The hatch is not a fill: an unlit cap is an ordinary cap, and its caption is whatever
            // its style says. The converter answers UnsetValue there, which is what lets the binding
            // fall back to the style rather than painting a third colour of its own.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, legend: "1");

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("TextPrimaryBrush", variant), CaptionOf(view).Foreground);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void APaintWashedCapsCaption_IsNotFlippedByAFillItCanBarelySee(string variantName)
        {
            // The 40% paint case. Under half coverage what the eye reads is mostly the hatch and the
            // cap's own face, so flipping the caption there would be the opposite of a contrast fix:
            // a near-black label on a dark cap wearing a faint wash.
            var variant = ToVariant(variantName);
            var view = KeyCap(showsLighting: true, paint: "#FFFFFF", paintOpacity: 0.4, legend: "1");

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            Assert.Equal(DesignTokens.Resolve("TextPrimaryBrush", variant), CaptionOf(view).Foreground);
        }

        /// <summary>
        /// Fails unless a cap lit <paramref name="fill"/> and wearing one of the two selections is
        /// visibly different from an identical cap that is not — everywhere in the band, and by a
        /// long way somewhere in it — while the face itself is left alone.
        /// </summary>
        private static void AssertTheRingReadsOverTheFace(string fill, ThemeVariant variant, bool paintSelection)
        {
            var selected = KeyCap(showsLighting: true, paint: fill);
            var bare = KeyCap(showsLighting: true, paint: fill);

            using var selectedHost = ThemedHost.Show(selected, variant, HostWidth, HostHeight);
            using var bareHost = ThemedHost.Show(bare, variant, HostWidth, HostHeight);

            // Both of them, and after Show: a Button with no Command is `:disabled`, `:disabled`
            // takes both rings AND the cap's border role, and the comparison would then be between
            // two dead caps rather than between a selected key and a live one.
            Live(selected);
            Live(bare);

            var model = (KeyboardKeyViewModel)selected.DataContext!;

            if (paintSelection)
            {
                model.IsLightingSelected = true;
            }
            else
            {
                model.IsSelected = true;
            }

            var band = BandSamples(selectedHost, selected);
            var unselected = BandSamples(bareHost, bare);
            var face = Softened(fill);

            // Every pixel of the 3px band moved. A ring that only repainted the cap's own 1px
            // border would pass a "something changed" test and still be the thin line the user
            // could not see.
            for (var column = 0; column < band.Count; column++)
            {
                Assert.True(
                    Distance(band[column], unselected[column]) > 40,
                    $"Column {column} of a {(paintSelection ? "paint-" : string.Empty)}selected cap lit "
                    + $"{fill} ({variant}) painted {band[column]}, near enough the unselected cap's "
                    + $"{unselected[column]}.");
            }

            // ...and somewhere in it the ring is a long way from the colour the key is lit, which
            // is the claim a translucent accent could not make: over #A41010 it painted a red.
            var contrast = band.Max(sample => Distance(sample, face));

            Assert.True(
                contrast >= 150,
                $"The ring on a cap lit {fill} ({variant}) is only {contrast} from its own face — "
                + $"the band painted {string.Join(", ", band)} against {face}.");

            // ...while the face is untouched: the ring is a band at the edge, not a tint over the
            // key, and the Lighting board exists to show the colour that is on file.
            AssertClose(
                face,
                FramePixels.At(
                    selectedHost.Capture(),
                    (int)OriginOf(selectedHost, selected).X + 5,
                    FaceRow(selectedHost, selected)));
        }

        /// <summary>The cap's outer three pixel columns, at <see cref="FaceRow"/>.</summary>
        private static IReadOnlyList<Color> BandSamples(ThemedHost host, KeyCapView view)
        {
            var frame = host.Capture();
            var origin = OriginOf(host, view);
            var row = FaceRow(host, view);

            return Enumerable.Range(0, 3)
                .Select(column => FramePixels.At(frame, (int)origin.X + column, row))
                .ToArray();
        }

        /// <summary>
        /// A row across the cap that is clear of both its rounded corners (radius 4) and of the
        /// caption's own line, so a sample of the left edge reads the ring and the face and never a
        /// glyph. <see cref="MidRow"/> is the caption's row and cannot be used for this.
        /// </summary>
        private static int FaceRow(ThemedHost host, KeyCapView view)
        {
            return (int)OriginOf(host, view).Y + 6;
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
        /// A real <see cref="KeyCapView"/> over a cap view model carrying the paint and effect
        /// layers given, on a picture that is or is not the lighting board. The first argument is
        /// the <b>surface's</b> answer and not the key's: <see cref="KeyboardView"/> hands it down,
        /// and a cap hosted alone takes the property's own default of false.
        /// </summary>
        private static KeyCapView KeyCap(
            bool showsLighting,
            string? paint = null,
            double paintOpacity = 1,
            string? effect = null,
            double effectIntensity = 1,
            string? legend = null,
            string? secondaryLegend = null,
            bool atUnitSize = false)
        {
            var key = TestLayouts.CreateLayout("esc").Layers[0].Keys[0];
            var visual = new KeyVisual(key.Index, 0, 0, legend: legend, secondaryLegend: secondaryLegend);
            var model = new KeyboardKeyViewModel(key, visual, TokenDialect.Gen1);

            // Through the view model's own two entry points rather than by setting the properties:
            // both are private-set, because the paint is the lighting model's answer and the effect
            // the previewed frame's, and neither is the cap's to decide.
            model.ApplyPaint(ParseColor(paint), paintOpacity);
            model.ApplyEffect(ParseColor(effect), effect is null ? 0 : effectIntensity);

            return new KeyCapView
            {
                DataContext = model,
                ShowsLighting = showsLighting,
                Width = atUnitSize ? UnitCapWidth : CapWidth,
                Height = atUnitSize ? UnitCapHeight : CapHeight,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>A <c>#RRGGBB</c> fixture colour, or null for "this layer is not drawn".</summary>
        private static LedColor? ParseColor(string? hex)
        {
            if (hex is null)
            {
                return null;
            }

            Assert.True(KeyColorOverlay.TryParseHex(hex, out var color), $"'{hex}' is not a colour.");

            return color;
        }

        private static Border RootOf(Button cap)
        {
            return NamedPart<Border>(cap, "Root");
        }

        private static Border RingOf(Button cap)
        {
            return NamedPart<Border>(cap, "Ring");
        }

        /// <summary>The Lighting board's own selection ring — a second element, see the theme.</summary>
        private static Border PaintRingOf(Button cap)
        {
            return NamedPart<Border>(cap, "PaintRing");
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

        /// <summary>
        /// A run of pixels across the cap's face, well inside its border and clear of its rounded
        /// corners. Sampled a row above the caption's own, so the legend's glyphs never land in it.
        /// </summary>
        private static IReadOnlyList<Color> FaceSamples(ThemedHost host, KeyCapView view)
        {
            var frame = host.Capture();
            var origin = OriginOf(host, view);
            var row = (int)origin.Y + 6;
            var samples = new List<Color>();

            for (var offset = 4; offset < (int)view.Bounds.Width - 4; offset++)
            {
                samples.Add(FramePixels.At(frame, (int)origin.X + offset, row));
            }

            return samples;
        }

        /// <summary>
        /// The mean of <see cref="FaceSamples"/>. The hatch under a translucent fill makes any
        /// single pixel a coin toss between a stripe and a gap; the mean is what the eye reads.
        /// </summary>
        private static Color MeanFace(ThemedHost host, KeyCapView view)
        {
            var samples = FaceSamples(host, view);

            return Color.FromRgb(
                (byte)Math.Round(samples.Average(sample => (double)sample.R)),
                (byte)Math.Round(samples.Average(sample => (double)sample.G)),
                (byte)Math.Round(samples.Average(sample => (double)sample.B)));
        }

        private static Button ButtonOf(KeyCapView view)
        {
            return view.GetVisualDescendants().OfType<Button>().Single();
        }

        /// <summary>
        /// The cap's <see cref="Button"/>, with a command on it. A Button whose <c>Command</c> is
        /// null is <b>disabled</b> — Avalonia's own rule — and <c>:disabled</c> beats the whole face
        /// ladder, both selection rings included. On the real board the command comes down from the
        /// <see cref="KeyboardView"/>; a cap hosted alone has no such ancestor, so a test about an
        /// accent state has to hand it one. It must be assigned <b>after</b> the host has shown the
        /// cap: the view's own <c>$parent[KeyboardView]</c> binding resolves to nothing when the
        /// tree is attached, and would clear anything set beforehand.
        /// </summary>
        private static Button Live(KeyCapView view)
        {
            var cap = ButtonOf(view);

            cap.Command = new RelayCommand(() => { });

            return cap;
        }

        /// <summary>
        /// The cap's lit face: the panel of stacked layers behind the caption. Found by its shape
        /// rather than by a name — it is the one panel in the cap holding nothing but Borders, and
        /// the whole point of the arrangement is that it is content and not a template part, so it
        /// has no name to look up.
        /// </summary>
        private static Panel FillOf(KeyCapView view)
        {
            return view.GetVisualDescendants()
                .OfType<Panel>()
                .Single(panel => panel.Children.Count == 4 && panel.Children.All(child => child is Border));
        }

        /// <inheritdoc cref="FillOf" />
        private static FillLayers FillLayersOf(KeyCapView view)
        {
            var layers = FillOf(view).Children.Cast<Border>().ToArray();

            return new FillLayers(layers[0], layers[1], layers[2], layers[3]);
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

        /// <summary>
        /// The cap's face layers, bottom to top: the hatch that means "off", the paint on file
        /// under the effect, the previewed effect's colour for this frame, and the paint on file
        /// over the effect. There are two paint Borders because the mode decides which side of the
        /// effect the paint is composited on — see <see cref="Paint"/>, and
        /// <c>KeyboardKeyViewModel.ShowsPaintUnderEffect</c> for the rule.
        /// </summary>
        private readonly record struct FillLayers(Border Hatch, Border PaintUnder, Border Effect, Border PaintOver)
        {
            /// <summary>
            /// The paint layer that is actually drawn, whichever side it is on. Exactly one of the
            /// two is ever visible, so a test that is not about the ORDER can read this one.
            /// </summary>
            public Border Paint => PaintOver.IsVisible ? PaintOver : PaintUnder;
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

        /// <summary>
        /// The colour a cap's face is actually painted when the lighting model holds
        /// <paramref name="hex"/> — i.e. the stored value after the preview softening
        /// (<c>LedPreviewTint</c>, issue #124).
        /// <para>
        /// Every expectation on this board goes through here rather than restating a literal,
        /// because the tint's two constants are a look tuned against rendered frames and are meant
        /// to be re-tuned. What these tests are about is the <b>layering</b> — which layer is drawn
        /// on which side of which, at what opacity — and that claim must not break when somebody
        /// makes the board a shade quieter. The tint has its own tests
        /// (<c>ViewModels/LedPreviewTintTests</c>), which is where its shape is pinned.
        /// </para>
        /// </summary>
        private static Color Softened(string hex)
        {
            Assert.True(KeyColorOverlay.TryParseHex(hex, out var color), $"'{hex}' is not a colour.");

            var softened = LedPreviewTint.Soften(color);

            return Color.FromRgb(softened.Red, softened.Green, softened.Blue);
        }

        /// <summary>
        /// <paramref name="over"/> composited on <paramref name="under"/> at
        /// <paramref name="opacity"/> — the arithmetic a stack of two opaque layers performs, so a
        /// test can name the blend it expects instead of a colour somebody worked out once.
        /// </summary>
        private static Color Blend(Color under, Color over, double opacity)
        {
            return Color.FromRgb(
                (byte)Math.Round((over.R * opacity) + (under.R * (1 - opacity))),
                (byte)Math.Round((over.G * opacity) + (under.G * (1 - opacity))),
                (byte)Math.Round((over.B * opacity) + (under.B * (1 - opacity))));
        }
    }
}
