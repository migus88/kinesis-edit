using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Design;

namespace KinesisEdit.Tests.Controls
{
    /// <summary>
    /// The arithmetic of the keyboard picture: the only place in the app that turns key units into
    /// pixels. The board is drawn on a <b>cell pitch</b>, not on a cap size — every 1U cell is
    /// <c>KeycapPitchX</c> wide and <c>KeycapPitchY</c> tall, and the cap inside it is inset by
    /// <c>KeycapGap</c>, so pitch minus gap is the handoff's "30x26 (1u), gap 4".
    /// <para>
    /// The pitch is deliberately <b>not square</b> (34 x 30), so a panel that took the wrong axis
    /// produces a visibly different number rather than the same one twice — and the fixture board is
    /// lopsided too, 10 units wide by 5 tall.
    /// </para>
    /// <para>
    /// Nothing here scales: growing the picture is <see cref="BoardScaleHost"/>'s job, and the panel
    /// reports mock scale whatever space it is offered.
    /// </para>
    /// <para>
    /// Layout rounding is off throughout. In the app the arranged caps are snapped to device pixels,
    /// which is right on screen and useless here: it hides every off-by-a-fraction the panel could
    /// commit.
    /// </para>
    /// </summary>
    public class KeyboardPanelTests
    {
        private const double BoardWidth = 10;

        private const double BoardHeight = 5;

        /// <summary>The board's natural width: 10 x 34 - 4. The trailing gap is never drawn.</summary>
        private const double NaturalWidth = (BoardWidth * KeyboardPanel.DefaultPitchX) - KeyboardPanel.DefaultGap;

        /// <summary>The board's natural height: 5 x 30 - 4.</summary>
        private const double NaturalHeight = (BoardHeight * KeyboardPanel.DefaultPitchY) - KeyboardPanel.DefaultGap;

        /// <summary>The 1U cap the pitch and the gap imply: 34 - 4.</summary>
        private const double CapWidth = KeyboardPanel.DefaultPitchX - KeyboardPanel.DefaultGap;

        /// <summary>The 1U cap's height: 30 - 4.</summary>
        private const double CapHeight = KeyboardPanel.DefaultPitchY - KeyboardPanel.DefaultGap;

        /// <summary>The protected layout method these tests drive directly; see MeasureBoard.</summary>
        private const string MeasureOverrideName = "MeasureOverride";

        [AvaloniaTheory]
        // Whatever is on offer, the answer is the same: the panel draws at mock scale and the whole
        // picture is grown by BoardScaleHost, so a panel that shrank here would scale it twice.
        [InlineData(1000, 1000)]
        [InlineData(100, 100)]
        [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
        [InlineData(double.NaN, 100)]
        [InlineData(0, 0)]
        [InlineData(-40, 100)]
        public void MeasureOverride_ReportsTheSectionAtMockScale(double availableWidth, double availableHeight)
        {
            var panel = CreatePanel();

            Assert.Equal(
                new Size(NaturalWidth, NaturalHeight),
                MeasureBoard(panel, availableWidth, availableHeight));
        }

        [AvaloniaFact]
        public void Measure_ThroughTheLayoutSystem_ReportsTheSameSize()
        {
            var panel = CreatePanel();

            // The same arithmetic, reached the way the layout system reaches it, so the direct
            // MeasureOverride calls cannot drift away from the real wiring.
            panel.Measure(Size.Infinity);

            Assert.Equal(new Size(NaturalWidth, NaturalHeight), panel.DesiredSize);
        }

        [AvaloniaFact]
        public void ARowOfSixSingleUnitCaps_IsExactlyTwoHundredPixelsWide()
        {
            // Six caps of 30 with five gaps of 4: 6 x 34 - 4 = 200. The trailing gap is not drawn,
            // which is what makes the section's box the caps' own extent and not a pitch wider.
            var panel = CreatePanel(boardWidth: 6, boardHeight: 1);
            var caps = new Border[6];

            for (var index = 0; index < caps.Length; index++)
            {
                caps[index] = AddKey(panel, unitX: index, unitY: 0);
            }

            Arrange(panel);

            Assert.Equal(200, panel.DesiredSize.Width);
            Assert.Equal(200, caps[^1].Bounds.Right, 6);
            Assert.Equal(0, caps[0].Bounds.Left, 6);
        }

        [AvaloniaTheory]
        [InlineData(0, 0, 0, 0)]
        [InlineData(1, 0, KeyboardPanel.DefaultPitchX, 0)]
        [InlineData(0, 1, 0, KeyboardPanel.DefaultPitchY)]
        [InlineData(2, 3, 2 * KeyboardPanel.DefaultPitchX, 3 * KeyboardPanel.DefaultPitchY)]
        [InlineData(0.5, 0.25, 0.5 * KeyboardPanel.DefaultPitchX, 0.25 * KeyboardPanel.DefaultPitchY)]
        public void Arrange_PutsEveryCapOnTheCellPitch(
            double unitX,
            double unitY,
            double expectedLeft,
            double expectedTop)
        {
            var panel = CreatePanel();
            var cap = AddKey(panel, unitX, unitY);

            Arrange(panel);

            Assert.Equal(new Rect(expectedLeft, expectedTop, CapWidth, CapHeight), cap.Bounds);
        }

        [AvaloniaFact]
        public void Arrange_LeavesExactlyOneGapBetweenHorizontallyAdjacentCaps()
        {
            var panel = CreatePanel();
            var left = AddKey(panel, unitX: 0, unitY: 0);
            var right = AddKey(panel, unitX: 1, unitY: 0);

            Arrange(panel);

            // The gap is a single inset on the trailing edge rather than half a gap on each side,
            // which is what keeps a wide cap's width its span in pitches minus one gap.
            Assert.Equal(KeyboardPanel.DefaultGap, right.Bounds.X - left.Bounds.Right, 6);
        }

        [AvaloniaFact]
        public void Arrange_LeavesExactlyOneGapBetweenVerticallyAdjacentCaps()
        {
            var panel = CreatePanel();
            var top = AddKey(panel, unitX: 0, unitY: 0);
            var bottom = AddKey(panel, unitX: 0, unitY: 1);

            Arrange(panel);

            Assert.Equal(KeyboardPanel.DefaultGap, bottom.Bounds.Y - top.Bounds.Bottom, 6);
        }

        [AvaloniaTheory]
        [InlineData(2, (2 * KeyboardPanel.DefaultPitchX) - KeyboardPanel.DefaultGap)]
        [InlineData(2.25, (2.25 * KeyboardPanel.DefaultPitchX) - KeyboardPanel.DefaultGap)]
        [InlineData(3.5, (3.5 * KeyboardPanel.DefaultPitchX) - KeyboardPanel.DefaultGap)]
        public void Arrange_WithAWideCap_SpansItsPitchesLessTheOneGap(double unitWidth, double expectedWidth)
        {
            // A 2U Backspace is 2 x 34 - 4 = 64, not two 30s and a gap drawn between them.
            var panel = CreatePanel();
            var cap = AddKey(panel, unitX: 2, unitY: 3, unitWidth: unitWidth);

            Arrange(panel);

            Assert.Equal(2 * KeyboardPanel.DefaultPitchX, cap.Bounds.X, 6);
            Assert.Equal(3 * KeyboardPanel.DefaultPitchY, cap.Bounds.Y, 6);
            Assert.Equal(expectedWidth, cap.Bounds.Width, 6);
        }

        [AvaloniaFact]
        public void Arrange_WithACapNarrowerThanTheGap_ClampsToZeroRatherThanGoingNegative()
        {
            var panel = CreatePanel();
            var cap = AddKey(panel, unitX: 0, unitY: 0, unitWidth: 0.05, unitHeight: 0.05);

            Arrange(panel);

            // 0.05 U is 1.7 px across against a 4 px gap. A negative Rect throws, so the sliver
            // collapses instead.
            Assert.Equal(0, cap.Bounds.Width);
            Assert.Equal(0, cap.Bounds.Height);
        }

        [AvaloniaFact]
        public void Arrange_SubtractsThePanelsOwnKeyUnitOrigin()
        {
            // What lets one panel draw a SECTION of a board: the caps keep the board-absolute units
            // arrow navigation reads, and the panel re-bases nothing but its own placement.
            var panel = CreatePanel();

            panel.UnitOriginX = 10.25;
            panel.UnitOriginY = 1;

            var corner = AddKey(panel, unitX: 10.25, unitY: 1);
            var inland = AddKey(panel, unitX: 12.25, unitY: 3);

            Arrange(panel);

            Assert.Equal(new Point(0, 0), corner.Bounds.Position);
            Assert.Equal(2 * KeyboardPanel.DefaultPitchX, inland.Bounds.X, 6);
            Assert.Equal(2 * KeyboardPanel.DefaultPitchY, inland.Bounds.Y, 6);
        }

        [AvaloniaFact]
        public void TheKeyUnitOrigin_DefaultsToTheBoardsOwn()
        {
            // A one-piece board is one section standing at 0,0 and says nothing about an origin.
            var panel = new KeyboardPanel();

            Assert.Equal(0, panel.UnitOriginX);
            Assert.Equal(0, panel.UnitOriginY);
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

            Arrange(panel);

            Assert.NotEqual(default, first.Bounds);
            Assert.NotEqual(default, second.Bounds);

            // The board goes away — a layer with no picture, or a device whose visual has not been
            // authored. An unarranged child keeps its stale bounds and would be drawn on top of the
            // empty picture, so every one of them has to be arranged to nothing.
            panel.BoardWidth = 0;

            Arrange(panel);

            Assert.Equal(default, first.Bounds);
            Assert.Equal(default, second.Bounds);
        }

        [AvaloniaFact]
        public void Arrange_WithAnInvalidBoard_StillFillsTheSpaceItWasGiven()
        {
            var panel = CreatePanel(0, 0);

            AddKey(panel, unitX: 0, unitY: 0);

            panel.Measure(Size.Infinity);
            panel.Arrange(new Rect(0, 0, 200, 100));

            Assert.Equal(new Size(200, 100), panel.Bounds.Size);
        }

        [AvaloniaFact]
        public void ThePitchAndGap_DefaultToTheirGeometryTokens()
        {
            // The panel's C# fallbacks are what a design-time preview and a targeted test draw on.
            // They are not a second source of truth: if Themes/Geometry.axaml moves, this says so.
            foreach (var variant in DesignTokens.Variants)
            {
                Assert.Equal(KeyboardPanel.DefaultPitchX, (double)DesignTokens.Resolve("KeycapPitchX", variant));
                Assert.Equal(KeyboardPanel.DefaultPitchY, (double)DesignTokens.Resolve("KeycapPitchY", variant));
                Assert.Equal(KeyboardPanel.DefaultGap, (double)DesignTokens.Resolve("KeycapGap", variant));
            }
        }

        [AvaloniaFact]
        public void ChangingThePitchAndGap_MovesTheBoard()
        {
            // The link the tokens are handed in for: the pitch is a property, not a literal baked
            // into the arithmetic, so a change in Themes/Geometry.axaml reaches the glass.
            var panel = CreatePanel();
            var cap = AddKey(panel, unitX: 1, unitY: 1);

            panel.PitchX = 50;
            panel.PitchY = 40;
            panel.Gap = 10;

            Arrange(panel);

            Assert.Equal(new Rect(50, 40, 40, 30), cap.Bounds);
            Assert.Equal(new Size((BoardWidth * 50) - 10, (BoardHeight * 40) - 10), panel.DesiredSize);
        }

        [AvaloniaFact]
        public void ThePanel_ScalesNothing()
        {
            // The regression BoardScaleHost exists to prevent: a panel that also scaled would scale
            // the picture twice, and the caps would part company with everything drawn on them.
            var panel = CreatePanel();
            var cap = AddKey(panel, unitX: 1, unitY: 1);

            panel.Measure(new Size(NaturalWidth * 4, NaturalHeight * 4));
            panel.Arrange(new Rect(0, 0, NaturalWidth * 4, NaturalHeight * 4));

            Assert.Equal(new Rect(
                KeyboardPanel.DefaultPitchX,
                KeyboardPanel.DefaultPitchY,
                CapWidth,
                CapHeight), cap.Bounds);
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
        /// questions: it rejects a NaN constraint outright, and the panel's whole claim is that the
        /// offered space changes nothing — which is only interesting if the offer can be absurd.
        /// </summary>
        private static Size MeasureBoard(KeyboardPanel panel, double width, double height)
        {
            var method = typeof(KeyboardPanel).GetMethod(
                MeasureOverrideName,
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("KeyboardPanel has no MeasureOverride.");

            return (Size)method.Invoke(panel, [new Size(width, height)])!;
        }

        /// <summary>
        /// Measures the panel unconstrained and arranges it at exactly the size it asked for, which
        /// is what <see cref="BoardScaleHost"/> does to it in the app.
        /// </summary>
        private static void Arrange(KeyboardPanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
            panel.Measure(Size.Infinity);
            panel.Arrange(new Rect(default, panel.DesiredSize));
        }
    }
}
