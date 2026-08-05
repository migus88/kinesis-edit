using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public sealed class DeviceEditorViewModelTests : IDisposable
    {
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly List<DeviceEditorViewModel> _editors = [];

        [Fact]
        public async Task LoadAsync_InDemoMode_BuildsTheLayoutInMemoryWithoutTouchingTheDrive()
        {
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            await editor.LoadAsync();

            Assert.Equal(0, _profiles.LoadCallCount);
            Assert.NotNull(editor.Layout);
            Assert.Equal(string.Empty, editor.ProfileCaption);
            Assert.False(editor.IsLoading);
            Assert.False(editor.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task LoadAsync_WithAConnectedDevice_LoadsTheDevicesFirstProfile()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var editor = CreateEditor(snapshot);

            await editor.LoadAsync();

            var call = Assert.Single(_profiles.LoadCalls);

            Assert.Same(snapshot.Location, call.Location);
            Assert.Equal(DeviceId.FreestyleEdgeRgb, call.Device);
            Assert.Equal(snapshot.Device.LayoutScheme.FirstProfileNumber, call.ProfileNumber);
            Assert.Equal("Profile 1", editor.ProfileCaption);
            Assert.True(editor.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task LoadAsync_WhenTheProfileCannotBeRead_DegradesToTheInMemoryLayout()
        {
            _profiles.ExceptionToThrow = new IOException("the v-Drive went away");

            var editor = CreateEditor();

            await editor.LoadAsync();

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(DeviceEditorViewModel.LoadFailureTitle, request.Title);
            Assert.Contains("the v-Drive went away", request.Message, StringComparison.Ordinal);
            Assert.NotNull(editor.Layout);
            Assert.Equal(2, editor.Layers.Count);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.IsLoading);
        }

        [Fact]
        public async Task LoadAsync_CalledTwice_ReadsTheProfileOnce()
        {
            var editor = CreateEditor();

            await editor.LoadAsync();
            await editor.LoadAsync();

            Assert.Equal(1, _profiles.LoadCallCount);
        }

        [Fact]
        public async Task Layers_ForTheFreestyleEdgeRgb_AreTheTopAndFnLayersOfNinetyFiveKeys()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(new[] { "Top", "Fn" }, editor.Layers.Select(layer => layer.Caption));
            Assert.All(editor.Layers, layer => Assert.Equal(95, layer.Keys.Count));
            Assert.Same(editor.Layers[0], editor.SelectedLayer);
            Assert.True(editor.Layers[0].IsSelected);
            Assert.False(editor.Layers[1].IsSelected);
        }

        [Fact]
        public async Task SelectLayerCommand_ForTheFnLayer_SwapsTheWholeKeyCollection()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectLayerCommand.Execute(editor.Layers[1]);

            Assert.Same(editor.Layers[1], editor.SelectedLayer);
            Assert.False(editor.Layers[0].IsSelected);
            Assert.True(editor.Layers[1].IsSelected);
            Assert.NotSame(editor.Layers[0].Keys[0], editor.SelectedLayer!.Keys[0]);
            Assert.Same(editor.Layout!.Layers[1].Keys[0], editor.SelectedLayer.Keys[0].Key);
        }

        [Fact]
        public async Task BoardSize_ComesFromTheAuthoredPicture()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.BoardWidth > 0);
            Assert.True(editor.BoardHeight > 0);
            Assert.Equal(editor.BoardWidth, editor.Layers[0].BoardWidth);
            Assert.Equal(editor.BoardHeight, editor.Layers[0].BoardHeight);
        }

        [Fact]
        public async Task InvalidLineMessages_ForALineTheParserCouldNotApply_AreSurfaced()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                InvalidLines = [new LayoutInvalidLine(3, 0, "[nonsense]>[more nonsense]", [])]
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.HasInvalidLines);
            Assert.Equal("Line 3: [nonsense]>[more nonsense]", Assert.Single(editor.InvalidLineMessages));
        }

        [Fact]
        public async Task InvalidLineMessages_WithoutAnyInvalidLine_AreEmpty()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.HasInvalidLines);
            Assert.Empty(editor.InvalidLineMessages);
        }

        [Fact]
        public async Task Tabs_ExceptTheKeysTab_AreVisibleButDisabled()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Lighting, EditorTab.Settings },
                editor.Tabs.Select(tab => tab.Tab));
            Assert.True(editor.Tabs[0].IsEnabled);
            Assert.All(editor.Tabs.Skip(1), tab => Assert.False(tab.IsEnabled));
            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
            Assert.False(editor.SelectTabCommand.CanExecute(editor.Tabs[1]));
        }

        [Fact]
        public async Task SelectedTab_SetToATabWithNothingBehindIt_StaysOnTheKeysTab()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectedTab = EditorTab.Macros;
            editor.SelectTabCommand.Execute(editor.Tabs[2]);

            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
            Assert.False(editor.Tabs[1].IsSelected);
        }

        [Fact]
        public async Task SaveCommand_InDemoMode_IsUnavailableAndWritesNothing()
        {
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            await editor.LoadAsync();
            await editor.SaveCommand.ExecuteAsync(null);

            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.Null(_profiles.SessionToReturn);
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task SaveCommand_ForAReadOnlyProfile_IsUnavailable()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                CanSave = false
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.Equal(0, _profiles.SessionToReturn.SaveCallCount);
        }

        [Fact]
        public async Task SaveCommand_WhenTheSaveSucceeds_ToastsThePostSaveMessage()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ResultToReturn = new ProfileSaveResult
                {
                    Success = true,
                    Violations = [],
                    Ejected = true,
                    PostSaveMessage = "To load Profile 1 to the keyboard, hold the SmartSet key and tap the 1 key."
                }
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, _profiles.SessionToReturn.SaveCallCount);
            Assert.Equal(
                "To load Profile 1 to the keyboard, hold the SmartSet key and tap the 1 key.",
                Assert.Single(_notifications.Toasts).Message);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.Equal(new string?[] { "Saving...", null }, _notifications.LoadingHistory);
        }

        [Fact]
        public async Task ResetCommands_WhileASaveIsInFlight_AreUnavailable()
        {
            // Save serializes the model on a background thread; letting a reset mutate it
            // underneath would race the serializer, so every editing command stands down.
            var observed = new List<bool>();
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            _profiles.SessionToReturn!.DuringSave = () =>
            {
                observed.Add(editor.IsBusy);
                observed.Add(editor.ResetKeyCommand.CanExecute(null));
                observed.Add(editor.ResetLayerCommand.CanExecute(null));
                observed.Add(editor.ResetLayoutCommand.CanExecute(null));
                observed.Add(editor.BeginRemapCommand.CanExecute(null));
            };

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(new[] { true, false, false, false, false }, observed);

            // ...and they come back once the save is done.
            Assert.False(editor.IsBusy);
            Assert.True(editor.ResetKeyCommand.CanExecute(null));
            Assert.True(editor.ResetLayerCommand.CanExecute(null));
            Assert.True(editor.ResetLayoutCommand.CanExecute(null));
        }

        [Fact]
        public async Task SaveCommand_WithoutAPostSaveMessage_ToastsNothing()
        {
            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, _profiles.SessionToReturn!.SaveCallCount);
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task SaveCommand_WhenValidationStopsTheSave_ReportsTheViolations()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                ResultToReturn = new ProfileSaveResult
                {
                    Success = false,
                    Violations =
                    [
                        new ModelViolation
                        {
                            Kind = ModelViolationKind.MacroCountExceeded,
                            Message = "The layout holds 120 macros; the device allows 100."
                        }
                    ],
                    Ejected = false
                }
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(DeviceEditorViewModel.SaveTitle, request.Title);
            Assert.Contains(DeviceEditorViewModel.SaveRejectedMessage, request.Message, StringComparison.Ordinal);
            Assert.Contains("the device allows 100", request.Message, StringComparison.Ordinal);
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task SaveCommand_WhenTheSaveThrows_ReportsItAndClaimsNoSuccess()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                SaveExceptionToThrow = new IOException("the v-Drive went away")
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(DeviceEditorViewModel.SaveTitle, request.Title);
            Assert.Contains("the v-Drive went away", request.Message, StringComparison.Ordinal);
            Assert.Empty(_notifications.Toasts);
            Assert.Null(_notifications.LoadingCaption);
            Assert.False(editor.IsBusy);
        }

        [Fact]
        public async Task Dispose_StopsCaptureAndDetachesFromIt()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.True(_capture.HasSubscribers);

            editor.Dispose();

            Assert.False(_capture.HasSubscribers);
            Assert.False(_capture.IsCapturing);
            Assert.Equal(1, _capture.StopCount);
        }

        private async Task<DeviceEditorViewModel> CreateLoadedEditorAsync()
        {
            var editor = CreateEditor();

            await editor.LoadAsync();

            return editor;
        }

        private DeviceEditorViewModel CreateEditor(DeviceSnapshot? snapshot = null)
        {
            var editor = new DeviceEditorViewModel(
                snapshot ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                _capture,
                _notifications);

            _editors.Add(editor);

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
