using KinesisEdit.Core.Devices;
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
            Assert.Equal("Remap (1)", editor.RemapCounterCaption);
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
            Assert.Equal("Remap (0)", editor.RemapCounterCaption);
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
