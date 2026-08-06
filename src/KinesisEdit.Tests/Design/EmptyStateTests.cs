using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Devices;
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The dashboard's empty state at the glass (docs/design/mockups.md §1d): the mono law inside
    /// the step sentences, the silhouettes, the two marks that replaced characters in captions, and
    /// the one thing about this screen that only a frame ever showed — that changing the pick must
    /// not move the screen.
    /// <para>
    /// The view-model half — the copy, the per-device steps, the counter — is
    /// <c>ViewModels/NoDeviceViewModelTests</c>; the picker's container theme and the three action
    /// weights are <see cref="ControlThemeBridgeTests"/>. This suite is only for what a rendered
    /// tree can answer and a view model cannot.
    /// </para>
    /// </summary>
    public class EmptyStateTests
    {
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task EveryStepsHardwareLiteral_IsMonoAndItsProseIsNot(string variantName)
        {
            // The mono law: mono is for values that exist verbatim on the hardware or the drive —
            // a keycap legend, an onboard shortcut, a volume label — and sans for everything the
            // app says in its own voice. A step is one sentence carrying both, so the split has to
            // survive inside a single TextBlock rather than between two of them.
            var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);
            var picker = (NoDeviceViewModel)view.DataContext!;

            picker.SelectedDevice = DeviceCatalog.GetById(DeviceId.Advantage2);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var mono = (FontFamily)DesignTokens.Resolve("FontMono", variant);
            var sentences = StepSentences(view);

            Assert.Equal(picker.Steps.Count, sentences.Count);

            foreach (var sentence in sentences)
            {
                var runs = sentence.Inlines!.OfType<Run>().ToArray();

                Assert.Equal(3, runs.Length);

                // The literal run, and only it, leaves the inherited sans.
                Assert.Equal(mono, runs[1].FontFamily);
                Assert.NotEqual(mono, sentence.FontFamily);
                Assert.NotEqual(mono, runs[0].FontFamily);
                Assert.NotEqual(mono, runs[2].FontFamily);
            }

            // ...and the sentences really are the ones the view model built, runs and all: an empty
            // middle run on a step that names no literal would pass the checks above vacuously.
            Assert.Equal(
                picker.Steps.Select(step => step.Text),
                sentences.Select(sentence => string.Concat(sentence.Inlines!.OfType<Run>().Select(run => run.Text))));
            Assert.Contains("ADVANTAGE2", picker.Steps.Select(step => step.ValueText));
        }

        [AvaloniaFact]
        public async Task EveryPickerRow_DrawsItsBoardAtThePickerScale()
        {
            // Mockup §1d: "all 7 known models with silhouettes". A device whose art went missing
            // would draw an icon-shaped hole — nothing throws, and no other guard reads this
            // converter's output.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);
            var picker = (NoDeviceViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var silhouettes = view.GetVisualDescendants()
                .OfType<Icon>()
                .Where(icon => icon.Classes.Contains("deviceArt"))
                .ToArray();

            Assert.Equal(picker.Devices.Count, silhouettes.Length);

            foreach (var silhouette in silhouettes)
            {
                Assert.NotNull(silhouette.Data);
                Assert.Equal(33, silhouette.Size);

                // Finer than the icon law's 1.5 on purpose: at 33px the key rects are under two
                // pixels across and a 1.5 pen welds them into a slab.
                Assert.Equal(0.6, silhouette.StrokeThickness);
                Assert.Null(silhouette.Fill);
            }
        }

        [AvaloniaFact]
        public async Task TheActionRow_DrawsItsArrowAndItsRefreshAsMarks()
        {
            // The mockup writes "Scan now" beside a refresh glyph and "Troubleshooting tips ↗".
            // Both are geometry here: a Latin-1 arrow in a caption is a glyph the shipped font may
            // not carry, and the icon system exists precisely so a caption never has to.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var marks = view.GetVisualDescendants()
                .OfType<Icon>()
                .Where(icon => !icon.Classes.Contains("deviceArt"))
                .Select(icon => icon.Data)
                .ToArray();

            Assert.Contains(DesignTokens.Resolve("IconRefresh", ThemeVariant.Dark), marks);
            Assert.Contains(DesignTokens.Resolve("IconExternalLink", ThemeVariant.Dark), marks);

            var text = string.Concat(view.GetVisualDescendants().OfType<TextBlock>().Select(block => block.Text));

            Assert.DoesNotContain('↗', text);
        }

        [AvaloniaFact]
        public async Task ChangingThePick_MovesNothingOnTheScreen()
        {
            // The regression a frame caught and no assertion did: the screen was arranged at its
            // *content's* width, so a device whose step sentences were shorter pulled both panels
            // inward — the picker's own rows sliding out from under the pointer that had just
            // clicked one. Same law as the dashboard's fixed card height.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);
            var picker = (NoDeviceViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            picker.SelectedDevice = DeviceCatalog.GetById(DeviceId.Advantage2);
            host.Capture();

            var before = PanelBoxes(view);

            // The TKO has the fewest steps and the shortest sentences of the seven.
            picker.SelectedDevice = DeviceCatalog.GetById(DeviceId.Tko);
            host.Capture();

            Assert.Equal(before, PanelBoxes(view));
        }

        [AvaloniaFact]
        public async Task DeselectingThePickedRow_PutsThePickBackInsteadOfLeavingNothingLit()
        {
            // A single-selection ListBox drops the row that is already selected when it is
            // Ctrl/Cmd-clicked. This screen has no "nothing picked" state, so the list would
            // otherwise sit with no row lit while the title, the steps, the demo target and the
            // support link beside it all still described a board. `SelectionMode="AlwaysSelected"`
            // is not the fix — it re-selects the *first* row, silently moving the pick.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(NoDeviceView).FullName!);
            var picker = (NoDeviceViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var list = view.GetVisualDescendants().OfType<ListBox>().Single();

            picker.SelectedDevice = DeviceCatalog.GetById(DeviceId.Tko);

            Dispatcher.UIThread.RunJobs();

            // What the toolkit does on a Ctrl/Cmd-click of the selected row.
            list.SelectedIndex = -1;

            Dispatcher.UIThread.RunJobs();

            Assert.Same(picker.SelectedOption, list.SelectedItem);
            Assert.Equal(DeviceId.Tko, picker.SelectedDevice.Id);
        }

        /// <summary>The two panels' rectangles in the view's own space.</summary>
        private static IReadOnlyList<Rect> PanelBoxes(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<Border>()
                .Where(border => border.Classes.Contains("card"))
                .Select(border => new Rect(
                    border.TranslatePoint(default, view) ?? throw new InvalidOperationException("A panel left the tree."),
                    border.Bounds.Size))
                .ToArray();
        }

        /// <summary>The step sentences: the wrapped prose beside each numbered marker.</summary>
        private static IReadOnlyList<TextBlock> StepSentences(Control view)
        {
            return view.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.Inlines is { Count: 3 })
                .ToArray();
        }
    }
}
