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
        /// <para>
        /// There are <b>two</b> of them since issue #139 and they arm different things: the Sequence
        /// header's runs a take until it is stopped and appends at the end, the composer's takes
        /// exactly one keystroke and writes it onto the selected step. Both are asserted here, and
        /// the pair being distinct commands is the assertion — a second button wired to the first's
        /// command would render identically and quietly turn the single shot into a take.
        /// </para>
        /// </summary>
        [AvaloniaFact]
        public async Task BothRecordButtons_CarryTheDrawnDotAndTheRedTheme()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            var records = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("recordAction"))
                .ToArray();

            Assert.Equal(
                [panel.RecordCommand, panel.RecordStepKeyCommand],
                records.Select(record => record.Command));

            Assert.All(records, record => Assert.NotNull(record.Theme));
            Assert.All(
                records,
                record => Assert.Contains(
                    record.GetVisualDescendants().OfType<Ellipse>(),
                    dot => dot.Classes.Contains("recordDot")));
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
        public async Task TheFooterMeters_ReadOutTheFourBudgetsTheMockNames(string variantName)
        {
            // FOUR since issue #140 moved `macros n / m` here from the deleted Macros tab's footer.
            // The new row is the only readout of that device limit left in the app, so its label,
            // its caption and its place in the stack are all part of the claim.
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.SpeedMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.MacroLengthMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.MacroCountMeterLabel, texts);
            Assert.Contains(panel.MacroLengthMeter.Caption, texts);
            Assert.Contains(panel.LayoutKeystrokeMeter.Caption, texts);
            Assert.Contains(panel.MacroCountMeter.Caption, texts);
        }

        /// <summary>
        /// Issue #140's own row, at the glass: <c>macros n / m</c> sits in line with the three
        /// meters it joined and inside the 300 px rail. A view-model test cannot see either — a
        /// fourth row that wrapped, overflowed or landed out of column would satisfy every
        /// assertion about the meter and still be visibly wrong on the panel.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheMacroCountRow_LinesUpWithItsNeighboursAndStaysInsideTheRail(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var labels = new[]
            {
                MacroInspectorPanelViewModel.MacroLengthMeterLabel,
                MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel,
                MacroInspectorPanelViewModel.MacroCountMeterLabel
            };

            var rows = labels
                .Select(label => Assert.Single(
                    view.GetVisualDescendants().OfType<TextBlock>(),
                    block => block.IsEffectivelyVisible && block.Text == label))
                .ToArray();

            var captions = new[] { panel.MacroLengthMeter, panel.LayoutKeystrokeMeter, panel.MacroCountMeter }
                .Select(meter => Assert.Single(
                    view.GetVisualDescendants().OfType<TextBlock>(),
                    block => block.IsEffectivelyVisible && block.Text == meter.Caption))
                .ToArray();

            // One column for the labels, one for the values: the new row is in both of them.
            Assert.All(rows, row => Assert.Equal(LeftEdgeOf(rows[0], view), LeftEdgeOf(row, view), precision: 3));
            Assert.All(
                captions,
                caption => Assert.Equal(RightEdgeOf(captions[0], view), RightEdgeOf(caption, view), precision: 3));

            // Stacked, not overlapping: each row sits below the one before it.
            Assert.True(
                rows[1].TranslatePoint(default, view)!.Value.Y < rows[2].TranslatePoint(default, view)!.Value.Y,
                "The macro-count row is not below the layout-keystroke row it joined.");

            // MEASURED, never pinned to a number — the rail's rule.
            Assert.All(
                rows.Concat(captions),
                block => Assert.True(
                    RightEdgeOf(block, view) <= WideRailWidth,
                    $"'{block.Text}' runs {RightEdgeOf(block, view) - WideRailWidth:0.#} px off the rail."));
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
        /// <para>
        /// Issue #139 moved it out of the per-row delay editor — that surface is deleted — and into
        /// the composer's <c>THEN WAIT</c> row, which is drawn at all times and <b>disabled</b>
        /// until a step is selected. The field being present-but-dead rather than absent is what
        /// keeps its <c>MonoValueField</c> bridge reachable on the real view, and it is the only
        /// shape that lets a typed count take the delay off <c>random</c>: a field gated on
        /// <c>fixed</c> already being the answer could never become live.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheComposersDelayField_IsAlwaysATypedField_AndComesAliveWithTheStep(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.Contains(MacroInspectorPanelViewModel.StepDelayLabel, VisibleTextsOf(view));

            var field = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                box => box.IsEffectivelyVisible);

            Assert.Contains("monoValue", field.Classes);
            Assert.NotNull(field.Theme);
            Assert.False(field.IsEffectivelyEnabled, "The delay field is live with no step selected.");

            // The three states of a step's trailing delay, and every one of them dead until the
            // composer is pointed at a row.
            var segments = DelaySegmentsOf(view);

            Assert.Equal(
                [MacroInspectorPanelViewModel.NoDelayCaption,
                 MacroInspectorPanelViewModel.FixedDelayCaption,
                 MacroInspectorStepViewModel.RandomDelayText],
                segments.Select(segment => segment.Content as string));
            Assert.All(segments, segment => Assert.False(segment.IsEffectivelyEnabled));

            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(field.IsEffectivelyEnabled);
            Assert.All(DelaySegmentsOf(view), segment => Assert.True(segment.IsEffectivelyEnabled));

            // ...and it writes as it is touched, with no `Set delay` to press: the arrow clamps 0
            // into §11.3's range, which is the route from "no delay" to a fixed one.
            panel.IncreaseStepDelayCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(panel.Steps.Items[0].HasDelay);
            Assert.Contains(panel.Steps.Items[0].DelayText, VisibleTextsOf(view));
        }

        /// <summary>
        /// The firmware gate is still answered <b>in place</b> and still has its
        /// <c>Update Firmware</c> button — the sanctioned "disabled rather than absent" that
        /// predates the composer's own, and the one thing §11.3's deleted editor could not be
        /// allowed to take with it. The scene's board clears the gate, so what is asserted here is
        /// that the refusal branch is <em>rendered and hidden</em> rather than missing: a branch
        /// that had been deleted with the editor would leave nothing to find.
        /// </summary>
        [AvaloniaFact]
        public async Task TheComposersDelay_KeepsTheFirmwareRefusalAndItsAction()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            Assert.True(panel.Steps.AreDelaysAvailable);

            var update = Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, panel.Steps.UpdateFirmwareCommand));

            Assert.False(update.IsEffectivelyVisible, "The gate passes, so its way out has nothing to say.");

            // The refusal is the block beside it, and it rides the advisory ramp and never the
            // error one — an old firmware is a fact about the device, not a failure
            // (docs/app/design-system.md, the amber-never-red law).
            var block = Assert.IsAssignableFrom<Panel>(update.GetVisualParent());
            var refusal = Assert.Single(block.GetVisualChildren().OfType<TextBlock>());

            Assert.Contains("statusWarning", refusal.Classes);
            Assert.DoesNotContain("statusError", refusal.Classes);
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
        /// <b>The Trigger strip</b> (issue #137): three left-hand latches, the trigger token and a
        /// status, in the header rather than six toggles under a <c>CO-TRIGGERS</c> label in the
        /// footer. Each latch is the step row's own mark — <c>⇧ ⌃ ⌥</c> — and each is a single run,
        /// because left is the unmarked side and the caption in the tooltip is what says which side
        /// a bare mark is.
        /// <para>
        /// The assertion that carries the face is the <b>selected foreground</b>. This site is type
        /// rather than geometry precisely because <c>ToggleSegment</c>'s <c>.selected</c> face sets
        /// <c>Foreground</c>, which a <c>TextBlock</c> inherits and an <c>Icon</c> — painting from
        /// <c>Stroke</c> — cannot; a <c>muted</c> or a hand-set colour on the run would leave the
        /// mark grey on the accent fill with every other assertion here still passing.
        /// </para>
        /// <para>
        /// The strip is <b>measured</b>, never pinned to a number: a font-metric shift on another
        /// machine moves the figure, and what must hold is that the row does not wrap and nothing
        /// runs off a 300 px rail.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheTriggerStrip_DrawsThreeLeftHandMarks_AndFitsTheRail(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            Assert.True(panel.HasCoTriggers);

            // Switched on through the panel's own command, so the `.selected` class arrives the way
            // the app puts it there rather than being set on the button by the test.
            panel.ToggleCoTriggerCommand.Execute(panel.CoTriggers[1]);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var toggles = view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.DataContext is MacroCoTriggerViewModel)
                .ToArray();

            Assert.Equal(panel.CoTriggers.Count, toggles.Length);

            // The strip, read off the glass, in the order it is drawn in. THREE, and no `⌘`.
            Assert.Equal(
                ["⇧", "⌃", "⌥"],
                toggles.Select(toggle => string.Concat(VisibleRunsOf(toggle).Select(run => run.Text))));

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.TriggerSectionLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.TriggerJoin, texts);
            Assert.Contains(panel.TriggerTokenText, texts);
            Assert.Contains(panel.TriggerStatus, texts);

            // The `CO-TRIGGERS` block left the footer with them; nothing may draw a second copy.
            Assert.DoesNotContain("CO-TRIGGERS", texts);

            var strip = toggles[0].FindAncestorOfType<Grid>()!;

            var rows = toggles
                .Select(toggle => Math.Round(toggle.TranslatePoint(new Point(0, 0), strip)!.Value.Y, 2))
                .Distinct()
                .Count();

            var used = toggles.Max(toggle => toggle.TranslatePoint(new Point(toggle.Bounds.Width, 0), strip)!.Value.X);

            Assert.True(used <= strip.Bounds.Width, $"The latches need {used} px of a {strip.Bounds.Width} px strip.");
            Assert.Equal(1, rows);
            Assert.True(strip.Bounds.Width <= WideRailWidth, $"The strip is {strip.Bounds.Width} px in a {WideRailWidth} px rail.");

            var accentText = DesignTokens.ResolveBrushColor("AccentTextBrush", variant);
            var secondary = DesignTokens.ResolveBrushColor("TextSecondaryBrush", variant);
            var selectedRuns = 0;

            foreach (var toggle in toggles)
            {
                var model = Assert.IsType<MacroCoTriggerViewModel>(toggle.DataContext);
                var runs = VisibleRunsOf(toggle);

                // ONE run each: left spells no side. The two-line caption is the tooltip now, and
                // nothing draws it.
                Assert.Single(runs);
                Assert.Equal(model.Symbol, runs[0].Text);
                Assert.Equal(model.Caption, toggle.GetValue(ToolTip.TipProperty));
                Assert.Contains("keySymbol", runs[0].Classes);

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

            // The run of the one that is on, or the assertion above passed vacuously.
            Assert.Equal(1, selectedRuns);
        }

        /// <summary>
        /// The status readout is an <b>advisory</b>: amber, never the error ramp, and it blocks
        /// nothing. Read off the glass in both variants, because the two roles are bound
        /// exclusively — a stylesheet reordered so <c>muted</c> won would leave a collision drawn as
        /// ordinary prose with every view-model assertion still passing.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheTriggerStatus_GoesAmberOnACollision_AndNeverReachesTheErrorRamp(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            var muted = DesignTokens.ResolveBrushColor("TextMutedBrush", variant);
            var advisory = DesignTokens.ResolveBrushColor("StatusAdvisoryTextBrush", variant);
            var error = DesignTokens.ResolveBrushColor("StatusErrorTextBrush", variant);

            Assert.False(panel.IsTriggerAdvisory);
            Assert.Equal(muted, ForegroundOfStatus(view, panel));

            // A second macro on the same key with the same (empty) co-trigger set — 06 §5's own
            // duplicate rule, which `Validate()` reports as MacroTriggerCollision and never refuses.
            panel.SelectedSlot = panel.SlotOptions[1];
            panel.RecordCommand.Execute(null);
            panel.ReceiveKeystroke(new CapturedKeystroke
            {
                Key = KeyRegistry.FindByToken("b", TokenDialect.Gen1)!,
                PhysicalKey = PhysicalKeyCode.None
            });
            panel.Deactivate();

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(panel.IsTriggerAdvisory);
            Assert.Contains(MacroInspectorPanelViewModel.BuildCollisionStatus(1), panel.TriggerStatus, StringComparison.Ordinal);

            var colour = ForegroundOfStatus(view, panel);

            Assert.Equal(advisory, colour);
            Assert.NotEqual(error, colour);
        }

        /// <summary>
        /// The slot strip: one dot per <b>persisted</b> slot, filled for the occupied ones, and a
        /// dropdown naming the slot under edit. No <c>ACTIVE</c> badge and no <c>Make active</c> —
        /// those belonged to the Macros tab's cards, which issue #140 deleted, and this panel
        /// refuses both.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheSlotStrip_DrawsADotPerPersistedSlot_AndNoActiveBadge(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.True(panel.HasSlotSelector);

            var dots = view.GetVisualDescendants()
                .OfType<Ellipse>()
                .Where(dot => dot.Classes.Contains("macroSlotDot") && dot.IsEffectivelyVisible)
                .ToArray();

            // Five on the Freestyle Edge RGB, read off MacroCapability and never a literal here.
            Assert.Equal(panel.SlotOptions.Count, dots.Length);
            Assert.Equal(
                panel.SlotOptions.Select(option => option.IsOccupied),
                dots.Select(dot => dot.Classes.Contains("filled")));

            // Slot 1 carries the recorded macro, so exactly one dot is filled.
            Assert.Equal(1, dots.Count(dot => dot.Classes.Contains("filled")));

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.SlotSectionLabel, texts);
            Assert.Contains(panel.SelectedSlot!.Caption, texts);

            // The literals, not the constants: issue #140 deleted MacroSlotViewModel with the tab
            // that owned them, and the claim — this panel says neither of these things — outlives
            // its owner. Written out on purpose so the assertion cannot quietly disappear with a
            // type.
            Assert.DoesNotContain("ACTIVE", texts);
            Assert.DoesNotContain("Make active", texts);
        }

        /// <summary>The status line's own run, found by the text the view model put there.</summary>
        private static Color ForegroundOfStatus(MacroInspectorPanelView view, MacroInspectorPanelViewModel panel)
        {
            var run = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == panel.TriggerStatus);

            return Assert.IsAssignableFrom<ISolidColorBrush>(run.Foreground).Color;
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
        /// AC A1 at the glass (issue #139): the composer is <b>always drawn</b> and every control
        /// inside it is <b>dead until a step is selected</b> — the deliberate exception to the
        /// design's "absent features are not shown, not disabled", recorded in
        /// docs/app/design-system.md. A disclosure would have moved the whole rail under the
        /// pointer on every click on a row, and an absent block would have moved it twice.
        /// <para>
        /// It also pins the two things #139 <em>removed</em> from this block: the eight sided
        /// modifier toggles are four left-hand ones (AC A4), and the key search is gone — the key
        /// is set by <c>Record</c> and by nothing else (AC A3).
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheComposer_IsAlwaysDrawn_AndIsDeadUntilAStepIsSelected(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.ComposerLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.StepKeyLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.StepModifiersLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.StepDirectionLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.StepDelayLabel, texts);

            // The sentence that explains the dead state is on screen for exactly that state.
            Assert.False(panel.Steps.HasSelection);
            Assert.Contains(MacroInspectorPanelViewModel.ComposerHint, texts);

            // ...and with no key on the selected step there is no token to draw, so the chip is the
            // dash rather than a purple token that names nothing.
            Assert.False(panel.HasStepKey);
            Assert.Contains(MacroInspectorPanelViewModel.NoStepKeyText, texts);

            // THE KEY SEARCH IS GONE. #128 hosted the shared picker here — its fourth call site —
            // and #139 took it away with the append-a-chord flow it served.
            Assert.Empty(view.GetVisualDescendants().OfType<TokenPickerView>());

            var latches = LatchesOf(view);
            var directions = DirectionSegmentsOf(view);

            // `⇧ ⌃ ⌥ ⌘` — four, left-hand, and no `R` on any of them, because left is the unmarked
            // side and nothing right-hand is authored here any more.
            Assert.Equal(
                ["⇧", "⌃", "⌥", "⌘"],
                latches.Select(latch => string.Concat(VisibleRunsOf(latch).Select(run => run.Text))));
            Assert.Equal("Left Ctrl", latches[1].GetValue(ToolTip.TipProperty));

            // The mark is set in the THIRD family: no IBM Plex face carries U+21E7 or U+2318, so a
            // latch that lost the class would draw tofu on CI and something plausible on a Mac.
            Assert.All(latches, latch => Assert.Contains("keySymbol", VisibleRunsOf(latch)[^1].Classes));

            Assert.Equal(
                [MacroInspectorStepViewModel.TapAction,
                 MacroInspectorStepViewModel.PressAction,
                 MacroInspectorStepViewModel.ReleaseAction],
                directions.Select(segment => segment.Content as string));

            // Dead, all of it — and the segments are `toggleSegment`, not the lighting tab's
            // icon-content `directionSegment`.
            Assert.All(latches, latch => Assert.False(latch.IsEffectivelyEnabled));
            Assert.All(directions, segment => Assert.False(segment.IsEffectivelyEnabled));
            Assert.All(directions, segment => Assert.Contains("toggleSegment", segment.Classes));
            Assert.False(RecordStepKeyButtonOf(view).IsEffectivelyEnabled);

            // ...except the two affordances a selection comes to exist through, which is the whole
            // exception in AC A1.
            Assert.True(
                view.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => ReferenceEquals(button.Command, panel.RecordCommand))
                    .IsEffectivelyEnabled);
            Assert.True(
                view.GetVisualDescendants()
                    .OfType<Button>()
                    .Single(button => ReferenceEquals(button.Command, panel.InsertStepCommand))
                    .IsEffectivelyEnabled);

            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // Alive, and the hint has said what it had to say.
            Assert.DoesNotContain(MacroInspectorPanelViewModel.ComposerHint, VisibleTextsOf(view));
            Assert.All(LatchesOf(view), latch => Assert.True(latch.IsEffectivelyEnabled));
            Assert.All(DirectionSegmentsOf(view), segment => Assert.True(segment.IsEffectivelyEnabled));
            Assert.True(RecordStepKeyButtonOf(view).IsEffectivelyEnabled);

            // The chip is the selected step's own token, tinted like the row it edits. Scoped to
            // the composer's KEY row — the step list draws the very same token in the very same
            // face, which is the point of the tint and would make an unscoped sweep find two.
            var keyRow = Assert.IsAssignableFrom<Panel>(RecordStepKeyButtonOf(view).GetVisualParent());
            var chip = Assert.Single(
                keyRow.GetVisualChildren().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && !block.Classes.Contains("sectionLabel"));

            Assert.Equal(panel.StepTokenText, chip.Text);
            Assert.Contains("monoValue", chip.Classes);
            Assert.Equal(
                DesignTokens.ResolveBrushColor("MacroStepKeyBrush", ToVariant(variantName)),
                ((ISolidColorBrush)chip.Foreground!).Color);

            // MEASURED, NEVER PINNED TO A NUMBER — the Trigger strip's rule, for the same reason:
            // eleven controls in four labelled rows is exactly the shape a font-metric shift on
            // another machine pushes off a 300 px rail, and nothing else here would notice.
            Assert.All(
                LatchesOf(view)
                    .Concat(DirectionSegmentsOf(view))
                    .Concat(DelaySegmentsOf(view))
                    .Cast<Control>()
                    .Concat([chip]),
                control => Assert.True(
                    RightEdgeOf(control, view) <= WideRailWidth,
                    $"'{control}' runs {RightEdgeOf(control, view) - WideRailWidth:0.#} px off the rail."));
        }

        /// <summary>
        /// The whole point issue #128's composer existed for, carried through its rewrite:
        /// <c>Ctrl+1</c> — which macOS keeps for itself, so no capture can ever hear it — authored
        /// without ever pressing it. The route changed and the answer did not: record the bare
        /// <c>1</c>, which the window server <em>does</em> deliver, then tick <c>⌃</c> on the step.
        /// </summary>
        [AvaloniaFact]
        public async Task TheComposer_StillAuthorsCtrl1_WithoutEverPressingIt()
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, "Dark");

            host.Capture();

            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[0]);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // The composer's own Record — a SINGLE shot, armed off the panel's one sink.
            var record = RecordStepKeyButtonOf(view);

            record.Command!.Execute(record.CommandParameter);

            Assert.Equal(MacroCaptureMode.SingleStep, panel.CaptureMode);

            panel.ReceiveKeystroke(new CapturedKeystroke
            {
                Key = KeyRegistry.FindByToken("1", TokenDialect.Gen1)!,
                PhysicalKey = PhysicalKeyCode.None
            });

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            // One keystroke and the arm is gone: it is not a take, it is a single value.
            Assert.Equal(MacroCaptureMode.None, panel.CaptureMode);
            Assert.Equal("[1]", panel.Steps.Items[0].TokenText);

            var control = LatchesOf(view)
                .Single(latch => latch.DataContext is MacroChordModifier { Modifier: MacroModifiers.LeftControl });

            control.Command!.Execute(control.CommandParameter);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var step = panel.Steps.Items[0];

            Assert.Equal("[1]", step.TokenText);
            Assert.Equal(MacroModifierMarks.ControlMark, Assert.Single(step.Modifiers).Symbol);
            Assert.Contains("[1]", VisibleTextsOf(view));
        }

        /// <summary>
        /// The composer edits the selected step, so moving the rail to another key has to take the
        /// selection — and any <c>＋</c> placeholder standing on it — with it. A placeholder that
        /// survived would point at an index in a macro that is no longer the one under edit, and
        /// the next captured key would land in the wrong step of the wrong macro.
        /// </summary>
        [AvaloniaFact]
        public async Task MovingToAnotherKey_DropsTheSelectionAndAnyPlaceholderWithIt()
        {
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorWithInspectorAsync();
            var layer = Assert.IsType<KeyboardLayerViewModel>(editor.SelectedLayer);

            editor.SelectKeyCommand.Execute(layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex));

            SelectMacroMode(editor);

            var panel = Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);

            panel.InsertStepCommand.Execute(null);

            Assert.True(panel.Steps.HasPlaceholder);
            Assert.True(panel.Steps.HasSelection);
            Assert.True(panel.IsComposerEnabled);

            editor.SelectKeyCommand.Execute(layer.FindByIndex(TestLayouts.RgbDigitTwoKeyIndex));

            Assert.False(panel.Steps.HasPlaceholder);
            Assert.False(panel.Steps.HasSelection);
            Assert.False(panel.IsComposerEnabled);
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

        /// <summary>How far <paramref name="control"/>'s right edge reaches in the panel's own box.</summary>
        private static double RightEdgeOf(Control control, Control view)
        {
            return control.TranslatePoint(new Point(control.Bounds.Width, 0), view)?.X
                   ?? throw new InvalidOperationException("The control is not in the panel's tree.");
        }

        /// <summary><paramref name="control"/>'s left edge in <paramref name="view"/>'s coordinates.</summary>
        private static double LeftEdgeOf(Control control, Control view)
        {
            return control.TranslatePoint(default, view)?.X
                   ?? throw new InvalidOperationException("The control is not in the panel's tree.");
        }

        /// <summary>The composer's four modifier latches, in 05 §5.1's table order.</summary>
        private static Button[] LatchesOf(Control view)
        {
            return ComposerControls<MacroChordModifier>(view);
        }

        /// <summary>The composer's <c>tap</c> / <c>press</c> / <c>release</c> segments.</summary>
        private static Button[] DirectionSegmentsOf(Control view)
        {
            return ComposerControls<MacroStepDirection>(view);
        }

        /// <summary>The composer's <c>none</c> / <c>fixed</c> / <c>random</c> segments.</summary>
        private static Button[] DelaySegmentsOf(Control view)
        {
            return ComposerControls<MacroStepDelayOption>(view);
        }

        /// <summary>
        /// The composer's own single-shot <c>Record</c> — told from the Sequence header's take by
        /// the command it runs, which is the only thing that distinguishes two red buttons whose
        /// captions agree at rest.
        /// </summary>
        private static Button RecordStepKeyButtonOf(Control view)
        {
            var panel = (MacroInspectorPanelViewModel)view.DataContext!;

            return Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => ReferenceEquals(button.Command, panel.RecordStepKeyCommand));
        }

        /// <summary>
        /// The buttons the composer generated for one of its immutable option types. Found by
        /// <c>DataContext</c> rather than by class: all three wear <c>toggleSegment</c>, so a class
        /// sweep would return the whole composer plus the Trigger strip's latches.
        /// </summary>
        private static Button[] ComposerControls<TOption>(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.DataContext is TOption)
                .ToArray();
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
