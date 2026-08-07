using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The keyboard canvas against the space it is given, on both boards, at four window sizes
    /// (issue #123). Two defects meet here and both are invisible to anything that asks a control
    /// about itself:
    /// <list type="number">
    /// <item><b>No ceiling.</b> <c>BoardScaleHost</c> grew the picture with every pixel on offer, so
    /// the Freestyle Edge RGB's 695x196 board was drawn <b>2238x631</b> in a 2560-wide window — caps
    /// the size of a fist under 40px legends.</item>
    /// <item><b>Measured with infinite height.</b> Both boards hung under a vertically oriented
    /// <see cref="StackPanel"/>, which measures its children with <see cref="double.PositiveInfinity"/>
    /// down its own axis. The host's vertical candidate therefore abstained, the scale came from the
    /// width alone, and the block's desired height exceeded its slot. <c>ClipToBounds</c> is
    /// deliberately false on every link of the board chain, so the overflow was <i>painted over</i>
    /// the rows around it rather than cut: at 1600x520 the picture ran to y=490 and the legend row to
    /// y=538, in a window 520 tall.</item>
    /// </list>
    /// <para>
    /// Everything here is asserted <b>in window coordinates</b>, through
    /// <see cref="Visual.TranslatePoint"/>, for the reason <see cref="BoardLegendTests"/> gives:
    /// a control's own bounds are exactly what was lying. The picture's rectangle is taken from the
    /// scale host's child — its <c>Bounds</c> stay the natural rectangle and the growth is a render
    /// transform, which <c>TranslatePoint</c> composes, so translating its far corner is the only
    /// way to learn where the drawn board actually ends.
    /// </para>
    /// <para>
    /// No golden images: the repo-wide refusal in design-system.md § "Rendered-frame capture".
    /// </para>
    /// </summary>
    public class BoardFitTests
    {
        /// <summary>Layout rounding puts an arranged edge up to half a pixel off the arithmetic.</summary>
        private const double Tolerance = 0.5;

        [AvaloniaTheory]
        // The 720x480 floor, the 1000x680 default, a short-wide window — the shape where the
        // height binds first, which is where the infinite measure actually showed — and a large
        // display, which is where the missing ceiling showed.
        [InlineData(720, 480, "Dark")]
        [InlineData(720, 480, "Light")]
        [InlineData(1000, 680, "Dark")]
        [InlineData(1000, 680, "Light")]
        [InlineData(1600, 520, "Dark")]
        [InlineData(1600, 520, "Light")]
        [InlineData(2560, 1440, "Dark")]
        [InlineData(2560, 1440, "Light")]
        public async Task TheLayoutBoard_AndItsLegendRow_StayInsideTheContentArea(
            double width,
            double height,
            string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName), width, height);

            host.Capture();

            var picture = PictureRect(view, host);
            var legend = RectOf(Single<BoardLegendView>(view), host);
            var rail = RectOf(Single<KeyInspectorView>(view), host);
            var tabs = RectOf(Single<TabStrip>(view), host);

            // The action row under the whole content area: the one thing the overflowing board was
            // painted over, so it is the neighbour worth naming.
            var actions = RectOf(Single<WrapPanel>(view), host);

            AssertInsideTheWindow(picture, host, "The picture");
            AssertInsideTheWindow(legend, host, "The legend row");

            Assert.True(
                picture.Top >= tabs.Bottom - Tolerance,
                $"The picture starts at {picture.Top}, above the tab bar's bottom edge at {tabs.Bottom}.");
            Assert.True(
                legend.Bottom <= actions.Top + Tolerance,
                $"The legend row ends at {legend.Bottom}, below the action row's top edge at {actions.Top}.");
            Assert.True(
                picture.Right <= rail.Left + Tolerance,
                $"The picture ends at {picture.Right}, past the key inspector rail's left edge at {rail.Left}.");
            Assert.True(
                picture.Bottom <= legend.Top + Tolerance,
                $"The picture ends at {picture.Bottom}, below the legend row's top edge at {legend.Top}.");
        }

        [AvaloniaTheory]
        [InlineData(720, 480, "Dark")]
        [InlineData(720, 480, "Light")]
        [InlineData(1000, 680, "Dark")]
        [InlineData(1000, 680, "Light")]
        [InlineData(1600, 520, "Dark")]
        [InlineData(1600, 520, "Light")]
        [InlineData(2560, 1440, "Dark")]
        [InlineData(2560, 1440, "Light")]
        public async Task TheLightingBoard_StaysInsideTheContentArea(
            double width,
            double height,
            string variantName)
        {
            // Hosted in the real editor rather than on its own, because the tab's content area is
            // the window minus 46px of toolbar, 38 of tab bar, 30 of advisory strip and the action
            // row — and a standalone LightingTabView would be handed all of that as if it were the
            // board's, which is exactly the extra height that hid the defect.
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var view = new KeyboardEditorView { DataContext = editor };

            editor.SelectedTab = EditorTab.Lighting;

            using var host = ThemedHost.Show(view, ToVariant(variantName), width, height);

            host.Capture();

            var picture = PictureRect(view, host);
            var rail = RectOf(Single<LightingModeRailView>(view), host);

            // The paint line sits directly under the picture and is on screen in every mode, so it
            // is what a board measured against infinity would have been drawn over.
            var paintLine = RectOf(PaintLineOf(view), host);

            AssertInsideTheWindow(picture, host, "The picture");

            Assert.True(
                picture.Right <= rail.Left + Tolerance,
                $"The picture ends at {picture.Right}, past the mode rail's left edge at {rail.Left}.");
            Assert.True(
                picture.Bottom <= paintLine.Top + Tolerance,
                $"The picture ends at {picture.Bottom}, below the paint line's top edge at {paintLine.Top}.");
            AssertInsideTheWindow(paintLine, host, "The paint line");
        }

        [AvaloniaTheory]
        [InlineData(EditorTab.Keys)]
        [InlineData(EditorTab.Lighting)]
        public async Task OnALargeDisplay_TheBoard_StopsAtTheCap(EditorTab tab)
        {
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var view = new KeyboardEditorView { DataContext = editor };

            editor.SelectedTab = tab;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 2560, 1440);

            host.Capture();

            var scaleHost = ScaleHostOf(view);
            var maximum = (double)DesignTokens.Resolve("BoardScaleMax", ThemeVariant.Dark);

            Assert.Equal(maximum, scaleHost.Scale);
        }

        [AvaloniaFact]
        public async Task AtTheCap_TheBoard_IsCentredInItsSlotRatherThanLeftAligned()
        {
            // The space a ceiling leaves over is the price of having one, and a picture stranded in
            // the corner of it would be worse than the wall of caps it replaced.
            //
            // It is the board-and-legend BLOCK that is measured against the column, not the picture
            // against the scale host: the block is content-sized and centred by the column, so the
            // host's own slot is exactly the picture and has no spare for the host to centre in.
            // BoardScaleHostTests owns the host's half of this; what is asserted here is that the
            // capped picture ends up in the middle of the space the screen has.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 2560, 1440);

            host.Capture();

            Assert.Equal(
                (double)DesignTokens.Resolve("BoardScaleMax", ThemeVariant.Dark),
                ScaleHostOf(view).Scale);

            var block = (Control)Single<KeyboardView>(view).GetVisualParent()!;
            var column = (Control)block.GetVisualParent()!;

            var blockRect = RectOf(block, host);
            var columnRect = RectOf(column, host);

            Assert.True(
                columnRect.Width - blockRect.Width > 1 && columnRect.Height - blockRect.Height > 1,
                "The board filled its column on both axes, so this test would pass vacuously.");

            AssertCentred(blockRect.Left - columnRect.Left, columnRect.Right - blockRect.Right, "horizontally");
            AssertCentred(blockRect.Top - columnRect.Top, columnRect.Bottom - blockRect.Bottom, "vertically");
        }

        [AvaloniaFact]
        public async Task AtTheWindowFloor_TheLightingBoard_IsNotStarvedByTheRowsBeneathIt()
        {
            // The cost of fitting the board to its slot: every row sharing the column became a
            // claim on the picture, and a Grid measures its Auto rows before its star one. At
            // 720x480 the Lighting tab's header, paint line and wrapped zone buttons want 231 of
            // the column's 235px, which drew the board FOUR PIXELS TALL — a board that fits its
            // content area perfectly and is not a board any more. Below HeightBoardMin the picture
            // stops giving way and the zone row runs past the column instead.
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var view = new KeyboardEditorView { DataContext = editor };

            editor.SelectedTab = EditorTab.Lighting;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, 720, 480);

            host.Capture();

            var floor = (double)DesignTokens.Resolve("HeightBoardMin", ThemeVariant.Dark);
            var picture = PictureRect(view, host);

            Assert.True(
                picture.Height >= floor - Tolerance,
                $"The board was drawn {picture.Height} tall at the 720x480 floor, under the {floor} its row keeps.");
        }

        [AvaloniaFact]
        public async Task BelowTheCap_TheBoard_StillGrowsWithTheWindow()
        {
            // The cap is a ceiling, not a fixed size: everything under it is the handoff's "scale
            // the whole board up with available space" exactly as before.
            //
            // Two views over the one editor, because a control can only have one visual parent and
            // the second window would refuse the first window's child.
            using var scenes = new ViewSceneFactory();

            double atTheFloor;

            using (var small = ThemedHost.Show(
                await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!),
                ThemeVariant.Dark,
                720,
                480))
            {
                small.Capture();

                atTheFloor = ScaleHostOf((Control)small.Window.Content!).Scale;
            }

            using var large = ThemedHost.Show(
                await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!),
                ThemeVariant.Dark,
                1000,
                680);

            large.Capture();

            var atTheDefault = ScaleHostOf((Control)large.Window.Content!).Scale;

            Assert.True(
                atTheDefault > atTheFloor,
                $"The board drew at {atTheDefault} in the default window and {atTheFloor} at the floor.");
            Assert.True(atTheDefault < (double)DesignTokens.Resolve("BoardScaleMax", ThemeVariant.Dark));
        }

        /// <summary>
        /// The drawn picture's rectangle, in the window's coordinates. The scale host's child keeps
        /// its <b>natural</b> bounds — the growth is a render transform — so the far corner has to
        /// be translated rather than added.
        /// </summary>
        private static Rect PictureRect(Control view, ThemedHost host)
        {
            var picture = ScaleHostOf(view).Child!;
            var topLeft = picture.TranslatePoint(default, host.Window)!.Value;
            var bottomRight = picture
                .TranslatePoint(new Point(picture.Bounds.Width, picture.Bounds.Height), host.Window)!
                .Value;

            return new Rect(topLeft, bottomRight);
        }

        private static BoardScaleHost ScaleHostOf(Control view)
        {
            return Assert.Single(
                view.GetVisualDescendants().OfType<BoardScaleHost>(),
                scaleHost => scaleHost.IsEffectivelyVisible);
        }

        private static Rect RectOf(Visual control, ThemedHost host)
        {
            var topLeft = control.TranslatePoint(default, host.Window)!.Value;

            return new Rect(topLeft, control.Bounds.Size);
        }

        /// <summary>
        /// The Lighting tab's paint line, found by the <c>Select all</c> button it carries — the row
        /// itself is an anonymous grid, and naming it by its own control would mean naming a
        /// template part.
        /// </summary>
        private static Control PaintLineOf(Control view)
        {
            var selectAll = Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => Equals(button.Content, LightingPaintSelection.SelectAllCaption));

            return (Control)selectAll.GetVisualParent()!;
        }

        private static T Single<T>(Control view) where T : Visual
        {
            return Assert.Single(view.GetVisualDescendants().OfType<T>(), item => item.IsEffectivelyVisible);
        }

        /// <summary>The two margins around a centred block, which have to agree to within a pixel.</summary>
        private static void AssertCentred(double before, double after, string axis)
        {
            Assert.True(
                Math.Abs(before - after) <= 1,
                $"The board is not centred {axis}: {before} of space before it and {after} after.");
        }

        private static void AssertInsideTheWindow(Rect rect, ThemedHost host, string what)
        {
            var client = new Rect(host.Window.ClientSize);

            Assert.True(
                rect.Left >= client.Left - Tolerance
                    && rect.Top >= client.Top - Tolerance
                    && rect.Right <= client.Right + Tolerance
                    && rect.Bottom <= client.Bottom + Tolerance,
                $"{what} is arranged at {rect}, outside the {client.Width}x{client.Height} window.");
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
