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
using KinesisEdit.Tests.Headless;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The legend row under the keyboard canvas (mockups 1e/2a), at the glass: the row is drawn
    /// under the board, each swatch is the cap badge's own token brush, the counts read what the
    /// layer holds, both actions are there, and the armed <c>Copy key…</c> prompt appears.
    /// <para>
    /// It asserts on <b>resolved brushes, arranged order and text</b>, never on a golden image —
    /// the documented refusal in design-system.md § "Rendered-frame capture". The brushes are the
    /// interesting half: the whole claim of the row is that it restates the badge vocabulary, and
    /// a swatch painted from the wrong token teaches the wrong board while every count still adds
    /// up.
    /// </para>
    /// </summary>
    public class BoardLegendTests
    {
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheLegendRow_IsDrawnUnderTheBoard(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());
            var board = Assert.Single(view.GetVisualDescendants().OfType<KeyboardView>(), picture => picture.IsEffectivelyVisible);

            Assert.True(row.IsEffectivelyVisible);

            var boardBottom = board.TranslatePoint(new Point(0, board.Bounds.Height), host.Window)!.Value.Y;
            var rowTop = row.TranslatePoint(default, host.Window)!.Value.Y;

            Assert.True(rowTop >= boardBottom, $"The legend row sits at {rowTop}, above the board's bottom edge at {boardBottom}.");
        }

        /// <summary>
        /// The row's two actions stay clear of the key inspector rail with a key selected.
        /// <para>
        /// This is the defect the visual gate caught on issue #92 and ~5 900 passing tests did not:
        /// the row was <c>Auto,*,Auto</c>, and a horizontal <see cref="StackPanel"/> measures its
        /// children at infinite width — so when the rail took 268px the counts column did not
        /// shrink, it overflowed, and <c>Reset layer</c> arranged 4px <em>under</em> the rail. Every
        /// assertion about the row was still true: the buttons existed, were bound, carried their
        /// captions and their theme. Only where they landed was wrong, and nothing that asks a
        /// control about itself can see that.
        /// </para>
        /// <para>
        /// It is asserted at the window, not at the row, because the row's own bounds are exactly
        /// what was lying: the overflowing child was outside them.
        /// </para>
        /// </summary>
        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRowsActions_AreNotArrangedUnderTheKeyInspectorRail(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var view = new KeyboardEditorView { DataContext = editor };

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            // The cap is clicked AFTER the view is on screen: attaching the editor makes the layer
            // switcher raise its initial selection, which stands the rail down again.
            var layer = Assert.IsType<KeyboardLayerViewModel>(editor.SelectedLayer);
            editor.SelectKeyCommand.Execute(layer.Keys[0]);
            Dispatcher.UIThread.RunJobs();
            host.Capture();

            var rail = Assert.Single(view.GetVisualDescendants().OfType<KeyInspectorView>());
            Assert.True(rail.IsEffectivelyVisible, "The rail did not open, so this test would pass vacuously.");

            var railLeft = rail.TranslatePoint(default, host.Window)!.Value.X;
            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());

            var actions = row.GetVisualDescendants().OfType<Button>().Where(button => button.IsEffectivelyVisible).ToList();

            Assert.Equal(2, actions.Count);

            foreach (var action in actions)
            {
                var right = action.TranslatePoint(default, host.Window)!.Value.X + action.Bounds.Width;

                Assert.True(
                    right <= railLeft,
                    $"'{action.Content}' is arranged to {right}, past the key inspector rail's left edge at {railLeft}.");
            }
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task EachSwatch_IsPaintedFromTheCapBadgesOwnToken(string variantName)
        {
            // The row's whole claim: these are the badges, restated at legend size. A swatch on the
            // wrong token teaches the wrong board and every count still adds up, so nothing but this
            // would notice.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(BoardLegendView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var fills = SwatchFills(view);

            Assert.Contains(DesignTokens.Resolve("AccentBrush", variant), fills);
            Assert.Contains(DesignTokens.Resolve("BadgeMacroBrush", variant), fills);
            Assert.Contains(DesignTokens.Resolve("BadgeTapHoldBrush", variant), fills);
            Assert.Contains(DesignTokens.Resolve("StatusAdvisoryStrongBrush", variant), fills);
            Assert.Contains(DesignTokens.Resolve("HatchBrush", variant), fills);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheAdvisorySwatch_IsAmberAndNeverRed(string variantName)
        {
            // Invariant 22, at the one place in this row where a "warning" colour appears at all.
            var variant = ToVariant(variantName);

            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(BoardLegendView).FullName!);

            using var host = ThemedHost.Show(view, variant);

            host.Capture();

            var fills = SwatchFills(view);

            Assert.Contains(DesignTokens.Resolve("StatusAdvisoryStrongBrush", variant), fills);
            Assert.DoesNotContain(DesignTokens.Resolve("StatusErrorBrush", variant), fills);
            Assert.DoesNotContain(DesignTokens.Resolve("StatusErrorTextBrush", variant), fills);
        }

        [AvaloniaFact]
        public async Task TheCounts_ReadWhatTheLayerHolds()
        {
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorWithAdvisoriesAsync();
            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());
            var texts = TextsOf(row);

            Assert.Contains(BoardLegendViewModel.LegendLabel, texts);

            // The advisories scene remapped one position onto a token the board already carries, so
            // the row reads one remap and the two positions that now hold the duplicate.
            Assert.Equal(1, editor.BoardLegend.RemappedCount);
            Assert.Equal(2, editor.BoardLegend.AdvisoryCount);

            AssertCount(texts, BoardLegendViewModel.RemappedCaption, "1");
            AssertCount(texts, BoardLegendViewModel.MacroCaption, "0");
            AssertCount(texts, BoardLegendViewModel.TapAndHoldCaption, "0");
            AssertCount(texts, BoardLegendViewModel.AdvisoryCaption, "2");
            AssertCount(texts, BoardLegendViewModel.LockedCaption, "0");
        }

        [AvaloniaFact]
        public async Task TheCounts_FollowAnEditWithoutTheViewBeingRebuilt()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());

            AssertCount(TextsOf(row), BoardLegendViewModel.RemappedCaption, "0");

            var key = editor.SelectedLayer!.Keys[0];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            scenes.Capture.RaiseKeystroke(editor.SelectedLayer.Keys[1].Key.OriginalKey);

            host.Capture();

            AssertCount(TextsOf(row), BoardLegendViewModel.RemappedCaption, "1");
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheRow_CarriesCopyKeyAndResetLayer(string variantName)
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());
            var buttons = row.GetVisualDescendants().OfType<Button>().ToArray();

            Assert.Equal(2, buttons.Length);
            Assert.Contains(buttons, button => Equals(button.Content, BoardLegendViewModel.CopyKeyCaption));
            Assert.Contains(buttons, button => Equals(button.Content, BoardLegendViewModel.ResetLayerCaption));

            // The row runs the editor's own commands; nothing is re-implemented here.
            Assert.Same(
                editor.ResetLayerCommand,
                buttons.Single(button => Equals(button.Content, BoardLegendViewModel.ResetLayerCaption)).Command);
            Assert.Same(
                editor.CopyKeyCommand,
                buttons.Single(button => Equals(button.Content, BoardLegendViewModel.CopyKeyCaption)).Command);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_PutsThePickPromptInTheRow_AndEscapeTakesItAway()
        {
            // Escape's own order is panel, then capture, then the copy. Nothing is capturing here,
            // so the Escape reaches the copy — and the row stops prompting.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var row = Assert.Single(view.GetVisualDescendants().OfType<BoardLegendView>());

            Assert.DoesNotContain(BoardLegendViewModel.CopyTargetPrompt, TextsOf(row));

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[0]);
            editor.CopyKeyCommand.Execute(null);

            host.Capture();

            Assert.Contains(BoardLegendViewModel.CopyTargetPrompt, VisibleTextsOf(row));

            host.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();
            host.Capture();

            Assert.False(editor.IsCopyArmed);
            Assert.DoesNotContain(BoardLegendViewModel.CopyTargetPrompt, VisibleTextsOf(row));
        }

        [AvaloniaFact]
        public async Task Escape_LeavesCaptureFirst_AndOnlyThenAnArmedCopy()
        {
            // `2b`: "Esc leaves capture mode first". The two are never live together — arming a
            // copy ends a listen and starting a remap ends a copy — so the order is stated here
            // rather than left to whichever branch happens to be written first.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[0]);
            editor.CopyKeyCommand.Execute(null);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);
            Assert.False(editor.IsCopyArmed);

            host.Window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();

            // The listening key went, and nothing else was there to go with it.
            Assert.False(editor.IsListening);
            Assert.False(editor.IsCopyArmed);
        }

        [AvaloniaFact]
        public async Task AnArmedCopy_IsFinishedByClickingACapOnTheBoard()
        {
            // The pick's second half really is a click on the picture, through the same command the
            // caps are bound to.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var board = view.GetVisualDescendants().OfType<KeyboardView>().Single(picture => picture.IsEffectivelyVisible);
            var source = editor.SelectedLayer!.Keys[0];
            var target = editor.SelectedLayer.Keys[1];

            editor.SelectKeyCommand.Execute(source);
            editor.BeginRemapCommand.Execute(null);

            scenes.Capture.RaiseKeystroke(editor.SelectedLayer.Keys[2].Key.OriginalKey);

            editor.SelectKeyCommand.Execute(source);
            editor.CopyKeyCommand.Execute(null);

            var cap = board.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => ReferenceEquals(button.DataContext, target));

            cap.Command!.Execute(cap.CommandParameter);

            Assert.True(target.Key.IsModified);
            Assert.False(editor.IsCopyArmed);
        }

        [AvaloniaFact]
        public async Task TheLightingBoard_DrawsNeitherStateBadgesNorALegendRow()
        {
            // A colouring board is about colour: the badges report the layout, which that board
            // cannot edit — so they are off, and the counts that follow them are absent with them.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(LightingTabView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var board = Assert.Single(view.GetVisualDescendants().OfType<KeyboardView>());

            Assert.False(board.ShowsStateBadges);
            Assert.True(board.ShowsLighting);
            Assert.Empty(view.GetVisualDescendants().OfType<BoardLegendView>());
        }

        [AvaloniaFact]
        public async Task TheKeysBoard_KeepsItsStateBadges()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var board = view.GetVisualDescendants().OfType<KeyboardView>().Single(picture => picture.IsEffectivelyVisible);

            Assert.True(board.ShowsStateBadges);
            Assert.False(board.ShowsLighting);
        }

        /// <summary>Every brush the row's swatches paint with, whatever shape carries them.</summary>
        private static IReadOnlyList<IBrush> SwatchFills(Control view)
        {
            var fills = new List<IBrush>();

            foreach (var shape in view.GetVisualDescendants().OfType<Shape>())
            {
                if (shape.Fill is { } fill)
                {
                    fills.Add(fill);
                }
            }

            foreach (var border in view.GetVisualDescendants().OfType<Border>())
            {
                if (border.Background is { } background)
                {
                    fills.Add(background);
                }
            }

            return fills;
        }

        private static IReadOnlyList<string> TextsOf(Control row)
        {
            return row.GetVisualDescendants()
                .OfType<TextBlock>()
                .Select(block => block.Text ?? string.Empty)
                .ToArray();
        }

        private static IReadOnlyList<string> VisibleTextsOf(Control row)
        {
            return row.GetVisualDescendants()
                .OfType<TextBlock>()
                .Where(block => block.IsEffectivelyVisible)
                .Select(block => block.Text ?? string.Empty)
                .ToArray();
        }

        /// <summary>Asserts the row reads "<c>caption</c> <c>count</c>", in that order.</summary>
        private static void AssertCount(IReadOnlyList<string> texts, string caption, string count)
        {
            var index = texts.ToList().IndexOf(caption);

            Assert.True(index >= 0, $"The legend row carries no '{caption}' entry: {string.Join(" | ", texts)}.");
            Assert.True(index + 1 < texts.Count, $"'{caption}' is the last thing in the row and carries no count.");
            Assert.Equal(count, texts[index + 1]);
        }

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }
}
