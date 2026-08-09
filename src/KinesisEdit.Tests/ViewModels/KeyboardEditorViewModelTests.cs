using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Profiles;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Design;
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
        public async Task LoadAsync_InDemoMode_ReadsTheProfileThroughTheSessionLikeAnyOtherLoad()
        {
            // Demo mode used to short-circuit here and edit a factory-default model, which is why
            // it opened an empty board. The ban of 03 §3.5 is on *writing*: a demo device has a
            // drive — a real one that is merely not writable, or the synthetic fixture drive — and
            // it is read through the ordinary path, by a file service that refuses the way back.
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess);

            var editor = CreateEditor(snapshot);

            await editor.LoadAsync();

            var call = Assert.Single(_profiles.LoadCalls);

            Assert.Same(snapshot.Location, call.Location);
            Assert.NotNull(editor.Layout);
            Assert.Equal("Profile 1", editor.ProfileCaption);
            Assert.False(editor.IsLoading);

            // Everything that would write is still refused.
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.ImportCommand.CanExecute(null));
        }

        [Fact]
        public async Task LoadAsync_InDemoModeWithNoDriveAtAll_StillBuildsTheLayoutInMemory()
        {
            // The board without demo content, unchanged: the demo gate answered null for it, so
            // there is no drive to read and the editor edits a factory-default model with no
            // session behind it. Six of the seven boards open exactly this way.
            var snapshot = DeviceSnapshot.CreateDemo(DeviceCatalog.GetById(DeviceId.Tko));

            Assert.Null(snapshot.Location);

            var editor = CreateEditor(snapshot);

            await editor.LoadAsync();

            Assert.Equal(0, _profiles.LoadCallCount);
            Assert.NotNull(editor.Layout);
            Assert.Equal(string.Empty, editor.ProfileCaption);
            Assert.False(editor.IsLoading);
            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.False(editor.ExportCommand.CanExecute(null));
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
            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
            Assert.All(editor.Tabs, tab => Assert.True(editor.SelectTabCommand.CanExecute(tab)));
        }

        [Fact]
        public void Tabs_ForABoardThisAppCannotLight_CarryNoLightingTabAtAll()
        {
            // The TKO's led file adds an edge section this panel does not edit (#40). The tab used
            // to be rendered and disabled; the design's law is capability-driven absence, so the
            // section is simply not on the strip and cannot be reached by any route.
            var editor = CreateEditor(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.DoesNotContain(EditorTab.Lighting, editor.Tabs.Select(tab => tab.Tab));

            editor.SelectedTab = EditorTab.Lighting;

            Assert.Equal(EditorTab.Keys, editor.SelectedTab);
            Assert.True(editor.Tabs[0].IsSelected);
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
            Assert.True(editor.IsMacroLibraryVisible);
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
            // A demo session is a real session now, so "no session" no longer does the refusing —
            // `!IsDemoMode` in CanSave is the whole of it, and Save must neither run nor reach the
            // session's own Save (which, on the real one, ends in an eject).
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            await editor.LoadAsync();

            var session = _profiles.SessionToReturn;

            Assert.NotNull(session);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.False(editor.SaveCommand.CanExecute(null));
            Assert.Equal(0, session.SaveCallCount);
            Assert.Empty(_notifications.Toasts);
            Assert.Empty(_files.WrittenPaths);
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
            // Staged dirty: since issue #133 Save writes only the profiles that changed, so a
            // session that never claimed to need saving is never written and there is no toast to
            // assert. It is also what this test always meant — the user edited the profile.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
                ResultToReturn = new ProfileSaveResult
                {
                    Success = true,
                    Violations = [],
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
            Assert.Equal(new string?[] { "Saving…", null }, _notifications.LoadingHistory);
        }

        [Fact]
        public async Task ResetCommands_WhileASaveIsInFlight_AreUnavailable()
        {
            // Save serializes the model on a background thread; letting a reset mutate it
            // underneath would race the serializer, so every editing command stands down.
            var observed = new List<bool>();
            var editor = await CreateLoadedEditorAsync();

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);

            _profiles.SessionToReturn!.IsDirty = true;
            _profiles.SessionToReturn.DuringSave = () =>
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

            _profiles.SessionToReturn!.IsDirty = true;

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.Equal(1, _profiles.SessionToReturn!.SaveCallCount);
            Assert.Empty(_notifications.Toasts);
        }

        [Fact]
        public async Task SaveCommand_WhenValidationStopsTheSave_ReportsTheViolations()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
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
                    ]
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
                IsDirty = true,
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

        // ===== The editor's own chrome (issue #90) ===========================================
        //
        // The toolbar this editor draws replaces the shell's app bar, so everything the bar shows
        // has to be on this view model: the flag that hides the shell's, the mount path, the shell
        // it reaches Home and the status chip through, and the dirty flag behind the amber Save.

        [Fact]
        public void ProvidesOwnChrome_IsTrue_SoTheShellHidesItsOwnBar()
        {
            // The mockups draw exactly one 46px bar while editing. The placeholder editor, which
            // draws none of its own, is the counter-example that keeps the flag honest.
            var editor = CreateEditor();
            var placeholder = new EditorPlaceholderViewModel(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.True(editor.ProvidesOwnChrome);
            Assert.False(placeholder.ProvidesOwnChrome);
        }

        [Fact]
        public void MountPath_ForAConnectedDrive_IsTheDrivesRootPathAndIsShown()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var editor = CreateEditor(snapshot);

            Assert.Equal(snapshot.Location!.RootPath, editor.MountPath);
            Assert.True(editor.HasMountPath);
        }

        [Fact]
        public void MountPath_InDemoMode_IsHiddenEvenWhenADriveIsMounted()
        {
            // Demo mode is entered for a drive that is merely not writable, so a path exists — but
            // nothing is written to it, and printing it would claim otherwise.
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            Assert.False(editor.HasMountPath);
        }

        [Fact]
        public void MountPath_OverTheDemoVDrive_IsHiddenBecauseItsRootIsNotAPlaceOnThisMachine()
        {
            // The mono slot is for values that exist verbatim on the machine, and
            // `kinesis-edit://demo/FreestyleEdgeRgb` exists nowhere. `HasMountPath` already refused
            // every demo device, so this is the case that must not become an exception when a demo
            // device acquires a location.
            var editor = CreateEditor(DeviceSnapshot.CreateDemo(
                DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb)));

            Assert.StartsWith(DemoVDrive.RootPrefix, editor.MountPath, StringComparison.Ordinal);
            Assert.False(editor.HasMountPath);
        }

        [Fact]
        public void Shell_IsNullUntilTheShellAssignsIt()
        {
            // Which is why every binding through it has to degrade to an empty chip: the headless
            // scenes host this view on its own.
            var editor = CreateEditor();

            Assert.Null(editor.Shell);

            var chrome = new FakeShellChrome();

            editor.Shell = chrome;

            Assert.Same(chrome, editor.Shell);
        }

        [Fact]
        public async Task IsDirty_ForAFreshlyLoadedProfile_IsFalse()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsDirty);
        }

        [Fact]
        public async Task IsDirty_InDemoMode_FollowsTheSessionBecauseThereIsOneNow()
        {
            // It used to be permanently false, and only because demo mode opened no session at all.
            // Now the session is real, so an edit really does move it — the amber Save reports the
            // truth about the model even where Save itself can never run (03 §3.5). Nothing about
            // that makes the profile savable, which is the pair of assertions below.
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            await editor.LoadAsync();

            Assert.False(editor.IsDirty);

            _profiles.SessionToReturn!.IsDirty = true;

            editor.SelectKeyCommand.Execute(editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex]);
            editor.ResetKeyCommand.Execute(null);

            Assert.True(editor.IsDirty);
            Assert.False(editor.SaveCommand.CanExecute(null));
        }

        [Fact]
        public async Task IsDirty_AfterALoadThatFoundADirtySession_FollowsTheSession()
        {
            // The load path itself: Apply ends in RefreshCounters, which re-reads the session.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.IsDirty);
        }

        /// <summary>
        /// One case per mutation path. The session's own <c>IsDirty</c> is staged rather than
        /// derived, because what is under test is that the path <b>re-asks</b> it at all: nothing
        /// pushes a notification when Core's model is mutated, so a path that forgets the refresh
        /// leaves Save grey while the user has unsaved work — the whole defect this feature exists
        /// to prevent.
        /// </summary>
        public static TheoryData<string> MutationPaths()
        {
            return new TheoryData<string>
            {
                "remap",
                "resetKey",
                "resetLayer",
                "resetLayout",
                "macroAssign",
                "lightingMode"
            };
        }

        [Theory]
        [MemberData(nameof(MutationPaths))]
        public async Task IsDirty_AfterEveryMutationPath_ReReadsTheSession(string path)
        {
            var lighting = new LightingModel();

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                Lighting = lighting
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsDirty);

            _profiles.SessionToReturn.IsDirty = true;

            RunMutation(editor, path);

            Assert.True(editor.IsDirty, $"The '{path}' path did not refresh the dirty state.");
        }

        [Fact]
        public async Task IsDirty_AfterALightingLayerSwitchThatNormalizedTheDirection_ReReadsTheSession()
        {
            // The eighth lighting write site, and the one that was silent. Rebound offers only
            // Left and Up (specs/07-lighting.md §3), so a file carrying `down` on a rebound layer
            // is rewritten the moment that layer is shown — a real write into the profile's model,
            // with no counter to move and, until this was fixed, no ModelChanged either. The
            // session went dirty and Save stayed grey.
            var lighting = new LightingModel();

            lighting.FnLayer.Mode = LightingMode.Rebound;
            lighting.FnLayer.Direction = LightingDirection.Down;

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                Lighting = lighting
            };

            // The Fn layer is behind the LightingLayerCustomization gate (LED ≥ 1.0.44, §3), so the
            // fixture has to name a firmware that passes it or the switch is refused outright.
            var editor = await CreateLoadedEditorAsync(CreateRgbWithLightingFirmware());

            Assert.False(editor.IsDirty);
            Assert.True(editor.Lighting.Layers[1].IsEnabled, "The Fn layer is locked on this fixture.");

            _profiles.SessionToReturn.IsDirty = true;

            editor.Lighting.SelectLayerCommand.Execute(editor.Lighting.Layers[1]);

            Assert.Equal(LightingDirection.Left, lighting.FnLayer.Direction);
            Assert.True(editor.IsDirty, "The lighting layer switch rewrote the direction without saying so.");
        }

        [Fact]
        public async Task IsDirty_AfterALightingLayerSwitchThatChangedNothing_IsNotAnnounced()
        {
            // The other half: a layer whose persisted direction the mode already accepts is not
            // written at all, so switching to it must not report a change the user did not make.
            var lighting = new LightingModel();

            lighting.FnLayer.Mode = LightingMode.Rebound;
            lighting.FnLayer.Direction = LightingDirection.Up;

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                Lighting = lighting
            };

            var editor = await CreateLoadedEditorAsync(CreateRgbWithLightingFirmware());

            _profiles.SessionToReturn.IsDirty = true;

            editor.Lighting.SelectLayerCommand.Execute(editor.Lighting.Layers[1]);

            Assert.Equal(LightingDirection.Up, lighting.FnLayer.Direction);
            Assert.False(editor.IsDirty);
        }

        /// <summary>
        /// A connected Freestyle Edge RGB whose LED firmware clears every §3 gate — the fixture the
        /// Fn lighting layer needs.
        /// </summary>
        private static DeviceSnapshot CreateRgbWithLightingFirmware()
        {
            return TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                versionFile: TestDevices.CreateVersionFile(DeviceId.FreestyleEdgeRgb, "1.0.121", "1.0.58"));
        }

        [Fact]
        public async Task IsDirty_AfterASettingsEdit_StaysFalse()
        {
            // Deliberate, and verified rather than assumed: the settings panel writes the device's
            // *settings* file through its own Save (docs/app/settings.md) and never touches the
            // layout or the lighting model, so it is not a profile-dirty path. The staged flag
            // proves the point — nothing here re-asks the session, because nothing here changed it.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var editor = await CreateLoadedEditorAsync();

            _profiles.SessionToReturn.IsDirty = true;

            var slider = Assert.IsType<SettingsSliderRowViewModel>(
                editor.Settings.Rows.First(row => row is SettingsSliderRowViewModel));

            slider.Value = slider.Maximum;

            Assert.False(editor.IsDirty);
        }

        [Fact]
        public async Task IsDirty_AfterAnImport_ReReadsTheSession()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));

            var editor = await CreateLoadedEditorAsync();

            _profiles.SessionToReturn.IsDirty = true;
            _filePicker.SetFile("layout3.txt", "[F1]>[esc]");

            await editor.ImportCommand.ExecuteAsync(null);

            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task IsDirty_AfterASuccessfulSave_IsCleared()
        {
            // Re-read rather than asserted, since issue #133: a successful save moves the session's
            // OWN baseline to the lines it just wrote (docs/app/profiles.md), so the session reports
            // itself clean and the editor simply asks. That is also what stops a second press of
            // Save rewriting a profile nothing has changed.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.IsDirty);

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.False(editor.IsDirty);
        }

        [Fact]
        public async Task IsDirty_AfterASaveThatValidationStopped_StaysSet()
        {
            // Nothing was written, so nothing may claim the work is safe.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
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
                    ]
                }
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task IsDirty_AfterASaveThatThrew_StaysSet()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
                SaveExceptionToThrow = new IOException("the v-Drive went away")
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task IsDirty_AfterASaveAndThenAnotherEdit_IsSetAgain()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorAsync();

            await editor.SaveCommand.ExecuteAsync(null);

            Assert.False(editor.IsDirty);

            RunMutation(editor, "resetLayout");

            Assert.True(editor.IsDirty);
        }

        // ===== The unsaved-changes guard (issue #52) =========================================
        //
        // This editor had no ConfirmCloseAsync at all: Home ended the session and dropped every
        // unsaved remap, macro and lighting edit without a word, and the amber Save was the only
        // thing between the user and that. Everything below is the one question — shared verbatim
        // with the pedal editor through UnsavedChangesPrompt — and the rule that hangs off it:
        // only a save that actually landed may let the navigation through.

        [Fact]
        public async Task ConfirmCloseAsync_WithNothingUnsaved_LeavesWithoutAsking()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.False(editor.IsDirty);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task ConfirmCloseAsync_InDemoMode_LeavesWithoutAsking()
        {
            // The behaviour is unchanged; the reason is not. Demo mode used to be clean by
            // construction (no session), so it fell out of the `!IsDirty` shortcut for free. Now it
            // is genuinely dirty after an edit and still must not ask: Save can never run there
            // (03 §3.5), so the question would offer an answer that does nothing and a Discard for
            // work that was never going anywhere.
            var editor = CreateEditor(TestDevices.CreateSnapshot(
                DeviceId.FreestyleEdgeRgb,
                VDriveConnectionStatus.CannotAccess));

            await editor.LoadAsync();

            _profiles.SessionToReturn!.IsDirty = true;

            RunMutation(editor, "resetKey");

            Assert.True(editor.IsDirty);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.DoesNotContain(
                _notifications.MessageBoxes,
                request => request.Title == UnsavedChangesPrompt.Title);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WithUnsavedWork_AsksTheDesignsQuestion()
        {
            // Mockup 1f, part for part: the wide card, three answers in Cancel/Discard/Save order,
            // Discard drawn as the one that loses data, and the opt-out that promises the opposite.
            var editor = await CreateDirtyEditorAsync();

            Answer(MessageBoxResult.Cancel);

            Assert.False(await editor.ConfirmCloseAsync());

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(UnsavedChangesPrompt.Title, request.Title);
            Assert.Equal(UnsavedChangesPrompt.KeyboardMessage, request.Message);
            Assert.Equal(MessageBoxIcon.Confirmation, request.Icon);
            Assert.Equal(MessageBoxButtons.YesNoCancel, request.Buttons);
            Assert.Equal(UnsavedChangesPrompt.SaveCaption, request.YesCaption);
            Assert.Equal(UnsavedChangesPrompt.DiscardCaption, request.NoCaption);
            Assert.Equal(UnsavedChangesPrompt.CancelCaption, request.CancelCaption);
            Assert.Equal(MessageBoxResult.No, request.DestructiveResult);
            Assert.True(request.IsWide);

            Assert.Equal(NotificationKeys.UnsavedChanges, request.SuppressionKey);
            Assert.Equal(UnsavedChangesPrompt.SuppressionCaption, request.SuppressionCaption);
            Assert.Equal(MessageBoxResult.Yes, request.SuppressedResult);
            // Narrowed: the promise is only recorded beside the answer that can keep it.
            Assert.Equal(MessageBoxResult.Yes, request.SuppressionResult);

            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserSaves_WritesTheProfileAndLeaves()
        {
            var editor = await CreateDirtyEditorAsync();

            Answer(MessageBoxResult.Yes);

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Equal(1, _profiles.SessionToReturn!.SaveCallCount);
            Assert.False(editor.IsDirty);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserDiscards_LeavesWithoutWriting()
        {
            var editor = await CreateDirtyEditorAsync();

            Answer(MessageBoxResult.No);

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserCancels_StaysInTheEditor()
        {
            var editor = await CreateDirtyEditorAsync();

            Answer(MessageBoxResult.Cancel);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheQuestionIsEscaped_StaysInTheEditor()
        {
            // Escape is not consent to lose work. Neither is a box nobody answered.
            var editor = await CreateDirtyEditorAsync();

            Answer(MessageBoxResult.None);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheQuestionCannotBeShown_StaysInTheEditor()
        {
            // The failure mode this guard exists to prevent, in its purest form: a box that could
            // not be put on screen must not be read as "Discard".
            var editor = await CreateDirtyEditorAsync();

            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no window");

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
            Assert.True(editor.IsDirty);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheSaveThrows_StaysInTheEditor()
        {
            // Failure path 1 of 3. Nothing reached the drive, so letting the navigation through
            // would discard the very work the question was asked about.
            var editor = await CreateDirtyEditorAsync();

            _profiles.SessionToReturn!.SaveExceptionToThrow = new IOException("the v-Drive went away");

            Answer(MessageBoxResult.Yes);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.True(editor.IsDirty);
            Assert.Equal(2, _notifications.MessageBoxes.Count);
            Assert.Equal(KeyboardEditorViewModel.SaveTitle, _notifications.MessageBoxes[1].Title);
            Assert.Contains("the v-Drive went away", _notifications.MessageBoxes[1].Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenValidationStopsTheSave_StaysInTheEditor()
        {
            // Failure path 2 of 3: the write was refused outright (04 §5.3), and the violation
            // dialog is the second box on screen.
            var editor = await CreateDirtyEditorAsync();

            _profiles.SessionToReturn!.ResultToReturn = new ProfileSaveResult
            {
                Success = false,
                Violations =
                [
                    new ModelViolation
                    {
                        Kind = ModelViolationKind.MacroCountExceeded,
                        Message = "The layout holds 120 macros; the device allows 100."
                    }
                ]
            };

            Answer(MessageBoxResult.Yes);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.True(editor.IsDirty);
            Assert.Equal(2, _notifications.MessageBoxes.Count);
            Assert.Contains(
                KeyboardEditorViewModel.SaveRejectedMessage,
                _notifications.MessageBoxes[1].Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheProfileTurnsUnwritableWhileTheQuestionIsUp_StaysInTheEditor()
        {
            // Failure path 3 of 3, and the quiet one: the save's own guard refuses before it starts,
            // reporting nothing at all. The box is modal but the drive is not frozen behind it, so
            // this is reachable — and a silent no-op that let Home through would be the worst of
            // the three.
            var editor = await CreateDirtyEditorAsync();

            _notifications.MessageBoxShowing = _ => _profiles.SessionToReturn!.CanSave = false;

            Answer(MessageBoxResult.Yes);

            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Equal(0, _profiles.SessionToReturn!.SaveCallCount);
            Assert.True(editor.IsDirty);
            Assert.Single(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task ConfirmCloseAsync_ForAProfileThatCanNeverBeSaved_AsksToDiscardInstead()
        {
            // A read-only profile can hold edits it can never write, so the three answers degrade
            // to two: offering Save there would be a question with no working answer, and there is
            // nothing to "always save", so the opt-out goes with it.
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true,
                CanSave = false
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.IsDirty);

            Answer(MessageBoxResult.No);

            Assert.False(await editor.ConfirmCloseAsync());

            var request = Assert.Single(_notifications.MessageBoxes);

            // Its own title and its own body, because the board's says "Saving writes the layout
            // files to the v-Drive" and there is no Save button here to make that true.
            Assert.Equal(UnsavedChangesPrompt.CannotSaveTitle, request.Title);
            Assert.Equal(UnsavedChangesPrompt.CannotSaveMessage, request.Message);
            Assert.DoesNotContain("Saving writes", request.Message, StringComparison.Ordinal);
            Assert.Equal(MessageBoxIcon.Warning, request.Icon);
            Assert.Equal(MessageBoxButtons.YesNo, request.Buttons);
            Assert.Equal(UnsavedChangesPrompt.DiscardCaption, request.YesCaption);
            Assert.Equal(UnsavedChangesPrompt.CancelCaption, request.NoCaption);
            Assert.Equal(MessageBoxResult.Yes, request.DestructiveResult);
            Assert.True(request.IsWide);
            Assert.Null(request.SuppressionKey);
            Assert.False(request.HasSuppressionOption);

            // Yes means Discard here, which is exactly why Interpret has to be told whether saving
            // was on offer.
            Answer(MessageBoxResult.Yes);

            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Equal(0, _profiles.SessionToReturn.SaveCallCount);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhileASaveIsInFlight_RefusesWithAToastInsteadOfAQuestion()
        {
            // Leaving mid-save would dispose this editor while ProfileSession.Save is still
            // writing, and the question would be the wrong one anyway — the changes are being
            // saved, not unsaveable. The refusal speaks, because a live button that does nothing
            // is worse than one that says why.
            var editor = await CreateDirtyEditorAsync();
            var gate = new TaskCompletionSource();

            _profiles.SessionToReturn!.DuringSave = () => gate.Task.Wait();

            var save = editor.SaveCommand.ExecuteAsync(null);

            Assert.True(editor.IsBusy);
            Assert.False(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);

            var toast = Assert.Single(_notifications.Toasts);

            Assert.Equal(UnsavedChangesPrompt.SaveInProgressTitle, toast.Title);
            Assert.Equal(UnsavedChangesPrompt.SaveInProgressMessage, toast.Message);

            gate.SetResult();

            await save;

            // The moment the write finishes there is nothing left to ask about, and leaving works.
            Assert.False(editor.IsDirty);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task ConfirmCloseAsync_WhenTheUserTicksAlwaysSave_TheAnswerReachesThePreferencesScreenAndBack()
        {
            // The end-to-end proof for this prompt's opt-out, through the real NotificationService
            // and the real preferences store: tick the box, and the same store the preferences
            // screen binds to reports the option off — after which leaving saves silently, because
            // "always save on leaving" is what the checkbox promised. Untick it there and the
            // question comes back.
            var presenter = new FakeMessageBoxPresenter
            {
                OutcomeToReturn = new MessageBoxOutcome
                {
                    Result = MessageBoxResult.Yes,
                    SuppressRequested = true
                }
            };

            var store = CreateFilePreferencesStore();

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorWithPreferencesAsync(store, presenter);

            Assert.True(AppPreferenceCatalog.UnsavedChangesWarning.GetValue(store.Current));
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Equal(1, AskedCount(presenter));
            Assert.Equal(1, _profiles.SessionToReturn.SaveCallCount);

            // What the preferences screen shows: the option is now off, and it was written as the
            // spec-08 hide flag rather than as the option itself.
            Assert.False(AppPreferenceCatalog.UnsavedChangesWarning.GetValue(store.Current));
            Assert.True(store.Current.IsUnsavedChangesWarningHidden);
            Assert.Contains(
                KeyValuePair.Create(SettingsKeys.UnsavedChangesMessage, "on"),
                _files.SettingsUpdates);

            // And leaving stops asking — but it saves, which is the difference between this opt-out
            // and every "don't ask this again".
            RunMutation(editor, "resetKey");

            Assert.True(editor.IsDirty);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Equal(1, AskedCount(presenter));
            Assert.Equal(2, _profiles.SessionToReturn.SaveCallCount);

            // Re-enabled the way the preferences screen does it — through the descriptor, never by
            // hand — and the question is back.
            store.Update(settings => AppPreferenceCatalog.UnsavedChangesWarning.SetValue(settings, true));

            RunMutation(editor, "resetKey");

            Assert.True(editor.IsDirty);
            Assert.True(await editor.ConfirmCloseAsync());
            Assert.Equal(2, AskedCount(presenter));
        }

        [Theory]
        [InlineData(MessageBoxResult.No)]
        [InlineData(MessageBoxResult.Cancel)]
        public async Task ConfirmCloseAsync_WhenTheBoxIsTickedBesideAnAnswerThatCannotKeepThePromise_RecordsNothing(
            MessageBoxResult answer)
        {
            // The opt-out reads "always save on leaving", so ticking it and then pressing Discard
            // or Cancel must arm nothing: that would turn one "throw this away" into every future
            // one. The narrowing is NotificationService's policy; this is the call site proving it
            // is asked for.
            var presenter = new FakeMessageBoxPresenter
            {
                OutcomeToReturn = new MessageBoxOutcome
                {
                    Result = answer,
                    SuppressRequested = true
                }
            };

            var store = CreateFilePreferencesStore();

            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorWithPreferencesAsync(store, presenter);

            await editor.ConfirmCloseAsync();

            Assert.True(AppPreferenceCatalog.UnsavedChangesWarning.GetValue(store.Current));
            Assert.DoesNotContain(
                KeyValuePair.Create(SettingsKeys.UnsavedChangesMessage, "on"),
                _files.SettingsUpdates);

            // Still dirty either way, so the very next attempt asks again rather than saving behind
            // the user's back.
            Assert.True(editor.IsDirty);

            await editor.ConfirmCloseAsync();

            Assert.Equal(2, AskedCount(presenter));
        }

        [Fact]
        public async Task Layers_CarryTheirShortcutHint()
        {
            var editor = await CreateLoadedEditorAsync();

            Assert.Equal(
                new[]
                {
                    KeyboardLayerViewModel.BuildShortcutHint(0, KeyCaption.IsMacOs),
                    KeyboardLayerViewModel.BuildShortcutHint(1, KeyCaption.IsMacOs)
                },
                editor.Layers.Select(layer => layer.ShortcutHint));

            // The mark is a flag, not a character in the string — U+2325 is in neither embedded
            // IBM Plex family, so the view draws `IconOption` beside the number instead.
            Assert.All(editor.Layers, layer => Assert.Equal(KeyCaption.IsMacOs, layer.ShowsOptionMark));
            Assert.All(editor.Layers, layer => Assert.DoesNotContain('⌥', layer.ShortcutHint));
        }

        [Theory]
        [InlineData(0, "1", "Alt+1")]
        [InlineData(1, "2", "Alt+2")]
        [InlineData(4, "5", "Alt+5")]
        public void BuildShortcutHint_SpellsTheNumberAloneOnMacOs_AndAltElsewhere(
            int layerIndex,
            string expectedOnMac,
            string expectedElsewhere)
        {
            // Both branches, on one machine: `isMacOs` is a parameter for exactly this reason. On
            // macOS the ⌥ in front of the number is drawn by the view, so the text is the bare
            // number; everywhere else the Option key IS Alt (handoff.md § "Interactions": "map
            // ⌘→Ctrl and ⌥→Alt on Windows/Linux") and the legend spells it out.
            Assert.Equal(expectedOnMac, KeyboardLayerViewModel.BuildShortcutHint(layerIndex, isMacOs: true));
            Assert.Equal(expectedElsewhere, KeyboardLayerViewModel.BuildShortcutHint(layerIndex, isMacOs: false));
            Assert.StartsWith(
                KeyboardLayerViewModel.ShortcutPrefix,
                KeyboardLayerViewModel.BuildShortcutHint(layerIndex, isMacOs: false),
                StringComparison.Ordinal);
        }

        /// <summary>
        /// Records one keystroke into <paramref name="key"/>'s macro through the key inspector's
        /// Macro panel — the app's one macro editor since issue #93. The Macros tab is a library and
        /// records nothing.
        /// </summary>
        private void RecordAMacro(KeyboardEditorViewModel editor, KeyboardKeyViewModel key)
        {
            editor.SelectKeyCommand.Execute(key);

            foreach (var tab in editor.Inspector.Tabs)
            {
                if (tab.Mode == KeyInspectorMode.Macro)
                {
                    editor.Inspector.SelectModeCommand.Execute(tab);
                }
            }

            var panel = Assert.IsType<MacroInspectorPanelViewModel>(editor.Inspector.ActivePanel);

            panel.RecordCommand.Execute(null);

            _capture.RaiseKeystroke(KeyRegistry.FindByToken("a", TokenDialect.Gen1)!);

            panel.Deactivate();
        }

        private void RunMutation(KeyboardEditorViewModel editor, string path)
        {
            // The session's own answer is STAGED, and staged here so every caller gets it: the fake
            // does not derive IsDirty from the model, and since issue #133 a successful Save clears
            // it (Core moves the baseline to the lines it wrote), so an edit made after a save has
            // to re-stage it. Set before the path runs, which is what leaves the assertion
            // afterwards meaning "the path re-asked the session" and nothing weaker.
            if (_profiles.SessionToReturn is { } session)
            {
                session.IsDirty = true;
            }

            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            switch (path)
            {
                case "remap":
                    editor.SelectKeyCommand.Execute(key);
                    editor.BeginRemapCommand.Execute(null);
                    _capture.RaiseKeystroke(KeyRegistry.FindByToken("esc", TokenDialect.Gen1)!);
                    break;
                case "resetKey":
                    editor.SelectKeyCommand.Execute(key);
                    editor.ResetKeyCommand.Execute(null);
                    break;
                case "resetLayer":
                    // Both scopes now confirm first, and the fake answers Ok by default, which is
                    // not the Yes the guard waits for.
                    ConfirmTheNextReset();
                    editor.ResetLayerCommand.Execute(null);
                    break;
                case "resetLayout":
                    ConfirmTheNextReset();
                    editor.ResetLayoutCommand.Execute(null);
                    break;
                case "macroAssign":
                    // The app's one macro editor is the key inspector's Macro panel (issue #93), so
                    // a macro is made by recording one there.
                    RecordAMacro(editor, key);
                    break;
                case "lightingMode":
                    editor.Lighting.SelectModeCommand.Execute(
                        editor.Lighting.Modes.Single(mode => mode.Mode == LightingMode.Wave));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown mutation path.");
            }
        }

        [Fact]
        public async Task ResetLayerCommand_AsksBeforeItErasesAnything()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            ConfirmTheNextReset();

            editor.ResetLayerCommand.Execute(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.ResetLayerTitle, request.Title);
            Assert.Equal(KeyboardEditorViewModel.ResetLayerConfirmation, request.Message);
            Assert.Equal(MessageBoxIcon.Confirmation, request.Icon);
            Assert.Equal(MessageBoxButtons.YesNo, request.Buttons);
            Assert.Equal(KeyboardEditorViewModel.ResetLayerConfirmCaption, request.YesCaption);
            Assert.Equal(KeyboardEditorViewModel.ResetDeclineCaption, request.NoCaption);

            // The whole point of the guard: it is the first production box that can be switched
            // off, and a switched-off one means "go ahead" rather than "do nothing".
            Assert.Equal(NotificationKeys.ResetLayer, request.SuppressionKey);
            Assert.True(request.HasSuppressionOption);
            Assert.Equal(MessageBoxResult.Yes, request.SuppressedResult);

            Assert.False(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task ResetLayerCommand_WhenTheConfirmationIsDeclined_ErasesNothing()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            RecordAMacro(editor, editor.SelectedLayer!.Keys[TestLayouts.RgbDigitTwoKeyIndex]);

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            editor.ResetLayerCommand.Execute(null);

            Assert.Single(_notifications.MessageBoxes);
            Assert.True(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
            Assert.Equal(1, editor.MacroCount);
        }

        [Fact]
        public async Task ResetLayoutCommand_AsksUnderTheSameKeyWithItsOwnWording()
        {
            // One preference governs both scopes, so the sentence is the only thing that says
            // which of them is about to run — and it has to say it.
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            ConfirmTheNextReset();

            editor.ResetLayoutCommand.Execute(null);

            var request = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(KeyboardEditorViewModel.ResetLayoutTitle, request.Title);
            Assert.Equal(KeyboardEditorViewModel.ResetLayoutConfirmation, request.Message);
            Assert.Equal(NotificationKeys.ResetLayer, request.SuppressionKey);
            Assert.NotEqual(KeyboardEditorViewModel.ResetLayerConfirmation, request.Message);
            Assert.Equal(0, editor.ModifiedKeyCount);
        }

        [Fact]
        public async Task ResetLayoutCommand_WhenTheConfirmationIsDeclined_ErasesNothing()
        {
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));
            editor.Layout.Layers[1].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            editor.ResetLayoutCommand.Execute(null);

            Assert.All(
                editor.Layout.Layers,
                layer => Assert.True(layer.Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified));
        }

        [Fact]
        public async Task ResetLayerCommand_WhenTheConfirmationCannotBeShown_ErasesNothing()
        {
            // A box that cannot be put on screen is not consent (the rule the lighting tab's
            // Reset All already follows), and it must not bring the editor down either.
            var editor = await CreateLoadedEditorAsync();

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no window");

            editor.ResetLayerCommand.Execute(null);

            Assert.True(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [Fact]
        public async Task ResetKeyCommand_StillAsksNothing()
        {
            // Deliberate asymmetry, pinned so it cannot become an accident: the catalog's
            // reset confirmation is "Confirm before resetting a layer", and the spec's other reset
            // flag (reset_key_msg) guards resetting a *macro*, which this command does not do —
            // it drops one position's remap.
            var editor = await CreateLoadedEditorAsync();
            var key = editor.SelectedLayer!.Keys[TestLayouts.RgbDigitOneKeyIndex];

            key.Key.ApplyRemap(TestLayouts.Gen1Key("z"));
            key.RefreshFromModel();

            editor.SelectKeyCommand.Execute(key);
            editor.ResetKeyCommand.Execute(null);

            Assert.Empty(_notifications.MessageBoxes);
            Assert.False(key.IsModified);
        }

        [Fact]
        public async Task ResetLayerCommand_WhenTheConfirmationIsHidden_ErasesWithoutAsking()
        {
            // Through the real NotificationService, because the short-circuit is its policy: the
            // editor never reads IsHidden itself, or there would be two policies for one decision.
            var presenter = new FakeMessageBoxPresenter();
            var store = new FakeAppPreferencesStore();

            store.SetInitiallyHidden(NotificationKeys.ResetLayer);

            var editor = await CreateLoadedEditorWithPreferencesAsync(store, presenter);

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            editor.ResetLayerCommand.Execute(null);

            Assert.Empty(presenter.Requests);
            Assert.False(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [Fact]
        public async Task ResetLayerCommand_WhenTheUserTicksDontAskAgain_TheAnswerReachesThePreferencesScreenAndBack()
        {
            // The end-to-end proof of the suppression pipeline, which had no production call site
            // before this: tick the box, and the same store the preferences screen binds to reports
            // the option off — then untick it there and the confirmation comes back.
            var presenter = new FakeMessageBoxPresenter
            {
                OutcomeToReturn = new MessageBoxOutcome
                {
                    Result = MessageBoxResult.Yes,
                    SuppressRequested = true
                }
            };

            var location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb);

            _files.SetFile(VDriveAppPreferencesStore.GetFilePath(location));

            var store = new VDriveAppPreferencesStore(TestDevices.CreateSettingsService(_files), location);
            var editor = await CreateLoadedEditorWithPreferencesAsync(store, presenter);

            Assert.True(AppPreferenceCatalog.ResetLayerConfirmation.GetValue(store.Current));

            editor.Layout!.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            editor.ResetLayerCommand.Execute(null);

            Assert.Single(presenter.Requests);
            Assert.False(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);

            // What the preferences screen shows: the option is now off, and it was written as the
            // spec-08 hide flag rather than as the option itself.
            Assert.False(AppPreferenceCatalog.ResetLayerConfirmation.GetValue(store.Current));
            Assert.True(store.Current.IsResetLayerConfirmationHidden);
            Assert.Contains(
                KeyValuePair.Create(SettingsKeys.ResetLayerMessage, "on"),
                _files.SettingsUpdates);

            // And the reset stops asking.
            editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            editor.ResetLayerCommand.Execute(null);

            Assert.Single(presenter.Requests);
            Assert.False(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);

            // Re-enabled the way the preferences screen does it — through the descriptor, never by
            // hand — and the question is back.
            store.Update(settings => AppPreferenceCatalog.ResetLayerConfirmation.SetValue(settings, true));

            editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].ApplyRemap(TestLayouts.Gen1Key("z"));

            editor.ResetLayerCommand.Execute(null);

            Assert.Equal(2, presenter.Requests.Count);
            Assert.False(editor.Layout.Layers[0].Keys[TestLayouts.RgbDigitOneKeyIndex].IsModified);
        }

        [AvaloniaTheory]
        [InlineData("WidthInspectorRail", HostPreferences.DefaultInspectorRailWidth)]
        [InlineData("WidthInspectorRailMin", HostPreferences.MinimumInspectorRailWidth)]
        [InlineData("WidthInspectorRailMax", HostPreferences.MaximumInspectorRailWidth)]
        [InlineData("WidthInspectorRailWide", KeyboardEditorViewModel.MacroInspectorRailWidth)]
        public void TheRailsWidths_AreWrittenTwice_AndMustAgree(string token, double expected)
        {
            // The splitter bounds the drag with the geometry tokens and the view model clamps with
            // plain C# constants, because a view model may not read Avalonia resources (app-shell.md
            // invariant 8). Two copies of four numbers is the price of that rule; this is what stops
            // them drifting apart in silence.
            Assert.Equal(expected, (double)DesignTokens.Resolve(token, ThemeVariant.Dark));
        }

        [Fact]
        public void InspectorRailWidth_WithNothingStored_IsTheAuthoredWidth()
        {
            var editor = CreateEditor();

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, editor.InspectorRailWidth);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, editor.EffectiveInspectorRailWidth);
        }

        [Fact]
        public void InspectorRailWidth_WithAStoredWidth_OpensAtIt()
        {
            // The acceptance criterion "the chosen width survives an app restart": a new editor over
            // a store that already carries one starts there rather than at the authored width.
            var editor = CreateEditor(new FakeHostPreferencesStore(
                HostPreferences.Default with { InspectorRailWidth = 396 }));

            Assert.Equal(396, editor.InspectorRailWidth);
        }

        [Fact]
        public void InspectorRailWidth_WithAStoredWidthOutsideTheBand_OpensClamped()
        {
            // Belt to the parser's brace: a store handed a hand-built record has never been through
            // the tolerant read at all.
            var editor = CreateEditor(new FakeHostPreferencesStore(
                HostPreferences.Default with { InspectorRailWidth = 9999 }));

            Assert.Equal(HostPreferences.MaximumInspectorRailWidth, editor.InspectorRailWidth);
        }

        [Theory]
        [InlineData(396, 396)]
        [InlineData(9999, HostPreferences.MaximumInspectorRailWidth)]
        [InlineData(10, HostPreferences.MinimumInspectorRailWidth)]
        public void InspectorRailWidth_WhenSet_IsClampedAndPersisted(double dragged, double expected)
        {
            var preferences = new FakeHostPreferencesStore();
            var editor = CreateEditor(preferences);

            editor.InspectorRailWidth = dragged;

            Assert.Equal(expected, editor.InspectorRailWidth);
            Assert.Equal(expected, preferences.Current.InspectorRailWidth);
        }

        [Fact]
        public void InspectorRailWidth_SetToANumberThatIsNotOne_FallsBackToTheAuthoredWidth()
        {
            // Math.Clamp would propagate the NaN onto a column definition and take the Layout tab's
            // whole measure pass with it. Started from a stored width so the fallback is a real
            // move rather than a no-op.
            var preferences = new FakeHostPreferencesStore(
                HostPreferences.Default with { InspectorRailWidth = 400 });

            var editor = CreateEditor(preferences);

            editor.InspectorRailWidth = double.NaN;

            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, editor.InspectorRailWidth);
            Assert.Equal(HostPreferences.DefaultInspectorRailWidth, preferences.Current.InspectorRailWidth);
        }

        [Fact]
        public void InspectorRailWidth_SetToTheWidthItAlreadyHas_WritesNothing()
        {
            // The "do not debounce" rule, from the other end: the store writes synchronously on
            // every real change, so a drag that reports continuously must cost one write at its
            // commit rather than one per pointer move.
            var preferences = new FakeHostPreferencesStore();
            var editor = CreateEditor(preferences);
            var notifications = 0;

            editor.InspectorRailWidth = 360;

            editor.PropertyChanged += (_, _) => notifications++;

            editor.InspectorRailWidth = 360;
            editor.InspectorRailWidth = 360;

            Assert.Equal(1, preferences.UpdateCount);
            Assert.Equal(0, notifications);
        }

        [Fact]
        public void InspectorRailWidth_SetRepeatedlyPastTheMaximum_WritesOnce()
        {
            // The same guard where it matters most: a drag held against the edge reports a new
            // number every frame and every one of them clamps to the same width.
            var preferences = new FakeHostPreferencesStore();
            var editor = CreateEditor(preferences);

            editor.InspectorRailWidth = 700;
            editor.InspectorRailWidth = 800;
            editor.InspectorRailWidth = 900;

            Assert.Equal(1, preferences.UpdateCount);
            Assert.Equal(HostPreferences.MaximumInspectorRailWidth, preferences.Current.InspectorRailWidth);
        }

        [Fact]
        public void InspectorRailWidth_WithNoHostStore_StillMovesAndClamps()
        {
            // An editor built without the store — a design scene, most of this suite — is a rail
            // that drags and forgets, never one that refuses to drag.
            var editor = CreateEditor();

            editor.InspectorRailWidth = 9999;

            Assert.Equal(HostPreferences.MaximumInspectorRailWidth, editor.InspectorRailWidth);
        }

        [Fact]
        public void InspectorRailWidth_WhenItMoves_AnnouncesTheEffectiveWidthToo()
        {
            var editor = CreateEditor();
            var announced = new List<string?>();

            editor.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            editor.InspectorRailWidth = 400;

            Assert.Contains(nameof(KeyboardEditorViewModel.InspectorRailWidth), announced);
            Assert.Contains(nameof(KeyboardEditorViewModel.EffectiveInspectorRailWidth), announced);
        }

        [Fact]
        public void InspectorRailWidth_SetOutsideTheBand_IsPushedBackAtTheBinding()
        {
            // The column is bound two-way. A width the clamp moved has to be announced even when
            // the field did not change, or the splitter would go on drawing a width the model
            // refused while the model says otherwise.
            var editor = CreateEditor();

            editor.InspectorRailWidth = 9999;

            var announced = new List<string?>();

            editor.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            editor.InspectorRailWidth = 10000;

            Assert.Contains(nameof(KeyboardEditorViewModel.InspectorRailWidth), announced);
        }

        [Fact]
        public async Task EffectiveInspectorRailWidth_WhileTheMacroPanelShows_NeverGoesBelowTheMacroWidth()
        {
            // The handoff's 300 is honoured as a FLOOR: a rail dragged narrow still gives macro
            // editing the room its step list needs.
            var editor = await CreateLoadedEditorAsync();

            editor.InspectorRailWidth = HostPreferences.MinimumInspectorRailWidth;

            SelectMacroMode(editor);

            Assert.True(editor.Inspector.IsWide);
            Assert.Equal(KeyboardEditorViewModel.MacroInspectorRailWidth, editor.EffectiveInspectorRailWidth);
            Assert.Equal(HostPreferences.MinimumInspectorRailWidth, editor.InspectorRailWidth);
        }

        [Fact]
        public async Task EffectiveInspectorRailWidth_WhenTheUserDraggedWider_KeepsTheDraggedWidth()
        {
            // ...and it is a floor rather than an override, which is the deviation issue #119
            // recorded: a user who dragged to 460 is not yanked back to 300 by opening a macro.
            var editor = await CreateLoadedEditorAsync();

            editor.InspectorRailWidth = 460;

            SelectMacroMode(editor);

            Assert.Equal(460, editor.EffectiveInspectorRailWidth);
        }

        [Fact]
        public async Task EffectiveInspectorRailWidth_IsAnnounced_WhenTheRailChangesMode()
        {
            // Its second input is the rail's, and the rail announces it — so the editor subscribes.
            var editor = await CreateLoadedEditorAsync();
            var announced = new List<string?>();

            SelectKey(editor);

            editor.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            SelectMode(editor, KeyInspectorMode.Macro);

            Assert.Contains(nameof(KeyboardEditorViewModel.EffectiveInspectorRailWidth), announced);
        }

        [Fact]
        public void Rail_IsTheVeryObjectTheLightingTabResizes()
        {
            // Issue #124's "one rail, one width, both tabs": the mode rail is not a second column
            // with a second preference, it is the same column with different content. Asserted as
            // reference identity because that is the mechanism — anything weaker would pass over two
            // objects that merely happen to agree at construction.
            var editor = CreateEditor();

            Assert.Same(editor.Rail, editor.Lighting.Rail);

            editor.InspectorRailWidth = 420;

            Assert.Equal(420, editor.Lighting.Rail.Width);

            editor.Lighting.Rail.Width = 340;

            Assert.Equal(340, editor.InspectorRailWidth);
        }

        [Fact]
        public async Task Rail_WhileTheMacroPanelShows_RaisesTheFloorForTheKeysTabOnly()
        {
            // THE LIGHTING TAB MUST NOT INHERIT THE 300 PX FLOOR. It exists for the macro panel's
            // step list, and the Lighting tab has no macro panel — a rail widened there would be
            // widened for a reason that does not exist on that tab. The Keys tab binds
            // EffectiveWidth, the Lighting tab binds Width, and this is the difference.
            var editor = await CreateLoadedEditorAsync();

            editor.InspectorRailWidth = HostPreferences.MinimumInspectorRailWidth;

            SelectMacroMode(editor);

            Assert.Equal(KeyboardEditorViewModel.MacroInspectorRailWidth, editor.EffectiveInspectorRailWidth);
            Assert.Equal(HostPreferences.MinimumInspectorRailWidth, editor.Lighting.Rail.Width);
        }

        [Fact]
        public async Task Dispose_StopsListeningToTheRailsWidthInputs()
        {
            var editor = await CreateLoadedEditorAsync();

            SelectKey(editor);
            SelectMode(editor, KeyInspectorMode.Remap);

            editor.Dispose();

            var announced = new List<string?>();

            editor.PropertyChanged += (_, e) => announced.Add(e.PropertyName);

            SelectMode(editor, KeyInspectorMode.Macro);

            Assert.DoesNotContain(nameof(KeyboardEditorViewModel.EffectiveInspectorRailWidth), announced);
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

        private async Task<KeyboardEditorViewModel> CreateLoadedEditorAsync(DeviceSnapshot? snapshot = null)
        {
            var editor = CreateEditor(snapshot);

            await editor.LoadAsync();

            return editor;
        }

        private KeyboardEditorViewModel CreateEditor(DeviceSnapshot? snapshot = null)
        {
            return CreateEditor(snapshot, _notifications, sessions: null);
        }

        /// <summary>An editor over a per-user store — the one that remembers the rail's width.</summary>
        private KeyboardEditorViewModel CreateEditor(IHostPreferencesStore hostPreferences)
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
                _urlLauncher,
                sessions: null,
                motionSettings: null,
                hostPreferences: hostPreferences);

            _editors.Add(editor);

            return editor;
        }

        /// <summary>Selects an editable position, which is what opens the rail on its tabs.</summary>
        private static void SelectKey(KeyboardEditorViewModel editor)
        {
            var layer = editor.SelectedLayer
                ?? throw new InvalidOperationException("The editor rendered no layer.");

            editor.SelectKeyCommand.Execute(layer.Keys[TestLayouts.RgbDigitOneKeyIndex]);
        }

        /// <summary>Puts the open rail on <paramref name="mode"/> through its own tab command.</summary>
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

            throw new InvalidOperationException($"The key inspector has no {mode} tab.");
        }

        /// <summary>Selects a position and puts the rail on the one mode that wants a wide rail.</summary>
        private static void SelectMacroMode(KeyboardEditorViewModel editor)
        {
            SelectKey(editor);
            SelectMode(editor, KeyInspectorMode.Macro);
        }

        private KeyboardEditorViewModel CreateEditor(
            DeviceSnapshot? snapshot,
            INotificationService notifications,
            IDeviceSessionAccessor? sessions)
        {
            var editor = new KeyboardEditorViewModel(
                snapshot ?? TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb),
                _profiles,
                _settings,
                _capture,
                notifications,
                _folderPicker,
                _filePicker,
                _files,
                _urlLauncher,
                sessions);

            _editors.Add(editor);

            return editor;
        }

        /// <summary>
        /// An editor over <paramref name="store"/>, wired to the <b>real</b>
        /// <see cref="NotificationService"/> so that the suppression policy under test is the
        /// app's own rather than a fake's.
        /// </summary>
        private async Task<KeyboardEditorViewModel> CreateLoadedEditorWithPreferencesAsync(
            IAppPreferencesStore store,
            IMessageBoxPresenter presenter)
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb);
            var sessions = new StubSessionAccessor(new DeviceSession(snapshot, store));
            var editor = CreateEditor(snapshot, new NotificationService(presenter, sessions), sessions);

            await editor.LoadAsync();

            return editor;
        }

        /// <summary>Answers the reset confirmation with its affirmative; the fake says Ok by default.</summary>
        private void ConfirmTheNextReset()
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Yes };
        }

        /// <summary>Answers whatever box goes up next with <paramref name="result"/>.</summary>
        private void Answer(MessageBoxResult result)
        {
            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = result };
        }

        /// <summary>
        /// A loaded editor whose session reports itself dirty — the state every unsaved-changes
        /// test starts from. The flag is staged on the fake rather than produced by an edit,
        /// because what is under test is the guard, not the paths that set the flag (those have
        /// their own theory above).
        /// </summary>
        private async Task<KeyboardEditorViewModel> CreateDirtyEditorAsync()
        {
            _profiles.SessionToReturn = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb))
            {
                IsDirty = true
            };

            var editor = await CreateLoadedEditorAsync();

            Assert.True(editor.IsDirty);

            return editor;
        }

        /// <summary>
        /// A preferences store over the fake drive's own <c>app_settings.txt</c>, so an opt-out can
        /// be followed all the way to the line that is written.
        /// </summary>
        private VDriveAppPreferencesStore CreateFilePreferencesStore()
        {
            var location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb);

            _files.SetFile(VDriveAppPreferencesStore.GetFilePath(location));

            return new VDriveAppPreferencesStore(TestDevices.CreateSettingsService(_files), location);
        }

        /// <summary>
        /// How often the unsaved-changes question actually reached the screen. Counted by title
        /// rather than by list length, because an editor raises other boxes — the reset
        /// confirmation, a save failure — through the same presenter.
        /// </summary>
        private static int AskedCount(FakeMessageBoxPresenter presenter)
        {
            return presenter.Requests.Count(request => request.Title == UnsavedChangesPrompt.Title);
        }

        public void Dispose()
        {
            foreach (var editor in _editors)
            {
                editor.Dispose();
            }
        }

        private sealed class StubSessionAccessor : IDeviceSessionAccessor
        {
            public DeviceSession? Active { get; }

            public StubSessionAccessor(DeviceSession? active)
            {
                Active = active;
            }
        }
    }
}
