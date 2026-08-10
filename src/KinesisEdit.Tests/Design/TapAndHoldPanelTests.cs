using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;
using KinesisEdit.ViewModels.Advisories;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The key inspector's Tap &amp; hold panel at the glass, in both theme variants (mockup
    /// <c>2h</c>): the two verbatim field labels with their own record buttons, the capture rule,
    /// the 1-999 slider on the device's own default, the amber budget block — and the firmware
    /// refusal, which is drawn <em>instead of</em> the fields rather than in place of the panel.
    /// <para>
    /// The panel is hosted at the rail's real <b>268 px</b>, not at a comfortable test width. That is
    /// the assertion behind half of this file: a label that reads fine at 600 px runs off the rail
    /// at 268, and no view-model test can see it.
    /// </para>
    /// </summary>
    public class TapAndHoldPanelTests
    {
        /// <summary>The rail's own width (<c>WidthInspectorRail</c>), which this panel lives inside.</summary>
        private const double RailWidth = 268;

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void BothCaptures_AreOnScreenWithTheirOwnRecordButton(string variantName)
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(TapAndHoldPanelViewModel.TapFieldLabel, texts);
            Assert.Contains(TapAndHoldPanelViewModel.HoldFieldLabel, texts);
            Assert.Contains(TapAndHoldPanelViewModel.CaptureRule, texts);
            Assert.Equal(2, texts.Count(text => text == TapAndHoldPanelViewModel.RecordCaption));

            var records = RecordButtonsOf(view);

            Assert.Equal(2, records.Length);
            Assert.Same(scene.Panel.ArmTapActionCommand, records[0].Command);
            Assert.Same(scene.Panel.ArmHoldActionCommand, records[1].Command);
        }

        /// <summary>
        /// Neither action field may be a <c>TextBox</c>: focus inside one auto-suspends the
        /// keystroke-capture service, so the field would swallow the very keypress it exists to
        /// record. They are <c>Border</c>s on the <c>TokenField</c> control theme.
        /// </summary>
        [AvaloniaFact]
        public void NeitherActionField_IsATextBox()
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, "Dark");

            host.Capture();

            Assert.Empty(view.GetVisualDescendants().OfType<TextBox>());

            var fields = ActionFieldsOf(view);

            Assert.Equal(2, fields.Length);
            Assert.All(fields, field => Assert.NotNull(field.Theme));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void ArmingAField_WearsTheAdvisoryBorderAndPulsesItsOwnCaption(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            // After Show, never before: a class raised before the first layout pass can be wiped by
            // the template being applied.
            scene.Panel.ArmHoldActionCommand.Execute(null);

            host.Capture();

            var fields = ActionFieldsOf(view);

            Assert.DoesNotContain("armed", (IEnumerable<string>)fields[0].Classes);
            Assert.Contains("armed", (IEnumerable<string>)fields[1].Classes);
            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryBrush", variant), fields[1].BorderBrush);

            // The 1.4s pulse is reached by the one class the listening cap and the macro banner
            // share — exactly one looping animation in the app.
            var captions = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Text == TapAndHoldPanelViewModel.RecordCaption)
                .ToArray();

            Assert.Equal(2, captions.Length);
            Assert.DoesNotContain("recording", (IEnumerable<string>)captions[0].Classes);
            Assert.Contains("recording", (IEnumerable<string>)captions[1].Classes);
        }

        /// <summary>
        /// Mockup <c>2h</c> writes the buttons as <c>● Record</c>. U+25CF is in <b>neither</b>
        /// embedded IBM Plex family, so the dot has to be geometry — a caption carrying it would
        /// draw as tofu, and every other assertion about the button would still pass.
        /// </summary>
        [AvaloniaFact]
        public void TheRecordDot_IsGeometryAndNotAGlyph()
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, "Dark");

            host.Capture();

            Assert.All(VisibleTextsOf(view), text => Assert.DoesNotContain('●', text));

            var dots = view.GetVisualDescendants()
                .OfType<Ellipse>()
                .Where(dot => dot.IsEffectivelyVisible)
                .ToArray();

            Assert.Equal(2, dots.Length);
            Assert.All(dots, dot => Assert.Equal(DesignTokens.Resolve("StatusErrorBrush", ThemeVariant.Dark), dot.Fill));
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheDelaySlider_CoversOneToNineNinetyNineAndOpensOnTheDevicesDefault(string variantName)
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var slider = Assert.Single(view.GetVisualDescendants().OfType<Slider>());

            Assert.Equal(1, slider.Minimum);
            Assert.Equal(999, slider.Maximum);
            Assert.Equal(250, slider.Value);

            var texts = VisibleTextsOf(view);

            Assert.Contains("250 ms", texts);
            Assert.Contains("default 250 · this device", texts);
        }

        /// <summary>
        /// The mono law: <c>250</c> is literally what <c>[t&amp;h250]</c> carries in the layout file,
        /// and <c>default 250 · this device</c> is the app talking about it.
        /// </summary>
        [AvaloniaFact]
        public void TheDelayReadoutIsMono_AndTheProseAroundItIsNot()
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, "Dark");

            host.Capture();

            var blocks = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .ToArray();

            var readout = Assert.Single(blocks, block => block.Text == "250 ms");
            var caption = Assert.Single(blocks, block => block.Text == "default 250 · this device");
            var rule = Assert.Single(blocks, block => block.Text == TapAndHoldPanelViewModel.CaptureRule);

            Assert.Contains("Mono", readout.FontFamily.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Mono", caption.FontFamily.Name, StringComparison.Ordinal);
            Assert.DoesNotContain("Mono", rule.FontFamily.Name, StringComparison.Ordinal);
        }

        /// <summary>
        /// The fields spell their action as the <b>file</b> does — <c>[lctrl]</c>, mockup <c>2h</c>'s
        /// own drawing — not as the cap does. A friendly caption is two lines on a stacked legend,
        /// which doubled the field's height in a 268 px rail; only a frame showed it.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheFilledFields_ShowTheBracketedFileTokenInMonoOnOneLine(string variantName)
        {
            var scene = new Scene();

            scene.Assign();

            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var fields = ActionFieldsOf(view);
            var tokens = fields
                .Select(field => Assert.Single(field.GetVisualDescendants().OfType<TextBlock>()))
                .ToArray();

            Assert.Equal(["a", "lctrl"], tokens.Select(token => token.Text));
            Assert.All(tokens, token => Assert.Contains("Mono", token.FontFamily.Name, StringComparison.Ordinal));

            // One line each: the two fields are the same height as an unfilled one.
            Assert.All(fields, field => Assert.InRange(field.Bounds.Height, 30, 36));
        }

        /// <summary>
        /// Amber, never red. A budget breach is an advisory about a legal layout — it still saves —
        /// and the app has exactly one warning colour.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheBudgetAdvisory_IsAmberAndNeverReachesTheErrorRamp(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene();

            scene.OverBudget();

            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var advisory = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == scene.Panel.BudgetAdvisory && block.IsEffectivelyVisible);

            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTextBrush", variant), advisory.Foreground);
            Assert.NotEqual(DesignTokens.Resolve("StatusErrorTextBrush", variant), advisory.Foreground);
        }

        /// <summary>
        /// A refusal the firmware cannot fix draws no <c>Update Firmware</c>. Only a frame showed
        /// it: at 11 of 10, §11.1's second pre-dialog check refuses and the panel was offering to
        /// update firmware that was already current.
        /// </summary>
        [AvaloniaFact]
        public void ARefusalThatIsNotTheGates_DrawsNoFirmwareUpdate()
        {
            var scene = new Scene();

            scene.OverBudget();

            var view = scene.CreateView();

            using var host = Show(view, "Dark");

            host.Capture();

            Assert.False(scene.Panel.IsAvailable);
            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible),
                button => Equals(button.Content, FirmwareFeatureGate.UpdateFirmwareButtonCaption));
        }

        [AvaloniaFact]
        public void WithNoBudgetBreach_NoAdvisoryBlockIsDrawn()
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, "Dark");

            host.Capture();

            Assert.False(scene.Panel.HasBudgetAdvisory);
            Assert.DoesNotContain(
                view.GetVisualDescendants().OfType<TextBlock>().Where(block => block.IsEffectivelyVisible),
                block => block.Text?.StartsWith("Tap-and-hold count", StringComparison.Ordinal) == true);
        }

        /// <summary>
        /// The sanctioned exception to "absent features are not shown": where the firmware is too
        /// old the panel <b>refuses politely</b> instead of disappearing, with the gate's own
        /// wording and its conditional <c>Update Firmware</c> action.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void BelowTheFirmwareGate_ThePanelRefusesInsteadOfDisappearing(string variantName)
        {
            var scene = new Scene(Firmware(1, 0, 0));
            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var texts = VisibleTextsOf(view);

            Assert.Contains(TapAndHoldPanelViewModel.FirmwareRefusalMessage, texts);
            Assert.DoesNotContain(TapAndHoldPanelViewModel.TapFieldLabel, texts);
            Assert.DoesNotContain(ActionFieldsOf(view), field => field.IsEffectivelyVisible);

            var update = Assert.Single(
                view.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible),
                button => Equals(button.Content, FirmwareFeatureGate.UpdateFirmwareButtonCaption));

            Assert.Same(scene.Panel.UpdateFirmwareCommand, update.Command);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public void TheRefusal_IsAmberAndNotRed(string variantName)
        {
            var variant = ToVariant(variantName);
            var scene = new Scene(Firmware(1, 0, 0));
            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var refusal = Assert.Single(
                view.GetVisualDescendants().OfType<TextBlock>(),
                block => block.Text == TapAndHoldPanelViewModel.FirmwareRefusalMessage);

            Assert.Equal(DesignTokens.Resolve("StatusAdvisoryTextBrush", variant), refusal.Foreground);
        }

        /// <summary>
        /// The defect a frame catches and no assertion about a control does: at 268 px a label that
        /// does not wrap runs off the rail, and a horizontal <c>StackPanel</c> measures its children
        /// with infinite width so <c>TextWrapping</c> would never fire inside one.
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark", "filled")]
        [InlineData("Light", "filled")]
        [InlineData("Dark", "refused")]
        [InlineData("Light", "refused")]
        public void NothingRunsOffTheTwoHundredAndSixtyEightPixelRail(string variantName, string state)
        {
            var scene = state == "refused" ? new Scene(Firmware(1, 0, 0)) : new Scene();

            scene.Assign();

            if (state == "refused")
            {
                scene.OverBudget();
            }

            var view = scene.CreateView();

            using var host = Show(view, variantName);

            host.Capture();

            var overflowing = view.GetVisualDescendants()
                .OfType<Control>()
                .Where(control => control.IsEffectivelyVisible && control.Bounds.Width > 0)
                .Where(control => RightEdgeOf(control, view) > RailWidth + 0.5)
                .Select(control => $"{control.GetType().Name} reaches {RightEdgeOf(control, view):0.##}")
                .ToArray();

            Assert.True(overflowing.Length == 0, string.Join(Environment.NewLine, overflowing));
        }

        [AvaloniaFact]
        public void EveryProseLabel_Wraps_SoNoneOfItIsTrimmedAwayAtRailWidth()
        {
            var scene = new Scene();
            var view = scene.CreateView();

            using var host = Show(view, "Dark");

            host.Capture();

            var prose = new[]
            {
                TapAndHoldPanelViewModel.TapFieldLabel,
                TapAndHoldPanelViewModel.HoldFieldLabel,
                TapAndHoldPanelViewModel.CaptureRule,
                TapAndHoldPanelViewModel.NoteText,
                "default 250 · this device"
            };

            var blocks = view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible && prose.Contains(block.Text))
                .ToArray();

            Assert.Equal(prose.Length, blocks.Length);
            Assert.All(blocks, block => Assert.Equal(TextWrapping.Wrap, block.TextWrapping));
            Assert.All(blocks, block => Assert.Equal(TextTrimming.None, block.TextTrimming));
        }

        private static double RightEdgeOf(Control control, Control root)
        {
            var origin = ((Visual)control).TranslatePoint(default, root) ?? default;

            return origin.X + control.Bounds.Width;
        }

        private static Border[] ActionFieldsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("actionField"))
                .ToArray();
        }

        private static Button[] RecordButtonsOf(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Button>()
                .Where(button => button.Classes.Contains("recordAction"))
                .ToArray();
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
            return ThemedHost.Show(view, ToVariant(variantName), RailWidth, 760);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        private static FirmwareState Firmware(int major, int minor, int revision)
        {
            return new FirmwareState { KeyboardFirmware = new FirmwareVersion(major, minor, revision) };
        }

        /// <summary>
        /// A panel over a real Freestyle Edge RGB position, built without an editor or a rail
        /// anywhere near it — the panel is decoupled from both by contract, and a scene that could
        /// only reach it through the editor would quietly assert the opposite.
        /// </summary>
        private sealed class Scene
        {
            public TapAndHoldPanelViewModel Panel { get; }

            private const int SelectedKeyIndex = 0;

            private readonly KeyboardLayout _layout;
            private readonly KeyboardLayerViewModel _layer;
            private readonly IUrlLauncher _urlLauncher = new FakeUrlLauncher();

            public Scene(FirmwareState? firmware = null)
            {
                var tokens = new[] { "esc", "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11" };

                _layout = TestLayouts.CreateLayout(tokens);

                var indexes = new int[tokens.Length];

                for (var index = 0; index < indexes.Length; index++)
                {
                    indexes[index] = index;
                }

                _layer = KeyboardLayerViewModel.BuildAll(
                    _layout,
                    TestLayouts.CreateVisual(indexes),
                    lighting: null)[0];

                Panel = new TapAndHoldPanelViewModel(
                    DeviceId.FreestyleEdgeRgb,
                    firmware ?? new FirmwareState { IsDemoMode = true },
                    _urlLauncher);
            }

            /// <summary>Gives the selected position a tap-and-hold, so both fields have a token in them.</summary>
            public void Assign()
            {
                _layer.FindByIndex(SelectedKeyIndex)!.Key.SetTapAndHold(
                    TestLayouts.Gen1Key("a"),
                    TestLayouts.Gen1Key("lctrl"),
                    250);
            }

            /// <summary>Drives the profile past the device's budget of ten, off the selected key.</summary>
            public void OverBudget()
            {
                var assigned = 0;

                foreach (var key in _layout.Layers[0].Keys)
                {
                    if (assigned >= 11)
                    {
                        return;
                    }

                    if (key.Index != SelectedKeyIndex
                        && key.SetTapAndHold(TestLayouts.Gen1Key("a"), TestLayouts.Gen1Key("lctrl"), 250))
                    {
                        assigned++;
                    }
                }
            }

            public TapAndHoldPanelView CreateView()
            {
                Panel.Refresh(
                    _layer.FindByIndex(SelectedKeyIndex),
                    _layer,
                    _layout,
                    EditorAdvisories.Build(_layout));

                return new TapAndHoldPanelView { DataContext = Panel };
            }
        }
    }
}
