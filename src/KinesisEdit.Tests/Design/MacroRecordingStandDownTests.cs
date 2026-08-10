using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.Tests.ViewModels;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Tests.Design
{
    /// <summary>
    /// When a macro recording ends, driven through Avalonia's <b>real</b> input pipeline (issue
    /// #122, AC 2–4). Recording is the one editor state that silently claims every keystroke in the
    /// window, so "what stands it down" is a safety property, not a convenience: a user who has
    /// moved on must never be left typing into a panel they are no longer looking at.
    /// <para>
    /// A headless suite rather than a view-model one because every trigger is a property of the
    /// <em>window</em> — a tunnelled pointer press, focus landing in a text box, the window losing
    /// focus, and the tunnel ordering that puts the capture service above the editor view. None of
    /// that exists without a real input pipeline and a real focus manager.
    /// </para>
    /// </summary>
    public class MacroRecordingStandDownTests
    {
        [AvaloniaFact]
        public async Task Escape_WhileRecording_IsRecordedAsAStepAndDoesNotEndTheRecording()
        {
            // The reported defect. HandleEscape's last stage clears the selection, which refreshes
            // the rail with a new (null) key, which stops the recording — so one Escape was both
            // appended as a step AND the end of the recording that took it. Escape is a remappable
            // position like any other (invariant 6): a macro has to be able to record one.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var panel = ArmRecording(editor);

            AttachCapturePreview(host, scenes.Capture, KeyRegistry.FindByToken("esc", TokenDialect.Gen1)!);

            Press(host, PhysicalKey.Escape);

            Assert.Equal(1, panel.Steps.Count);
            Assert.Equal("esc", panel.Steps.Items[0].TokenText);
            Assert.True(panel.IsRecording, "Escape ended the recording it was supposed to be a step of.");
            Assert.NotNull(editor.SelectedKey);
        }

        [AvaloniaFact]
        public async Task Escape_WhileRecording_KeepsRecordingForEveryFurtherEscape()
        {
            // Not a one-off exemption: Escape is an ordinary recordable key now, so the second one
            // is a second step.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var panel = ArmRecording(editor);

            AttachCapturePreview(host, scenes.Capture, KeyRegistry.FindByToken("esc", TokenDialect.Gen1)!);

            Press(host, PhysicalKey.Escape);
            Press(host, PhysicalKey.Escape);

            Assert.Equal(2, panel.Steps.Count);
            Assert.True(panel.IsRecording);
        }

        [AvaloniaFact]
        public async Task Escape_WithNothingRecording_StillClearsTheSelection()
        {
            // The stage the latch now guards is otherwise untouched: with nothing having consumed
            // the keystroke, Escape backs out of the selection exactly as issue #119 left it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[0]);

            Assert.NotNull(editor.SelectedKey);

            Press(host, PhysicalKey.Escape);

            Assert.Null(editor.SelectedKey);
        }

        [AvaloniaFact]
        public async Task APointerPressOnUnrelatedChrome_StandsTheRecordingDown()
        {
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            host.Capture();

            var panel = ArmRecording(editor);
            var selected = editor.SelectedKey;

            host.Capture();

            ClickCentreOf(host, view.GetVisualDescendants().OfType<BoardLegendView>().First());

            Assert.False(panel.IsRecording);

            // …and it really was the pointer seam, not the key-change path in disguise.
            Assert.Same(selected, editor.SelectedKey);
        }

        [AvaloniaFact]
        public async Task APointerPressOnTheRecordButton_TogglesInsteadOfFightingItself()
        {
            // The trap: the stand-down runs on the TUNNEL, before the button has done anything. A
            // seam that did not exempt the record control would stop the recording on the very
            // click meant to start it, and would make Stop take two clicks.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var panel = ShowMacroPanel(editor);

            host.Capture();

            var record = view.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => ReferenceEquals(button.Command, panel.RecordCommand));

            ClickCentreOf(host, record);

            Assert.True(panel.IsRecording, "The click meant to start the recording stopped it instead.");

            ClickCentreOf(host, record);

            Assert.False(panel.IsRecording, "Stop needed a second click.");
        }

        [AvaloniaFact]
        public async Task TheWindowLosingFocus_StandsTheRecordingDown()
        {
            // An OS-reserved chord (⌘Tab, ⌘Q, ⌘Space, ⌘H) never reaches the app at all and can be
            // neither captured nor swallowed — capture is focused-window only, by design. What the
            // app can do is not come back still armed, and this is what buys that.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var panel = ArmRecording(editor);

            Deactivate(host.Window);

            Assert.False(panel.IsRecording);
        }

        [AvaloniaFact]
        public async Task FocusEnteringTheComposersMillisecondField_StandsTheRecordingDown()
        {
            // AC 4. The capture service auto-suspends while a TextBox has focus — silently, which
            // is the wrong answer here: the banner would still say "recording" while the digits
            // went into the box. The leak is turned into an explicit stand-down. Reachable in this
            // panel through §11.3's millisecond field, and by Tab as well as by click, which is why
            // it is a focus handler rather than only a pointer one.
            //
            // Issue #139 moved that field: the per-row delay editor is gone and the count is typed
            // in the composer's own THEN WAIT row, which is drawn whether or not a step is selected
            // and is live once one is. The rule the test guards did not move with it.
            using var scenes = new ViewSceneFactory();

            var view = await scenes.CreateAsync(typeof(KeyboardEditorView).FullName!);
            var editor = (KeyboardEditorViewModel)view.DataContext!;

            using var host = ThemedHost.Show(view, ThemeVariant.Dark);

            var panel = ArmRecording(editor);

            scenes.Capture.RaiseKeystroke(KeyRegistry.FindByToken("a", TokenDialect.Gen1)!);

            // The composer edits the SELECTED step, and its delay row is dead without one — a
            // disabled field cannot take focus, so the state this test is about would never arise.
            panel.Steps.SelectStepCommand.Execute(panel.Steps.Items[0]);

            Assert.True(panel.Steps.HasSelection);
            Assert.True(panel.IsStepDelayEnabled);
            Assert.True(panel.IsRecording, "Pointing the composer at a step must not end the take.");

            host.Capture();

            // Named by its class rather than by being the panel's only one: since issue #141 the
            // name field is a second real TextBox here, and it stands the recording down for the
            // same reason — this case is about the millisecond count specifically.
            var field = view.GetVisualDescendants()
                .OfType<MacroInspectorPanelView>()
                .Single()
                .GetVisualDescendants()
                .OfType<TextBox>()
                .Single(box => box.IsEffectivelyVisible && box.Classes.Contains("monoValue"));

            Assert.Contains("monoValue", field.Classes);

            field.Focus();

            Dispatcher.UIThread.RunJobs();

            Assert.False(panel.IsRecording);
        }

        [AvaloniaFact]
        public async Task TheStandDown_IsSilentWhenNothingWasRecording()
        {
            // Idempotence, and the reason for the guard: without it every pointer press in the app
            // would raise RecordingChanged and put the capture service's start/stop bookkeeping
            // through a cycle.
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var announcements = 0;

            editor.Inspector.RecordingChanged += (_, _) => announcements++;

            editor.StopRecordingOnInteraction();
            editor.StopRecordingOnInteraction();

            Assert.Equal(0, announcements);
            Assert.Equal(0, scenes.Capture.StopCount);
        }

        [AvaloniaFact]
        public async Task TheStandDown_HandsTheCaptureServiceBack()
        {
            using var scenes = new ViewSceneFactory();

            var editor = await scenes.CreateEditorAsync();
            var panel = ArmRecording(editor);

            Assert.True(scenes.Capture.IsCapturing);

            editor.StopRecordingOnInteraction();

            Assert.False(panel.IsRecording);
            Assert.False(scenes.Capture.IsCapturing);

            // Twice is once: nothing moves, and nothing is announced a second time.
            editor.StopRecordingOnInteraction();

            Assert.False(scenes.Capture.IsCapturing);
        }

        /// <summary>Puts the open rail on its Macro panel for a position that can carry one.</summary>
        private static MacroInspectorPanelViewModel ShowMacroPanel(KeyboardEditorViewModel editor)
        {
            var layer = editor.SelectedLayer
                ?? throw new InvalidOperationException("The editor scene rendered no layer.");

            editor.SelectKeyCommand.Execute(layer.FindByIndex(TestLayouts.RgbDigitOneKeyIndex));

            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            return editor.Inspector.ActivePanel as MacroInspectorPanelViewModel
                   ?? throw new InvalidOperationException("The key inspector hosts no Macro panel.");
        }

        /// <summary>…and arms it, which is what really starts the capture service.</summary>
        private static MacroInspectorPanelViewModel ArmRecording(KeyboardEditorViewModel editor)
        {
            var panel = ShowMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            if (!panel.IsRecording)
            {
                throw new InvalidOperationException("The Macro panel refused to record.");
            }

            return panel;
        }

        /// <summary>
        /// Installs the capture service's own half of a keystroke on the host window, exactly as
        /// <c>EditorGrammarTests</c> does: the real adapter previews key events on the
        /// <see cref="TopLevel"/> in the <b>tunnel</b> phase — above the editor view, so the view's
        /// handler always runs second — pushes the resolved keystroke into the editor and marks the
        /// event handled. <see cref="FakeKeystrokeCaptureService"/> attaches nothing itself, so a
        /// test that needs that ordering puts it back here.
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
        private static void Press(ThemedHost host, PhysicalKey key)
        {
            host.Window.KeyPressQwerty(key, RawInputModifiers.None);

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>A real click in the middle of <paramref name="target"/>.</summary>
        private static void ClickCentreOf(ThemedHost host, Visual target)
        {
            var centre = target.TranslatePoint(
                new Point(target.Bounds.Width / 2, target.Bounds.Height / 2),
                host.Window) ?? throw new InvalidOperationException("The target is not in the window's visual tree.");

            host.Window.MouseDown(centre, MouseButton.Left);
            host.Window.MouseUp(centre, MouseButton.Left);

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>
        /// Drives the window's own deactivation. The headless platform never raises it — there is
        /// no window manager to take focus away — and <c>Deactivated</c> is a public event only the
        /// toolkit may raise, so the test calls the very hook the real backend calls. If a future
        /// Avalonia renames it this throws rather than passing quietly, which is the whole point of
        /// looking it up by name instead of faking the event.
        /// </summary>
        private static void Deactivate(Window window)
        {
            for (var type = window.GetType(); type is not null; type = type.BaseType)
            {
                var hook = type.GetMethod(
                    "HandleDeactivated",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);

                if (hook is null)
                {
                    continue;
                }

                hook.Invoke(window, null);

                Dispatcher.UIThread.RunJobs();

                return;
            }

            throw new InvalidOperationException("Avalonia no longer declares WindowBase.HandleDeactivated().");
        }
    }
}
