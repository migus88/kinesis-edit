using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
    /// The key inspector's Macro panel at the glass, in both theme variants — the designer's
    /// <i>"Standing compose bar"</i> mock (issue #146): the slot and trigger chips on one row, the
    /// numberless step rows with their key chip and delay pill, the fixed-height list, the compose
    /// bar pinned under it, the Speed/Repeat block and the red <c>Delete</c> — plus the one
    /// measurement the whole panel hangs off, the rail widening from 268 px to <b>440</b>.
    /// <para>
    /// The panel is hosted at the rail's real <b>440 px</b>, not at a comfortable test width: a row
    /// that reads fine at 600 px runs off the rail at 440, and no view-model test can see it.
    /// </para>
    /// </summary>
    public class MacroInspectorPanelTests
    {
        /// <summary>The rail's macro-editing width (<c>WidthInspectorRailWide</c>).</summary>
        private const double WideRailWidth = 440;

        /// <summary>Its ordinary width (<c>WidthInspectorRail</c>), for the comparison below.</summary>
        private const double RailWidth = 268;

        /// <summary>The step list's fixed box (<c>ScrollViewer.macroStepList</c>).</summary>
        private const double StepListHeight = 280;

        /// <summary>
        /// How far a probed pixel may sit from the token it is meant to be painted in. Wider than a
        /// filled face's tolerance on purpose: a glyph at 11 px is mostly its own anti-aliased edge,
        /// so the assertion that carries the weight is the <em>relative</em> one — closer to the
        /// tint than to the ramp the row would use without it.
        /// </summary>
        private const double GlyphAntiAliasTolerance = 60;

        /// <summary>
        /// The handoff states 300 px for "the macro-editing variant"; issue #146 widened it to 440,
        /// because the compose bar's two rows of latches, a key field and a Record cannot be
        /// authored inside 300. Measured on the <b>laid-out</b> rail, because a bridge that stopped
        /// matching leaves the property unset and the frame looks plausible.
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

        /// <summary>
        /// A row as the mock draws it: a bordered <b>key chip</b>, the action word, and the delay at
        /// the right — an em dash where there is none and an accent pill where there is one. And
        /// <b>no number anywhere</b>: not on a row, not on the <c>＋</c> row, not in a banner. A
        /// step is identified by the key it strikes.
        /// </summary>
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
            Assert.Contains(MacroInspectorStepsViewModel.InsertStepCaption, texts);

            // The count took the `drag ⠿ · ⌥↑↓` hint's place in the header. The shortcut itself is
            // still written down — in the grip's tooltip, which is now its only home.
            Assert.Equal(3, panel.Steps.Count);
            Assert.Contains(panel.StepCountText, texts);
            Assert.Equal("3 steps", panel.StepCountText);
            Assert.Equal(
                MacroInspectorStepsViewModel.ReorderHandleHint,
                GripOf(view, 1).GetValue(ToolTip.TipProperty));

            // Three recorded steps: `[e] tap`, `[s] tap`, `[t] tap` — each token in a chip of its
            // own, so a chord would read as one keystroke rather than as three words in a row.
            foreach (var step in panel.Steps.Items)
            {
                var chip = Assert.Single(
                    RowOf(view, step.Position).GetVisualDescendants().OfType<Border>(),
                    border => border.Classes.Contains("macroStepToken"));

                Assert.True(chip.IsEffectivelyVisible);
                Assert.Contains(step.TokenText, VisibleRunsOf(chip).Select(run => run.Text));
            }

            Assert.Contains("[e]", texts);
            Assert.Contains(MacroInspectorStepViewModel.TapAction, texts);

            // NO NUMBERS. Written out rather than taken off a constant, because the constants that
            // formatted them were deleted and the claim — this panel counts nothing — outlives
            // them.
            foreach (var number in new[] { "01", "02", "03", "04" })
            {
                Assert.DoesNotContain(number, texts);
            }

            // No delay on any of the three, so every row ends in the em dash rather than a pill.
            // Counted per ROW: the composer's key readout is a dash of its own with nothing
            // selected, and an unscoped sweep would find four.
            Assert.All(panel.Steps.Items, step => Assert.False(step.HasDelay));
            Assert.Empty(VisiblePillsOf(view));
            Assert.All(
                panel.Steps.Items,
                step => Assert.Single(
                    VisibleRunsOf(RowOf(view, step.Position)),
                    run => run.Text == MacroInspectorPanelViewModel.NoStepKeyText));

            // ...and the pill arrives with the delay, drawn in the row it belongs to.
            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[1]);
            panel.StepDelayMilliseconds = 80;

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var delayed = panel.Steps.Items[1];

            Assert.True(delayed.HasDelay);

            var pill = Assert.Single(VisiblePillsOf(view));

            Assert.Contains(delayed.DelayText, VisibleRunsOf(pill).Select(run => run.Text));
            Assert.Contains(pill, RowOf(view, delayed.Position).GetVisualDescendants());
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
        /// The panel's two Record buttons, and the fact that they are no longer the same face
        /// (issue #146). The Sequence header's <c>● Record sequence</c> keeps the app's red
        /// <c>recordAction</c>; the compose bar's <c>● Record key</c> is a neutral
        /// <c>secondary</c> whose dot is <b>muted while its arm is idle</b> and goes red — by
        /// dropping <c>.standby</c> — the moment the single-shot arm goes live. So red still means
        /// "this is recording now" everywhere in the app, rather than "recording starts here".
        /// <para>
        /// The pair being <em>distinct commands</em> is part of the claim: a second button wired to
        /// the first's command would render identically and quietly turn the single shot into a
        /// take.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheTwoRecordButtons_CarryTheirOwnFaceAndTheirOwnDot(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            var take = Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => button.Classes.Contains("recordAction"));

            Assert.Same(panel.RecordCommand, take.Command);
            Assert.NotNull(take.Theme);
            Assert.Equal(MacroInspectorPanelViewModel.RecordSequenceCaption, panel.RecordCommandCaption);

            var takeDot = DotOf(take);

            Assert.DoesNotContain("standby", takeDot.Classes);
            Assert.Equal(DesignTokens.Resolve("StatusErrorBrush", variant), takeDot.Fill);

            var single = RecordStepKeyButtonOf(view);

            Assert.Contains("secondary", single.Classes);
            Assert.DoesNotContain("recordAction", single.Classes);
            Assert.NotNull(single.Theme);
            Assert.NotSame(take.Command, single.Command);

            var singleDot = DotOf(single);

            Assert.Contains("standby", singleDot.Classes);
            Assert.Equal(DesignTokens.Resolve("TextBodyMutedBrush", variant), singleDot.Fill);

            // Armed, the class goes and the dot is the app's red again.
            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[0]);
            panel.RecordStepKeyCommand.Execute(null);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(MacroCaptureMode.SingleStep, panel.CaptureMode);
            Assert.DoesNotContain("standby", DotOf(RecordStepKeyButtonOf(view)).Classes);
            Assert.Equal(DesignTokens.Resolve("StatusErrorBrush", variant), DotOf(RecordStepKeyButtonOf(view)).Fill);

            panel.Deactivate();
        }

        /// <summary>
        /// The banner appears only while an arm is live, and since issue #146 it carries the
        /// OS-reserved note as its <b>second line</b>. That sentence used to stand under the capture
        /// rule at all times, spending two lines of the rail restating a limitation nobody had met
        /// yet; it belongs at the moment the take is running and the chord fails to arrive. The
        /// capture rule itself is gone — the banner says what it said.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRecordingBanner_CarriesTheOsReservedNote_AndOnlyShowsWhileArmed(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var atRest = VisibleTextsOf(view);

            Assert.DoesNotContain(panel.RecordingBanner, atRest);
            Assert.DoesNotContain(MacroInspectorPanelViewModel.OsReservedNote, atRest);

            panel.RecordCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var armed = VisibleTextsOf(view);

            Assert.Equal(MacroInspectorPanelViewModel.RecordingBannerText, panel.RecordingBanner);
            Assert.Contains(panel.RecordingBanner, armed);
            Assert.Contains(MacroInspectorPanelViewModel.OsReservedNote, armed);

            // NO STEP NUMBER, in either banner. The rows carry none, so a banner naming one would
            // be pointing at something the user cannot see.
            foreach (var number in new[] { "01", "02", "03", "04" })
            {
                Assert.DoesNotContain(number, panel.RecordingBanner, StringComparison.Ordinal);
            }

            // Both lines in one amber block, so the note reads as part of what the banner is
            // saying rather than as a paragraph that happens to be beneath it.
            var block = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("pickerAdvisory") && border.IsEffectivelyVisible);

            Assert.Equal(
                [panel.RecordingBanner, MacroInspectorPanelViewModel.OsReservedNote],
                VisibleRunsOf(block).Select(run => run.Text));

            panel.Deactivate();
        }

        /// <summary>
        /// The four budgets, in the mock's own shape: <c>Speed</c> reads <c>5 of 9</c> beside its
        /// slider, the layout keystroke budget reads <c>1 014 / 7 200</c> with <c>chars</c> after
        /// it, and <c>this macro</c> / <c>macros</c> survive as one muted line under them.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheMeters_ReadOutTheFourBudgetsTheMockNames(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.SpeedMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.RepeatLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.MacroLengthMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.MacroCountMeterLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.MeterJoin, texts);
            Assert.Contains(MacroInspectorPanelViewModel.LayoutKeystrokeUnit, texts);

            Assert.Contains(panel.SpeedMeter.Caption, texts);
            Assert.Contains(panel.MacroLengthMeter.Caption, texts);
            Assert.Contains(panel.LayoutKeystrokeMeter.Caption, texts);
            Assert.Contains(panel.MacroCountMeter.Caption, texts);

            // `N of M`, not `N / M`: a playback speed is a step out of a scale rather than a
            // consumption against a budget.
            Assert.Contains(MacroMeterViewModel.OfSeparator, panel.SpeedMeter.Caption, StringComparison.Ordinal);
            Assert.DoesNotContain(MacroMeterViewModel.CaptionSeparator, panel.SpeedMeter.Caption, StringComparison.Ordinal);

            // The keystroke budget's own label is NOT drawn — `chars` says what it counts, which is
            // what the mock draws and what fits beside the stepper.
            Assert.DoesNotContain(MacroInspectorPanelViewModel.LayoutKeystrokeMeterLabel, texts);

            // MEASURED, never pinned to a number — the rail's rule.
            var readouts = new[]
            {
                panel.SpeedMeter.Caption,
                panel.MacroLengthMeter.Caption,
                panel.LayoutKeystrokeMeter.Caption,
                panel.MacroCountMeter.Caption
            };

            foreach (var readout in readouts)
            {
                var block = view.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .First(candidate => candidate.IsEffectivelyVisible && candidate.Text == readout);

                Assert.True(
                    RightEdgeOf(block, view) <= WideRailWidth,
                    $"'{readout}' runs {RightEdgeOf(block, view) - WideRailWidth:0.#} px off the rail.");
            }
        }

        /// <summary>
        /// <c>Repeat</c> is a <c>−</c> / value / <c>+</c> stepper since issue #146, not a slider: it
        /// is a small integer range (06 §6) that a slider could neither be read exactly on nor
        /// stepped by one. Both buttons run the panel's own commands and both are clamped by the
        /// device's range, which is what takes the stepper dead at a bound.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRepeatRow_IsAStepperClampedToTheDeviceRange(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            Assert.True(panel.HasRepeat);

            var stepper = Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("numericStepper") && border.IsEffectivelyVisible);

            var buttons = stepper.GetVisualDescendants().OfType<Button>().ToArray();

            Assert.Equal(2, buttons.Length);
            Assert.All(buttons, button => Assert.Contains("ghost", button.Classes));
            Assert.Same(panel.DecreaseRepeatCommand, buttons[0].Command);
            Assert.Same(panel.IncreaseRepeatCommand, buttons[1].Command);

            // The slider it replaced. The panel keeps exactly one — Speed's.
            var sliders = view.GetVisualDescendants().OfType<Slider>().Where(slider => slider.IsEffectivelyVisible);

            Assert.Single(sliders);

            Assert.Equal(panel.RepeatMinimum, panel.Repeat);
            Assert.False(buttons[0].IsEffectivelyEnabled, "Repeat can be stepped below the device's minimum.");

            buttons[1].Command!.Execute(buttons[1].CommandParameter);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(panel.RepeatMinimum + 1, panel.Repeat);
            Assert.Contains(
                panel.Repeat.ToString(System.Globalization.CultureInfo.InvariantCulture),
                VisibleTextsOf(stepper));
            Assert.True(buttons[0].IsEffectivelyEnabled);
        }

        /// <summary>
        /// The footer's two macro actions, and the swap that keeps the rail from growing a control
        /// it only sometimes needs: <c>Copy macro to…</c> is replaced by <c>Cancel copy</c> for as
        /// long as the pick is armed. <c>Delete</c> wears the <b>red</b> <c>discard</c> face since
        /// issue #146 — a deliberate widening of a face this app had reserved for a message box's
        /// destructive answer, because the designer drew it that way and emptying a slot really is
        /// the one action on this rail that destroys what the file holds.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheFooter_SwapsTheCopyWhileItIsArmed_AndDrawsDeleteInTheRedFace(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            var captions = VisibleButtonCaptionsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.CopyMacroCaption, captions);
            Assert.Contains(MacroInspectorPanelViewModel.DeleteMacroCaption, captions);
            Assert.DoesNotContain(MacroInspectorPanelViewModel.CancelCopyCaption, captions);

            // The noun is the whole point: the rail's own footer carries a `Copy to…` that copies
            // the WHOLE position, and the two must not read alike — which is why this one keeps its
            // noun even though the mock shortens it.
            Assert.DoesNotContain(KeyInspectorViewModel.CopyToCaption, captions);

            var delete = DeleteButtonOf(view);

            Assert.Contains("discard", delete.Classes);
            Assert.DoesNotContain("secondary", delete.Classes);
            Assert.Same(DesignTokens.Resolve("DiscardButton", variant), delete.Theme);
            Assert.Equal(DesignTokens.Resolve("StatusErrorTintBrush", variant), delete.Background);

            panel.CopyMacroCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(panel.IsCopyArmed, "Running the editor's own copy command did not arm the pick.");

            captions = VisibleButtonCaptionsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.CancelCopyCaption, captions);
            Assert.DoesNotContain(MacroInspectorPanelViewModel.CopyMacroCaption, captions);

            panel.CancelCopyCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            captions = VisibleButtonCaptionsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.CopyMacroCaption, captions);
            Assert.DoesNotContain(MacroInspectorPanelViewModel.CancelCopyCaption, captions);
        }

        /// <summary>
        /// A slot with no macro has nothing to copy or delete, and says so by drawing the two dead
        /// rather than by dropping them — the composer's own rule, for the composer's own reason:
        /// the feature is not missing from the device, the user has simply not put a macro there
        /// yet, and a block that came and went as slots were picked would move everything under it
        /// on every click.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task OnAnEmptySlot_TheDeleteIsDrawnDead(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            // The last persisted slot of the same key: empty, and reachable by clicking its chip.
            ClickSlotChip(view, panel.SlotOptions.Count);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(panel.HasMacro);
            Assert.Equal("no steps", panel.StepCountText);

            var delete = DeleteButtonOf(view);

            Assert.False(delete.IsEffectivelyEnabled, "Delete is live with nothing to delete.");

            // The chip is still drawn and still says the slot is empty, so the panel is visibly
            // pointing somewhere rather than merely blank.
            var chips = SlotChipsOf(view);

            Assert.Contains("selected", chips[^1].Classes);
            Assert.Equal(MacroSlotOption.EmptyChipText, chips[^1].Content);
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

            var readout = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .First(block => block.IsEffectivelyVisible && block.Text == panel.MacroLengthMeter.Caption);

            Assert.Contains("statusWarning", readout.Classes);
            Assert.DoesNotContain("statusError", readout.Classes);

            // The two roles are bound EXCLUSIVELY rather than layered, so which one wins is a fact
            // about the selector and never about the order the stylesheet declares them in.
            Assert.DoesNotContain("muted", readout.Classes);

            Assert.Equal(
                DesignTokens.ResolveBrushColor("StatusAdvisoryTextBrush", ToVariant(variantName)),
                ((ISolidColorBrush)readout.Foreground!).Color);
        }

        /// <summary>
        /// The delay field <b>is</b> a real <c>TextBox</c>, and that is the opposite of the action
        /// fields' rule: focus inside one suspends the capture service, which is exactly right for a
        /// value that is typed rather than pressed (§11.3's millisecond count). It is the panel's
        /// <b>only</b> text field since issue #146 took the macro name away.
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

            // `then wait` is an inline label now, not a `THEN WAIT` section caption.
            Assert.Contains(MacroInspectorPanelViewModel.StepDelayLabel, VisibleTextsOf(view));
            Assert.Equal("then wait", MacroInspectorPanelViewModel.StepDelayLabel);

            var field = Assert.Single(
                view.GetVisualDescendants().OfType<TextBox>(),
                box => box.IsEffectivelyVisible);

            Assert.Contains("monoValue", field.Classes);
            Assert.NotNull(field.Theme);
            Assert.False(field.IsEffectivelyEnabled, "The delay field is live with no step selected.");

            // The three states of a step's trailing delay, and every one of them dead until the
            // composer is pointed at a row. `none` is kept although the mock draws only the other
            // two: without it "no delay" is unauthorable.
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

            // ...and it writes as it is touched, with no `Set delay` to press: a usable number in
            // the field is the write, which since issue #146 removed the `+`/`-` arrows is the whole
            // route from "no delay" to a fixed one.
            panel.StepDelayMilliseconds = 80;
            Dispatcher.UIThread.RunJobs();

            Assert.True(panel.Steps.Items[0].HasDelay);
            Assert.Contains(panel.Steps.Items[0].DelayText, VisibleTextsOf(view));
        }

        /// <summary>
        /// The firmware gate is still answered <b>in place</b> and still has its
        /// <c>Update Firmware</c> button. The scene's board clears the gate, so what is asserted
        /// here is that the refusal branch is <em>rendered and hidden</em> rather than missing.
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
            // error one — an old firmware is a fact about the device, not a failure.
            var block = Assert.IsAssignableFrom<Panel>(update.GetVisualParent());
            var refusal = Assert.Single(block.GetVisualChildren().OfType<TextBlock>());

            Assert.Contains("statusWarning", refusal.Classes);
            Assert.DoesNotContain("statusError", refusal.Classes);
        }

        /// <summary>
        /// The held modifiers are the mock's marks — the mark itself set in <c>.keySymbol</c>, and
        /// the file's two-character codes nowhere in the list — and since issue #146 they live
        /// <b>inside the key chip</b>, so <c>⌃2</c> reads as one keystroke. That is what took the
        /// hard-coded <c>muted</c> off the mark: the chip sets <c>TextElement.Foreground</c> and
        /// both runs inherit it, so a chord is one colour rather than a grey mark beside a purple
        /// token.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task AModifiedStep_DrawsItsMarksInsideTheKeyChip(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();

            RecordStepWith(panel, "b", "lshft");

            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            // Scoped to the STEP ROWS deliberately: the trigger latches and the composer's own draw
            // marks too, so an unscoped sweep of the panel finds seven and this assertion is about
            // the one in the list.
            var step = panel.Steps.Items[^1];
            var chip = Assert.Single(
                RowOf(view, step.Position).GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("macroStepToken"));

            var runs = VisibleRunsOf(chip);
            var mark = Assert.Single(runs, run => run.Classes.Contains("keySymbol"));

            Assert.Equal(MacroModifierMarks.ShiftMark, mark.Text);

            // Set in the THIRD family, not in Plex: no IBM Plex face carries U+21E7, so a mark that
            // inherited the row's mono family would draw tofu.
            Assert.Equal((FontFamily)DesignTokens.Resolve("FontKeySymbols", variant), mark.FontFamily);

            // THE MARK AND THE TOKEN ARE ONE HUE, inherited from the chip. A `muted` run here — the
            // shape this replaced — would split the chord in half and no other assertion would see
            // it.
            var tint = DesignTokens.ResolveBrushColor("MacroStepKeyBrush", variant);

            Assert.All(
                runs,
                run => Assert.Equal(tint, Assert.IsAssignableFrom<ISolidColorBrush>(run.Foreground).Color));
            Assert.Contains(runs, run => run.Text == step.TokenText);

            // LEFT DRAWS NO SIDE RUN AT ALL — it is the unmarked side. Asserted against the letters
            // themselves rather than against `MacroModifierMarks.LeftSide`, which is the empty
            // string and would make this vacuous.
            Assert.DoesNotContain(runs, run => run.Text is "L" or "R");

            // The mark still carries the words that say WHICH shift it was, because the glyph no
            // longer can: `⇧` is worn by Left Shift and by a generic Shift alike.
            Assert.Equal("Left Shift", MarkTipOf(view, step.Position));

            // ...and nothing in the row still reads as the file's own spelling.
            foreach (var run in runs)
            {
                Assert.NotEqual("LS", run.Text);
                Assert.NotEqual("S ", run.Text);
            }
        }

        /// <summary>
        /// The spelling rule at the glass (issue #122): <b>only a right-hand modifier draws a side
        /// run</b>. A left one is the bare mark and nothing beside it, which is exactly what makes
        /// it indistinguishable from a generic modifier on the row — so the tooltip is asserted
        /// here too, because it is the only thing left that separates them.
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
        /// The struck key really is painted <c>MacroStepKeyBrush</c> — read off the frame, not off
        /// the binding, because a token that resolves is not the same question as a token that
        /// reaches the glass. It is inherited from the chip now rather than set on the run, which is
        /// exactly the kind of move that resolves and does not arrive.
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

            var token = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .First(block => block.IsEffectivelyVisible && block.Text == panel.Steps.Items[0].TokenText);

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
        /// The list is a <b>fixed box</b> since issue #146, not a list as tall as its contents, and
        /// that is the whole point of a standing compose bar: with a <c>MaxHeight</c>, recording a
        /// step, deleting one or opening a placeholder slid the composer — the modifier latches, the
        /// key field and <c>Record key</c> — up or down the rail <em>under the pointer</em> between
        /// one click and the next.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheStepList_IsAFixedBox_SoTheComposerNeverMoves(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            var list = Assert.Single(
                view.GetVisualDescendants().OfType<ScrollViewer>(),
                scroller => scroller.Classes.Contains("macroStepList"));

            // Bounded, and NOT a SelectingItemsControl — the editor's grammar leaves ⌥↑↓ to any
            // such control in the focused ancestry, so a ListBox here would swallow the very
            // shortcut the grip's tooltip advertises.
            Assert.Equal(StepListHeight, list.Bounds.Height);
            Assert.Empty(list.GetVisualDescendants().OfType<SelectingItemsControl>());

            var composer = ComposerBoxOf(view);
            var origin = composer.TranslatePoint(default, view)!.Value;

            // Three rows short of full, then two, then a placeholder: the box does not breathe.
            panel.Steps.RemoveStepCommand.Execute(panel.Steps.Items[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(2, panel.Steps.Count);
            Assert.Equal(StepListHeight, list.Bounds.Height);
            Assert.Equal(origin, ComposerBoxOf(view).TranslatePoint(default, view)!.Value);

            panel.InsertStepCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(panel.Steps.HasPlaceholder);
            Assert.Equal(StepListHeight, list.Bounds.Height);
            Assert.Equal(origin, ComposerBoxOf(view).TranslatePoint(default, view)!.Value);
        }

        /// <summary>
        /// The selection ring spans the <b>whole</b> row — grip, body and <c>×</c> — which is why it
        /// moved off the row button and onto a frame around all three (a button may not nest inside
        /// another button). The row button keeps the hit target and its hover and carries no
        /// selected face of its own any more.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheSelectedRow_IsRingedFromTheGripToTheDeleteMark(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            var frame = RowOf(view, 2);

            Assert.DoesNotContain("selected", frame.Classes);

            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[1]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Contains("selected", frame.Classes);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), frame.BorderBrush);
            Assert.Equal(DesignTokens.Resolve("AccentSelectionFillBrush", variant), frame.Background);

            // The ring really is around all three: the grip and the delete mark are inside the
            // frame's own box, not beside it.
            var grip = GripOf(view, 2);
            var remove = frame.GetVisualDescendants().OfType<Button>().First(button => (button.Content as string) == "×");

            Assert.True(LeftEdgeOf(grip, frame) >= 0);
            Assert.True(RightEdgeOf(remove, frame) <= frame.Bounds.Width);

            // ...and the row button no longer carries one of its own, or the two would stack.
            var body = frame.GetVisualDescendants().OfType<Button>().First(button => button.Classes.Contains("macroStepRow"));

            Assert.DoesNotContain("selected", body.Classes);
        }

        /// <summary>
        /// AC 7, and the defect that made the whole gesture dead: column 1 of a row is a
        /// <c>Button</c>, which handles the left press, so a handler attached from the markup — with
        /// <c>handledEventsToo: false</c> — never saw a press on the row <b>body</b>. Only the 12 px
        /// grip armed anything. This is the drag a user actually makes.
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
        /// A reorder with no feedback reads as broken even once it works. The row the drop would
        /// land on wears a ring while the step is carried, and nothing wears it once the pointer is
        /// up. The ring is told from the selected frame's and the delay pill's — which are both the
        /// accent too — by being the one border that cannot be hit: that is not a nicety, it is what
        /// keeps the release's hit test landing on the row.
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
        /// Issue #128, the reported defect: <i>"the view is only updated after the movement. I want
        /// to see a draggable element while I drag."</i> From the 4 px threshold the source row dims
        /// in place and a copy of it rides the pointer — carrying the row's <b>whole</b> face,
        /// delay included, because a step moves with its delay and a ghost that dropped it would be
        /// showing something other than what the release is about to move.
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

            // A delay on the row that will be carried, so the ghost has one to keep.
            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[0]);
            panel.StepDelayMilliseconds = 80;

            Dispatcher.UIThread.RunJobs();
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

            // It carries the row's own face — the token AND the delay folded behind it — rather
            // than a second, drifting copy of it.
            var carried = VisibleRunsOf(ghost).Select(run => run.Text).ToArray();

            Assert.Contains(panel.Steps.Items[0].TokenText, carried);
            Assert.Contains(panel.Steps.Items[0].DelayText, carried);

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
        /// <b>The slot strip is a row of chips</b> (issue #146): the slot's number when it holds a
        /// macro, a <c>+</c> when it does not, and the accent ring on the one under edit. No names,
        /// no previews, no dots and no drop-down — and still no <c>ACTIVE</c> badge or
        /// <c>Make active</c>, which belonged to the Macros tab's cards and which this panel refuses.
        /// The tooltip survives the dropdown, because a bare numeral is not accessible text.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheSlotStrip_DrawsAChipPerPersistedSlot_AndNoActiveBadge(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            Assert.True(panel.HasSlotSelector);

            var chips = SlotChipsOf(view);

            // Five on the Freestyle Edge RGB, read off MacroCapability and never a literal here.
            Assert.Equal(panel.SlotOptions.Count, chips.Length);
            Assert.Equal(
                panel.SlotOptions.Select(option => option.ChipText),
                chips.Select(chip => chip.Content as string));
            Assert.Equal(
                panel.SlotOptions.Select(option => option.Caption),
                chips.Select(chip => chip.GetValue(ToolTip.TipProperty) as string));

            // Slot 1 carries the recorded macro, so exactly one chip is a number and the rest are
            // the empty `+`.
            Assert.Equal("1", chips[0].Content);
            Assert.All(chips.Skip(1), chip => Assert.Equal(MacroSlotOption.EmptyChipText, chip.Content));

            // ...and exactly one is ringed as the one under edit.
            var selected = Assert.Single(chips, chip => chip.Classes.Contains("selected"));

            Assert.Same(chips[0], selected);
            Assert.Equal(DesignTokens.Resolve("AccentBrush", variant), selected.BorderBrush);
            Assert.All(chips, chip => Assert.DoesNotContain("colliding", chip.Classes));

            // Sans, not mono: a slot ordinal is this app counting, not a value out of a config file.
            var numeral = Assert.Single(VisibleRunsOf(selected));

            Assert.Equal((FontFamily)DesignTokens.Resolve("FontSans", variant), numeral.FontFamily);

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.SlotSectionLabel, texts);
            Assert.Equal("SLOTS", MacroInspectorPanelViewModel.SlotSectionLabel);

            // The dropdown and the dot strip it replaced, gone: nothing on this panel is either.
            Assert.Empty(view.GetVisualDescendants().OfType<ComboBox>());
            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<Ellipse>(),
                dot => dot.Classes.Contains("macroSlotDot"));

            // The literals, not the constants: issue #140 deleted the type that owned them, and the
            // claim — this panel says neither of these things — outlives its owner.
            Assert.DoesNotContain("ACTIVE", texts);
            Assert.DoesNotContain("Make active", texts);
        }

        /// <summary>
        /// <b>Clicking a chip moves the panel, and the trigger latches follow it.</b> That coupling
        /// (<c>SelectSlot → ReadFromModel → RefreshTrigger</c>) has always existed and became
        /// load-bearing with issue #146, which put the latches on the same row as the chips: two
        /// slots of one key are told apart by <em>nothing else</em>, so latches that went on showing
        /// the previous slot's co-triggers would be the panel describing the wrong macro.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task PickingASlot_MovesThePanel_AndTheTriggerLatchesFollowIt(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };

            using var host = Show(view, variantName);

            host.Capture();

            // A second macro on slot 2, carrying `⌃` where slot 1's carries nothing.
            ClickSlotChip(view, 2);

            Dispatcher.UIThread.RunJobs();

            RecordStep(panel, "b");

            var control = Assert.Single(panel.CoTriggers, trigger => trigger.Symbol == MacroModifierMarks.ControlMark);

            panel.ToggleCoTriggerCommand.Execute(control);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal([false, true, false], LatchStatesOf(view));
            Assert.Equal(["[b]"], TokensOf(panel));

            // Back to slot 1 — a different macro, with a different trigger — through the chip's own
            // command, which is how the app puts the write there.
            ClickSlotChip(view, 1);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal(["[e]", "[s]", "[t]"], TokensOf(panel));
            Assert.Equal([false, false, false], LatchStatesOf(view));

            // ...and back again, so the relight is proved in both directions.
            ClickSlotChip(view, 2);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.Equal([false, true, false], LatchStatesOf(view));
        }

        /// <summary>
        /// <b>The trigger latches</b>: three left-hand marks on the slot row, right-aligned under
        /// <c>TRIGGER +</c>, wearing the same chip face as the slots because the mock draws them
        /// identically and they mean the same kind of thing.
        /// <para>
        /// The assertion that carries the face is the <b>selected foreground</b>. This site is type
        /// rather than geometry precisely because the chip's <c>.selected</c> face sets
        /// <c>Foreground</c>, which a <c>TextBlock</c> inherits and an <c>Icon</c> — painting from
        /// <c>Stroke</c> — cannot; a <c>muted</c> or a hand-set colour on the run would leave the
        /// mark grey with every other assertion here still passing.
        /// </para>
        /// <para>
        /// The strip is <b>measured</b>, never pinned to a number: a font-metric shift on another
        /// machine moves the figure, and what must hold is that the row does not wrap and nothing
        /// runs off a 440 px rail — with five slot chips on the same line.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheTriggerLatches_DrawThreeLeftHandMarks_AndShareTheSlotRow(string variantName)
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

            var latches = CoTriggerChipsOf(view);

            Assert.Equal(panel.CoTriggers.Count, latches.Length);

            // The strip, read off the glass, in the order it is drawn in. THREE, and no `⌘`.
            Assert.Equal(
                ["⇧", "⌃", "⌥"],
                latches.Select(latch => string.Concat(VisibleRunsOf(latch).Select(run => run.Text))));

            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.TriggerSectionLabel, texts);
            Assert.Contains(MacroInspectorPanelViewModel.TriggerJoin, texts);

            // The token left the strip with issue #146 — the rail's own header names the position,
            // and drawing `[hk7]` twice on one rail said nothing the second time.
            Assert.DoesNotContain("[hk7]", texts);

            // The `CO-TRIGGERS` block left the footer with them; nothing may draw a second copy.
            Assert.DoesNotContain("CO-TRIGGERS", texts);

            // ONE ROW, slots and latches together, inside the rail.
            var chips = SlotChipsOf(view).Concat(latches).ToArray();
            var rows = chips
                .Select(chip => Math.Round(chip.TranslatePoint(new Point(0, 0), view)!.Value.Y, 2))
                .Distinct()
                .Count();

            Assert.Equal(1, rows);
            Assert.All(
                chips,
                chip => Assert.True(
                    RightEdgeOf(chip, view) <= WideRailWidth,
                    $"A chip runs {RightEdgeOf(chip, view) - WideRailWidth:0.#} px off the rail."));

            // The latches are to the RIGHT of every slot chip, which is what "the trigger half is
            // right-aligned" means when both halves are measured rather than assumed.
            Assert.True(
                latches.Min(latch => LeftEdgeOf(latch, view))
                > SlotChipsOf(view).Max(chip => RightEdgeOf(chip, view)));

            var accent = DesignTokens.ResolveBrushColor("AccentBrush", variant);
            var secondary = DesignTokens.ResolveBrushColor("TextSecondaryBrush", variant);
            var selectedRuns = 0;

            foreach (var latch in latches)
            {
                var model = Assert.IsType<MacroCoTriggerViewModel>(latch.DataContext);
                var runs = VisibleRunsOf(latch);

                // ONE run each: left spells no side. The two-line caption is the tooltip, and
                // nothing draws it.
                Assert.Single(runs);
                Assert.Equal(model.Symbol, runs[0].Text);
                Assert.Equal(model.Caption, latch.GetValue(ToolTip.TipProperty));
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

                    // Inherited from the chip in both states — which is the point of drawing this
                    // one as type.
                    if (model.IsOn)
                    {
                        Assert.Equal(accent, colour);
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
        /// The trigger status is drawn <b>only when it is an advisory</b> since issue #146: at rest
        /// the mock says nothing, because <c>bare press · no collision</c> was the panel restating
        /// what the empty latches already showed. A collision draws one amber line <em>and</em> rings
        /// <b>both</b> slot chips involved — a ring on one of two indistinguishable macros would
        /// name a culprit where there is only a pair. Amber, never the error ramp: a collision is
        /// what <c>Validate()</c> reports, and it reports rather than blocks.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheTriggerStatus_IsDrawnOnlyAsAnAdvisory_AndRingsBothCollidingChips(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            var advisory = DesignTokens.ResolveBrushColor("StatusAdvisoryTextBrush", variant);
            var error = DesignTokens.ResolveBrushColor("StatusErrorTextBrush", variant);

            // AT REST NOTHING IS SAID, and nothing is even drawn — a hidden run holding the old
            // `bare press · no collision` would be the sentence coming back on the next binding.
            Assert.False(panel.IsTriggerAdvisory);
            Assert.Equal(string.Empty, panel.TriggerStatus);
            Assert.DoesNotContain("bare press", string.Concat(VisibleTextsOf(view)), StringComparison.Ordinal);

            // A second macro on the same key with the same (empty) co-trigger set — 06 §5's own
            // duplicate rule, which `Validate()` reports as MacroTriggerCollision and never refuses.
            ClickSlotChip(view, 2);

            Dispatcher.UIThread.RunJobs();

            RecordStep(panel, "b");

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.True(panel.IsTriggerAdvisory);
            Assert.Contains(
                MacroInspectorPanelViewModel.BuildCollisionStatus(1),
                panel.TriggerStatus,
                StringComparison.Ordinal);

            var status = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.IsEffectivelyVisible && block.Text == panel.TriggerStatus);

            Assert.Contains("statusWarning", status.Classes);
            Assert.DoesNotContain("statusError", status.Classes);

            var colour = Assert.IsAssignableFrom<ISolidColorBrush>(status.Foreground).Color;

            Assert.Equal(advisory, colour);
            Assert.NotEqual(error, colour);

            // BOTH SIDES OF THE CLASH ARE RINGED, and only they.
            var chips = SlotChipsOf(view);
            var ringed = DesignTokens.Resolve("StatusAdvisoryBrush", variant);

            Assert.Equal(
                [true, true, false, false, false],
                chips.Select(chip => chip.Classes.Contains("colliding")));
            Assert.Equal(ringed, chips[0].BorderBrush);
            Assert.Equal(ringed, chips[1].BorderBrush);
        }

        /// <summary>
        /// The compose bar at the glass: <b>one bordered box</b> pinned under the list, always
        /// drawn and <b>dead until a step is selected</b> — the deliberate exception to the design's
        /// "absent features are not shown, not disabled", recorded in docs/app/design-system.md.
        /// <para>
        /// It also pins what issue #146 <em>removed</em> from the block: the four section captions
        /// <c>KEY</c> / <c>HELD</c> / <c>ACTION</c> / <c>THEN WAIT</c>, which spent four lines of the
        /// rail labelling controls that label themselves.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheComposer_IsABorderedBox_AlwaysDrawn_AndDeadUntilAStepIsSelected(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var panel = await scenes.CreateMacroInspectorPanelAsync();
            var view = new MacroInspectorPanelView { DataContext = panel };
            var variant = ToVariant(variantName);

            using var host = Show(view, variantName);

            host.Capture();

            var composer = ComposerBoxOf(view);
            var texts = VisibleTextsOf(view);

            Assert.Contains(MacroInspectorPanelViewModel.ComposerLabel, texts);
            Assert.Equal(DesignTokens.Resolve("SurfaceInsetBrush", variant), composer.Background);
            Assert.Equal(DesignTokens.Resolve("SurfaceLineBrush", variant), composer.BorderBrush);

            // THE FOUR CAPTIONS ARE GONE. Written out rather than taken off constants: the
            // constants were deleted, and the claim outlives them.
            foreach (var caption in new[] { "KEY", "HELD", "ACTION", "THEN WAIT" })
            {
                Assert.DoesNotContain(caption, texts);
            }

            // The sentence that explains the dead state is on screen for exactly that state.
            Assert.False(panel.Steps.HasSelection);
            Assert.Contains(MacroInspectorPanelViewModel.ComposerHint, texts);

            // ...and with no key on the selected step there is no token to draw, so the field is
            // the dash rather than a purple token that names nothing.
            Assert.False(panel.HasStepKey);
            Assert.Equal(MacroInspectorPanelViewModel.NoStepKeyText, VisibleRunsOf(KeyFieldOf(view))[0].Text);

            // THE KEY SEARCH IS GONE. #128 hosted the shared picker here and #139 took it away with
            // the append-a-chord flow it served.
            Assert.Empty(view.GetVisualDescendants().OfType<TokenPickerView>());

            var latches = LatchesOf(view);
            var directions = DirectionSegmentsOf(view);

            // `⇧ ⌃ ⌥ ⌘` — four, left-hand, and no `R` on any of them, on the same chip face the
            // slots and the trigger latches wear.
            Assert.Equal(
                ["⇧", "⌃", "⌥", "⌘"],
                latches.Select(latch => string.Concat(VisibleRunsOf(latch).Select(run => run.Text))));
            Assert.Equal("Left Ctrl", latches[1].GetValue(ToolTip.TipProperty));
            Assert.All(latches, latch => Assert.Contains("macroChip", latch.Classes));

            // The mark is set in the THIRD family: no IBM Plex face carries U+21E7 or U+2318, so a
            // latch that lost the class would draw tofu on CI and something plausible on a Mac.
            Assert.All(latches, latch => Assert.Contains("keySymbol", VisibleRunsOf(latch)[^1].Classes));

            Assert.Equal(
                [MacroInspectorStepViewModel.TapAction,
                 MacroInspectorStepViewModel.PressAction,
                 MacroInspectorStepViewModel.ReleaseAction],
                directions.Select(segment => segment.Content as string));

            // Dead, all of it — and the direction segments are `toggleSegment` and not the chip
            // face: they are a one-of-N choice, which the segment's filled face is what says.
            Assert.All(latches, latch => Assert.False(latch.IsEffectivelyEnabled));
            Assert.All(directions, segment => Assert.False(segment.IsEffectivelyEnabled));
            Assert.All(directions, segment => Assert.Contains("toggleSegment", segment.Classes));
            Assert.False(RecordStepKeyButtonOf(view).IsEffectivelyEnabled);

            // ...except the two affordances a selection comes to exist through.
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

            // The field is the selected step's own token, tinted like the row it edits. It is a
            // READOUT and not an `actionField`: that one records a keypress when it is clicked and
            // goes amber while armed, and wearing its face here would promise a click that does
            // nothing.
            var field = KeyFieldOf(view);
            var token = Assert.Single(VisibleRunsOf(field));

            Assert.Equal(panel.StepTokenText, token.Text);
            Assert.Contains("monoValue", token.Classes);
            Assert.DoesNotContain("actionField", field.Classes);
            Assert.Equal(
                DesignTokens.ResolveBrushColor("MacroStepKeyBrush", variant),
                ((ISolidColorBrush)token.Foreground!).Color);

            // MEASURED, NEVER PINNED TO A NUMBER — the strip's rule, for the same reason: two rows
            // of latches, a key field, a Record and a millisecond box is exactly the shape a
            // font-metric shift on another machine pushes off the rail.
            Assert.All(
                LatchesOf(view)
                    .Concat(DirectionSegmentsOf(view))
                    .Concat(DelaySegmentsOf(view))
                    .Cast<Control>()
                    .Concat([field]),
                control => Assert.True(
                    RightEdgeOf(control, view) <= WideRailWidth,
                    $"'{control}' runs {RightEdgeOf(control, view) - WideRailWidth:0.#} px off the rail."));
        }

        /// <summary>
        /// The whole point issue #128's composer existed for, carried through two rewrites:
        /// <c>Ctrl+1</c> — which macOS keeps for itself, so no capture can ever hear it — authored
        /// without ever pressing it. Record the bare <c>1</c>, which the window server <em>does</em>
        /// deliver, then tick <c>⌃</c> on the step.
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

            // The chord reads as ONE keystroke on the row — the mark and the token in one chip.
            var chip = Assert.Single(
                RowOf(view, step.Position).GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("macroStepToken"));

            Assert.Equal(
                MacroModifierMarks.ControlMark + "[1]",
                string.Concat(VisibleRunsOf(chip).Select(run => run.Text)));
        }

        /// <summary>
        /// The composer edits the selected step, so moving the rail to another key has to take the
        /// selection — and any <c>＋</c> placeholder standing on it — with it. A placeholder that
        /// survived would point at an index in a macro that is no longer the one under edit.
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

        /// <summary>The floating copy of the carried row, hidden except while a drag is in flight.</summary>
        private static Border GhostOf(Control view)
        {
            return Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("macroStepGhost"));
        }

        /// <summary>The `COMPOSE A STEP` box, pinned under the list.</summary>
        private static Border ComposerBoxOf(Control view)
        {
            return Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("composerBox"));
        }

        /// <summary>The composer's key readout — a bordered field, never an armed action field.</summary>
        private static Border KeyFieldOf(Control view)
        {
            return Assert.Single(
                view.GetVisualDescendants().OfType<Border>(),
                border => border.Classes.Contains("macroComposerKey"));
        }

        /// <summary>The `Delete` in the footer, found by the caption it is pinned to.</summary>
        private static Button DeleteButtonOf(Control view)
        {
            return Assert.Single(
                view.GetVisualDescendants().OfType<Button>(),
                button => button.IsEffectivelyVisible
                          && (button.Content as string) == MacroInspectorPanelViewModel.DeleteMacroCaption);
        }

        /// <summary>The record dot inside one of the panel's two Record buttons.</summary>
        private static Ellipse DotOf(Button record)
        {
            return Assert.Single(
                record.GetVisualDescendants().OfType<Ellipse>(),
                dot => dot.Classes.Contains("recordDot"));
        }

        /// <summary>The `SLOTS` strip's chips, in slot order.</summary>
        private static Button[] SlotChipsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.DataContext is MacroSlotOption)
                .ToArray();
        }

        /// <summary>The `TRIGGER +` latches, in 05 §5.1's table order.</summary>
        private static Button[] CoTriggerChipsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.DataContext is MacroCoTriggerViewModel)
                .ToArray();
        }

        /// <summary>Which of the three trigger latches are lit, read off the glass.</summary>
        private static IReadOnlyList<bool> LatchStatesOf(Control view)
        {
            return CoTriggerChipsOf(view).Select(latch => latch.Classes.Contains("selected")).ToArray();
        }

        /// <summary>Runs the chip of <paramref name="slot"/> the way a click on it does.</summary>
        private static void ClickSlotChip(Control view, int slot)
        {
            var chip = SlotChipsOf(view)[slot - 1];

            chip.Command!.Execute(chip.CommandParameter);
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
                panel.ReceiveKeystroke(new CapturedKeystroke
                {
                    Key = key,
                    PhysicalKey = PhysicalKeyCode.None
                });
            }

            panel.Deactivate();
        }

        /// <summary>Records one plain step through the panel's own capture path.</summary>
        private static void RecordStep(MacroInspectorPanelViewModel panel, string token)
        {
            panel.RecordCommand.Execute(null);

            panel.ReceiveKeystroke(new CapturedKeystroke
            {
                Key = KeyRegistry.FindByToken(token, TokenDialect.Gen1)!,
                PhysicalKey = PhysicalKeyCode.None
            });

            panel.Deactivate();
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

        /// <summary>How far <paramref name="control"/>'s right edge reaches in the given box.</summary>
        private static double RightEdgeOf(Control control, Control box)
        {
            return control.TranslatePoint(new Point(control.Bounds.Width, 0), box)?.X
                   ?? throw new InvalidOperationException("The control is not in that box's tree.");
        }

        /// <summary><paramref name="control"/>'s left edge in <paramref name="box"/>'s coordinates.</summary>
        private static double LeftEdgeOf(Control control, Control box)
        {
            return control.TranslatePoint(default, box)?.X
                   ?? throw new InvalidOperationException("The control is not in that box's tree.");
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
        /// The composer's own single-shot <c>Record key</c> — told from the Sequence header's take
        /// by the command it runs.
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
        /// <c>DataContext</c> rather than by class: the two segment strips share
        /// <c>toggleSegment</c> and the latches share <c>macroChip</c> with the slots.
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

        /// <summary>Every delay pill on screen — the accent `80 ms` box at the right of a row.</summary>
        private static Border[] VisiblePillsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("macroStepDelay") && border.IsEffectivelyVisible)
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

        /// <summary>
        /// The row of <paramref name="position"/> — the frame that carries its selection ring, and
        /// so the outermost control the row is made of: grip, body, delete mark and drop ring.
        /// </summary>
        private static Border RowOf(Control view, int position)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .First(border => border.Classes.Contains("macroStepRowFrame")
                                 && border.DataContext is MacroInspectorStepViewModel step
                                 && step.Position == position);
        }

        /// <summary>That row's 12 px drag grip.</summary>
        private static Icon GripOf(Control view, int position)
        {
            return RowOf(view, position)
                .GetVisualDescendants()
                .OfType<Icon>()
                .First(icon => icon.Classes.Contains("dragHandle"));
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

        /// <summary>The middle of that row's grip.</summary>
        private static Point GripPointOf(ThemedHost host, Control view, int position)
        {
            return CentreOf(host, GripOf(view, position));
        }

        private static Point CentreOf(ThemedHost host, Control control)
        {
            return control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), host.Window)
                   ?? throw new InvalidOperationException("The control is not in the window's tree.");
        }

        /// <summary>
        /// How many drop rings that row is showing. Three borders in a row can be the accent — the
        /// selected frame, the delay pill and this one — and the ring is the only one that is
        /// <b>not hit-testable</b>, which is also exactly why it works: the release's hit test has
        /// to reach the row under it.
        /// </summary>
        private static int VisibleDropRingsIn(Control row, ThemeVariant variant)
        {
            var accent = DesignTokens.ResolveBrushColor("AccentBrush", variant);

            return row.GetVisualDescendants()
                .OfType<Border>()
                .Count(border => border.IsEffectivelyVisible
                                 && !border.IsHitTestVisible
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

        /// <summary>
        /// The caption of every button the panel is really showing. A `Content` that is not a string
        /// is somebody else's control (the record dots, the composer's two-run latches), so it
        /// contributes nothing rather than a type name.
        /// </summary>
        private static IReadOnlyList<string> VisibleButtonCaptionsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.IsEffectivelyVisible)
                .Select(button => button.Content as string ?? string.Empty)
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
