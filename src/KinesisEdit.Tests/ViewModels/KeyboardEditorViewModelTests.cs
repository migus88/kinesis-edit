using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public sealed class KeyboardEditorViewModelTests : IDisposable
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

            Assert.Equal(KeyboardEditorViewModel.LoadFailureTitle, request.Title);
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
        public async Task Tabs_ForALitDeviceWithSettings_AreTheFourAndAllOpen()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(
                new[] { EditorTab.Keys, EditorTab.Macros, EditorTab.Lighting, EditorTab.Settings },
                editor.Tabs.Select(tab => tab.Tab));
            Assert.All(editor.Tabs, tab => Assert.True(tab.IsEnabled));
            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
            Assert.All(editor.Tabs, tab => Assert.True(editor.SelectTabCommand.CanExecute(tab)));
        }

        [Fact]
        public void SelectedTab_SetToATabWithNothingBehindIt_StaysOnTheKeysTab()
        {
            // The TKO's led file adds an edge section this panel does not edit (#40), so its
            // Lighting tab is present but dark — and a tab with nothing behind it stays shut
            // whichever way it is asked for, the two-way binding included.
            var editor = CreateEditor(TestDevices.CreateSnapshot(DeviceId.Tko));
            var lighting = Assert.Single(editor.Tabs, tab => tab.Tab == EditorTab.Lighting);

            Assert.False(lighting.IsEnabled);
            Assert.False(editor.SelectTabCommand.CanExecute(lighting));

            editor.SelectedTab = EditorTab.Lighting;
            editor.SelectTabCommand.Execute(lighting);

            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
            Assert.False(lighting.IsSelected);
        }

        [Fact]
        public void SelectedTab_SetToASectionTheDeviceDoesNotCarry_StaysOnTheKeysTab()
        {
            // The CROSSFIRE has no app-managed settings file, so the strip has no Settings tab at
            // all: absent and disabled are refused by the same guard.
            var editor = CreateEditor(TestDevices.CreateSnapshot(DeviceId.CrossfireKeypad));

            Assert.DoesNotContain(EditorTab.Settings, editor.Tabs.Select(tab => tab.Tab));

            editor.SelectedTab = EditorTab.Settings;

            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
        }

        [Fact]
        public async Task SelectedTab_SetToTheMacrosTab_Opens()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectedTab = EditorTab.Macros;

            Assert.Equal(EditorTab.Macros, editor.SelectedTab);
            Assert.True(editor.Tabs[1].IsSelected);
            Assert.True(editor.IsMacroPanelVisible);
        }

        [Fact]
        public async Task SelectTabCommand_ForTheLightingTab_OpensIt()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectTabCommand.Execute(editor.Tabs[2]);

            Assert.Equal(EditorTab.Lighting, editor.SelectedTab);
            Assert.True(editor.Tabs[2].IsSelected);
        }

        [Fact]
        public async Task Lighting_ForALoadedProfile_EditsTheModelTheSessionHandedOut()
        {
            var lighting = new LightingModel();

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                Lighting = lighting
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.Lighting.IsAvailable);
            Assert.Equal(2, editor.Lighting.Layers.Count);

            editor.Lighting.SelectModeCommand.Execute(
                editor.Lighting.Modes.Single(mode => mode.Mode == LightingMode.Wave));

            // No second save path: the session already serializes whatever its Lighting holds.
            Assert.Equal(LightingMode.Wave, lighting.TopLayer.Mode);
        }

        [Fact]
        public async Task Lighting_IsSupportedForTheEditorsDeviceButNotForTheOtherLitBoards()
        {
            // Only the support predicate is checked here — what a tab built for such a device does
            // is LightingTabViewModelTests.Attach_ForADeviceThePanelCannotEdit_DoesNothing. The
            // TKO's led file carries a second, edge section (issue #40) and the Advantage 360's
            // holds six indicators (issue #41): neither is this panel's model.
            Assert.False(LightingTabViewModel.IsSupported(DeviceCatalog.GetById(DeviceId.Tko)));
            Assert.False(LightingTabViewModel.IsSupported(DeviceCatalog.GetById(DeviceId.Advantage360)));

            var editor = await CreateLoadedEditorAsync();

            Assert.True(LightingTabViewModel.IsSupported(editor.Device.Device));
        }

        [Fact]
        public async Task Lighting_InDemoMode_IsExplorableButNeverSaved()
        {
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.NotDetected));

            await editor.LoadAsync();

            Assert.True(editor.Lighting.IsAvailable);
            Assert.Equal(2, editor.Lighting.Layers.Count);
            Assert.Equal(LightingTabViewModel.DemoModeHint, editor.Lighting.StatusMessage);
            Assert.False(editor.Lighting.Picker.CanStoreCustomColors);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.Empty(_settings.AppSettingsSaves);
        }

        [Fact]
        public async Task SelectTabCommand_ForTheSettingsTab_OpensIt()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.SelectTabCommand.Execute(editor.Tabs[3]);

            Assert.Equal(EditorTab.Settings, editor.SelectedTab);
            Assert.True(editor.Tabs[3].IsSelected);
            Assert.False(editor.Tabs[0].IsSelected);
        }

        [Fact]
        public async Task SelectTab_WhileAKeyIsListening_CancelsTheRemapAndStopsCapture()
        {
            // Capture is app-wide and never left running (docs/app/keyboard-editor.md,
            // invariant 4): leaving the keyboard picture must end the listen.
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            editor.SelectKeyCommand.Execute(key);
            editor.BeginRemapCommand.Execute(null);

            Assert.True(editor.IsListening);

            editor.SelectedTab = EditorTab.Settings;

            Assert.Equal(EditorTab.Settings, editor.SelectedTab);
            Assert.False(editor.IsListening);
            Assert.False(key.IsListening);
            Assert.False(_capture.IsCapturing);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public async Task Settings_ForTheOpenDevice_IsLoadedThroughTheSettingsSeam()
        {
            _settings.KeyboardSettingsToReturn = new KeyboardSettings { MacroSpeed = 7 };

            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(1, _settings.LoadKeyboardCallCount);
            Assert.False(editor.Settings.IsLoading);

            var macroSpeed = Assert.IsType<SettingsSliderRowViewModel>(
                editor.Settings.Rows.Single(row => row.Caption == KeyboardSettingsRows.MacroSpeedCaption));

            Assert.Equal(7, macroSpeed.Value);
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

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, request.Title);
            Assert.Contains(KeyboardEditorViewModel.SaveRejectedMessage, request.Message, StringComparison.Ordinal);
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

            Assert.Equal(KeyboardEditorViewModel.SaveTitle, request.Title);
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

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync()
        {
            var editor = CreateEditor();

            await editor.LoadAsync();

            return editor;
        }

        private KeyboardEditorViewModel CreateEditor(DeviceSnapshot? snapshot = null)
        {
            var editor = new KeyboardEditorViewModel(
                snapshot ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                _settings,
                _capture,
                _notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher);

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
