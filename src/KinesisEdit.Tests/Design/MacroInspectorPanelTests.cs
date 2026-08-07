using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
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
