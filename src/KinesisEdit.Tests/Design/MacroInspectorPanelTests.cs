using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The key inspector's Macro panel at the glass, in both theme variants (mockup <c>2i</c>): the
    /// steps as the mock draws them, the reorder affordance, the record banner, the footer meters
    /// and the amber-never-red budget — plus the one measurement the whole panel hangs off, the
    /// rail widening from 268 px to 300.
    /// <para>
    /// The panel is hosted at the rail's real <b>300 px</b>, not at a comfortable test width: a row
    /// that reads fine at 600 px runs off the rail at 300, and no view-model test can see it.
    /// </para>
    /// </summary>
    public class MacroInspectorPanelTests
    {
        /// <summary>The rail's macro-editing width (<c>WidthInspectorRailWide</c>).</summary>
        private const double WideRailWidth = 300;

        /// <summary>Its ordinary width (<c>WidthInspectorRail</c>), for the comparison below.</summary>
        private const double RailWidth = 268;

        /// <summary>
        /// How far a probed pixel may sit from the token it is meant to be painted in. Wider than a
        /// filled face's tolerance on purpose: a glyph at 11 px is mostly its own anti-aliased edge,
        /// so the assertion that carries the weight is the <em>relative</em> one — closer to the
        /// tint than to the ramp the row would use without it.
        /// </summary>
        private const double GlyphAntiAliasTolerance = 60;

        /// <summary>
        /// The handoff states both widths — "inspector rail: 268px wide on Layout, 300px on the
        /// macro-editing variant" — and <c>WidthInspectorRailWide</c> carried the second with
        /// nothing reaching it until now. Measured on the <b>laid-out</b> rail, because a bridge
        /// that stopped matching leaves the property unset and the frame looks plausible.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRail_Widens_WhileTheMacroPanelIsShowing(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var view = new KeyboardEditorView { DataContext = editor };

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            // The cap is clicked AFTER the view is on screen: attaching the editor makes the layer
            // switcher raise its initial selection, which closes the rail again.
            var layer = Assert.IsType<KeyboardLayerViewModel>(editor.SelectedLayer);

            editor.SelectKeyCommand.Execute(layer.Keys[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var rail = Assert.Single(view.GetVisualDescendants().OfType<KeyInspectorView>());
            var frame = Assert.Single(rail.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("inspectorRail"));

            Assert.False(editor.Inspector.IsWide);
            Assert.Equal(RailWidth, frame.Bounds.Width);

            SelectMacroMode(editor);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(editor.Inspector.IsWide);
            Assert.Equal(WideRailWidth, frame.Bounds.Width);

            // ...and back, or the rail would stay wide for every mode after the first macro edit.
            SelectRemapMode(editor);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(editor.Inspector.IsWide);
            Assert.Equal(RailWidth, frame.Bounds.Width);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheSteps_RenderAsTheMockDrawsThem(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorStepsViewModel.SectionTitle, texts);
            Assert.Contains(MacroInspectorStepsViewModel.ReorderHintPrefix, texts);
            Assert.Contains(panel.Steps.ReorderShortcut, texts);
            Assert.Contains(MacroInspectorStepsViewModel.InsertStepCaption, texts);
            Assert.Contains(MacroInspectorPanelViewModel.CaptureRule, texts);

            // Three recorded steps: "01 [e] tap", "02 [s] tap", "03 [t] tap".
            Assert.Equal(3, panel.Steps.Count);
            Assert.Contains("01", texts);
            Assert.Contains("[e]", texts);
            Assert.Contains(MacroInspectorStepViewModel.TapAction, texts);

            // The trailing row is numbered as the step the next keystroke lands in.
            Assert.Contains("04", texts);
        }

        /// <summary>
        /// Neither the grip nor the insert affordance may be a character: <c>⠿</c> (U+283F) and
        /// <c>＋</c> (U+FF0B) are in <b>neither</b> embedded IBM Plex family and would draw as tofu.
        /// They are geometry marks drawn by the <c>Icon</c> control.
        /// </summary>
        [AvaloniaFact]
        public async Task TheGripAndTheInsertMark_AreGeometryAndNotGlyphs()
        {
            using var scenes = new ViewSceneFactory();

            var view = new MacroInspectorPanelView { DataContext = await scenes.CreateMacroInspectorPanelAsync() };

            using var host = Show(view, "Dark");

            host.Capture();

            var icons = view.GetVisualDescendants()
                .OfType<Icon>()
                .Where(icon => icon.IsEffectivelyVisible)
                .ToArray();

            Assert.Contains(icons, icon => icon.Classes.Contains("dragHandle"));
            Assert.Contains(icons, icon => icon.Classes.Contains("insertMark"));
            Assert.All(icons, icon => Assert.NotNull(icon.Data));

            foreach (var text in VisibleTextsOf(view))
            {
                Assert.DoesNotContain('⠿', text);
                Assert.DoesNotContain('＋', text);
                Assert.DoesNotContain('●', text);
            }
        }

        /// <summary>
        /// The record dot is an <c>Ellipse</c> for the same reason, and the button is the app's red
        /// one — <c>handoff.md:82</c> gives that hue to Record as well as to Discard.
        /// </summary>
        [AvaloniaFact]
        public async Task TheRecordButton_CarriesTheDrawnDotAndTheRedTheme()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            var record = Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => button.Classes.Contains("recordAction"));

            Assert.Same(panel.RecordCommand, record.Command);
            Assert.NotNull(record.Theme);
            Assert.Contains(record.GetVisualDescendants().OfType<Ellipse>(), dot => dot.Classes.Contains("recordDot"));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRecordingBanner_NamesTheStepAndOnlyShowsWhileArmed(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.DoesNotContain(panel.RecordingBanner, VisibleTextsOf(view));

            panel.RecordCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Contains(
                MacroInspectorPanelViewModel.BuildRecordingBanner(panel.Steps.NextStepNumberText),
                VisibleTextsOf(view));

            panel.Deactivate();
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheFooterMeters_ReadOutTheThreeBudgetsTheMockNames(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.SpeedMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.MacroLengthMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel, texts);
            Assert.Contains(panel.MacroLengthMeter.Caption, texts);
            Assert.Contains(panel.LayoutKeystrokeMeter.Caption, texts);
        }

        /// <summary>
        /// An over-budget meter goes <b>amber and still saves</b>. It must never reach the error
        /// ramp — that is the design law the three flipped macro-budget rows of #91 exist to
        /// protect, and this is the same law on the rail.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AnOverBudgetMeter_IsAmberAndNeverTheErrorRamp(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.False(panel.MacroLengthMeter.IsOverBudget);

            OverfillTheMacro(panel);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(panel.MacroLengthMeter.IsOverBudget);

            var readout = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == panel.MacroLengthMeter.Caption);

            Assert.Contains("statusWarning", readout.Classes);
            Assert.DoesNotContain("statusError", readout.Classes);

            Assert.Equal(
                DesignTokens.ResolveBrushColor("StatusAdvisoryTextBrush", ToVariant(variantName)),
                ((Avalonia.Media.ISolidColorBrush)readout.Foreground!).Color);
        }

        /// <summary>
        /// The delay field <b>is</b> a real <c>TextBox</c>, and that is the opposite of the action
        /// fields' rule: focus inside one suspends the capture service, which is exactly right for a
        /// value that is typed rather than pressed (§11.3's millisecond count).
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheDelayEditor_OpensOverARowWithATypedField(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBox>(), box => box.IsEffectivelyVisible);

            panel.Steps.EditDelayCommand.Execute(panel.Steps.Items[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorStepsViewModel.RandomDelayCaption, texts);
            Assert.Contains(MacroInspectorStepsViewModel.CustomDelayCaption, texts);

            var field = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                box => box.IsEffectivelyVisible);

            Assert.Contains("monoValue", field.Classes);
            Assert.NotNull(field.Theme);
        }

        /// <summary>
        /// AC 5: the held modifiers are mockup <c>2i</c>'s marks — the mark itself set in
        /// <c>.keySymbol</c>, and the file's two-character codes nowhere in the list. The step
        /// recorded here holds <b>Left</b> Shift, which is the unmarked side, so the row is one run
        /// and carries no side letter at all; the two-runs-in-two-faces case is
        /// <see cref="ARightModifierDrawsItsSideRun_AndALeftOneDrawsNone"/>.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AModifiedStep_DrawsItsMarksInTheKeySymbolFace(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();

            RecordShiftedStep(panel);

            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            // Scoped to the STEP ROWS deliberately: the co-trigger toggles in the footer draw marks
            // of their own now, so an unscoped sweep of the panel finds seven and this assertion is
            // about the one in the list.
            var blocks = panel.Steps.Items
                .Select(step => RowOf(view, step.Position))
                .SelectMany(row => row.GetVisualDescendants().OfType<TextBlock>())
                .Where(block => block.IsEffectivelyVisible)
                .ToArray();

            var mark = Assert.Single(blocks, block => block.Classes.Contains("keySymbol"));

            Assert.Equal(MacroModifierMarks.ShiftMark, mark.Text);

            // Set in the THIRD family, not in Plex: no IBM Plex face carries U+21E7, so a mark that
            // inherited the row's mono family would draw tofu.
            Assert.Equal(
                (FontFamily)DesignTokens.Resolve("FontKeySymbols", ToVariant(variantName)),
                mark.FontFamily);

            // LEFT DRAWS NO SIDE RUN AT ALL — it is the unmarked side, so nothing in the list is a
            // side letter. Asserted against the letters themselves rather than against
            // `MacroModifierMarks.LeftSide`, which is the empty string and would make this vacuous.
            Assert.DoesNotContain(blocks, block => block.Text is "L" or "R");

            // The mark still carries the words that say WHICH shift it was, because the glyph no
            // longer can: `⇧` is worn by Left Shift and by a generic Shift alike.
            Assert.Equal("Left Shift", MarkTipOf(view, panel.Steps.Items[^1].Position));

            // ...and nothing in the list still reads as the file's own spelling.
            foreach (var block in blocks)
            {
                Assert.NotEqual("LS", block.Text);
                Assert.NotEqual("S ", block.Text);
            }
        }

        /// <summary>
        /// The spelling rule at the glass (issue #122): <b>only a right-hand modifier draws a side
        /// run</b>. A left one is the bare mark and nothing beside it, which is exactly what makes
        /// it indistinguishable from a generic modifier on the row — so the tooltip is asserted
        /// here too, because it is the only thing left that separates them.
        /// <para>
        /// At the glass rather than in the view model on purpose: the side run is a
        /// <c>TextBlock</c> under <c>IsVisible="{Binding HasSide}"</c>, and a binding that stopped
        /// matching would leave an empty run in the tree that every view-model assertion survives.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task ARightModifierDrawsItsSideRun_AndALeftOneDrawsNone(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();

            RecordStepWith(panel, "b", "lshft");
            RecordStepWith(panel, "c", "rshft");

            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var left = Assert.Single(panel.Steps.Items, step => step.TokenText == "[b]");
            var right = Assert.Single(panel.Steps.Items, step => step.TokenText == "[c]");

            Assert.Equal(MacroModifierMarks.NoSide, Assert.Single(left.Modifiers).Side);
            Assert.Equal(MacroModifierMarks.RightSide, Assert.Single(right.Modifiers).Side);

            // The LEFT row: the mark, in the key-symbol face, and no side letter anywhere in it.
            var leftRuns = VisibleRunsOf(RowOf(view, left.Position));

            Assert.Equal(
                MacroModifierMarks.ShiftMark,
                Assert.Single(leftRuns, run => run.Classes.Contains("keySymbol")).Text);
            Assert.DoesNotContain(leftRuns, run => run.Text is "L" or "R");

            // The RIGHT row: the same mark, with an `R` beside it — a run of its own in the
            // ordinary mono face, because the key-symbol subset carries no Latin letters.
            var rightRuns = VisibleRunsOf(RowOf(view, right.Position));

            Assert.Equal(
                MacroModifierMarks.ShiftMark,
                Assert.Single(rightRuns, run => run.Classes.Contains("keySymbol")).Text);

            var side = Assert.Single(rightRuns, run => run.Text == MacroModifierMarks.RightSide);

            Assert.Contains("monoValueSmall", side.Classes);
            Assert.DoesNotContain("keySymbol", side.Classes);

            // And the words, which are the only place the file's own distinction still shows.
            Assert.Equal("Left Shift", MarkTipOf(view, left.Position));
            Assert.Equal("Right Shift", MarkTipOf(view, right.Position));
        }

        /// <summary>
        /// AC 6: the struck key really is painted <c>MacroStepKeyBrush</c> — read off the frame, not
        /// off the binding, because a token that resolves is not the same question as a token that
        /// reaches the glass.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheStruckKey_IsPaintedWithTheMacroStepTint(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            var frame = host.Capture();

            var token = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == panel.Steps.Items[0].TokenText);

            var origin = token.TranslatePoint(new Point(0, 0), host.Window)
                ?? throw new InvalidOperationException("The step token is not in the window's tree.");

            var tint = DesignTokens.ResolveBrushColor("MacroStepKeyBrush", variant);
            var untinted = DesignTokens.ResolveBrushColor("TextPrimaryBrush", variant);
            var painted = new List<Color>();

            for (var x = 0; x < token.Bounds.Width; x++)
            {
                for (var y = 0; y < token.Bounds.Height; y++)
                {
                    painted.Add(FramePixels.At(frame, (int)origin.X + x, (int)origin.Y + y));
                }
            }

            var toTint = painted.Min(pixel => Distance(pixel, tint));
            var toUntinted = painted.Min(pixel => Distance(pixel, untinted));

            Assert.True(
                toTint <= GlyphAntiAliasTolerance,
                $"No pixel of `{token.Text}` came within {GlyphAntiAliasTolerance} of {variantName}'s "
                + $"MacroStepKey; the closest was {toTint:F1} away.");
            Assert.True(
                toTint < toUntinted,
                $"The step token is closer to the ordinary text ramp ({toUntinted:F1}) than to the "
                + $"macro tint ({toTint:F1}); it is not being tinted at all.");
        }

        /// <summary>
        /// AC 7, and the defect that made the whole gesture dead: column 1 of a row is a
        /// <c>Button</c>, which handles the left press, so a handler attached from the markup — with
        /// <c>handledEventsToo: false</c> — never saw a press on the row <b>body</b>. Only the 12 px
        /// grip armed anything. This is the drag a user actually makes.
        /// <para>
        /// It is driven through Avalonia's own input pipeline rather than by calling
        /// <c>MoveStep</c>: both defects this covers live in the view, and a view-model test cannot
        /// see either.
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public async Task ADragFromTheRowBody_ReordersTheStep()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            Assert.Equal(["[e]", "[s]", "[t]"], TokensOf(panel));

            Drag(host, RowBodyPointOf(host, view, 1), RowBodyPointOf(host, view, 3));

            host.Capture();

            // The first step carried to the last row: the delay folded behind a step travels with
            // it, which is MoveStep's rule and stays MoveStep's rule.
            Assert.Equal(["[s]", "[t]", "[e]"], TokensOf(panel));
        }

        /// <summary>
        /// The same gesture from the grip — which is the one place the old code <em>could</em> arm —
        /// and it still did nothing, because Avalonia implicitly captures the pointer on press, so
        /// the release's source was the row the drag started from and <c>MoveStep(from, from)</c>
        /// answered false. The drop row is now resolved by hit-testing the release position.
        /// </summary>
        [AvaloniaFact]
        public async Task ADragFromTheGrip_ReordersTheStep()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            Drag(host, GripPointOf(host, view, 3), GripPointOf(host, view, 1));

            host.Capture();

            Assert.Equal(["[t]", "[e]", "[s]"], TokensOf(panel));
        }

        /// <summary>
        /// A press and release that never moved is the ordinary click that <b>selects</b> the step
        /// <c>⌥↑↓</c> will move — not a reorder, and not a no-op either. That is what the movement
        /// threshold buys: under it the row button keeps its own capture and fires.
        /// </summary>
        [AvaloniaFact]
        public async Task APressWithNoMovement_SelectsTheStepAndReordersNothing()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            var point = RowBodyPointOf(host, view, 2);

            host.Window.MouseMove(point);
            host.Window.MouseDown(point, MouseButton.Left);
            host.Window.MouseUp(point, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(["[e]", "[s]", "[t]"], TokensOf(panel));
            Assert.Same(panel.Steps.Items[1], panel.Steps.SelectedStep);
        }

        /// <summary>
        /// AC 7's other half: a reorder with no feedback reads as broken even once it works. The row
        /// the drop would land on wears a ring while the step is carried, and nothing wears it once
        /// the pointer is up.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRowUnderACarriedStep_WearsTheDropRing(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            Assert.All(panel.Steps.Items, step => Assert.False(step.IsDropTarget));

            var from = RowBodyPointOf(host, view, 1);
            var to = RowBodyPointOf(host, view, 3);

            host.Window.MouseMove(from);
            host.Window.MouseDown(from, MouseButton.Left);
            host.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // The row released on, and only it — the row the drag started from is not a drop
            // target, because releasing on it moves nothing.
            Assert.Equal([false, false, true], panel.Steps.Items.Select(step => step.IsDropTarget));
            Assert.Equal(0, VisibleDropRingsIn(RowOf(view, 1), variant));
            Assert.Equal(1, VisibleDropRingsIn(RowOf(view, 3), variant));

            host.Window.MouseUp(to, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.All(panel.Steps.Items, step => Assert.False(step.IsDropTarget));
        }

        /// <summary>
        /// The six co-trigger toggles are the step row's own marks — <c>⇧ R⇧ ⌃ R⌃ ⌥ R⌥</c> — and
        /// not the six two-line captions they replaced, which was the panel's tallest block for the
        /// least information in it. <b>Only the right-hand three draw a side run</b>: left is the
        /// unmarked side, so half of them are a single run and the caption in the tooltip is what
        /// says which side a bare mark is.
        /// <para>
        /// The assertion that carries the change is the <b>selected foreground</b>. This site is
        /// type rather than geometry precisely because <c>ToggleSegment</c>'s <c>.selected</c> face
        /// sets <c>Foreground</c>, which a <c>TextBlock</c> inherits and an <c>Icon</c> — painting
        /// from <c>Stroke</c> — cannot; a <c>muted</c> or a hand-set colour on either run would
        /// leave the mark grey on the accent fill with every other assertion here still passing.
        /// The toggle switched on is therefore a <b>right</b> one, so both of its runs are read.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheCoTriggers_AreOneLineMarks_SpellOnlyTheRightSide_AndTheSelectedFaceReachesEveryRun(
            string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            Assert.True(panel.HasCoTriggers);

            // Switched on through the panel's own command, so the `.selected` class arrives the way
            // the app puts it there rather than being set on the button by the test. Index 1 is
            // Right Shift — the two-run case, which is the only one that can prove the selected
            // foreground reaches a side letter as well as a mark.
            panel.ToggleCoTriggerCommand.Execute(panel.CoTriggers[1]);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var toggles = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.DataContext is MacroCoTriggerViewModel)
                .ToArray();

            Assert.Equal(panel.CoTriggers.Count, toggles.Length);

            // The block, read off the glass, in the order it is drawn in.
            Assert.Equal(
                ["⇧", "R⇧", "⌃", "R⌃", "⌥", "R⌥"],
                toggles.Select(toggle => string.Concat(VisibleRunsOf(toggle).Select(run => run.Text))));

            var strip = toggles[0].FindAncestorOfType<WrapPanel>()!;

            // THE SIX NOW FIT ONE ROW. With both sides prefixed they needed 282 px against the
            // strip's 276 and wrapped 5 + 1; dropping the three `L`s brings them to 260, so the
            // block is one line shorter again. Measured against the strip's own width rather than
            // pinned to a number, because a font-metric shift on another machine moves 260 — what
            // must hold is that nothing wraps and nothing runs off the rail.
            var rows = toggles
                .Select(toggle => Math.Round(toggle.TranslatePoint(new Point(0, 0), strip)!.Value.Y, 2))
                .Distinct()
                .Count();

            var used = toggles.Max(toggle => toggle.TranslatePoint(new Point(toggle.Bounds.Width, 0), strip)!.Value.X);

            Assert.True(used <= strip.Bounds.Width, $"The co-triggers need {used} px of a {strip.Bounds.Width} px strip.");
            Assert.Equal(1, rows);

            var accentText = DesignTokens.ResolveBrushColor("AccentTextBrush", variant);
            var secondary = DesignTokens.ResolveBrushColor("TextSecondaryBrush", variant);
            var selectedRuns = 0;

            foreach (var toggle in toggles)
            {
                var model = Assert.IsType<MacroCoTriggerViewModel>(toggle.DataContext);
                var runs = VisibleRunsOf(toggle);

                // Two runs for a right-hand modifier — the side letter, then the mark — and ONE for
                // a left one, which spells no side. The two-line caption is the tooltip now, and
                // nothing draws it.
                Assert.Equal(model.HasSide ? 2 : 1, runs.Length);
                Assert.Equal(model.Symbol, runs[^1].Text);
                Assert.Equal(model.Caption, toggle.GetValue(ToolTip.TipProperty));

                if (model.HasSide)
                {
                    Assert.Equal(model.Side, runs[0].Text);

                    // The side letter is NOT in the key-symbol face. The subset carries no Latin
                    // letters, so an `R` in that class would fall back to a system font.
                    Assert.DoesNotContain("keySymbol", runs[0].Classes);
                }

                Assert.Contains("keySymbol", runs[^1].Classes);

                foreach (var run in runs)
                {
                    // One line box. A two-line caption would stand more than twice its own font
                    // size tall, which is the shape this replaced.
                    Assert.DoesNotContain('\n', run.Text ?? string.Empty);
                    Assert.True(
                        run.Bounds.Height < 2 * run.FontSize,
                        $"'{run.Text}' is {run.Bounds.Height} tall at {run.FontSize}px — that is more than one line.");

                    var colour = Assert.IsAssignableFrom<ISolidColorBrush>(run.Foreground).Color;

                    // Inherited from the button in both states — which is the point of drawing this
                    // one as type.
                    if (model.IsOn)
                    {
                        Assert.Equal(accentText, colour);
                        selectedRuns++;
                    }
                    else
                    {
                        Assert.Equal(secondary, colour);
                    }
                }
            }

            // Both runs of the one that is on — the `R` and the `⇧` — or the assertion above passed
            // vacuously. Two is the number precisely because a RIGHT toggle was switched on.
            Assert.Equal(2, selectedRuns);
        }

        /// <summary>
        /// Issue #128, the reported defect: <i>"the view is only updated after the movement. I want
        /// to see a draggable element while I drag."</i> The drop ring said where the step would
        /// land and nothing at all said <b>what</b> was being carried, so until the pointer came up
        /// the list looked inert. From the 4 px threshold the source row dims in place and a copy of
        /// it rides the pointer.
        /// <para>
        /// Driven through Avalonia's own input pipeline, because everything asserted here lives in
        /// the view: a view-model test of <c>MoveStep</c> passes with no ghost at all.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheCarriedStep_LiftsIntoAGhostThatTracksThePointer(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var ghost = GhostOf(view);

            Assert.False(ghost.IsEffectivelyVisible);
            Assert.All(panel.Steps.Items, step => Assert.False(step.IsDragSource));

            var from = RowBodyPointOf(host, view, 1);

            host.Window.MouseMove(from);
            host.Window.MouseDown(from, MouseButton.Left);

            // UNDER THE THRESHOLD NOTHING LIFTS. A user who taps a row to point ⌥↑↓ at it must not
            // see the row flicker out from under them.
            host.Window.MouseMove(from + new Point(2, 0), RawInputModifiers.LeftMouseButton);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(ghost.IsEffectivelyVisible);
            Assert.All(panel.Steps.Items, step => Assert.False(step.IsDragSource));

            var overThird = RowBodyPointOf(host, view, 3);

            host.Window.MouseMove(overThird, RawInputModifiers.LeftMouseButton);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // The carried row, and ONLY it.
            Assert.Equal([true, false, false], panel.Steps.Items.Select(step => step.IsDragSource));
            Assert.True(ghost.IsEffectivelyVisible);

            // MANDATORY, and not a nicety: the drop row is found by hit-testing the pointer's
            // position against the live tree, so a hittable ghost would answer every one of those
            // tests itself and the drag would land on nothing.
            Assert.False(ghost.IsHitTestVisible);

            // It carries the row's own face rather than a second, drifting copy of it.
            Assert.Contains(
                panel.Steps.Items[0].TokenText,
                ghost.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

            var left = Canvas.GetLeft(ghost);
            var top = Canvas.GetTop(ghost);
            var overSecond = RowBodyPointOf(host, view, 2);

            host.Window.MouseMove(overSecond, RawInputModifiers.LeftMouseButton);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // IT FOLLOWS THE POINTER rather than snapping to a row: it keeps the grab point the
            // press had inside the row, so the ghost's own movement is exactly the pointer's.
            Assert.Equal(left + (overSecond.X - overThird.X), Canvas.GetLeft(ghost), 1);
            Assert.Equal(top + (overSecond.Y - overThird.Y), Canvas.GetTop(ghost), 1);

            host.Window.MouseUp(overSecond, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // ...and both are gone the moment the gesture ends. The reorder still happened.
            Assert.False(ghost.IsEffectivelyVisible);
            Assert.All(panel.Steps.Items, step => Assert.False(step.IsDragSource));
            Assert.Equal(["[s]", "[e]", "[t]"], TokensOf(panel));
        }

        /// <summary>
        /// A capture stolen mid-gesture — a flyout, a window deactivation — drops the drag, and the
        /// ghost has to go with it. Without this the panel would be left with a floating copy of a
        /// row over a list that is not being dragged.
        /// </summary>
        [AvaloniaFact]
        public async Task TheGhost_IsDroppedWhenTheCaptureIsLost()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            var ghost = GhostOf(view);
            var from = RowBodyPointOf(host, view, 1);

            host.Window.MouseMove(from);
            host.Window.MouseDown(from, MouseButton.Left);
            host.Window.MouseMove(RowBodyPointOf(host, view, 3), RawInputModifiers.LeftMouseButton);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(ghost.IsEffectivelyVisible);

            using var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);

            view.RaiseEvent(new PointerCaptureLostEventArgs(view, pointer));

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(ghost.IsEffectivelyVisible);
            Assert.All(panel.Steps.Items, step => Assert.False(step.IsDragSource));

            // And the stale gesture cannot fire on the next unrelated release.
            host.Window.MouseUp(RowBodyPointOf(host, view, 3), MouseButton.Left);

            Dispatcher.UIThread.RunJobs();

            Assert.Equal(["[e]", "[s]", "[t]"], TokensOf(panel));
        }

        /// <summary>
        /// Issue #128's other half: the chord composer. It is <b>closed at rest</b> — the rail is
        /// 300 px wide and the picker alone is most of a screenful — and discloses the eight sided
        /// modifier marks plus the same key search the rail's Remap panel hosts.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheChordComposer_IsClosedAtRest_AndDisclosesItsMarksAndItsPicker(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.Contains(MacroInspectorPanelViewModel.ComposerLabel, VisibleTextsOf(view));
            Assert.Contains(MacroInspectorPanelViewModel.ComposeChordCaption, VisibleTextsOf(view));

            // Closed: nothing of the picker is on screen, and in particular its search field is not
            // — the Steps panel's own "there is no visible TextBox until the delay editor opens"
            // rule has to survive the composer being added beside it.
            Assert.DoesNotContain(view.GetVisualDescendants().OfType<TextBox>(), box => box.IsEffectivelyVisible);
            Assert.DoesNotContain(MacroInspectorPanelViewModel.ComposerHint, VisibleTextsOf(view));

            panel.ToggleComposerCommand.Execute(null);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Contains(MacroInspectorPanelViewModel.ComposerHint, VisibleTextsOf(view));
            Assert.Contains(MacroInspectorPanelViewModel.HideComposerCaption, VisibleTextsOf(view));

            var toggles = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.DataContext is MacroChordModifier)
                .ToArray();

            // `⇧ R⇧ ⌃ R⌃ ⌥ R⌥ ⌘ R⌘` — the step list's own spelling, left unmarked and only the
            // right side spelled, with the words in the tooltip because a mark alone is not
            // accessible text.
            Assert.Equal(
                ["⇧", "R⇧", "⌃", "R⌃", "⌥", "R⌥", "⌘", "R⌘"],
                toggles.Select(toggle => string.Concat(VisibleRunsOf(toggle).Select(run => run.Text))));
            Assert.Equal("Left Ctrl", toggles[2].GetValue(ToolTip.TipProperty));

            // The mark is in the third family; the `R` beside it is not, because the key-symbol
            // subset carries no Latin letters.
            foreach (var toggle in toggles)
            {
                var runs = VisibleRunsOf(toggle);

                Assert.Contains("keySymbol", runs[^1].Classes);

                if (runs.Length > 1)
                {
                    Assert.DoesNotContain("keySymbol", runs[0].Classes);
                }
            }

            // The picker is really there, and it is the app's own — one implementation, now four
            // call sites.
            Assert.Single(view.GetVisualDescendants().OfType<TokenPickerView>());
            Assert.Single(view.GetVisualDescendants().OfType<TextBox>(), box => box.IsEffectivelyVisible);
        }

        /// <summary>
        /// The whole point of the composer, at the glass: <c>Ctrl+1</c> — which macOS keeps for
        /// itself and no capture can ever hear — authored with two clicks and written as a step.
        /// </summary>
        [AvaloniaFact]
        public async Task TheChordComposer_AuthorsCtrl1_WithoutAKeypress()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            panel.ToggleComposerCommand.Execute(null);

            Dispatcher.UIThread.RunJobs();

            var control = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.DataContext is MacroChordModifier { Modifier: MacroModifiers.LeftControl });

            control.Command!.Execute(control.CommandParameter);

            panel.ChordPicker.Query = "1";
            panel.ChordPicker.SelectedRow = panel.ChordPicker.Rows.Single(
                row => ReferenceEquals(row.Definition, KeyRegistry.FindByToken("1", TokenDialect.Gen1)));

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var insert = Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible
                          && ReferenceEquals(button.Command, panel.InsertChordCommand));

            insert.Command!.Execute(null);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var step = Assert.Single(panel.Steps.Items, item => item.TokenText == "[1]");

            Assert.Equal(MacroModifierMarks.ControlMark, Assert.Single(step.Modifiers).Symbol);
            Assert.Contains("[1]", VisibleTextsOf(view));
        }

        /// <summary>
        /// The sentence that says why the composer exists, on the panel and beside the rule it
        /// qualifies. Read off the glass, because a string constant nobody draws helps nobody.
        /// </summary>
        [AvaloniaFact]
        public async Task TheOsReservedNote_IsDrawnBesideTheCaptureRule()
        {
            using var scenes = new ViewSceneFactory();

            var view = new MacroInspectorPanelView { DataContext = await scenes.CreateMacroInspectorPanelAsync() };

            using var host = Show(view, "Dark");

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.CaptureRule, texts);
            Assert.Contains(MacroInspectorPanelViewModel.OsReservedNote, texts);
        }

        /// <summary>The floating copy of the carried row, hidden except while a drag is in flight.</summary>
        private static Border GhostOf(Control view)
        {
            return Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("macroStepGhost"));
        }

        private static void SelectMacroMode(KeyboardEditorViewModel editor)
        {
            SelectMode(editor, KeyInspectorMode.Macro);
        }

        private static void SelectRemapMode(KeyboardEditorViewModel editor)
        {
            SelectMode(editor, KeyInspectorMode.Remap);
        }

        private static void SelectMode(KeyboardEditorViewModel editor, KeyInspectorMode mode)
        {
            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == mode)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }
        }

        /// <summary>
        /// Pushes the macro past the device's per-macro cap through the panel's own record path, so
        /// the amber comes from the model rather than from a hand-set flag.
        /// </summary>
        private static void OverfillTheMacro(MacroInspectorPanelViewModel panel)
        {
            var limit = panel.MacroLengthMeter.Limit ?? 0;
            var key = KeyRegistry.FindByToken("a", TokenDialect.Gen1)!;

            panel.RecordCommand.Execute(null);

            for (var index = panel.MacroLengthMeter.Value; index <= limit; index++)
            {
                panel.ReceiveKeystroke(new Core.Input.CapturedKeystroke
                {
                    Key = key,
                    PhysicalKey = Core.Input.PhysicalKeyCode.None
                });
            }

            panel.Deactivate();
        }

        /// <summary>
        /// Records one more step with Left Shift held, through the panel's own capture path, so the
        /// step list carries a modified step to draw marks for.
        /// </summary>
        private static void RecordShiftedStep(MacroInspectorPanelViewModel panel)
        {
            RecordStepWith(panel, "b", "lshft");
        }

        /// <summary>
        /// Records one step — <paramref name="token"/> struck with <paramref name="modifierToken"/>
        /// held — through the panel's own capture path, so the modifier arrives as the flags
        /// <c>MacroModifierCodes</c> resolves rather than as one the test set by hand.
        /// </summary>
        private static void RecordStepWith(MacroInspectorPanelViewModel panel, string token, string modifierToken)
        {
            panel.RecordCommand.Execute(null);

            panel.ReceiveKeystroke(new CapturedKeystroke
            {
                Key = KeyRegistry.FindByToken(token, TokenDialect.Gen1)!,
                PhysicalKey = PhysicalKeyCode.None,
                HeldModifiers = [KeyRegistry.FindByToken(modifierToken, TokenDialect.Gen1)!]
            });

            panel.Deactivate();
        }

        /// <summary>
        /// The <c>Description</c> the one mark in that row hands its tooltip — the words that keep
        /// a bare <c>⇧</c> tellable apart from a generic one now that left spells no side.
        /// </summary>
        private static object? MarkTipOf(Control view, int position)
        {
            return Assert.Single(
                RowOf(view, position)
                    .GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Where(mark => mark.DataContext is MacroModifierMarks.Mark)
                    .Select(mark => mark.GetValue(ToolTip.TipProperty)));
        }

        /// <summary>The visible text runs of a control, in tree order.</summary>
        private static TextBlock[] VisibleRunsOf(Control control)
        {
            return control.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .ToArray();
        }

        /// <summary>The step tokens in playback order — what a reorder is read back through.</summary>
        private static IReadOnlyList<string> TokensOf(MacroInspectorPanelViewModel panel)
        {
            return panel.Steps.Items.Select(step => step.TokenText).ToArray();
        }

        /// <summary>
        /// One press-drag-release through Avalonia's own input pipeline. The move carries the left
        /// button, because a move without it is not a drag.
        /// </summary>
        private static void Drag(ThemedHost host, Point from, Point to)
        {
            host.Window.MouseMove(from);
            host.Window.MouseDown(from, MouseButton.Left);
            host.Window.MouseMove(to, RawInputModifiers.LeftMouseButton);
            host.Window.MouseUp(to, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>The row of <paramref name="position"/>, outermost first (the template's root grid).</summary>
        private static Control RowOf(Control view, int position)
        {
            return view.GetVisualDescendants()
                .OfType<Grid>()
                .First(grid => grid.DataContext is MacroInspectorStepViewModel step && step.Position == position);
        }

        /// <summary>The middle of that row's selecting button — the row <b>body</b>, not the grip.</summary>
        private static Point RowBodyPointOf(ThemedHost host, Control view, int position)
        {
            return CentreOf(
                host,
                RowOf(view, position)
                    .GetVisualDescendants()
                    .OfType<Button>()
                    .First(button => button.Classes.Contains("macroStepRow")));
        }

        /// <summary>The middle of that row's 12 px drag grip.</summary>
        private static Point GripPointOf(ThemedHost host, Control view, int position)
        {
            return CentreOf(
                host,
                RowOf(view, position)
                    .GetVisualDescendants()
                    .OfType<Icon>()
                    .First(icon => icon.Classes.Contains("dragHandle")));
        }

        private static Point CentreOf(ThemedHost host, Control control)
        {
            return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), host.Window)
                   ?? throw new InvalidOperationException("The control is not in the window's tree.");
        }

        /// <summary>
        /// How many accent rings that row is showing. The ring is the drop target's own; the
        /// selected row wears <c>AccentSelectedRing</c> instead, which is a different colour on
        /// purpose so the two can never be confused.
        /// </summary>
        private static int VisibleDropRingsIn(Control row, ThemeVariant variant)
        {
            var accent = DesignTokens.ResolveBrushColor("AccentBrush", variant);

            return row.GetVisualDescendants()
                .OfType<Border>()
                .Count(border => border.IsEffectivelyVisible
                                 && border.BorderBrush is ISolidColorBrush brush
                                 && brush.Color == accent);
        }

        private static double Distance(Color first, Color second)
        {
            var red = first.R - second.R;
            var green = first.G - second.G;
            var blue = first.B - second.B;

            return Math.Sqrt((red * red) + (green * green) + (blue * blue));
        }

        private static IReadOnlyList<string> VisibleTextsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .ToArray();
        }

        private static ThemedHost Show(Control view, string variantName)
        {
            return ThemedHost.Show(view, ToVariant(variantName), WideRailWidth, 900);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
