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
            Assert.Equal(3, RootOf(cap).BoxShadow[0].Spread);
            Assert.Equal(2, RingOf(cap).BoxShadow[0].Spread);
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
        public void AnUnlitKey_ShowsTheHatchRatherThanGoingDarkOrVanishing(string variantName)
        {
            // "Off is hatched, never black", because black is a colour a key can legitimately be
            // lit. The strip is therefore always drawn and the hatch is its own background; the
            // colour, when there is one, covers it.
            var variant = ToVariant(variantName);
            var view = KeyCap(overlay: null);

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var strip = StripOf(view);

            Assert.True(strip.IsVisible, "The LED strip vanished when the key had no colour.");
            Assert.Equal(DesignTokens.Resolve("HatchBrush", variant), strip.Background);
            Assert.False(ColorPatchOf(strip).IsVisible, "An unlit key painted a colour patch.");

            var frame = host.Capture();
            var origin = OriginOf(host, strip);
            var stripe = DesignTokens.ResolveBrushColor("SurfaceLineHighBrush", variant);
            var painted = new List<Color>();

            for (var offset = 2; offset < 16; offset++)
            {
                painted.Add(FramePixels.At(frame, (int)origin.X + offset, (int)origin.Y + 1));
            }

            Assert.Contains(painted, sample => Distance(sample, stripe) <= 24);
            Assert.DoesNotContain(painted, sample => Distance(sample, Colors.Black) <= 24);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ALitKey_CoversTheHatchWithItsColour(string variantName)
        {
            // The other half: the hatch means "off", so a key that has a colour must not show any
            // of it through.
            var variant = ToVariant(variantName);
            var view = KeyCap(overlay: "#FF0000");

            using var host = ThemedHost.Show(view, variant, HostWidth, HostHeight);

            var patch = ColorPatchOf(StripOf(view));

            Assert.True(patch.IsVisible, "A lit key drew no colour.");

            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(patch.Background);

            Assert.Equal(Color.FromRgb(0xFF, 0x00, 0x00), brush.Color);
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

        private static RingSample Sample(ThemeVariant variant, string[] classes, bool focused)
        {
            var cap = Cap(variant, classes);

            using var host = ThemedHost.Show(cap, variant, HostWidth, HostHeight);

            if (focused)
            {
                Assert.True(cap.Focus(NavigationMethod.Tab), "The cap refused keyboard focus.");
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

        /// <summary>A real <see cref="KeyCapView"/> over a cap view model carrying <paramref name="overlay"/>.</summary>
        private static KeyCapView KeyCap(string? overlay)
        {
            var key = TestLayouts.CreateLayout("esc").Layers[0].Keys[0];
            var model = TestLayouts.CreateKeyViewModel(key);

            model.ColorOverlayHex = overlay;

            return new KeyCapView
            {
                DataContext = model,
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
