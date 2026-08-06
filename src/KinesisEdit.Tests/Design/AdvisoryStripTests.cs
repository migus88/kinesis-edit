using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The editor's advisory strip, rendered (docs/design/handoff.md § "Editor shell", mockup 1e):
    /// 30 px on the <c>StatusAdvisory</c> ramp, absent when the open section has nothing to say,
    /// and a <c>Review N</c> that really moves the selection.
    /// <para>
    /// Its second job is the design law that no rendering test would otherwise cover: <b>nothing
    /// about an advisory is red</b>. The macro panel's three budget rows used to bind
    /// <c>statusError</c>, and a budget is reported rather than refused — so this suite reads the
    /// resolved foregrounds and fails if any of them lands on the <c>StatusError</c> ramp.
    /// </para>
    /// <para>
    /// Asserted on arranged sizes, resolved brushes and named pixels, never on a golden image (the
    /// documented refusal in design-system.md § "Rendered-frame capture").
    /// </para>
    /// </summary>
    public class AdvisoryStripTests
    {
        /// <summary>The design's advisory-strip height (Themes/Geometry.axaml, <c>HeightAdvisoryStrip</c>).</summary>
        private const double StripHeight = 30;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheStrip_RendersAtThirtyOnTheAdvisoryTint(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            view.DataContext = await scenes.CreateEditorWithAdvisoriesAsync();

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var strip = StripOf(view);

            Assert.NotNull(strip);
            Assert.Equal(StripHeight, strip.Bounds.Height);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTintBrush", variant), strip.Background);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTintBorderBrush", variant), strip.BorderBrush);
        }

        [AvaloniaFact]
        public async Task TheStripsTint_ReachesTheGlass()
        {
            // Through the whole system, not just the resolved property: the token, under a variant,
            // applied by the view, drawn by Skia, read back as a pixel. The tint is translucent, so
            // the expected colour is what it composites to over the window behind it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            view.DataContext = await scenes.CreateEditorWithAdvisoriesAsync();

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var frame = host.Capture();
            var strip = StripOf(view)!;

            // Flat ground: inside the strip, clear of the dot, the sentence and the button.
            var sample = strip.TranslatePoint(new Point(4, strip.Bounds.Height / 2), host.Window);

            Assert.NotNull(sample);

            var tint = DesignTokens.ResolveBrushColor("StatusAdvisoryTintBrush", ThemeVariant.Dark);
            var actual = FramePixels.At(frame, (int)sample.Value.X, (int)sample.Value.Y);

            // The amber is there, and it is not the error ramp's red.
            Assert.True(actual.R > actual.B, $"The strip drew {actual}, which is not an amber tint.");
            Assert.True(
                actual.R >= actual.G && actual.G > actual.B,
                $"The strip drew {actual}, which is not on the {tint} ramp.");
            Assert.NotEqual(
                DesignTokens.ResolveBrushColor("StatusErrorBrush", ThemeVariant.Dark),
                actual);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheStrip_IsAbsentWhenTheSectionHasNothingToSay(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var editor = (KeyboardEditorViewModel)view.DataContext!;

            Assert.Equal(0, editor.AdvisoryStrip.AdvisoryCount);
            Assert.False(editor.AdvisoryStrip.HasAdvisories);
            Assert.Null(StripOf(view));

            // And the row it sits in collapses, so nothing below it shifts when it appears.
            var strip = view.GetVisualDescendants().OfType<AdvisoryStripView>().Single();

            Assert.Equal(0, strip.Bounds.Height);
        }

        [AvaloniaFact]
        public async Task TheStrip_CarriesADotTheSentenceAndReviewN()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = await scenes.CreateEditorWithAdvisoriesAsync();

            view.DataContext = editor;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var strip = StripOf(view)!;

            Assert.Single(strip.GetVisualDescendants().OfType<Ellipse>());

            var sentence = strip.GetVisualDescendants().OfType<TextBlock>().First();

            Assert.Equal(editor.AdvisoryStrip.AdvisorySummary, sentence.Text);
            Assert.False(string.IsNullOrWhiteSpace(sentence.Text));

            var review = strip.GetVisualDescendants().OfType<Button>().Single();

            Assert.Equal(editor.AdvisoryStrip.ReviewCaption, review.Content);
            Assert.Equal(AdvisoryText.Review(editor.AdvisoryStrip.AdvisoryCount), review.Content);
            Assert.Same(editor.AdvisoryStrip.ReviewAdvisoriesCommand, review.Command);

            // Bordered, per the handoff, and reached through the class bridge like every button.
            Assert.Same(DesignTokens.Resolve("SecondaryButton", ThemeVariant.Dark), review.Theme);
        }

        [AvaloniaFact]
        public async Task ReviewN_SelectsTheFirstAnchoredKeyAndThenCyclesThroughTheRest()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = await scenes.CreateEditorWithAdvisoriesAsync();

            view.DataContext = editor;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var anchored = editor.Advisories
                .ForTab(EditorTab.Keys, editor.SelectedLayer!.Index)
                .Where(advisory => advisory.KeyIndex is not null)
                .Select(advisory => advisory.KeyIndex!.Value)
                .ToArray();

            Assert.True(anchored.Length >= 2, $"The scene anchored only {anchored.Length} advisories.");

            var review = StripOf(view)!.GetVisualDescendants().OfType<Button>().Single();

            foreach (var expected in anchored)
            {
                review.Command!.Execute(review.CommandParameter);

                Assert.Equal(expected, editor.SelectedKey?.Index);

                // Reviewing is reading: it must never arm the keyboard for the next keystroke.
                Assert.False(editor.IsListening);
            }

            // And it wraps rather than stopping at the end.
            review.Command!.Execute(review.CommandParameter);

            Assert.Equal(anchored[0], editor.SelectedKey?.Index);
        }

        [AvaloniaFact]
        public async Task TheStripsSentenceAndCount_FollowTheOpenSection()
        {
            // One control in one row serves all four tabs, which only works if what it shows is the
            // selected tab's.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = await scenes.CreateEditorWithAdvisoriesAsync();

            view.DataContext = editor;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.NotNull(StripOf(view));

            editor.SelectedTab = EditorTab.Macros;

            host.Capture();

            // The scene's advisories are all duplicate-key notes, which belong to the Layout tab.
            Assert.Equal(0, editor.AdvisoryStrip.AdvisoryCount);
            Assert.Null(StripOf(view));

            editor.SelectedTab = EditorTab.Keys;

            host.Capture();

            Assert.NotNull(StripOf(view));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheMacroBudgetRows_AreAmberAndNeverRed(string variantName)
        {
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            var editor = (KeyboardEditorViewModel)view.DataContext!;

            editor.SelectedTab = EditorTab.Macros;

            host.Capture();

            // Scoped to the macro panel: the editor also hosts the settings panel, which reports
            // genuine failures (an unwritable settings file) and is legitimately red.
            var panel = view.GetVisualDescendants()
                .OfType<Control>()
                .First(control => control.DataContext is MacroPanelViewModel);

            var marked = panel.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Classes.Contains("statusWarning") || block.Classes.Contains("statusError"))
                .ToArray();

            Assert.NotEmpty(marked);
            Assert.All(marked, block => Assert.DoesNotContain("statusError", block.Classes));

            // Not just the class name: the resolved colour, in both variants.
            var advisory = DesignTokens.Resolve("StatusAdvisoryTextBrush", variant);
            var error = DesignTokens.Resolve("StatusErrorTextBrush", variant);

            Assert.All(marked, block => Assert.Equal(advisory, block.Foreground));
            Assert.All(marked, block => Assert.NotEqual(error, block.Foreground));
        }

        [AvaloniaFact]
        public void TheEditorsMarkup_BindsTheThreeBudgetRowsToAmberAndNamesNoErrorRampAtAll()
        {
            // The rows only carry the class while a budget is actually over, so no rendered scene
            // of a clean profile can see this. It is the flip itself: `statusError` (red) became
            // `statusWarning` (amber) on all three, and the editor now names no error role at all —
            // an advisory is never red, and every state this screen reports is an advisory.
            var xaml = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Views/KeyboardEditorView.axaml"]);

            foreach (var flag in new[] { "IsMacroOverBudget", "IsTotalOverBudget", "IsMacroCountOverBudget" })
            {
                Assert.Contains(
                    $"Classes.statusWarning=\"{{Binding Budget.{flag}}}\"",
                    xaml,
                    StringComparison.Ordinal);
            }

            Assert.DoesNotContain("statusError", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("StatusError", xaml, StringComparison.Ordinal);
        }

        [AvaloniaFact]
        public void TheStripsOwnMarkup_NamesTheAdvisoryRampAndNeverTheErrorOne()
        {
            var xaml = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Views/AdvisoryStripView.axaml"]);

            Assert.Contains("StatusAdvisoryTintBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("StatusAdvisoryTintBorderBrush", xaml, StringComparison.Ordinal);
            Assert.Contains("StatusAdvisoryBrush", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("StatusError", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain("statusError", xaml, StringComparison.Ordinal);
        }

        /// <summary>The advisory strip's own Border, or null when it is not on screen.</summary>
        private static Border? StripOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<AdvisoryStripView>()
                .SelectMany(strip => strip.GetVisualDescendants().OfType<Border>())
                .FirstOrDefault(border => border.IsVisible && border.IsEffectivelyVisible);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
