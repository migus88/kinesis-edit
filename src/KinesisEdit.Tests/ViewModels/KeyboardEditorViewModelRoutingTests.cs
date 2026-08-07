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
    /// panel is open, and the Macros tab that the macro panel lives on.
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
        public async Task SelectTabCommand_ForTheMacrosTab_ShowsThePanelAndTheKeysTabHidesItAgain()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.NotNull(editor.MacroPanel);
            Assert.False(editor.IsMacroPanelVisible);

            editor.SelectTabCommand.Execute(editor.Tabs[1]);

            Assert.Equal(EditorTab.Macros, editor.SelectedTab);
            Assert.True(editor.IsMacroPanelVisible);

            editor.SelectTabCommand.Execute(editor.Tabs[0]);

            Assert.False(editor.IsMacroPanelVisible);
        }

        [Fact]
        public async Task MacroPanel_TriggerKey_IsTheBoardsSelectionOnBothTabs()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectTabCommand.Execute(editor.Tabs[1]);
            editor.SelectKeyCommand.Execute(key);

            Assert.Same(key, editor.MacroPanel!.TriggerKey);

            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            // A layer switch drops the selection, and the panel follows it.
            Assert.Null(editor.MacroPanel.TriggerKey);
            Assert.Equal(MacroPanelViewModel.NoTriggerKeyMessage, editor.MacroPanel.Message);
        }

        [Fact]
        public async Task RecordCommand_StartsCaptureAndStopRecordingStopsIt()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);

            Assert.Equal(1, _capture.StartCount);
            Assert.True(_capture.IsCapturing);

            editor.MacroPanel.StopRecordingCommand.Execute(null);

            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public async Task KeystrokeCaptured_WhileTheMacroPanelRecords_AppendsAStepAndRemapsNothing()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Equal("Z", Assert.Single(editor.MacroPanel.Steps.Items).KeyText);
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
            Assert.Empty(editor.MacroPanel!.Steps.Items);
        }

        [Fact]
        public async Task KeystrokeCaptured_WithAnOpenSinkOverlay_GoesToTheOverlayBeforeTheMacroPanel()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);

            var overlay = new SinkOverlay();

            editor.ShowOverlay(overlay);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            // Priority 1: an overlay that wants keystrokes takes them from everything else.
            Assert.Equal(TestLayouts.Gen1Key("z"), Assert.Single(overlay.Received).Key);
            Assert.Empty(editor.MacroPanel.Steps.Items);
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
            Assert.Empty(editor.MacroPanel!.Steps.Items);
        }

        /// <summary>
        /// specs/11-feature-dialogs.md's panels are modal over the editor, and specs/10's routing
        /// gives an open Tap and Hold dialog the keystrokes. A macro recording underneath owns the
        /// capture service, so opening any panel ends it — otherwise every key aimed at the panel,
        /// Escape included, is appended to the macro instead and the panel cannot be dismissed
        /// from the keyboard at all.
        /// </summary>
        [Fact]
        public async Task ShowOverlay_WhileTheMacroPanelRecords_StopsTheRecordingAndReleasesCapture()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);

            Assert.True(editor.MacroPanel.IsRecording);
            Assert.True(_capture.IsCapturing);

            var overlay = new SinkOverlay { WantsKeystrokes = false };

            editor.ShowOverlay(overlay);

            Assert.False(editor.MacroPanel.IsRecording);
            Assert.False(_capture.IsCapturing);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Empty(editor.MacroPanel.Steps.Items);
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
        /// Gate 1 of the editor's keyboard grammar, from the side that decides it: the three
        /// consumers of "one keystroke, one target" are exactly the three states in which no
        /// shortcut may be handled at all.
        /// </summary>
        [Fact]
        public async Task IsCaptureActive_IsTrueForEachOfTheThreeKeystrokeConsumers()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsCaptureActive);

            SelectDigitOne(editor);

            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsCaptureActive);

            editor.CancelRemapCommand.Execute(null);

            Assert.False(editor.IsCaptureActive);

            editor.SelectTabCommand.Execute(editor.Tabs[1]);
            editor.MacroPanel!.RecordCommand.Execute(null);

            Assert.True(editor.IsCaptureActive);

            editor.MacroPanel.StopRecordingCommand.Execute(null);

            Assert.False(editor.IsCaptureActive);

            editor.ShowOverlay(new SinkOverlay());

            Assert.True(editor.IsCaptureActive);

            // An open panel that is *not* waiting for a keypress is not a keystroke consumer: it
            // suspended capture, so ⌘S there is a save and not a swallowed 's'.
            editor.ShowOverlay(new TextEntryOverlay());

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
        public async Task BeginRemapCommand_WhileTheMacroPanelRecords_IsUnavailable()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);

            Assert.False(editor.BeginRemapCommand.CanExecute(null));
        }

        [Fact]
        public async Task MacroCounterCaption_FollowsTheProfilesMacroCount()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal("Macro (0)", editor.MacroCounterCaption);

            SelectDigitOne(editor);

            editor.MacroPanel!.InsertKeystroke(TestLayouts.Gen1Key("a"));
            editor.MacroPanel.AssignCommand.Execute(null);

            Assert.Equal(1, editor.MacroCount);
            Assert.Equal("Macro (1)", editor.MacroCounterCaption);
            Assert.Equal("Macro (7)", KeyboardEditorViewModel.BuildMacroCounterCaption(7));
        }

        [Fact]
        public async Task ResetLayoutCommand_ClearsTheMacrosAndRefreshesThePanel()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            editor.MacroPanel!.InsertKeystroke(TestLayouts.Gen1Key("a"));
            editor.MacroPanel.AssignCommand.Execute(null);

            // The reset scopes confirm first (NotificationKeys.ResetLayer), and the fake answers
            // Ok by default rather than the Yes the guard waits for.
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            editor.ResetLayoutCommand.Execute(null);

            Assert.Equal(0, editor.MacroCount);
            Assert.Equal("Macro (0)", editor.MacroCounterCaption);
            Assert.Empty(editor.MacroPanel.Macros);
            Assert.Empty(editor.MacroPanel.Steps.Items);
        }

        [Fact]
        public async Task SelectTabCommand_LeavingTheMacrosTab_StopsRecording()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectTabCommand.Execute(editor.Tabs[1]);

            SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);
            editor.SelectTabCommand.Execute(editor.Tabs[0]);

            Assert.False(editor.MacroPanel.IsRecording);
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
        public async Task Dispose_WhileTheMacroPanelRecords_StopsCaptureAndDetachesThePanel()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectDigitOne(editor);

            editor.MacroPanel!.RecordCommand.Execute(null);

            editor.Dispose();

            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.HasSubscribers);
            Assert.Null(editor.MacroPanel);
        }

        private static KeyboardKeyViewModel SelectDigitOne(KeyboardEditorViewModel editor)
        {
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);

            return key;
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
