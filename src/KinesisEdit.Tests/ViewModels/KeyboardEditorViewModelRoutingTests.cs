using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Input;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The keystroke routing of specs/10-apps-and-ui.md — "a captured key is forwarded to the Tap
    /// and Hold dialog if that dialog is open; otherwise it is applied as a remap, or appended to
    /// the active macro" — plus the overlay host around it: capture suspended while a text-entry
    /// panel is open.
    /// <para>
    /// Since issue #93 the <b>only</b> surface that records a macro is the key inspector's Macro
    /// panel; the Macros tab is a library and takes no keystroke at all. Every "a macro is
    /// recording" case below therefore arms the rail.
    /// </para>
    /// </summary>
    public sealed class KeyboardEditorViewModelRoutingTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeSettingsService _settings = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeFolderPickerService _folderPicker = new();
        private readonly FakeFilePickerService _filePicker = new();
        private readonly FakeVDriveFileService _files = new();
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly List<KeyboardEditorViewModel> _editors = [];

        [Fact]
        public async Task Tabs_TheMacrosTab_IsTheSecondOneAndIsReachable()
        {
            // The precondition of every routing test below: the panel these tests drive is behind
            // an open tab. Which sections the strip carries for which device is
            // EditorTabViewModelTests' and KeyboardEditorViewModelTests' subject, not this file's.
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(EditorTab.Keys, editor.Tabs[0].Tab);
            Assert.Equal(EditorTab.Macros, editor.Tabs[1].Tab);
            Assert.True(editor.SelectTabCommand.CanExecute(editor.Tabs[1]));
        }

        [Fact]
        public async Task SelectTabCommand_ForTheMacrosTab_ShowsTheLibraryAndTheLayoutTabHidesItAgain()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.NotNull(editor.MacroLibraryPanel);
            Assert.False(editor.IsMacroLibraryVisible);

            editor.SelectTabCommand.Execute(editor.Tabs[1]);

            Assert.Equal(EditorTab.Macros, editor.SelectedTab);
            Assert.True(editor.IsMacroLibraryVisible);

            editor.SelectTabCommand.Execute(editor.Tabs[0]);

            Assert.False(editor.IsMacroLibraryVisible);
        }

        [Fact]
        public async Task MacroLibrary_SlotBranch_FollowsTheBoardsSelection()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectTabCommand.Execute(editor.Tabs[1]);
            editor.SelectKeyCommand.Execute(key);

            Assert.NotEmpty(editor.MacroLibraryPanel.Slots);

            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            // A layer switch drops the selection, and the tab's slot branch follows it.
            Assert.Empty(editor.MacroLibraryPanel.Slots);
            Assert.Equal(MacroLibraryViewModel.NoKeySubtitle, editor.MacroLibraryPanel.Subtitle);
        }

        [Fact]
        public async Task RecordCommand_StartsCaptureAndStoppingItStopsCapture()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            Assert.Equal(1, _capture.StartCount);
            Assert.True(_capture.IsCapturing);

            panel.RecordCommand.Execute(null);

            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public async Task KeystrokeCaptured_WhileTheRailsMacroPanelRecords_AppendsAStepAndRemapsNothing()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);
            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Equal("[z]", Assert.Single(panel.Steps.Items).TokenText);
            Assert.False(key.IsModified);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task KeystrokeCaptured_WithNothingElseWanting_StillRemaps()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            editor.BeginRemapCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.True(key.IsModified);
            Assert.Equal("Remap (1)", editor.RemapCounterCaption);
            Assert.Equal(0, editor.MacroCount);
        }

        [Fact]
        public async Task KeystrokeCaptured_WithAnOpenSinkOverlay_GoesToTheOverlayBeforeTheRail()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            var overlay = new SinkOverlay();

            editor.ShowOverlay(overlay);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            // Priority 1: an overlay that wants keystrokes takes them from everything else.
            Assert.Equal(TestLayouts.Gen1Key("z"), Assert.Single(overlay.Received).Key);
            Assert.Empty(panel.Steps.Items);
        }

        /// <summary>
        /// specs/10-apps-and-ui.md routes a captured key to the Tap and Hold dialog "if that
        /// dialog is open" — not "if one of its fields is armed". An unarmed panel therefore still
        /// takes the keystroke and drops it, so nothing underneath a modal panel can quietly eat
        /// keys aimed at it.
        /// </summary>
        [Fact]
        public async Task KeystrokeCaptured_WithASinkOverlayThatWantsNothing_StillGoesToTheOverlay()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var overlay = new SinkOverlay { WantsKeystrokes = false };

            editor.ShowOverlay(overlay);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Single(overlay.Received);
            Assert.Equal(0, editor.MacroCount);
        }

        /// <summary>
        /// specs/11-feature-dialogs.md's panels are modal over the editor, and specs/10's routing
        /// gives an open Tap and Hold dialog the keystrokes. A macro recording underneath owns the
        /// capture service, so opening any panel ends it — otherwise every key aimed at the panel,
        /// Escape included, is appended to the macro instead and the panel cannot be dismissed
        /// from the keyboard at all.
        /// </summary>
        [Fact]
        public async Task ShowOverlay_WhileTheRailsMacroPanelRecords_StopsTheRecordingAndReleasesCapture()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            Assert.True(panel.IsRecording);
            Assert.True(_capture.IsCapturing);

            var overlay = new SinkOverlay { WantsKeystrokes = false };

            editor.ShowOverlay(overlay);

            Assert.False(panel.IsRecording);
            Assert.False(_capture.IsCapturing);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Empty(panel.Steps.Items);
        }

        /// <summary>
        /// Invariant 6, at the level it is actually decided: <b>Escape is a remappable key, not a
        /// shortcut</b>. The capture service swallows every physical key it resolves while a key
        /// listens, Escape included — a keyboard must be able to carry an Escape remap — so the
        /// keystroke arrives here like any other and becomes the assignment. By the time the
        /// view's own Escape handler runs, listening is over and <c>CancelRemapCommand</c> can no
        /// longer execute, which is what stops the two from double-firing.
        /// </summary>
        [Fact]
        public async Task KeystrokeCaptured_WithEscapeWhileAKeyIsListening_IsAssignedRatherThanTreatedAsCancel()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.CancelRemapCommand.CanExecute(null));

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("esc"));

            Assert.True(key.IsModified);
            Assert.Equal(TestLayouts.Gen1Key("esc"), key.Key.ModifiedOrOriginalKey);
            Assert.Equal("Remap (1)", editor.RemapCounterCaption);
            Assert.False(editor.IsListening);
            Assert.False(editor.CancelRemapCommand.CanExecute(null));
        }

        /// <summary>
        /// The key inspector's branch, which sits <b>ahead of everything else</b>: an armed record
        /// button in the rail is the most specific claim on the next keystroke there is.
        /// </summary>
        [Fact]
        public async Task KeystrokeCaptured_WithAnArmedInspectorPanel_GoesToTheRailFirst()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            // The board is listening too: the rail's armed field is the more specific claim and
            // takes the keystroke ahead of it.
            editor.BeginRemapCommand.Execute(null);

            var remap = Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            remap.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Equal(TestLayouts.Gen1Key("z"), key.Key.ModifiedOrOriginalKey);
            Assert.Equal(0, editor.MacroCount);
        }

        /// <summary>
        /// <b>The one real difference from the modal this replaced.</b> A feature panel took a
        /// keystroke on being merely <em>open</em>, because the scrim under it meant nothing else
        /// could want one. The rail is not modal, so a panel that is only showing must let the key
        /// fall through — otherwise it eats the remap being recorded on the cap beside it.
        /// </summary>
        [Fact]
        public async Task KeystrokeCaptured_WithAnOpenButUnarmedInspectorPanel_FallsThroughToTheBoard()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            Assert.True(editor.Inspector.IsOpen);
            Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            editor.BeginRemapCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.True(key.IsModified);
            Assert.Equal(TestLayouts.Gen1Key("z"), key.Key.ModifiedOrOriginalKey);
            Assert.False(editor.IsListening);
        }

        /// <summary>
        /// Capture belongs to the editor, never to a panel — and the rail announces its recording
        /// state on every mode switch and every selection change, not only when something really
        /// armed. Hence "never stop a capture you did not start": the macro underneath keeps the
        /// service it owns.
        /// </summary>
        [Fact]
        public async Task TheInspectorsRecording_StartsAndStopsCaptureWithoutStoppingSomebodyElses()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var remap = Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            Assert.False(_capture.IsCapturing);

            remap.RecordCommand.Execute(null);

            Assert.True(_capture.IsCapturing);
            Assert.True(editor.IsCaptureActive);

            remap.RecordCommand.Execute(null);

            Assert.False(_capture.IsCapturing);
            Assert.False(editor.IsCaptureActive);

            // Somebody else's capture — a listening key on the board. Switching the rail's mode
            // announces its recording state again, unchanged and false, and answering that with an
            // unconditional Stop would silently deafen the key that owns the service.
            editor.BeginRemapCommand.Execute(null);

            Assert.True(_capture.IsCapturing);

            SelectMode(editor, KeyInspectorMode.TapAndHold);

            Assert.True(_capture.IsCapturing);
            Assert.True(editor.IsListening);
        }

        /// <summary>
        /// The shell disposes the open editor whenever it is replaced — Home, or another device —
        /// so this is the navigation path too. The capture service is app-wide: a rail left armed
        /// would go on swallowing the dashboard's keystrokes behind the editor that is gone.
        /// </summary>
        [Fact]
        public async Task Dispose_WithAnArmedInspectorPanel_LeavesNoCaptureRunning()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var remap = Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            remap.RecordCommand.Execute(null);

            Assert.True(_capture.IsCapturing);

            editor.Dispose();

            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.HasSubscribers);
            Assert.False(editor.Inspector.IsOpen);
            Assert.False(remap.IsRecording);
        }

        /// <summary>
        /// Gate 1 of the editor's keyboard grammar, from the side that decides it: the consumers
        /// of "one keystroke, one target" are exactly the states in which no shortcut may be
        /// handled at all.
        /// </summary>
        [Fact]
        public async Task IsCaptureActive_IsTrueForEachKeystrokeConsumer()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsCaptureActive);

            SelectDigitOne(editor);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsCaptureActive);

            editor.CancelRemapCommand.Execute(null);

            Assert.False(editor.IsCaptureActive);

            var macro = OpenMacroPanel(editor);

            macro.RecordCommand.Execute(null);

            Assert.True(editor.IsCaptureActive);

            macro.RecordCommand.Execute(null);

            Assert.False(editor.IsCaptureActive);

            editor.ShowOverlay(new SinkOverlay());

            Assert.True(editor.IsCaptureActive);

            // An open panel that is *not* waiting for a keypress is not a keystroke consumer: it
            // suspended capture, so ⌘S there is a save and not a swallowed 's'.
            editor.ShowOverlay(new TextEntryOverlay());

            Assert.False(editor.IsCaptureActive);

            // ...and the fourth consumer, which no gate but this one covers: the rail is not modal,
            // so an armed record button in it is invisible to the view's "an open panel owns the
            // keyboard" gate. Without this term ⌘S fires while a hold action is being recorded.
            editor.CloseOverlayCommand.Execute(null);

            SelectMode(editor, KeyInspectorMode.Remap);

            var remap = Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            remap.RecordCommand.Execute(null);

            Assert.True(editor.IsCaptureActive);

            remap.RecordCommand.Execute(null);

            Assert.False(editor.IsCaptureActive);
        }

        /// <summary>
        /// The Escape route out of a panel reads this rather than <c>KeyEventArgs.Handled</c>:
        /// capture may be running for something else, and only a panel that is itself waiting for
        /// a keystroke owns Escape (docs/app/keyboard-editor.md, "Escape is a remappable key").
        /// </summary>
        [Fact]
        public async Task IsOverlayAwaitingKeystroke_FollowsTheOpenSinksArmedField()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsOverlayAwaitingKeystroke);

            editor.ShowOverlay(new SinkOverlay { WantsKeystrokes = false });

            Assert.True(editor.HasActiveOverlay);
            Assert.False(editor.IsOverlayAwaitingKeystroke);

            editor.ShowOverlay(new SinkOverlay());

            Assert.True(editor.IsOverlayAwaitingKeystroke);

            editor.ShowOverlay(new TextEntryOverlay());

            Assert.False(editor.IsOverlayAwaitingKeystroke);
        }

        [Fact]
        public async Task ShowOverlay_WithATextEntryPanel_SuspendsCaptureUntilItCloses()
        {
            var editor = await CreateLoadedEditorAsync();
            var overlay = new TextEntryOverlay();

            editor.ShowOverlay(overlay);

            // specs/10-apps-and-ui.md: capture is suspended whenever a dialog that needs real
            // typing is open. Delays, Search Keys and Export are not keystroke sinks.
            Assert.Same(overlay, editor.ActiveOverlay);
            Assert.True(editor.HasActiveOverlay);
            Assert.Equal(1, _capture.SuspendCount);
            Assert.True(_capture.IsSuspended);

            overlay.Accept();

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(1, _capture.ResumeCount);
            Assert.False(_capture.IsSuspended);
        }

        [Fact]
        public async Task ShowOverlay_WithAKeystrokeSinkPanel_LeavesCaptureAlone()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.ShowOverlay(new SinkOverlay());

            // Tap and Hold wants the keystrokes, so suspending would deafen it.
            Assert.Equal(0, _capture.SuspendCount);
            Assert.False(_capture.IsSuspended);
        }

        [Fact]
        public async Task ShowOverlay_WhileAKeyIsListening_CancelsTheListen()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            editor.ShowOverlay(new TextEntryOverlay());

            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
            Assert.False(editor.BeginRemapCommand.CanExecute(null));
        }

        [Fact]
        public async Task CloseOverlayCommand_DismissesThePanelAndResumesCapture()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.CloseOverlayCommand.CanExecute(null));

            editor.ShowOverlay(new TextEntryOverlay());

            Assert.True(editor.CloseOverlayCommand.CanExecute(null));

            editor.CloseOverlayCommand.Execute(null);

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(1, _capture.ResumeCount);
            Assert.False(editor.CloseOverlayCommand.CanExecute(null));
        }

        [Fact]
        public async Task ShowOverlay_WhileAnotherIsOpen_ReplacesItAndBalancesTheSuspension()
        {
            var editor = await CreateLoadedEditorAsync();
            var first = new TextEntryOverlay();
            var second = new TextEntryOverlay();

            editor.ShowOverlay(first);
            editor.ShowOverlay(second);

            Assert.Same(second, editor.ActiveOverlay);
            Assert.Equal(1, _capture.ResumeCount);
            Assert.Equal(2, _capture.SuspendCount);

            second.Cancel();

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(2, _capture.ResumeCount);

            // The replaced overlay is detached, so its late close cannot clear the property again.
            first.Cancel();

            Assert.Null(editor.ActiveOverlay);
            Assert.Equal(2, _capture.ResumeCount);
        }

        [Fact]
        public async Task BeginRemapCommand_WhileTheRailsMacroPanelRecords_IsUnavailable()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            OpenMacroPanel(editor).RecordCommand.Execute(null);

            Assert.False(editor.BeginRemapCommand.CanExecute(null));
        }

        [Fact]
        public async Task MacroCounterCaption_FollowsTheProfilesMacroCount()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal("Macro (0)", editor.MacroCounterCaption);

            RecordAMacro(editor, "a");

            Assert.Equal(1, editor.MacroCount);
            Assert.Equal("Macro (1)", editor.MacroCounterCaption);
            Assert.Equal("Macro (7)", KeyboardEditorViewModel.BuildMacroCounterCaption(7));
        }

        [Fact]
        public async Task ResetLayoutCommand_ClearsTheMacrosAndRefreshesTheLibrary()
        {
            var editor = await CreateLoadedEditorAsync();

            RecordAMacro(editor, "a");

            Assert.Single(editor.MacroLibraryPanel.Rows);

            // The reset scopes confirm first (NotificationKeys.ResetLayer), and the fake answers
            // Ok by default rather than the Yes the guard waits for.
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            editor.ResetLayoutCommand.Execute(null);

            Assert.Equal(0, editor.MacroCount);
            Assert.Equal("Macro (0)", editor.MacroCounterCaption);
            Assert.Empty(editor.MacroLibraryPanel.Rows);
        }

        [Fact]
        public async Task SelectTabCommand_LeavingTheLayoutTab_StandsTheRailsRecordingDown()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            editor.SelectTabCommand.Execute(editor.Tabs[1]);

            Assert.False(panel.IsRecording);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public async Task Dispose_WithAnOpenOverlay_DetachesItAndStopsCapture()
        {
            var editor = await CreateLoadedEditorAsync();
            var overlay = new TextEntryOverlay();

            editor.ShowOverlay(overlay);

            editor.Dispose();

            Assert.Null(editor.ActiveOverlay);
            Assert.False(_capture.HasSubscribers);
            Assert.False(_capture.IsCapturing);

            // A close after disposal must not run the editor's teardown a second time.
            overlay.Cancel();

            Assert.Null(editor.ActiveOverlay);
        }

        [Fact]
        public async Task Dispose_WhileTheRailsMacroPanelRecords_StopsCaptureAndClosesTheRail()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            editor.Dispose();

            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.HasSubscribers);
            Assert.False(panel.IsRecording);
            Assert.False(editor.Inspector.IsOpen);
        }

        private static KeyboardKeyViewModel SelectDigitOne(KeyboardEditorViewModel editor)
        {
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);

            return key;
        }

        /// <summary>
        /// Puts the key inspector on its Macro mode and hands the panel back — the app's one macro
        /// editor since issue #93.
        /// </summary>
        private static MacroInspectorPanelViewModel OpenMacroPanel(KeyboardEditorViewModel editor)
        {
            SelectMode(editor, KeyInspectorMode.Macro);

            return Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);
        }

        /// <summary>Records one keystroke into the selected position's macro, through the rail.</summary>
        private void RecordAMacro(KeyboardEditorViewModel editor, string token)
        {
            SelectDigitOne(editor);

            var panel = OpenMacroPanel(editor);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key(token));

            panel.Deactivate();
        }

        /// <summary>Puts the key inspector on <paramref name="mode"/> through its own tab.</summary>
        private static void SelectMode(KeyboardEditorViewModel editor, KeyInspectorMode mode)
        {
            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == mode)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);

                    return;
                }
            }

            throw new InvalidOperationException($"The key inspector carries no {mode} tab.");
        }

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync()
        {
            var editor = new KeyboardEditorViewModel(
                TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

            _editors.Add(editor);

            await editor.LoadAsync();

            return editor;
        }

        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                editor.Dispose();
            }
        }

        /// <summary>A feature panel with text entry — Delays, Search Keys, Export — and no sink.</summary>
        private sealed class TextEntryOverlay : EditorOverlayViewModel
        {
            public TextEntryOverlay() : base("Macro Timing Delays")
            {
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }

        /// <summary>A feature panel that consumes captured keystrokes — the Tap and Hold shape.</summary>
        private sealed class SinkOverlay : EditorOverlayViewModel, IKeystrokeSink
        {
            public bool WantsKeystrokes { get; init; } = true;

            public List<CapturedKeystroke> Received { get; } = [];

            public SinkOverlay() : base("Tap and Hold")
            {
            }

            public void ReceiveKeystroke(CapturedKeystroke keystroke)
            {
                Received.Add(keystroke);
            }

            protected override bool TryAccept()
            {
                return true;
            }
        }
    }
}
