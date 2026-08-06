using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The <see cref="Icon"/> control at the glass: what it paints, where it puts it, and how thick
    /// the pen reads once the fit transform has scaled everything else.
    /// <para>
    /// These are the claims no resource lookup can answer. A geometry that resolves, a brush that
    /// resolves and a control that draws neither of them where the design says look identical to
    /// every test that stops at the resource dictionary.
    /// </para>
    /// </summary>
    public class IconRenderTests
    {
        /// <summary>Big enough for the largest mark these tests draw, small enough to rasterise fast.</summary>
        private const double HostSize = 240;

        /// <summary>The size the fit tests draw at: four times the 16px grid, so a unit is 4px.</summary>
        private const double LargeSize = 64;

        /// <summary>Scale of <see cref="LargeSize"/> against the 16px grid.</summary>
        private const double LargeScale = LargeSize / 16;

        /// <summary>Channel-sum distance below which two colours are the same paint.</summary>
        private const int SameColour = 6;

        /// <summary>Channel-sum distance above which a pixel counts as painted rather than as background.</summary>
        private const int Painted = 30;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AnIcon_InEachVariant_PaintsTheStrokeItWasHanded(string variantName)
        {
            // The colour comes from the token the call site names, never from the geometry — which
            // is why the same mark can be the connected green here and the demo purple elsewhere.
            var variant = ToVariant(variantName);
            var icon = CreateIcon("IconConnected", LargeSize);

            icon.Stroke = (IBrush)DesignTokens.Resolve("StatusOkBrush", variant);
            icon.StrokeThickness = 6;

            using var host = ThemedHost.Show(icon, variant, HostSize, HostSize);

            var frame = host.Capture();

            // The top of the ring: centre (8,8), radius 7, so authoring point (8,1).
            AssertClose(
                DesignTokens.ResolveBrushColor("StatusOkBrush", variant),
                PaintAt(host, frame, icon, 8, 1));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void AStrokeOnlyMark_LeavesItsInteriorUnpainted(string variantName)
        {
            // A ring is a ring, not a disc. `IconConnected` is stroke-only precisely because
            // filling it would fill the ring solid and the connected mark would lose its core dot.
            var variant = ToVariant(variantName);
            var icon = CreateIcon("IconConnected", LargeSize);

            icon.Stroke = (IBrush)DesignTokens.Resolve("StatusOkBrush", variant);
            icon.StrokeThickness = 6;

            using var host = ThemedHost.Show(icon, variant, HostSize, HostSize);

            var frame = host.Capture();
            var interior = PaintAt(host, frame, icon, 8, 8);

            AssertClose(DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", variant), interior);
            Assert.True(
                Distance(DesignTokens.ResolveBrushColor("StatusOkBrush", variant), interior) > Painted,
                $"The ring's interior painted {interior}, which is the stroke colour: it is drawing as a disc.");
        }

        [AvaloniaFact]
        public void AFillMark_PaintsItsInterior()
        {
            // The eject mark is filled, not stroked: both its subpaths are closed regions, and
            // stroking them would fatten the triangle by a pen width and outline the bar.
            var icon = CreateIcon("IconEject", LargeSize);

            icon.Fill = (IBrush)DesignTokens.Resolve("TextSecondaryBrush", ThemeVariant.Dark);

            using var host = ThemedHost.Show(icon, ThemeVariant.Dark, HostSize, HostSize);

            var frame = host.Capture();

            // Inside the triangle, which spans y 2.3 to 9.1 around x 8.
            AssertClose(
                DesignTokens.ResolveBrushColor("TextSecondaryBrush", ThemeVariant.Dark),
                PaintAt(host, frame, icon, 8, 7));

            // And the gap between the triangle and the bar is genuinely a gap.
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", ThemeVariant.Dark),
                PaintAt(host, frame, icon, 8, 10.2));
        }

        [AvaloniaFact]
        public void ASmallMark_IsFittedByItsBoxRatherThanByItsOwnInk()
        {
            // The regression this pins: `IconConnectedCore` is a 5.6-wide dot on the 16px grid. Fit
            // by its own bounds it would inflate into a disc filling the whole box, the connected
            // mark's ring would disappear behind it, and every mark in the set would lose the
            // shared centre and the relative weights it was drawn with.
            var icon = CreateIcon("IconConnectedCore", LargeSize);

            icon.Fill = (IBrush)DesignTokens.Resolve("StatusOkBrush", ThemeVariant.Dark);

            using var host = ThemedHost.Show(icon, ThemeVariant.Dark, HostSize, HostSize);

            var frame = host.Capture();

            // The control still occupies the full box...
            Assert.Equal(LargeSize, icon.Bounds.Width, 1);
            Assert.Equal(LargeSize, icon.Bounds.Height, 1);

            // ...the dot is painted on the box's centre...
            AssertClose(
                DesignTokens.ResolveBrushColor("StatusOkBrush", ThemeVariant.Dark),
                PaintAt(host, frame, icon, 8, 8));

            // ...and it is still a 2.8-radius dot: 4 units above the centre is outside it, and
            // would be inside a disc that had been inflated to fill the box.
            AssertClose(
                DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", ThemeVariant.Dark),
                PaintAt(host, frame, icon, 8, 4));
        }

        [AvaloniaTheory]
        [InlineData(92)]
        [InlineData(33)]
        public void DeviceArt_KeepsItsProportionsAtAnySize(double size)
        {
            // The silhouettes are drawn in one 92x56 box at true proportion to one another, so the
            // fit is uniform and letterboxes rather than stretching. A squashed keyboard reads as a
            // bug immediately; a keyboard scaled to its own ink reads as the wrong keyboard.
            var icon = new Icon
            {
                Classes = { IconCatalog.DeviceArtClass },
                Data = IconCatalog.ResolveGeometry("DeviceArtTko", ThemeVariant.Dark),
                Fill = (IBrush)DesignTokens.Resolve("TextSecondaryBrush", ThemeVariant.Dark),
                Size = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,

                // In the app an arranged control is snapped to device pixels, which is right on
                // screen and useless here: it rounds the 20.09 letterbox to 21 and hides every
                // fraction the proportion is made of.
                UseLayoutRounding = false
            };

            using var host = ThemedHost.Show(icon, ThemeVariant.Dark, HostSize, HostSize);

            // The class carries the box; nothing about it is re-declared at the call site.
            Assert.Equal(IconCatalog.DeviceArtBox, icon.SourceBox);

            var expectedHeight = size * IconCatalog.DeviceArtBox.Height / IconCatalog.DeviceArtBox.Width;

            Assert.Equal(size, icon.Bounds.Width, 1);
            Assert.Equal(expectedHeight, icon.Bounds.Height, 1);
        }

        [AvaloniaFact]
        public void AMarkInASlotLargerThanItsFit_IsCentredInIt()
        {
            // A non-square box is letterboxed, and the letterbox is even: the drawing sits on the
            // slot's centre rather than in a corner of it. Drawn with a rectangle that fills the
            // whole authoring box, so the painted extent *is* the fitted extent.
            var icon = new Icon
            {
                Data = Geometry.Parse("M 0,0 H 92 V 56 H 0 Z"),
                Fill = (IBrush)DesignTokens.Resolve("StatusOkBrush", ThemeVariant.Dark),
                SourceBox = IconCatalog.DeviceArtBox,
                Size = 92,
                Width = 120,
                Height = 80,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            using var host = ThemedHost.Show(icon, ThemeVariant.Dark, HostSize, HostSize);

            var frame = host.Capture();
            var fill = DesignTokens.ResolveBrushColor("StatusOkBrush", ThemeVariant.Dark);
            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", ThemeVariant.Dark);

            Assert.Equal(120, icon.Bounds.Width, 1);
            Assert.Equal(80, icon.Bounds.Height, 1);

            // 14px of margin either side of the 92-wide drawing, 12px above and below the 56-high
            // one. Sampled in control coordinates, which are the slot's.
            AssertClose(fill, PixelAt(host, frame, icon, 16, 40));
            AssertClose(fill, PixelAt(host, frame, icon, 104, 40));
            AssertClose(canvas, PixelAt(host, frame, icon, 8, 40));
            AssertClose(canvas, PixelAt(host, frame, icon, 112, 40));
            AssertClose(canvas, PixelAt(host, frame, icon, 60, 6));
            AssertClose(canvas, PixelAt(host, frame, icon, 60, 74));
        }

        [AvaloniaFact]
        public void TheStroke_AtALargeSize_StaysTheThicknessItWasGiven()
        {
            // The pen does not scale with the geometry. It is built at StrokeThickness / scale so
            // the fit transform cancels back out to exactly StrokeThickness on screen; without that
            // a mark blown up ten times would read as a mark drawn with a ten-times fatter pen.
            const double size = 160;
            const double thickness = 6;

            var icon = CreateIcon("IconConnected", size);

            icon.Stroke = (IBrush)DesignTokens.Resolve("StatusOkBrush", ThemeVariant.Dark);
            icon.StrokeThickness = thickness;

            using var host = ThemedHost.Show(icon, ThemeVariant.Dark, HostSize, HostSize);

            var frame = host.Capture();
            var canvas = DesignTokens.ResolveBrushColor("SurfaceCanvasBrush", ThemeVariant.Dark);
            var origin = OriginOf(host, icon);
            var column = (int)Math.Round(origin.X + (size / 2));
            var painted = 0;

            // Down the middle of the mark, from the top edge to the centre: the only ink in that
            // run is the top of the ring, so the count is the band's width.
            for (var y = 0; y < size / 2; y++)
            {
                if (Distance(canvas, FramePixels.At(frame, column, (int)Math.Round(origin.Y) + y)) > Painted)
                {
                    painted++;
                }
            }

            // The band plus at most a pixel of antialiasing either side. A pen that scaled with the
            // geometry would measure 60.
            Assert.InRange((double)painted, thickness - 1, thickness + 2);
        }

        [AvaloniaFact]
        public void TheControlsDefaults_AreTheGeometryTokens()
        {
            // A styled property's default is resolved before any resource dictionary is in scope,
            // so these two numbers are written out in Icon.cs as well as in Themes/Geometry.axaml.
            // Duplicated values drift; this is the only thing that stops them.
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(Icon.DefaultSize, (double)DesignTokens.Resolve("IconSize", variant));
                Assert.Equal(Icon.DefaultStrokeThickness, (double)DesignTokens.Resolve("IconStrokeThickness", variant));
            }

            var icon = new Icon();

            Assert.Equal(Icon.DefaultSize, icon.Size);
            Assert.Equal(Icon.DefaultStrokeThickness, icon.StrokeThickness);
            Assert.Equal(IconCatalog.IconBox, icon.SourceBox);

            // "Square caps" is the law, not a call-site choice.
            Assert.Equal(PenLineCap.Square, Icon.StrokeLineCap);
            Assert.Equal(PenLineJoin.Miter, Icon.StrokeLineJoin);
        }

        private static ThemeVariant ToVariant(string variantName)
        {
            return variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>One mark, centred so the control's bounds are exactly the fitted box.</summary>
        private static Icon CreateIcon(string key, double size)
        {
            return new Icon
            {
                Data = IconCatalog.ResolveGeometry(key, ThemeVariant.Dark),
                Size = size,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        /// <summary>The colour at an <i>authoring-unit</i> point of a mark drawn at <see cref="LargeSize"/>.</summary>
        private static Color PaintAt(ThemedHost host, WriteableBitmap frame, Icon icon, double x, double y)
        {
            return PixelAt(host, frame, icon, x * LargeScale, y * LargeScale);
        }

        /// <summary>The colour at a point in <paramref name="icon"/>'s own coordinates.</summary>
        private static Color PixelAt(ThemedHost host, WriteableBitmap frame, Icon icon, double x, double y)
        {
            var origin = OriginOf(host, icon);

            return FramePixels.At(frame, (int)Math.Round(origin.X + x), (int)Math.Round(origin.Y + y));
        }

        private static Point OriginOf(ThemedHost host, Icon icon)
        {
            return icon.TranslatePoint(default, host.Window)
                ?? throw new InvalidOperationException("The icon is not in the window's visual tree.");
        }

        /// <summary>Channel-sum distance, so antialiasing and rounding are not failures.</summary>
        private static int Distance(Color left, Color right)
        {
            return Math.Abs(left.R - right.R) + Math.Abs(left.G - right.G) + Math.Abs(left.B - right.B);
        }

        private static void AssertClose(Color expected, Color actual)
        {
            Assert.True(Distance(expected, actual) <= SameColour, $"Expected about {expected}, painted {actual}.");
        }
    }
}
