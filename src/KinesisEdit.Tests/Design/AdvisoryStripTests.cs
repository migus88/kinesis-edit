using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Core.Settings;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The editor's advisory strip, rendered (docs/design/handoff.md § "Editor shell", mockup 1e):
    /// 30 px on the <c>StatusAdvisory</c> ramp, <b>unpainted but still 30 px</b> when the open
    /// section has nothing to say, and a <c>Review N</c> that really moves the selection.
    /// <para>
    /// <b>The row is reserved, not collapsed</b> (issue #119). This suite used to assert the
    /// opposite — that a clean strip measured zero — on the reasoning that a bar nobody needs
    /// should cost nothing. That was backwards: collapsing is exactly what made an advisory
    /// arriving mid-edit shove the board, the inspector rail and the legend row down 30 px. Holding
    /// the floor in both states prevents the shift outright, which is a stronger guarantee than the
    /// one it replaces, so the assertion is inverted deliberately and paired with the two that keep
    /// it honest: a reserved bar with nothing to say paints no tint and no hairline.
    /// </para>
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

        /// <summary>
        /// Window width for the expansion tests: narrow enough that the scene's sentence needs more
        /// than one line, which is the only state in which "shown in full" differs from "trimmed".
        /// </summary>
        private const double NarrowWidth = 520;

        /// <summary>Window height for those tests; the strip is measured, not the shell.</summary>
        private const double NarrowHeight = 200;

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
        public async Task TheStrip_HoldsItsRowAndPaintsNothingWhenTheSectionHasNothingToSay(string variantName)
        {
            // THE INVERTED ONE. This asserted `Assert.Equal(0, strip.Bounds.Height)` — "the row
            // collapses, so nothing below it shifts when it appears" — which is the reasoning that
            // caused the bug: a row that collapses is a row that grows back, and the growth pushed
            // the board, the rail and the legend down mid-edit. The strip now keeps its 30 px in
            // both states, so there is nothing to grow. Same goal, kept by the opposite means.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var editor = (KeyboardEditorViewModel)view.DataContext!;

            Assert.Equal(0, editor.AdvisoryStrip.AdvisoryCount);
            Assert.False(editor.AdvisoryStrip.HasAdvisories);

            var strip = StripOf(view);

            Assert.Equal(StripHeight, strip.Bounds.Height);
            Assert.Equal(StripHeight, view.GetVisualDescendants().OfType<AdvisoryStripView>().Single().Bounds.Height);

            // Reserved is not the same as drawn: an always-amber bar over a clean profile would be
            // a standing false alarm. No tint, no hairline, and no content in the bar at all.
            AssertPaintsNothing(strip);
            Assert.NotEqual(DesignTokens.Resolve("StatusAdvisoryTintBrush", variant), strip.Background);
            Assert.NotEqual(DesignTokens.Resolve("StatusAdvisoryTintBorderBrush", variant), strip.BorderBrush);
            Assert.False(ContentOf(view).IsVisible);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AnAdvisoryArriving_MovesNothingBelowTheStrip(string variantName)
        {
            // The complaint behind issue #119, stated as the thing that must not happen: the user
            // makes an edit, an advisory appears, and the whole design under it jumps. The scene
            // factory's editor is shared, so the advisory really does arrive on the board that is
            // already on screen, through the app's own remap path.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var editor = (KeyboardEditorViewModel)view.DataContext!;

            Assert.False(editor.AdvisoryStrip.HasAdvisories);

            var stripBefore = StripOf(view).Bounds;
            var below = ContentBelowTheStrip(view);
            var belowBefore = below.Bounds;

            await scenes.CreateEditorWithAdvisoriesAsync();

            host.Capture();

            Assert.True(editor.AdvisoryStrip.HasAdvisories);

            // The bar is now painted and populated...
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTintBrush", ToVariant(variantName)), StripOf(view).Background);
            Assert.True(ContentOf(view).IsVisible);

            // ...and it occupies exactly the room it occupied while it was empty, so everything
            // below it — the board, the inspector rail and the legend row all live in that one
            // control — is where it was, to the pixel.
            Assert.Equal(stripBefore.Height, StripOf(view).Bounds.Height);
            Assert.Equal(stripBefore.Y, StripOf(view).Bounds.Y);
            Assert.Equal(belowBefore.Y, below.Bounds.Y);
            Assert.Equal(belowBefore.Height, below.Bounds.Height);
        }

        [AvaloniaFact]
        public async Task TheStrip_KeepsItsHeightAsAdvisoriesArriveAndClear()
        {
            // The other direction, on the strip alone: projecting a section with nothing to say
            // empties the bar without shrinking it, so clearing an advisory cannot shift the board
            // either. Settings is the empty section here — the scene's advisories are duplicate-key
            // notes, which belong to the Layout tab. (It was Macros until issue #140 deleted it.)
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorWithAdvisoriesAsync();
            var strip = CreateStrip(new FakeAppPreferencesStore());
            var view = new AdvisoryStripView { DataContext = strip };

            strip.Project(editor.Advisories, EditorTab.Keys, editor.SelectedLayer!.Index);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.True(strip.HasAdvisories);
            Assert.Equal(StripHeight, BorderOf(view).Bounds.Height);

            strip.Project(editor.Advisories, EditorTab.Settings, editor.SelectedLayer!.Index);

            host.Capture();

            Assert.False(strip.HasAdvisories);
            Assert.Equal(StripHeight, BorderOf(view).Bounds.Height);
            AssertPaintsNothing(BorderOf(view));

            strip.Project(editor.Advisories, EditorTab.Keys, editor.SelectedLayer!.Index);

            host.Capture();

            Assert.True(strip.HasAdvisories);
            Assert.Equal(StripHeight, BorderOf(view).Bounds.Height);
            Assert.Equal(
                DesignTokens.Resolve("StatusAdvisoryTintBrush", ThemeVariant.Dark),
                BorderOf(view).Background);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheReservedStrip_DrawsTheGroundBehindItUntilThereIsSomethingToSay(string variantName)
        {
            // Through the whole system, as TheStripsTint_ReachesTheGlass does for the amber: the
            // resolved property says "transparent", and this says the glass agrees — the reserved
            // band is the same pixel as the ground just below it, and it warms to amber only when
            // an advisory arrives.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            var frame = host.Capture();
            var strip = StripOf(view);

            // Flat ground inside the reserved band, clear of the 24px padding's contents, and the
            // same column a few pixels below the bar.
            var inside = strip.TranslatePoint(new Point(4, strip.Bounds.Height / 2), host.Window);
            var beneath = strip.TranslatePoint(new Point(4, strip.Bounds.Height + 6), host.Window);

            Assert.NotNull(inside);
            Assert.NotNull(beneath);

            var clean = FramePixels.At(frame, (int)inside.Value.X, (int)inside.Value.Y);

            Assert.Equal(FramePixels.At(frame, (int)beneath.Value.X, (int)beneath.Value.Y), clean);

            await scenes.CreateEditorWithAdvisoriesAsync();

            var advisory = FramePixels.At(host.Capture(), (int)inside.Value.X, (int)inside.Value.Y);

            Assert.NotEqual(clean, advisory);
            Assert.True(
                advisory.R > advisory.B,
                $"The strip drew {advisory} once it had something to say, which is not an amber tint.");
            Assert.NotEqual(DesignTokens.ResolveBrushColor("StatusErrorBrush", variant), advisory);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheStrip_IsTheOpenSectionsWidthAndNotTheWindows(string variantName)
        {
            // Issue #128. The strip used to be a full-bleed row of the editor grid, so it ran on
            // under the key inspector rail and read as chrome for the whole screen rather than as a
            // note about the section it annotates. It is the section's column now — the same column
            // the board and the action row live in — on every tab, which on Settings happens to be
            // the whole window because that section has no rail.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var strip = view.GetVisualDescendants().OfType<AdvisoryStripView>().Single();

            foreach (var tab in Enum.GetValues<EditorTab>())
            {
                editor.SelectedTab = tab;

                Dispatcher.UIThread.RunJobs();
                host.Capture();

                Assert.Equal(SectionColumnOf(view).Bounds.Width, strip.Bounds.Width, precision: 3);
            }

            // ...and on the two tabs that have a rail, that is strictly narrower than the window: it
            // stops at the seam, exactly where the board does.
            foreach (var tab in new[] { EditorTab.Keys, EditorTab.Lighting })
            {
                editor.SelectedTab = tab;

                Dispatcher.UIThread.RunJobs();
                host.Capture();

                var rail = view.GetVisualDescendants()
                    .OfType<Control>()
                    .Single(control => control.Classes.Contains("inspectorRail") && control.IsEffectivelyVisible);

                Assert.True(
                    strip.Bounds.Width < host.Window.ClientSize.Width,
                    $"The strip is {strip.Bounds.Width} wide in a {host.Window.ClientSize.Width} window on {tab}.");
                Assert.True(
                    strip.TranslatePoint(new Point(strip.Bounds.Width, 0), host.Window)!.Value.X
                        <= rail.TranslatePoint(default, host.Window)!.Value.X + 0.5,
                    $"The strip runs past the rail's left edge on {tab}.");
            }
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
            // One control in one row serves every tab, which only works if what it shows is the
            // selected tab's.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = await scenes.CreateEditorWithAdvisoriesAsync();

            view.DataContext = editor;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Assert.True(ContentOf(view).IsVisible);

            editor.SelectedTab = EditorTab.Settings;

            host.Capture();

            // The scene's advisories are all duplicate-key notes, which belong to the Layout tab.
            // The bar stays where it is; what it holds is what empties.
            Assert.Equal(0, editor.AdvisoryStrip.AdvisoryCount);
            Assert.False(ContentOf(view).IsVisible);
            Assert.Equal(StripHeight, StripOf(view).Bounds.Height);

            editor.SelectedTab = EditorTab.Keys;

            host.Capture();

            Assert.True(ContentOf(view).IsVisible);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheMacroBudgetRows_AreAmberAndNeverRed(string variantName)
        {
            // Read on the rail's Macro panel since issue #140 deleted the Macros tab that used to
            // carry these rows. The claim is unchanged and is the reason this case exists: the
            // three macro budget rows were once bound to `statusError`, and a budget is reported
            // rather than refused.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            // The same shared editor the view is bound to, with its rail opened on the Macro panel
            // over a position that really carries a macro.
            await scenes.CreateMacroInspectorPanelAsync();

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // Scoped to the Macro panel: the editor also hosts the settings panel, which reports
            // genuine failures (an unwritable settings file) and is legitimately red.
            var panel = view.GetVisualDescendants()
                .OfType<Control>()
                .First(control => control.DataContext is MacroInspectorPanelViewModel);

            var marked = panel.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Classes.Contains("statusWarning") || block.Classes.Contains("statusError"))
                .ToArray();

            // EXACTLY ONE error-ramp block on the whole panel, and it is not an advisory: it is the
            // composer's §11.3 message for a millisecond value the user TYPED outside 1-999, which
            // is a rejected input rather than a budget being reported. Everything else the panel
            // marks is amber. Pinned as `Single` so a second one — a budget flipped back to red,
            // say — fails here rather than shipping.
            var typedValueError = Assert.Single(marked, block => block.Classes.Contains("statusError"));

            Assert.False(typedValueError.IsEffectivelyVisible, "The delay error is showing with nothing typed.");

            var advisories = marked.Where(block => !ReferenceEquals(block, typedValueError)).ToArray();

            Assert.NotEmpty(advisories);
            Assert.All(advisories, block => Assert.Contains("statusWarning", block.Classes));

            // Not just the class name: the resolved colour, in both variants.
            var advisory = DesignTokens.Resolve("StatusAdvisoryTextBrush", variant);
            var error = DesignTokens.Resolve("StatusErrorTextBrush", variant);

            Assert.All(advisories, block => Assert.Equal(advisory, block.Foreground));
            Assert.All(advisories, block => Assert.NotEqual(error, block.Foreground));
        }

        [AvaloniaFact]
        public void TheMacroPanelsMarkup_BindsEveryBudgetMeterToAmberAndNeitherFileNamesAnErrorRamp()
        {
            // The meters only carry the class while a budget is actually over, so no rendered scene
            // of a clean profile can see this. It is the flip itself: `statusError` (red) became
            // `statusWarning` (amber) on every one, and neither the panel nor the editor around it
            // names an error role at all — an advisory is never red, and every state this screen
            // reports is an advisory.
            //
            // The meters live in Views/MacroInspectorPanelView.axaml since issue #140 deleted the
            // Macros tab and moved the last of them onto the rail; the editor is still swept for the
            // error ramp, so the claim covers the same screen it always did.
            var files = AuthoredXaml.Files();
            var panel = AuthoredXaml.WithoutComments(files["Views/MacroInspectorPanelView.axaml"]);
            var editor = AuthoredXaml.WithoutComments(files["Views/KeyboardEditorView.axaml"]);

            // BOTH of the panel's meters. It was four until issue #148 deleted the muted
            // `this macro … · macros …` line the designer's mock does not draw, taking
            // `MacroLengthMeter` and `MacroCountMeter` with it.
            var meters = new[] { "SpeedMeter", "LayoutKeystrokeMeter" };

            foreach (var meter in meters)
            {
                Assert.Contains(
                    $"Classes.statusWarning=\"{{Binding {meter}.IsOverBudget}}\"",
                    panel,
                    StringComparison.Ordinal);
            }

            // The editor around them names no error role at all.
            Assert.DoesNotContain("statusError", editor, StringComparison.Ordinal);
            Assert.DoesNotContain("StatusError", editor, StringComparison.Ordinal);

            // The panel names it EXACTLY ONCE, and not on an advisory: the composer's §11.3
            // message for a millisecond value the user typed outside 1-999. A rejected input is a
            // failure, a budget is a report, and the count is pinned so a budget cannot quietly
            // rejoin the error ramp.
            Assert.Equal(1, panel.Split("statusError", StringSplitOptions.None).Length - 1);
            Assert.Contains("Text=\"{Binding StepDelayError}\"", panel, StringComparison.Ordinal);
            Assert.DoesNotContain("StatusError", panel, StringComparison.Ordinal);
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

        [AvaloniaFact]
        public void TheStripsTwoFaces_AreClassSettersAndNotValuesOnTheElement()
        {
            // The mechanism, read off the markup, because the mistake it guards against renders
            // perfectly and is simply always amber: a local value beats a class setter, so a tint
            // written on the Border would win over the clean face for ever. It is the same trap the
            // file's sentence styles already document, and nothing but this can see it — a Border
            // that never drops its tint still passes every test that looks at the amber state.
            var xaml = AuthoredXaml.WithoutComments(AuthoredXaml.Files()["Views/AdvisoryStripView.axaml"]);

            Assert.Contains("Classes.hasAdvisories=\"{Binding HasAdvisories}\"", xaml, StringComparison.Ordinal);
            Assert.Contains("Selector=\"Border.advisoryStrip.hasAdvisories\"", xaml, StringComparison.Ordinal);
            Assert.DoesNotContain(
                "Background=\"{DynamicResource StatusAdvisoryTintBrush}\"",
                xaml,
                StringComparison.Ordinal);
            Assert.DoesNotContain(
                "BorderBrush=\"{DynamicResource StatusAdvisoryTintBorderBrush}\"",
                xaml,
                StringComparison.Ordinal);

            // And the Border itself no longer hides: exactly one IsVisible is left in the file, on
            // the contents. A second one would be the collapsing row coming back.
            Assert.Equal(
                1,
                xaml.Split("IsVisible=\"{Binding HasAdvisories}\"", StringSplitOptions.None).Length - 1);
        }

        [AvaloniaFact]
        public async Task TheSentence_IsOneTrimmedLineUntilThePreferenceSaysOtherwise()
        {
            // The default, and the only shape the strip had before `advisory_detail` existed: one
            // ellipsised line at exactly the design's 30px, however long the sentence is.
            using var scenes = new ViewSceneFactory();

            var store = new FakeAppPreferencesStore();
            var view = await CreateStripViewAsync(scenes, store);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, NarrowWidth, NarrowHeight);

            host.Capture();

            var strip = (AdvisoryStripViewModel)view.DataContext!;
            var sentence = SentenceOf(view);

            Assert.False(strip.IsExpanded);
            Assert.Equal(TextTrimming.CharacterEllipsis, sentence.TextTrimming);
            Assert.Equal(TextWrapping.NoWrap, sentence.TextWrapping);
            Assert.Equal(StripHeight, BorderOf(view).Bounds.Height);
        }

        [AvaloniaFact]
        public async Task TheSentence_IsShownWholeAndGrowsTheStrip_WhenThePreferenceIsOn()
        {
            using var scenes = new ViewSceneFactory();

            var store = new FakeAppPreferencesStore();

            store.SetInitial(AppPreferenceCatalog.AdvisoryDetail.SetValue(AppSettings.Empty, true));

            var view = await CreateStripViewAsync(scenes, store);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, NarrowWidth, NarrowHeight);

            host.Capture();

            var strip = (AdvisoryStripViewModel)view.DataContext!;
            var sentence = SentenceOf(view);
            var border = BorderOf(view);

            Assert.True(strip.IsExpanded);
            Assert.Equal(TextTrimming.None, sentence.TextTrimming);
            Assert.Equal(TextWrapping.Wrap, sentence.TextWrapping);

            // Grown to fit, not scrolled and not clipped: the whole sentence is on screen.
            Assert.True(
                border.Bounds.Height > StripHeight,
                $"The expanded strip is {border.Bounds.Height}px, so the sentence did not grow it.");
            Assert.True(sentence.Bounds.Height > 0);

            // Still an advisory: amber, and nothing about it became a new surface.
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTintBrush", ThemeVariant.Dark), border.Background);
        }

        [AvaloniaFact]
        public async Task TheStrip_FollowsThePreferenceWhileItIsOnScreen()
        {
            // The preference is edited on the Settings tab of the very editor this strip is drawn
            // in, so a toggle there has to move it without reopening anything.
            using var scenes = new ViewSceneFactory();

            var store = new FakeAppPreferencesStore();
            var view = await CreateStripViewAsync(scenes, store);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark, NarrowWidth, NarrowHeight);

            host.Capture();

            var strip = (AdvisoryStripViewModel)view.DataContext!;

            Assert.Equal(StripHeight, BorderOf(view).Bounds.Height);

            store.Update(settings => AppPreferenceCatalog.AdvisoryDetail.SetValue(settings, true));

            host.Capture();

            Assert.True(strip.IsExpanded);
            Assert.Equal(TextWrapping.Wrap, SentenceOf(view).TextWrapping);
            Assert.True(BorderOf(view).Bounds.Height > StripHeight);

            // And back: unticking it returns the strip to the one trimmed line.
            store.Update(settings => AppPreferenceCatalog.AdvisoryDetail.SetValue(settings, false));

            host.Capture();

            Assert.False(strip.IsExpanded);
            Assert.Equal(TextTrimming.CharacterEllipsis, SentenceOf(view).TextTrimming);
            Assert.Equal(StripHeight, BorderOf(view).Bounds.Height);
        }

        /// <summary>
        /// An <see cref="AdvisoryStripView"/> over a strip of this scene's advisories and
        /// <paramref name="store"/>. The scene's own editor holds no session, so its strip reads
        /// the default preferences; this one is built by hand to put a real store behind it.
        /// </summary>
        private static async Task<AdvisoryStripView> CreateStripViewAsync(
            ViewSceneFactory scenes,
            FakeAppPreferencesStore store)
        {
            var editor = await scenes.CreateEditorWithAdvisoriesAsync().ConfigureAwait(true);
            var strip = CreateStrip(store);

            strip.Project(editor.Advisories, EditorTab.Keys, editor.SelectedLayer!.Index);

            Assert.True(strip.HasAdvisories);

            return new AdvisoryStripView { DataContext = strip };
        }

        private static AdvisoryStripViewModel CreateStrip(FakeAppPreferencesStore store)
        {
            return new AdvisoryStripViewModel(_ => { }, _ => { }, store);
        }

        /// <summary>The strip's sentence, which is its only <see cref="TextBlock"/>.</summary>
        private static TextBlock SentenceOf(Control view)
        {
            return view.GetVisualDescendants().OfType<TextBlock>().First();
        }

        /// <summary>
        /// Asserts that <paramref name="strip"/> draws nothing: no tint, no hairline. Read off the
        /// resolved colour rather than the brush instance, because the styles' <c>Transparent</c>
        /// is parsed into a brush of its own and never the <see cref="Brushes"/> singleton.
        /// </summary>
        private static void AssertPaintsNothing(Border strip)
        {
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(strip.Background);
            var border = Assert.IsAssignableFrom<ISolidColorBrush>(strip.BorderBrush);

            Assert.Equal(Colors.Transparent, background.Color);
            Assert.Equal(Colors.Transparent, border.Color);
            Assert.Equal(default, strip.BorderThickness);
        }

        /// <summary>The strip's own Border, for a view hosted on its own rather than in the editor.</summary>
        private static Border BorderOf(Control view)
        {
            return view.GetVisualDescendants().OfType<Border>().First();
        }

        /// <summary>
        /// The advisory strip's own Border, hosted in the editor. It is never absent any more —
        /// the bar holds its row in both states — so this no longer answers null and no longer
        /// filters by visibility; whether it has anything <i>in</i> it is <see cref="ContentOf"/>.
        /// </summary>
        private static Border StripOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<AdvisoryStripView>()
                .SelectMany(strip => strip.GetVisualDescendants().OfType<Border>())
                .First();
        }

        /// <summary>The strip's contents — the dot, the sentence and <c>Review N</c> — which are what come and go.</summary>
        private static Grid ContentOf(Control view)
        {
            return StripOf(view).GetVisualDescendants().OfType<Grid>().First();
        }

        /// <summary>
        /// The control the editor draws directly below the strip. The board, the action row and the
        /// legend row all live inside it, so its position is what "the design shifted" means. Found
        /// by grid row rather than by type, so it survives whatever the tab is rebuilt into.
        /// <para>
        /// The inspector rail is <b>not</b> in it any more (issue #128): it is a column of the body
        /// grid rather than a row of the content, which is exactly why it now runs the whole height.
        /// </para>
        /// </summary>
        private static Control ContentBelowTheStrip(Control view)
        {
            var strip = view.GetVisualDescendants().OfType<AdvisoryStripView>().Single();
            var rows = (Grid)strip.GetVisualParent()!;
            var stripRow = Grid.GetRow(strip);

            return rows.Children.OfType<Control>().Single(child => Grid.GetRow(child) > stripRow);
        }

        /// <summary>
        /// The open section's column of the editor's body grid — the seam's left-hand neighbour,
        /// which holds the advisory strip over the padded content.
        /// </summary>
        private static Control SectionColumnOf(Control view)
        {
            var seam = view.GetVisualDescendants().OfType<GridSplitter>().Single();
            var body = (Grid)seam.GetVisualParent()!;

            return body.Children.OfType<Control>().Single(child => Grid.GetColumn(child) == 0);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
