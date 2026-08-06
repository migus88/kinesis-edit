using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using KinesisEdit.Controls;

namespace KinesisEdit.Tests.Controls
{
    /// <summary>
    /// The arithmetic of the keyboard picture: the only place in the app that turns key units into
    /// pixels. Everything here is measured against a deliberately lopsided board — 10 units wide by
    /// 5 tall — so a scale taken from the wrong axis produces a visibly different number rather
    /// than the same one twice.
    /// <para>
    /// Layout rounding is off throughout. In the app the arranged caps are snapped to device
    /// pixels, which is right on screen and useless here: it rounds a 0.6 px half-gap to 1 and
    /// hides every off-by-a-fraction the panel could commit.
    /// </para>
    /// </summary>
    public class KeyboardPanelTests
    {
        private const double BoardWidth = 10;

        private const double BoardHeight = 5;

        /// <summary>The protected layout method these tests drive directly; see MeasureBoard.</summary>
        private const string MeasureOverrideName = "MeasureOverride";

        [AvaloniaTheory]
        // Both axes usable: the smaller scale wins, so the board keeps its aspect.
        [InlineData(100, 100, 100, 50)]
        [InlineData(1000, 50, 100, 50)]
        // One axis unusable contributes nothing, and the other alone sets the scale. An auto-sizing
        // or scrolling parent offers infinity; a NaN or non-positive constraint is what a parent
        // mid-layout hands down.
        [InlineData(double.PositiveInfinity, 100, 200, 100)]
        [InlineData(double.NaN, 100, 200, 100)]
        [InlineData(0, 100, 200, 100)]
        [InlineData(-40, 100, 200, 100)]
        [InlineData(100, double.PositiveInfinity, 100, 50)]
        [InlineData(100, double.NaN, 100, 50)]
        [InlineData(100, 0, 100, 50)]
        [InlineData(100, -40, 100, 50)]
        public void MeasureOverride_ScalesFromTheUsableAxesAlone(
            double availableWidth,
            double availableHeight,
            double expectedWidth,
            double expectedHeight)
        {
            var panel = CreatePanel();

            Assert.Equal(new Size(expectedWidth, expectedHeight), MeasureBoard(panel, availableWidth, availableHeight));
        }

        [AvaloniaTheory]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
        [InlineData(double.NaN, double.NaN)]
        [InlineData(0, 0)]
        [InlineData(double.PositiveInfinity, 0)]
        [InlineData(-1, double.NaN)]
        public void MeasureOverride_WithNeitherAxisUsable_FallsBackToTheNaturalUnitSize(double width, double height)
        {
            var panel = CreatePanel();

            Assert.Equal(44.0, KeyboardPanel.NaturalUnitSize);
            Assert.Equal(
                new Size(BoardWidth * KeyboardPanel.NaturalUnitSize, BoardHeight * KeyboardPanel.NaturalUnitSize),
                MeasureBoard(panel, width, height));
        }

        [AvaloniaFact]
        public void Measure_ThroughTheLayoutSystem_ReportsTheScaledBoard()
        {
            var panel = CreatePanel();

            // The same arithmetic as above, but reached the way the layout system reaches it, so
            // the direct MeasureOverride calls cannot drift away from the real wiring.
            panel.Measure(new Size(100, 100));

            Assert.Equal(new Size(100, 50), panel.DesiredSize);
        }

        [AvaloniaFact]
        public void Measure_WithAnUnconstrainedWidth_ReportsTheBoardTheHeightAllows()
        {
            var panel = CreatePanel();

            panel.Measure(new Size(double.PositiveInfinity, 100));

            Assert.Equal(new Size(200, 100), panel.DesiredSize);
        }

        [AvaloniaFact]
        public void Arrange_OnTheNonBindingAxis_CentresTheBoard()
        {
            var panel = CreatePanel();
            var child = AddKey(panel, unitX: 0, unitY: 0);

            Arrange(panel, 200, 200);

            // Width binds at 20 px per unit, so the 5-unit-tall board is 100 px in a 200 px box and
            // the spare 100 px is split above and below it.
            Assert.Equal(HalfGap(20), child.Bounds.X, 6);
            Assert.Equal(50 + HalfGap(20), child.Bounds.Y, 6);
        }

        [AvaloniaFact]
        public void Arrange_WhenTheBoardFillsBothAxes_CentresNothing()
        {
            var panel = CreatePanel();
            var child = AddKey(panel, unitX: 0, unitY: 0);

            Arrange(panel, 200, 100);

            Assert.Equal(HalfGap(20), child.Bounds.X, 6);
            Assert.Equal(HalfGap(20), child.Bounds.Y, 6);
        }

        [AvaloniaFact]
        public void Arrange_SplitsTheGapAcrossBothEdgesOfEveryCap()
        {
            var panel = CreatePanel();
            var child = AddKey(panel, unitX: 0, unitY: 0);

            Arrange(panel, 200, 100);

            var scale = 20.0;

            Assert.Equal(0.06, KeyboardPanel.KeyGapUnits, 6);
            Assert.Equal(HalfGap(scale), child.Bounds.X, 6);
            Assert.Equal(scale - (2 * HalfGap(scale)), child.Bounds.Width, 6);
            Assert.Equal(scale - (2 * HalfGap(scale)), child.Bounds.Height, 6);
        }

        [AvaloniaFact]
        public void Arrange_LeavesExactlyOneGapBetweenHorizontallyAdjacentCaps()
        {
            var panel = CreatePanel();
            var left = AddKey(panel, unitX: 0, unitY: 0);
            var right = AddKey(panel, unitX: 1, unitY: 0);

            Arrange(panel, 200, 100);

            // Half a gap on each facing edge is what makes the space between two caps one whole
            // gap, and the space at the board's own edge half of one.
            Assert.Equal(KeyboardPanel.KeyGapUnits * 20, right.Bounds.X - (left.Bounds.X + left.Bounds.Width), 6);
        }

        [AvaloniaFact]
        public void Arrange_LeavesExactlyOneGapBetweenVerticallyAdjacentCaps()
        {
            var panel = CreatePanel();
            var top = AddKey(panel, unitX: 0, unitY: 0);
            var bottom = AddKey(panel, unitX: 0, unitY: 1);

            Arrange(panel, 200, 100);

            Assert.Equal(KeyboardPanel.KeyGapUnits * 20, bottom.Bounds.Y - (top.Bounds.Y + top.Bounds.Height), 6);
        }

        [AvaloniaFact]
        public void Arrange_WithACapNarrowerThanTheGap_ClampsToZeroRatherThanGoingNegative()
        {
            var panel = CreatePanel();
            var child = AddKey(panel, unitX: 0, unitY: 0, unitWidth: 0.02, unitHeight: 0.01);

            Arrange(panel, 200, 100);

            // 0.02 u at 20 px/u is 0.4 px against a 1.2 px gap. A negative Rect throws, so the
            // sliver collapses instead.
            Assert.Equal(0, child.Bounds.Width);
            Assert.Equal(0, child.Bounds.Height);
        }

        [AvaloniaFact]
        public void Arrange_WithAWideCap_ScalesItsWidthWithTheRest()
        {
            var panel = CreatePanel();
            var space = AddKey(panel, unitX: 2, unitY: 3, unitWidth: 3, unitHeight: 1);

            Arrange(panel, 200, 100);

            Assert.Equal((2 * 20) + HalfGap(20), space.Bounds.X, 6);
            Assert.Equal((3 * 20) + HalfGap(20), space.Bounds.Y, 6);
            Assert.Equal((3 * 20) - (2 * HalfGap(20)), space.Bounds.Width, 6);
        }

        [AvaloniaTheory]
        [InlineData(0, BoardHeight)]
        [InlineData(BoardWidth, 0)]
        [InlineData(double.NaN, BoardHeight)]
        [InlineData(BoardWidth, double.PositiveInfinity)]
        [InlineData(-1, -1)]
        public void MeasureOverride_WithAnInvalidBoard_ReportsNoSize(double boardWidth, double boardHeight)
        {
            var panel = CreatePanel(boardWidth, boardHeight);

            AddKey(panel, unitX: 0, unitY: 0);

            Assert.Equal(default, MeasureBoard(panel, 200, 100));
        }

        [AvaloniaFact]
        public void Arrange_WithAnInvalidBoard_StillArrangesEveryChild()
        {
            var panel = CreatePanel();
            var first = AddKey(panel, unitX: 0, unitY: 0);
            var second = AddKey(panel, unitX: 1, unitY: 0);

            Arrange(panel, 200, 100);

            Assert.NotEqual(default, first.Bounds);
            Assert.NotEqual(default, second.Bounds);

            // The board goes away — a layer with no picture, or a device whose visual has not been
            // authored. An unarranged child keeps its stale bounds and would be drawn on top of the
            // empty picture, so every one of them has to be arranged to nothing.
            panel.BoardWidth = 0;

            Arrange(panel, 200, 100);

            Assert.Equal(default, first.Bounds);
            Assert.Equal(default, second.Bounds);
        }

        [AvaloniaFact]
        public void Arrange_WithAnInvalidBoard_StillFillsTheSpaceItWasGiven()
        {
            var panel = CreatePanel(0, 0);

            AddKey(panel, unitX: 0, unitY: 0);

            Arrange(panel, 200, 100);

            Assert.Equal(new Size(200, 100), panel.Bounds.Size);
        }

        [AvaloniaFact]
        public void UnitProperties_RoundTripThroughTheAttachedAccessors()
        {
            var control = new Border();

            KeyboardPanel.SetUnitX(control, 3.5);
            KeyboardPanel.SetUnitY(control, 2.25);
            KeyboardPanel.SetUnitWidth(control, 2);
            KeyboardPanel.SetUnitHeight(control, 1.5);

            Assert.Equal(3.5, KeyboardPanel.GetUnitX(control));
            Assert.Equal(2.25, KeyboardPanel.GetUnitY(control));
            Assert.Equal(2, KeyboardPanel.GetUnitWidth(control));
            Assert.Equal(1.5, KeyboardPanel.GetUnitHeight(control));
        }

        [AvaloniaFact]
        public void UnitProperties_DefaultToASingleUnitCapAtTheOrigin()
        {
            var control = new Border();

            Assert.Equal(0, KeyboardPanel.GetUnitX(control));
            Assert.Equal(0, KeyboardPanel.GetUnitY(control));
            Assert.Equal(1, KeyboardPanel.GetUnitWidth(control));
            Assert.Equal(1, KeyboardPanel.GetUnitHeight(control));
        }

        private static KeyboardPanel CreatePanel(double boardWidth = BoardWidth, double boardHeight = BoardHeight)
        {
            return new KeyboardPanel
            {
                BoardWidth = boardWidth,
                BoardHeight = boardHeight,
                UseLayoutRounding = false
            };
        }

        private static Border AddKey(
            KeyboardPanel panel,
            double unitX,
            double unitY,
            double unitWidth = 1,
            double unitHeight = 1)
        {
            var child = new Border { UseLayoutRounding = false };

            KeyboardPanel.SetUnitX(child, unitX);
            KeyboardPanel.SetUnitY(child, unitY);
            KeyboardPanel.SetUnitWidth(child, unitWidth);
            KeyboardPanel.SetUnitHeight(child, unitHeight);

            panel.Children.Add(child);

            return child;
        }

        /// <summary>
        /// Invokes <c>MeasureOverride</c> directly. The public <c>Measure</c> cannot ask these
        /// questions: it rejects a NaN constraint outright, and it clamps the answer to the space
        /// it offered — which would hide the very fallback this panel exists to provide when the
        /// offered space says nothing.
        /// </summary>
        private static Size MeasureBoard(KeyboardPanel panel, double width, double height)
        {
            var method = typeof(KeyboardPanel).GetMethod(
                MeasureOverrideName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("KeyboardPanel has no MeasureOverride.");

            return (Size)method.Invoke(panel, [new Size(width, height)])!;
        }

        private static double HalfGap(double scale)
        {
            return KeyboardPanel.KeyGapUnits * scale / 2;
        }

        private static void Arrange(KeyboardPanel panel, double width, double height)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
            panel.Measure(new Size(width, height));
            panel.Arrange(new Rect(0, 0, width, height));
        }
    }
}
