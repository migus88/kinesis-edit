using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The remap workflow of specs/10-apps-and-ui.md: "click an on-screen key — the key enters
    /// 'listening' state; the next physical keypress captured by the app becomes the new
    /// assignment", plus the reset scopes around it.
    /// </summary>
    public sealed class KeyboardEditorViewModelRemapTests : IDisposable
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
        public async Task SelectKeyCommand_OnAnUnselectedKey_SelectsItWithoutListening()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);

            Assert.Same(key, editor.SelectedKey);
            Assert.True(key.IsSelected);
            Assert.False(editor.IsListening);
            Assert.Equal(0, _capture.StartCount);
        }

        [Fact]
        public async Task SelectKeyCommand_OnTheSelectedKey_EntersListeningAndStartsCapture()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.SelectKeyCommand.Execute(key);

            Assert.True(key.IsListening);
            Assert.Same(key, editor.ListeningKey);
            Assert.True(editor.IsListening);
            Assert.Equal(1, _capture.StartCount);
            Assert.True(_capture.IsCapturing);
        }

        [Fact]
        public async Task SelectKeyCommand_OnTheListeningKey_CancelsListening()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.SelectKeyCommand.Execute(key);
            editor.SelectKeyCommand.Execute(key);

            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public async Task SelectKeyCommand_OnAnotherKeyWhileListening_LeavesOnlyOneKeyListening()
        {
            var editor = await CreateLoadedEditorAsync();
            var first = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];
            var second = editor.SelectedLayer.Keys[TestLayouts.RgbDigitTwoKeyIndex];

            editor.SelectKeyCommand.Execute(first);
            editor.BeginRemapCommand.Execute(null);
            editor.SelectKeyCommand.Execute(second);

            Assert.False(first.IsListening);
            Assert.False(second.IsListening);
            Assert.False(first.IsSelected);
            Assert.Same(second, editor.SelectedKey);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public async Task BeginRemapCommand_ForALockedKey_IsUnavailable()
        {
            _profiles.SessionToReturn = new FakeProfileSession(TestLayouts.CreateLockedKeyLayout());

            var editor = await CreateLoadedEditorAsync();
            var lockedKey = editor.SelectedLayer!.Keys[1];

            editor.SelectKeyCommand.Execute(lockedKey);

            Assert.False(lockedKey.CanEdit);
            Assert.False(editor.BeginRemapCommand.CanExecute(null));

            editor.SelectKeyCommand.Execute(lockedKey);

            Assert.False(editor.IsListening);
            Assert.Equal(0, _capture.StartCount);
        }

        [Fact]
        public async Task KeystrokeCaptured_WhileAKeyIsListening_AppliesTheRemapAndStopsCapture()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.True(key.IsModified);
            Assert.Equal("Z", key.Caption);
            Assert.Equal(1, editor.ModifiedKeyCount);
            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.IsCapturing);
        }

        [Fact]
        public async Task KeystrokeCaptured_WhenNothingIsListening_ChangesNothing()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);

            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.False(key.IsModified);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task CancelRemapCommand_WhileListening_StopsCaptureWithoutChangingTheKey()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];
            var caption = key.Caption;

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.CancelRemapCommand.CanExecute(null));

            editor.CancelRemapCommand.Execute(null);

            Assert.False(editor.IsListening);
            Assert.False(key.IsModified);
            Assert.Equal(caption, key.Caption);
            Assert.Same(key, editor.SelectedKey);
            Assert.Equal(1, _capture.StopCount);
            Assert.False(editor.CancelRemapCommand.CanExecute(null));
        }

        [Fact]
        public async Task SelectLayerCommand_WhileAKeyIsListening_CancelsIt()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);
            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
            Assert.Null(editor.SelectedKey);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public async Task KeystrokeCaptured_WithTheKeysOwnAction_ClearsTheRemapAgain()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);
            _capture.RaiseKeystroke(TestLayouts.Gen1Key("z"));

            Assert.Equal(1, editor.ModifiedKeyCount);

            editor.BeginRemapCommand.Execute(null);
            _capture.RaiseKeystroke(key.Key.OriginalKey);

            // specs/04-layout-file-format.md §2.1: assigning a key its own action clears the remap.
            Assert.False(key.IsModified);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task ResetKeyCommand_DropsTheRemapWithoutClearingTapAndHold()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            // The tolerant load paths are the only way to build a position carrying both rules —
            // which is exactly the state that tells ClearRemap() and Remap(OriginalKey) apart.
            key.Key.ApplyRemap(TestLayouts.Gen1Key("z"));
            key.Key.ApplyTapAndHold(TestLayouts.Gen1Key("a"), TestLayouts.Gen1Key("b"), 250);
            key.RefreshFromModel();

            editor.SelectKeyCommand.Execute(key);
            editor.ResetKeyCommand.Execute(null);

            Assert.False(key.IsModified);
            Assert.True(key.Key.IsTapAndHold);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task ResetLayerCommand_ResetsEveryKeyOfTheShownLayerOnly()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));
            editor.Layout.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            // Both reset scopes confirm first (NotificationKeys.ResetLayer); the fake's default
            // answer is Ok, which is not the Yes the guard waits for.
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            editor.ResetLayerCommand.Execute(null);

            Assert.False(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.True(editor.Layout.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.False(editor.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.Equal(1, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task ResetLayoutCommand_ResetsEveryLayer()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));
            editor.Layout.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };

            editor.ResetLayoutCommand.Execute(null);

            Assert.Equal(0, editor.ModifiedKeyCount);
            Assert.All(editor.Layers, layer => Assert.All(layer.Keys, key => Assert.False(key.IsModified)));
        }

        [Fact]
        public async Task Dispose_WhileAKeyIsListening_StopsCapture()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            editor.Dispose();

            Assert.False(_capture.IsCapturing);
            Assert.False(_capture.HasSubscribers);
            Assert.Equal(1, _capture.StopCount);
        }

        /// <summary>
        /// The rail is built in the <b>constructor</b>, before any profile has been read — and its
        /// picker still holds the right catalog, because the dialect is a fact about the
        /// <em>device</em> (<c>KeyboardLayout.DialectFor</c>) and not about the profile.
        /// <para>
        /// That agreement is the whole resolution of the construction-ordering problem, so it is
        /// pinned rather than assumed: if the two ever diverged, the picker would offer a Gen1
        /// catalog for a Legacy board and nothing else would notice.
        /// </para>
        /// </summary>
        [Fact]
        public async Task TheKeyInspector_IsBuiltBeforeTheProfileLoads_OnTheDevicesOwnDialect()
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

            // Nothing has been read yet: no layout, and therefore no Layout.Dialect to ask.
            Assert.Null(editor.Layout);
            Assert.NotNull(editor.Inspector);

            var remap = Assert.IsType<RemapPanelViewModel>(editor.Inspector.ActivePanel);

            Assert.Equal(TokenDialect.Gen1, remap.Picker.Dialect);
            Assert.True(remap.Picker.TotalCount > 0);

            await editor.LoadAsync();

            Assert.Equal(editor.Layout!.Dialect, remap.Picker.Dialect);
        }

        /// <summary>
        /// Clicking a cap opens the rail, and clicking the <em>already selected</em> cap opens it
        /// again — a second click is a request for the inspector, not only the click contract's
        /// promotion to listening. Without the second half, a rail the user pressed Escape on could
        /// only be brought back by selecting some other key first.
        /// </summary>
        [Fact]
        public async Task SelectKeyCommand_OpensTheKeyInspector_AndReopensADismissedOne()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            Assert.False(editor.Inspector.IsOpen);

            editor.SelectKeyCommand.Execute(key);

            Assert.True(editor.Inspector.IsOpen);

            editor.Inspector.CloseCommand.Execute(null);

            Assert.False(editor.Inspector.IsOpen);
            Assert.Same(key, editor.SelectedKey);

            editor.SelectKeyCommand.Execute(key);

            Assert.True(editor.Inspector.IsOpen);

            // ...and the click contract still applies underneath it.
            Assert.True(editor.IsListening);
        }

        /// <summary>
        /// The rail is <b>not</b> modal, so nothing about it may be phrased as an overlay: the board
        /// stays clickable, a remap can still be started, and <c>HasActiveOverlay</c> never moves.
        /// </summary>
        [Fact]
        public async Task TheKeyInspector_BeingOpen_LeavesTheBoardAndItsCommandsAlone()
        {
            var editor = await CreateLoadedEditorAsync();
            var first = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];
            var second = editor.SelectedLayer.Keys[TestLayouts.RgbDigitTwoKeyIndex];

            editor.SelectKeyCommand.Execute(first);

            Assert.True(editor.Inspector.IsOpen);
            Assert.False(editor.HasActiveOverlay);
            Assert.Null(editor.ActiveOverlay);
            Assert.True(editor.BeginRemapCommand.CanExecute(null));
            Assert.True(editor.CopyKeyCommand.CanExecute(null));

            // Another cap simply refreshes it; the rail never intercepted the click.
            editor.SelectKeyCommand.Execute(second);

            Assert.Same(second, editor.SelectedKey);
            Assert.True(editor.Inspector.IsOpen);
        }

        /// <summary>
        /// Every mutation reaches the rail, because the rail hangs off <c>RefreshLegend</c> — the
        /// tail of the funnel every writing path already ends in. Core announces nothing, so a path
        /// that missed it would leave the rail showing the assignment before the edit.
        /// </summary>
        [Fact]
        public async Task EveryWriteRefreshesTheKeyInspector_ThroughTheOneFunnel()
        {
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);

            var factory = editor.Inspector.FactoryAssignmentText;

            Assert.Equal(factory, editor.Inspector.CurrentAssignmentText);

            editor.BeginRemapCommand.Execute(null);
            _capture.RaiseKeystroke(TestLayouts.Gen1Key("F5"));

            Assert.NotEqual(factory, editor.Inspector.CurrentAssignmentText);

            // ...and the header still names the position by what the board shipped it doing.
            Assert.Equal(factory, editor.Inspector.PositionToken);

            editor.ResetKeyCommand.Execute(null);

            Assert.Equal(factory, editor.Inspector.CurrentAssignmentText);
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
    }
}
