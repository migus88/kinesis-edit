using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Headless;

namespace KinesisEdit.Tests.Controls
{
    /// <summary>
    /// The one control that scales the keyboard canvas: it grows the finished board as a single
    /// picture and centres it, so the caps, the hairlines, the legends and the badges keep their
    /// proportions (docs/design/handoff.md § "Geometry": "scale the whole board up with available
    /// space; keep proportions").
    /// <para>
    /// The fixture child is a deliberately lopsided 200 x 100, so a scale taken from the wrong axis
    /// produces a visibly different number rather than the same one twice.
    /// </para>
    /// <para>
    /// Layout rounding is off throughout, for the reason it is off in
    /// <see cref="KeyboardPanelTests"/>: snapping to device pixels is right on screen and hides
    /// every fractional error here.
    /// </para>
    /// </summary>
    public class BoardScaleHostTests
    {
        private const double NaturalWidth = 200;

        private const double NaturalHeight = 100;

        [AvaloniaTheory]
        // Both axes usable: the smaller scale wins, so the picture keeps its aspect.
        [InlineData(100, 1000, 100, 50)]
        [InlineData(1000, 50, 100, 50)]
        [InlineData(100, 100, 100, 50)]
        // One axis unusable constrains nothing, and the other alone sets the scale. An auto-sizing
        // or scrolling parent offers infinity.
        [InlineData(400, double.PositiveInfinity, 400, 200)]
        [InlineData(double.PositiveInfinity, 400, 800, 400)]
        public void Measure_ScalesFromTheUsableAxesAlone(
            double availableWidth,
            double availableHeight,
            double expectedWidth,
            double expectedHeight)
        {
            var host = CreateHost(out _);

            host.Measure(new Size(availableWidth, availableHeight));

            Assert.Equal(new Size(expectedWidth, expectedHeight), host.DesiredSize);
        }

        [AvaloniaFact]
        public void Measure_WithNeitherAxisConstraining_ReportsTheChildAtMockScale()
        {
            // Never infinity: with nothing to fit into, the board is the size it was authored at.
            var host = CreateHost(out _);

            host.Measure(Size.Infinity);

            Assert.Equal(new Size(NaturalWidth, NaturalHeight), host.DesiredSize);
        }

        [AvaloniaFact]
        public void Measure_WithNoChild_ReportsNothing()
        {
            var host = new BoardScaleHost { UseLayoutRounding = false };

            host.Measure(new Size(400, 400));

            Assert.Equal(default, host.DesiredSize);
        }

        [AvaloniaFact]
        public void Measure_NeverTalksTheChildDown()
        {
            // The child's mock-scale size is the thing being scaled, so it is measured unconstrained
            // however little room the host itself was offered. A child measured at the offered size
            // would report a board already shrunk, and the transform would shrink it a second time.
            var host = CreateHost(out var child);

            host.Measure(new Size(20, 20));

            Assert.Equal(new Size(NaturalWidth, NaturalHeight), child.DesiredSize);
        }

        [AvaloniaFact]
        public void Arrange_DrawsTheChildAtItsNaturalSizeThroughAUniformTransform()
        {
            var host = CreateHost(out var child);

            Arrange(host, 1000, 1000);

            // Bounds stay the natural rectangle: the growth is a render transform, not a bigger
            // layout slot, which is exactly what keeps every hairline and legend inside it in step.
            Assert.Equal(new Size(NaturalWidth, NaturalHeight), child.Bounds.Size);

            var transform = Assert.IsType<ScaleTransform>(child.RenderTransform);

            Assert.Equal(5, transform.ScaleX);
            Assert.Equal(5, transform.ScaleY);
            Assert.Equal(RelativePoint.TopLeft, child.RenderTransformOrigin);
        }

        [AvaloniaFact]
        public void Arrange_CentresTheScaledPictureOnTheAxisThatDoesNotBind()
        {
            var host = CreateHost(out var child);

            // Width binds at 5x, so the 100-tall picture is 500 in a 1000 box and the spare 500 is
            // split above and below it.
            Arrange(host, 1000, 1000);

            Assert.Equal(new Point(0, 250), child.Bounds.Position);
        }

        [AvaloniaFact]
        public void Arrange_WhenThePictureFillsBothAxes_CentresNothing()
        {
            var host = CreateHost(out var child);

            Arrange(host, 1000, 500);

            Assert.Equal(new Point(0, 0), child.Bounds.Position);
        }

        [AvaloniaFact]
        public void Arrange_ReportsTheScaleItDrewAt()
        {
            var host = CreateHost(out _);

            Arrange(host, 1000, 1000);

            Assert.Equal(5, host.Scale);

            Arrange(host, 100, 1000);

            Assert.Equal(0.5, host.Scale);
        }

        [AvaloniaFact]
        public void TheHost_AppliesNoMaximumScale()
        {
            // Deliberate: the handoff caps nothing, and the board is vector all the way down, so a
            // ceiling would only strand a small picture in the middle of a large window.
            var host = CreateHost(out _);

            Arrange(host, NaturalWidth * 100, NaturalHeight * 100);

            Assert.Equal(100, host.Scale);
        }

        [AvaloniaTheory]
        [InlineData(0, 0)]
        [InlineData(0, 400)]
        [InlineData(400, 0)]
        public void Arrange_WithNoRoom_ArrangesTheChildAwayAndReportsNoScale(double width, double height)
        {
            // Arrange reads its size as WHAT YOU GET, not as what constrains you: a zero dimension
            // here is no room at all. The child is still arranged — an unarranged child keeps its
            // previous bounds, and with no clip anywhere on the chain it would be painted over a
            // picture meant to be empty.
            var host = CreateHost(out var child);

            Arrange(host, 1000, 1000);

            Assert.NotEqual(default, child.Bounds);

            Arrange(host, width, height);

            Assert.Equal(default, child.Bounds);
            Assert.Equal(0, host.Scale);
            Assert.Null(child.RenderTransform);
        }

        [AvaloniaFact]
        public void AChildWithNoPicture_IsArrangedAwayRatherThanDividedBy()
        {
            var host = new BoardScaleHost { UseLayoutRounding = false };
            var child = new Border { UseLayoutRounding = false };

            host.Child = child;

            Arrange(host, 400, 400);

            Assert.Equal(default, child.Bounds);
            Assert.Equal(0, host.Scale);
        }

        [AvaloniaFact]
        public void TheHost_NeverClips()
        {
            // A focused cap casts a 3px halo past its own bounds into a grid whose caps are 4px
            // apart; a clip anywhere between it and the window erases it.
            var host = CreateHost(out _);

            Assert.False(host.ClipToBounds);
        }

        [AvaloniaFact]
        public void AClickAtANonUnitScale_LandsOnTheChildUnderThePointer()
        {
            // The transform has to be part of hit-testing, not only of painting: at 2x the right
            // half of the picture starts at 200 in the host's own coordinates, not at 100.
            var left = CreateTarget();
            var right = CreateTarget();
            var board = new Canvas
            {
                Width = NaturalWidth,
                Height = NaturalHeight,
                UseLayoutRounding = false,
                Children = { left, right }
            };

            Canvas.SetLeft(right, NaturalWidth / 2);

            var host = new BoardScaleHost
            {
                Child = board,
                Width = NaturalWidth * 2,
                Height = NaturalHeight * 2,
                UseLayoutRounding = false
            };

            using var themed = ThemedHost.Show(host, ThemeVariant.Dark, 600, 400);

            themed.Capture();

            Assert.Equal(2, host.Scale);
            Assert.Same(right, HitTest(host, new Point(300, 100)));
            Assert.Same(left, HitTest(host, new Point(100, 100)));
        }

        /// <summary>
        /// The host over a child whose 200 x 100 comes from its <b>content</b>, exactly as the real
        /// board's does. It matters: an explicit <c>Width</c>/<c>Height</c> on the host's own child
        /// would be restored by <c>ApplyLayoutConstraints</c> after any arrange, so a child arranged
        /// away would silently keep its size and the degenerate cases would prove nothing.
        /// </summary>
        private static BoardScaleHost CreateHost(out Border child)
        {
            child = new Border
            {
                UseLayoutRounding = false,
                Child = new Border
                {
                    Width = NaturalWidth,
                    Height = NaturalHeight,
                    UseLayoutRounding = false
                }
            };

            return new BoardScaleHost
            {
                Child = child,
                UseLayoutRounding = false
            };
        }

        /// <summary>
        /// A half of the fixture board. The fill is what makes a <see cref="Border"/> hit-testable
        /// at all; the colour is irrelevant and never reaches a view.
        /// </summary>
        private static Border CreateTarget()
        {
            return new Border
            {
                Width = NaturalWidth / 2,
                Height = NaturalHeight,
                Background = Brushes.Transparent,
                UseLayoutRounding = false
            };
        }

        /// <summary>
        /// What Avalonia would hand a pointer press at <paramref name="point"/>, in
        /// <paramref name="host"/>'s own coordinates — the leaf, or the nearest ancestor of it that
        /// is one of the fixture's halves.
        /// </summary>
        private static object? HitTest(BoardScaleHost host, Point point)
        {
            if (host.InputHitTest(point) is not Visual hit)
            {
                return null;
            }

            return hit.GetSelfAndVisualAncestors()
                .OfType<Border>()
                .FirstOrDefault(border => border.Background is not null);
        }

        private static void Arrange(BoardScaleHost host, double width, double height)
        {
            host.InvalidateMeasure();
            host.InvalidateArrange();
            host.Measure(new Size(Math.Max(0, width), Math.Max(0, height)));
            host.Arrange(new Rect(0, 0, width, height));
        }
    }
}
