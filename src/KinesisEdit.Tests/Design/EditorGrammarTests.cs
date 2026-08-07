using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Controls;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Input;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// The editor's keyboard grammar, driven through Avalonia's <b>real</b> input pipeline
    /// (docs/design/mockups.md <c>2b</c>): arrows across the physical board, ⌥n, ⌘F/⌘S/⌘W, the two
    /// gates, and each branch of Escape.
    /// <para>
    /// A headless suite rather than a view-model one because every interesting part of it is a
    /// property of the <em>window</em>: which control has focus, whether the tunnel route reaches
    /// the editor, and whether the cap the arrow landed on wears the ring. None of that exists
    /// without a real input pipeline and a real focus manager.
    /// </para>
    /// <para>
    /// Only the focus-ring case runs in both theme variants — that is the one assertion about
    /// paint. The rest is behaviour, identical under either variant, and a second run of it would
    /// buy nothing but time.
    /// </para>
    /// </summary>
    public class EditorGrammarTests
    {
        /// <summary>The "s" cap of the left half, the Core adjacency tests' own starting point.</summary>
        private const int CapsRowKey = 55;

        /// <summary>The right-most caps-row cap of the left half; a full 1U split gap from 59.</summary>
        private const int LastKeyOfTheLeftHalf = 58;

        /// <summary>The left-most caps-row cap of the right half.</summary>
        private const int FirstKeyOfTheRightHalf = 59;

        [AvaloniaTheory]
        [InlineData(PhysicalKey.ArrowRight, 56)]
        [InlineData(PhysicalKey.ArrowLeft, 54)]
        [InlineData(PhysicalKey.ArrowUp, 38)]
        [InlineData(PhysicalKey.ArrowDown, 70)]
        public async Task Arrows_MoveTheSelectionByPhysicalAdjacency(PhysicalKey arrow, int expectedIndex)
        {
            // "Move key selection across the physical grid, not tab order" (2b). The expected
            // landings are KeyAdjacencyTests' own, so a scoring change fails in both places.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Select(editor, CapsRowKey);

            Press(host, arrow);

            Assert.Equal(expectedIndex, editor.SelectedKey!.Index);
        }

        [AvaloniaFact]
        public async Task Arrows_CrossTheSplitHalvesInBothDirections()
        {
            // The Freestyle Edge RGB's two halves are one continuous coordinate space with a 1U
            // gap, so nothing special-cases the crossing — but it is the case a row/column
            // implementation would get wrong, which is why it is pinned at this level too.
            //
            // The picture is now two separate bordered panels with the design's gutter between
            // them, and this is the crossing that must not care: KeyAdjacency works on the
            // board-absolute Core coordinates, which no renderer re-bases.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Select(editor, LastKeyOfTheLeftHalf);

            Press(host, PhysicalKey.ArrowRight);

            Assert.Equal(FirstKeyOfTheRightHalf, editor.SelectedKey!.Index);

            Press(host, PhysicalKey.ArrowLeft);

            Assert.Equal(LastKeyOfTheLeftHalf, editor.SelectedKey!.Index);

            // And the two really are drawn in different panels, or the crossing would be a
            // crossing of nothing.
            Assert.NotEqual(PanelOf(view, LastKeyOfTheLeftHalf), PanelOf(view, FirstKeyOfTheRightHalf));
        }

        [AvaloniaFact]
        public async Task Arrowing_NeverStartsCapture()
        {
            // The whole reason the move lands through SelectKeyDirectly: SelectKeyCommand promotes
            // a second hit on the already-selected cap into listening, and an arrow that armed the
            // keyboard would eat the user's next keystroke.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Select(editor, CapsRowKey);

            foreach (var arrow in new[]
                     {
                         PhysicalKey.ArrowRight, PhysicalKey.ArrowLeft,
                         PhysicalKey.ArrowRight, PhysicalKey.ArrowLeft,
                         PhysicalKey.ArrowDown, PhysicalKey.ArrowUp
                     })
            {
                Press(host, arrow);

                Assert.False(editor.IsListening, $"{arrow} put a cap into listening state.");
                Assert.NotNull(editor.SelectedKey);
            }

            // Right then Left is a round trip along one row, and it is the sequence that would arm
            // the keyboard if the move went through SelectKeyCommand: the second press lands back
            // on the cap that is already selected.
            Select(editor, CapsRowKey);

            Press(host, PhysicalKey.ArrowRight);
            Press(host, PhysicalKey.ArrowLeft);

            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
            Assert.False(editor.IsListening);
        }

        [AvaloniaFact]
        public async Task AnArrow_WithNothingSelected_SelectsTheFirstCapOfTheLayer()
        {
            // The grammar needs an entry point that does not require a click first — the editor
            // takes focus on the way in, so the very first arrow has to land somewhere.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.Null(editor.SelectedKey);

            Press(host, PhysicalKey.ArrowRight);

            Assert.Same(editor.SelectedLayer!.Keys[0], editor.SelectedKey);
            Assert.False(editor.IsListening);
        }

        [AvaloniaTheory]
        [InlineData("Dark")]
        [InlineData("Light")]
        public async Task TheLandedCap_WearsTheFocusRingAndKeepsTheSelection(string variantName)
        {
            // 2b, verbatim: "Focus is always visible when it exists; mouse clicks suppress it,
            // arrow/Tab summon it. Selection and focus can coexist on the same key and must stay
            // distinguishable." The Directional navigation hint is what raises :focus-visible.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ToVariant(variantName));

            host.Capture();

            Select(editor, CapsRowKey);

            Press(host, PhysicalKey.ArrowRight);

            var focused = Assert.IsAssignableFrom<Control>(FocusedElement(view));
            var cap = Assert.Single(focused.GetSelfAndVisualAncestors().OfType<KeyCapView>());

            Assert.Same(editor.SelectedKey, cap.DataContext);
            Assert.Contains(":focus-visible", focused.Classes);
            Assert.Contains("selected", focused.Classes);
        }

        [AvaloniaFact]
        public async Task NoShortcutIsHandled_WhileAKeyIsListening()
        {
            // Gate 1. Capture owns the keyboard outright: a user assigning ⌘S to a key must get
            // s-with-Meta recorded, not a save (keyboard-editor.md, invariant 5).
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Select(editor, CapsRowKey);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);
            Assert.True(editor.IsCaptureActive);

            Press(host, PhysicalKey.ArrowRight);
            Press(host, PhysicalKey.Digit2, RawInputModifiers.Alt);
            Press(host, PhysicalKey.S, CommandModifier);
            Press(host, PhysicalKey.W, CommandModifier);
            Press(host, PhysicalKey.F, CommandModifier);

            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
            Assert.Equal(0, editor.SelectedLayer!.Index);
            Assert.Equal(0, chrome.HomeCallCount);
            Assert.Equal(0, scenes.Session!.SaveCallCount);
            Assert.Null(editor.ActiveOverlay);
            Assert.True(editor.IsListening);
        }

        [AvaloniaFact]
        public async Task NoShortcutIsHandled_WhileAMacroIsRecording()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Select(editor, CapsRowKey);

            // The app's one recording surface since issue #93: the key inspector's Macro panel.
            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            var panel = Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);

            panel.RecordCommand.Execute(null);

            Assert.True(panel.IsRecording);
            Assert.True(editor.IsCaptureActive);

            Press(host, PhysicalKey.ArrowRight);
            Press(host, PhysicalKey.W, CommandModifier);

            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
            Assert.Equal(0, chrome.HomeCallCount);
            Assert.True(panel.IsRecording);
        }

        [AvaloniaFact]
        public async Task NoShortcutIsHandled_WhileAPanelIsAwaitingAKeystroke()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Select(editor, CapsRowKey);

            editor.ShowOverlay(new ArmedSinkPanelViewModel());

            Assert.True(editor.IsCaptureActive);

            Press(host, PhysicalKey.ArrowRight);
            Press(host, PhysicalKey.W, CommandModifier);

            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
            Assert.Equal(0, chrome.HomeCallCount);
        }

        [AvaloniaFact]
        public async Task Arrows_AreLeftAlone_WhenFocusSitsInATextInput()
        {
            // Gate 2. The Macros tab's search field is a real TextBox — arrows there move the
            // caret, and the board must not steal them. (It used to be the old macro panel's
            // NumericUpDown; issue #93 replaced that panel with the macro library, whose search box
            // is the text input this tab now carries.)
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            editor.SelectTabCommand.Execute(editor.Tabs[1]);

            Select(editor, CapsRowKey);

            host.Capture();

            var field = view.GetVisualDescendants()
                .OfType<TextBox>()
                .First(box => box.Classes.Contains("searchField"));

            field.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.True(field.IsFocused, "The macro library's search field did not take focus.");

            Press(host, PhysicalKey.ArrowRight);
            Press(host, PhysicalKey.ArrowLeft);

            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
        }

        [AvaloniaFact]
        public async Task Arrows_AreLeftAlone_InTheLayerSwitcher()
        {
            // The switch is a one-of-N SelectingItemsControl and arrows are how it moves; stealing
            // them would break the segmented control outright.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            // The focus target is a segment, not the ListBox: that is where focus really sits when
            // a user tabs into the switch, and it is what makes the ancestry walk meaningful.
            var switcher = view.GetVisualDescendants().OfType<ListBox>().First();
            var segment = switcher.GetVisualDescendants().OfType<ListBoxItem>().First();

            segment.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.True(segment.IsFocused, "The layer switcher's first segment did not take focus.");

            Press(host, PhysicalKey.ArrowRight);

            Assert.Equal(1, editor.SelectedLayer!.Index);
        }

        [AvaloniaFact]
        public async Task Arrows_AreLeftAlone_InTheTabStrip()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            Select(editor, CapsRowKey);

            var strip = view.GetVisualDescendants().OfType<TabStrip>().Single();
            var tab = strip.GetVisualDescendants().OfType<TabStripItem>().First();

            tab.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.True(tab.IsFocused, "The tab strip's first tab did not take focus.");

            Press(host, PhysicalKey.ArrowRight);

            // The strip took the arrow: the section moved and the board's selection did not.
            Assert.Equal(EditorTab.Macros, editor.SelectedTab);
            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
        }

        [AvaloniaFact]
        public async Task AltAndADigit_SelectsThatLayerAndIsANoOpPastTheLayerCount()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.Equal(2, editor.Layers.Count);

            Press(host, PhysicalKey.Digit2, RawInputModifiers.Alt);

            Assert.Equal(1, editor.SelectedLayer!.Index);

            // A Freestyle Edge RGB has two layers; ⌥3 has nothing to open and changes nothing.
            Press(host, PhysicalKey.Digit3, RawInputModifiers.Alt);

            Assert.Equal(1, editor.SelectedLayer!.Index);

            Press(host, PhysicalKey.Digit1, RawInputModifiers.Alt);

            Assert.Equal(0, editor.SelectedLayer!.Index);
        }

        [AvaloniaFact]
        public async Task CommandS_Saves()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Press(host, PhysicalKey.S, CommandModifier);

            if (editor.SaveCommand.ExecutionTask is { } save)
            {
                await save;
            }

            Assert.Equal(1, scenes.Session!.SaveCallCount);
        }

        [AvaloniaFact]
        public async Task CommandW_ReturnsToTheDashboard()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Press(host, PhysicalKey.W, CommandModifier);

            Assert.Equal(1, chrome.HomeCallCount);
        }

        [AvaloniaFact]
        public async Task CommandW_WithNoShell_DoesNothingRatherThanThrow()
        {
            // Shell is null wherever the editor is hosted without one — every headless scene that
            // does not assign it, and the design-time preview.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            editor.Shell = null;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Press(host, PhysicalKey.W, CommandModifier);

            Assert.Null(editor.ActiveOverlay);
        }

        /// <summary>
        /// ⌘F has somewhere to write now. It used to open the §11.6 picker as a modal whose Ok
        /// merely closed — a token with nowhere to go, recorded as deviation 23 until issue #92.
        /// It puts the caret in the <b>key inspector's</b> field instead, where ↵ on a row assigns
        /// to the selected position; and it opens nothing at all, because the rail is not modal.
        /// </summary>
        [AvaloniaFact]
        public async Task CommandF_PutsTheCaretInTheKeyInspectorsSearchFieldAndOpensNoOverlay()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[0]);

            Press(host, PhysicalKey.F, CommandModifier);

            Assert.Null(editor.ActiveOverlay);
            Assert.False(editor.HasActiveOverlay);
            Assert.True(editor.Inspector.IsOpen);

            host.Capture();

            var rail = view.GetVisualDescendants().OfType<KeyInspectorView>().Single();
            var field = rail.GetVisualDescendants().OfType<TextBox>().Single();

            Assert.True(field.IsFocused, "⌘F did not focus the key inspector's search field.");
        }

        [AvaloniaFact]
        public async Task Escape_WithAPanelOpenThatIsNotAwaitingAKeystroke_ClosesIt()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            editor.ShowOverlay(new TextEntryPanelViewModel());

            Assert.NotNull(editor.ActiveOverlay);

            Press(host, PhysicalKey.Escape);

            Assert.Null(editor.ActiveOverlay);
        }

        [AvaloniaFact]
        public async Task Escape_WithAPanelAwaitingAKeystroke_FillsTheFieldAndLeavesThePanelOpen()
        {
            // The one carve-out of invariant 6's carve-out, driven in the order the app really
            // produces: the capture service previews the Escape on the window ABOVE this view, the
            // armed field takes it and disarms, and only then does the view's own handler run — by
            // which time IsOverlayAwaitingKeystroke is already false. One Escape used to both fill
            // the field and destroy the panel. The latch is what stops it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var overlay = new ArmedSinkPanelViewModel();

            editor.ShowOverlay(overlay);

            AttachCapturePreview(host, scenes.Capture, editor.SelectedLayer!.Keys[0].Key.OriginalKey);

            Press(host, PhysicalKey.Escape);

            Assert.Equal(1, overlay.ReceivedCount);
            Assert.False(overlay.WantsKeystrokes);
            Assert.Same(overlay, editor.ActiveOverlay);

            // The field disarmed as it took the key, so capture is off and the NEXT Escape reaches
            // the view with nothing having consumed it — which is when the panel closes.
            Press(host, PhysicalKey.Escape);

            Assert.Equal(1, overlay.ReceivedCount);
            Assert.Null(editor.ActiveOverlay);
        }

        [AvaloniaFact]
        public async Task AnOrdinaryKeystrokeTakenByAPanel_DoesNotStopTheNextEscapeClosingIt()
        {
            // The latch is about the keystroke being handled right now. A panel that took an `a`
            // and disarmed must still close on the Escape that follows, or the latch would have
            // traded one stuck state for another.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var overlay = new ArmedSinkPanelViewModel();

            editor.ShowOverlay(overlay);

            AttachCapturePreview(host, scenes.Capture, editor.SelectedLayer!.Keys[0].Key.OriginalKey);

            Press(host, PhysicalKey.A);

            Assert.Equal(1, overlay.ReceivedCount);
            Assert.Same(overlay, editor.ActiveOverlay);

            Press(host, PhysicalKey.Escape);

            Assert.Null(editor.ActiveOverlay);
        }

        /// <summary>
        /// Escape's fourth and last stage — <b>deliberately changed by issue #119</b>. It used to
        /// close the key inspector rail and leave the selection alone; the rail cannot be closed any
        /// more, because it is a permanent column of the Layout tab and dismissing it would collapse
        /// that column and shove the board sideways, which is the defect the issue exists to remove.
        /// So the stage clears the <em>selection</em> instead: the cap loses its ring and the rail
        /// falls to its empty state, which is the same "back out of what I am in" the grammar always
        /// meant. The three stages ahead of it — overlay, cancel remap, cancel copy — are untouched
        /// in order and in meaning.
        /// </summary>
        [AvaloniaFact]
        public async Task Escape_WithNothingNarrowerToCancel_ClearsTheKeySelection()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var key = editor.SelectedLayer!.Keys[0];

            editor.SelectKeyCommand.Execute(key);

            Assert.Same(key, editor.SelectedKey);
            Assert.True(editor.Inspector.HasSelection);

            Press(host, PhysicalKey.Escape);

            Assert.Null(editor.SelectedKey);
            Assert.False(key.IsSelected, "The cap kept its selection ring.");
            Assert.False(editor.Inspector.HasSelection);

            // The rail is still on screen — it fell to its empty state rather than collapsing.
            host.Capture();

            var rail = Assert.Single(view.GetVisualDescendants().OfType<KeyInspectorView>());

            Assert.True(rail.IsEffectivelyVisible);
            Assert.True(rail.Bounds.Width > 0);

            // ...and clicking the cap again selects it as it always did.
            editor.SelectKeyCommand.Execute(key);

            Assert.Same(key, editor.SelectedKey);
            Assert.True(editor.Inspector.HasSelection);
        }

        [AvaloniaFact]
        public async Task Escape_WithNothingSelectedAtAll_IsNotHandled()
        {
            // The last stage does nothing when there is nothing to deselect, so the key falls
            // through untouched rather than being swallowed by an editor the user has not clicked
            // into.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Assert.Null(editor.SelectedKey);

            Press(host, PhysicalKey.Escape);

            Assert.Null(editor.SelectedKey);
            Assert.False(editor.Inspector.HasSelection);
        }

        [AvaloniaFact]
        public async Task Escape_WithAKeyListeningAndAKeySelected_CancelsTheListenFirst()
        {
            // The stated order, not a lucky one: capture is narrower than the selection, so the
            // first Escape leaves listening and the cap is still selected for the second.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var key = editor.SelectedLayer!.Keys[0];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);
            Assert.Same(key, editor.SelectedKey);

            Press(host, PhysicalKey.Escape);

            Assert.False(editor.IsListening);
            Assert.Same(key, editor.SelectedKey);

            Press(host, PhysicalKey.Escape);

            Assert.Null(editor.SelectedKey);
        }

        [AvaloniaTheory]
        [InlineData(PhysicalKey.S)]
        [InlineData(PhysicalKey.W)]
        public async Task NoShortcutIsHandled_WhileAPanelIsOpenOverTheEditor(PhysicalKey key)
        {
            // Gate 2. An open modal owns the keyboard: the scrim covers the editor, so nothing
            // behind it may be reached from the keyboard either. ⌘S is the one that mattered — the
            // panel is a sibling of the content grid rather than inside the !IsBusy region, so a
            // save started from under it would serialize the model on a background thread while
            // the panel above is still writing to it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            // A text-entry panel: nothing is armed, so gate 1 is wide open — IsCaptureActive is
            // false, which is exactly why this needed a gate of its own.
            var overlay = new TextEntryPanelViewModel();

            editor.ShowOverlay(overlay);

            Assert.False(editor.IsCaptureActive);
            Assert.True(editor.HasActiveOverlay);

            Press(host, key, CommandModifier);

            if (editor.SaveCommand.ExecutionTask is { } save)
            {
                await save;
            }

            Assert.Equal(0, scenes.Session!.SaveCallCount);
            Assert.Equal(0, chrome.HomeCallCount);
            Assert.Same(overlay, editor.ActiveOverlay);
        }

        [AvaloniaFact]
        public async Task AltAndADigit_StillWorksAfterALayerSegmentWasClicked()
        {
            // The layer switcher is a ListBox and the tab bar a TabStrip, so gating ⌥n on the same
            // ancestry test the arrows use killed the shortcut the moment the user touched either
            // with the mouse — including the very control the ⌥n legend is printed on. No
            // SelectingItemsControl consumes Alt+digit, so only the arrows are gated on it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var switcher = view.GetVisualDescendants().OfType<ListBox>().First();
            var segment = switcher.GetVisualDescendants().OfType<ListBoxItem>().First();

            segment.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.True(segment.IsFocused, "The layer switcher's first segment did not take focus.");

            Press(host, PhysicalKey.Digit2, RawInputModifiers.Alt);

            Assert.Equal(1, editor.SelectedLayer!.Index);

            // And from the tab strip, the other SelectingItemsControl of the bar.
            var tab = view.GetVisualDescendants().OfType<TabStrip>().Single()
                .GetVisualDescendants().OfType<TabStripItem>().First();

            tab.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.True(tab.IsFocused, "The tab strip's first tab did not take focus.");

            Press(host, PhysicalKey.Digit1, RawInputModifiers.Alt);

            Assert.Equal(0, editor.SelectedLayer!.Index);
        }

        [AvaloniaFact]
        public async Task AltAndADigit_IsStillLeftAloneInATextInput()
        {
            // The one clause the layer shortcuts keep: ⌥1 types `¡` on macOS, so a focused field
            // wins.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            editor.SelectTabCommand.Execute(editor.Tabs[1]);

            host.Capture();

            var field = view.GetVisualDescendants()
                .OfType<TextBox>()
                .First(box => box.Classes.Contains("searchField"));

            field.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.True(field.IsFocused, "The macro library's search field did not take focus.");

            Press(host, PhysicalKey.Digit2, RawInputModifiers.Alt);

            Assert.Equal(0, editor.SelectedLayer!.Index);
        }

        [AvaloniaFact]
        public async Task Escape_WithAKeyListeningAndNoPanel_IsNeverAShortcutAndOnlyCancelsAsASafetyNet()
        {
            // Escape is a remappable key (invariant 6). In the app the capture service on the
            // window above has already consumed it and made it the assignment, so
            // CancelRemapCommand is unavailable by the time this handler runs and nothing
            // double-fires — KeyboardEditorViewModelRoutingTests pins that half. These scenes use
            // a fake capture service, i.e. exactly the case where nothing consumed the key, and
            // there the handler is the documented safety net.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;
            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            Select(editor, CapsRowKey);

            editor.BeginRemapCommand.Execute(null);

            Press(host, PhysicalKey.Escape);

            Assert.False(editor.IsListening);
            Assert.Equal(CapsRowKey, editor.SelectedKey!.Index);
            Assert.Equal(0, chrome.HomeCallCount);
            Assert.Null(editor.ActiveOverlay);
        }

        /// <summary>Which raw modifier ⌘ is on the machine this suite is running on.</summary>
        private static RawInputModifiers CommandModifier =>
            KeyCaption.IsMacOs ? RawInputModifiers.Meta : RawInputModifiers.Control;

        private static ThemeVariant ToVariant(string name)
        {
            return name == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        }

        /// <summary>
        /// Moves the board's selection without going near the grammar under test. It clears the
        /// selection first, because <c>SelectKeyCommand</c> on the cap that is already selected is
        /// the click contract's promotion into listening.
        /// </summary>
        private static void Select(KeyboardEditorViewModel editor, int keyIndex)
        {
            var key = editor.SelectedLayer!.FindByIndex(keyIndex)
                ?? throw new InvalidOperationException($"The board has no cap {keyIndex}.");

            editor.SelectKeyCommand.Execute(null);
            editor.SelectKeyCommand.Execute(key);

            Assert.Equal(keyIndex, editor.SelectedKey!.Index);
            Assert.False(editor.IsListening);
        }

        /// <summary>
        /// Installs the capture service's own half of a keystroke on the host window:
        /// <see cref="AvaloniaKeystrokeCaptureService"/> previews key events on the
        /// <see cref="TopLevel"/> in the <b>tunnel</b> phase — above the editor view, so the view's
        /// handler always runs second — pushes the resolved keystroke into the editor and marks the
        /// event handled. <see cref="FakeKeystrokeCaptureService"/> attaches nothing itself, so a
        /// test that needs that ordering puts it back here.
        /// <para>
        /// It only fires while the service is actually capturing, exactly like the real one: the
        /// overlay host stops capture the moment the sink stops wanting keystrokes.
        /// </para>
        /// </summary>
        private static void AttachCapturePreview(
            ThemedHost host,
            FakeKeystrokeCaptureService capture,
            KeyDefinition assignment)
        {
            host.Window.AddHandler(
                InputElement.KeyDownEvent,
                (object? _, KeyEventArgs e) =>
                {
                    if (!capture.IsCapturing)
                    {
                        return;
                    }

                    capture.RaiseKeystroke(assignment);

                    e.Handled = true;
                },
                RoutingStrategies.Tunnel,
                handledEventsToo: true);
        }

        /// <summary>A real key press on the host window, through Avalonia's own input pipeline.</summary>
        private static void Press(
            ThemedHost host,
            PhysicalKey key,
            RawInputModifiers modifiers = RawInputModifiers.None)
        {
            host.Window.KeyPressQwerty(key, modifiers);

            Dispatcher.UIThread.RunJobs();
        }

        private static object? FocusedElement(Control view)
        {
            return TopLevel.GetTopLevel(view)?.FocusManager?.GetFocusedElement();
        }

        /// <summary>
        /// The board panel the cap of <paramref name="keyIndex"/> is drawn in. A split board is two
        /// of them, so this is how a test says "the arrow left the panel it started in".
        /// </summary>
        private static KeyboardPanel PanelOf(Control view, int keyIndex)
        {
            var cap = view.GetVisualDescendants()
                .OfType<KeyCapView>()
                .First(candidate => candidate.DataContext is KeyboardKeyViewModel key && key.Index == keyIndex);

            return cap.GetVisualAncestors().OfType<KeyboardPanel>().First();
        }

        /// <summary>
        /// A feature panel with text entry — Delays, Search Keys, Export — and no sink.
        /// <para>
        /// The <c>ViewModel</c> suffix is load-bearing: these panels are really rendered here, and
        /// <see cref="ViewLocator"/> turns a name's <c>ViewModel</c> into <c>View</c>. A stand-in
        /// without the suffix resolves to <em>itself</em> and the locator then tries to cast a view
        /// model to a <c>Control</c>.
        /// </para>
        /// </summary>
        private sealed class TextEntryPanelViewModel : EditorOverlayViewModel
        {
            public TextEntryPanelViewModel() : base("Macro Timing Delays")
            {
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }

        /// <summary>
        /// A Tap and Hold-shaped panel whose field is armed for the next keystroke — and which
        /// <b>disarms as it takes one</b>, exactly as <see cref="TapAndHoldPanelViewModel"/>
        /// does. That is the whole reason the Escape defect existed: an empty
        /// <c>ReceiveKeystroke</c> leaves the panel looking armed, and the test never reaches the
        /// state the app is really in.
        /// </summary>
        private sealed class ArmedSinkPanelViewModel : EditorOverlayViewModel, IKeystrokeSink
        {
            public bool WantsKeystrokes
            {
                get => _wantsKeystrokes;
                set => SetProperty(ref _wantsKeystrokes, value);
            }

            /// <summary>How many keystrokes the armed field actually took.</summary>
            public int ReceivedCount
            {
                get => _receivedCount;
            }

            private bool _wantsKeystrokes = true;
            private int _receivedCount;

            public ArmedSinkPanelViewModel() : base("Tap and Hold")
            {
            }

            public void ReceiveKeystroke(CapturedKeystroke keystroke)
            {
                if (!WantsKeystrokes)
                {
                    return;
                }

                _receivedCount++;

                WantsKeystrokes = false;
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }
    }
}
