using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The key cap's control theme — <c>Themes/ControlThemes/Keycap.axaml</c> — and the LED strip
    /// of <c>Controls/KeyCapView.axaml</c> that sits inside it.
    /// <para>
    /// The cap carries the densest state matrix in the app: six states on one small square, four of
    /// which can be true at once. Avalonia ranks no selector, so "listening beats selected beats
    /// hover" is true only for as long as somebody keeps writing the <c>:not(...)</c> qualifiers —
    /// which is invisible to a resource test and is most of what follows. The rest is at the glass,
    /// because a halo that is configured and then clipped away, or a hatch that draws one stripe and
    /// stops, both look perfectly correct in the object graph.
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

        /// <summary>How far outside the cap's own edge the focus halo is sampled; inside its 3px.</summary>
        private const int OutsideProbe = -2;

        /// <summary>
        /// How far inside the cap's edge the selection ring is sampled. The outer pixel column is
        /// the cap's own 1px border; column 1 is the first of the face, and the ring's 2px covers
        /// both.
        /// </summary>
        private const int InsideProbe = 1;

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
                { "modified", "AccentSelectionFillBrush" },
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
                // modified loses its face to every state above it...
                { ["modified", "locked"], "SurfaceInsetBrush" },
                { ["modified", "selected"], "SurfaceKeySelectedBrush" },
                { ["modified", "listening"], "StatusAdvisoryTintBrush" },

                // ...locked loses its face to hover, to selection and to listening...
                { ["locked", "selected"], "SurfaceKeySelectedBrush" },
                { ["locked", "listening"], "StatusAdvisoryTintBrush" },

                // ...and selection loses only to listening.
                { ["selected", "listening"], "StatusAdvisoryTintBrush" },
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

        /// <summary>The three states that paint a cap with a hue of its own, and can outlive it.</summary>
        public static TheoryData<string> LiveCapStates()
        {
            return new TheoryData<string> { "modified", "selected", "listening" };
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

            foreach (var state in new[] { "modified", "locked", "selected", "listening" })
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
        /// A real <see cref="KeyCapView"/> over a cap view model carrying <paramref name="overlay"/>,
        /// on a picture that is or is not showing lighting. The second argument is the surface's
        /// answer, not the key's: <see cref="KeyboardView"/> hands it down, and a cap hosted alone
        /// takes the property's own default of false.
        /// </summary>
        private static KeyCapView KeyCap(string? overlay, bool showsLedStrip)
        {
            var key = TestLayouts.CreateLayout("esc").Layers[0].Keys[0];
            var model = TestLayouts.CreateKeyViewModel(key);

            model.ColorOverlayHex = overlay;

            return new KeyCapView
            {
                DataContext = model,
                ShowsLedStrip = showsLedStrip,
                Width = CapWidth,
                Height = CapHeight,
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

        /// <summary>The cap's LED strip: the 3px band under the caption.</summary>
        private static Border StripOf(KeyCapView view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Single(border => border.Height == 3);
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
