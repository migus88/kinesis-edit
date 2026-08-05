using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public sealed class MainWindowViewModelTests : IDisposable
    {
        private static readonly TimeSpan _neverPolls = TimeSpan.FromHours(1);

        private readonly FakeVDriveScanner _scanner = new();
        private readonly FakeVDriveFileService _fileService = new();
        private readonly FakeDeviceEjectService _ejectService = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeProfileSessionFactory _profiles = new();
        private readonly FakeKeystrokeCaptureService _capture = new();
        private readonly ISettingsService _settings;
        private readonly DeviceSessionManager _sessions;
        private readonly DeviceMonitorService _monitor;
        private readonly DashboardViewModel _dashboard;
        private readonly MainWindowViewModel _shell;

        public MainWindowViewModelTests()
        {
            var ejectNotifier = new VDriveEjectNotifier(_ejectService, _notifications);
            _settings = TestDevices.CreateSettingsService(_fileService);
            _sessions = new DeviceSessionManager(_settings);
            _monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _fileService,
                new FakeUiDispatcher(),
                _neverPolls);

            // The real factory over fake collaborators: which editor a device resolves to is part
            // of what OpenDevice has to get right.
            var editors = new EditorViewModelFactory(
                _profiles,
                _settings,
                () => _capture,
                _notifications,
                new PedalFileService(_fileService));

            _dashboard = new DashboardViewModel(_monitor, ejectNotifier, new FakeFirmwareUpdatePresenter(), new FakeUrlLauncher());
            _shell = new MainWindowViewModel(_dashboard, _monitor, _sessions, _notifications, ejectNotifier, editors);
        }

        [Fact]
        public void CurrentView_Initially_IsTheDashboard()
        {
            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_shell.Editor);
            Assert.False(_shell.IsEditorOpen);
            Assert.False(_shell.HomeCommand.CanExecute(null));
        }

        [Fact]
        public void OpenDevice_WithConnectedDevice_SwapsInTheEditorForThatSession()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);

            _shell.OpenDevice(snapshot);

            Assert.Same(_shell.Editor, _shell.CurrentView);
            Assert.Equal("TKO", _shell.Editor!.DeviceName);
            Assert.False(_shell.Editor.IsDemoMode);
            Assert.Same(snapshot, _sessions.Active!.Device);
            Assert.True(_shell.HomeCommand.CanExecute(null));
        }

        [Fact]
        public void OpenDevice_WithADeviceThatHasAnAuthoredPicture_SwapsInTheKeyboardEditor()
        {
            // Demo mode on purpose: the editor's own load then builds its model in memory instead
            // of racing this test through the profile factory.
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));

            var editor = Assert.IsType<KeyboardEditorViewModel>(_shell.Editor);

            Assert.Same(editor, _shell.CurrentView);
            Assert.Equal("Freestyle Edge RGB", editor.DeviceName);
            Assert.True(editor.IsDemoMode);
        }

        [Fact]
        public void OpenDevice_WithTheSavantElite2_SwapsInTheReadOnlyPedalView()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");

            _shell.OpenDevice(snapshot);

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_shell.Editor);

            Assert.Same(pedal, _shell.CurrentView);
            Assert.Equal(PedalLoadState.Loaded, pedal.LoadState);
            Assert.Equal(7, pedal.Inputs.Count);
            Assert.Equal("[lmouse]", pedal.Inputs[0].AssignmentText);
            Assert.Same(snapshot, _sessions.Active!.Device);
            Assert.True(_shell.IsEditorOpen);
            Assert.True(_shell.HomeCommand.CanExecute(null));
        }

        [Fact]
        public void OpenDevice_WithTheSavantElite2InDemoMode_ReadsNothingFromTheDrive()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2, VDriveConnectionStatus.CannotAccess);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");

            _shell.OpenDevice(snapshot);

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_shell.Editor);

            Assert.Equal(0, _fileService.ReadCount);
            Assert.Equal(PedalLoadState.DemoMode, pedal.LoadState);
            Assert.True(_shell.IsDemoMode);
        }

        [Fact]
        public async Task HomeCommand_AfterOpeningTheSavantElite2_ClosesTheEditorAndEjects()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");
            _shell.OpenDevice(snapshot);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_sessions.Active);
            Assert.Equal(snapshot.Location!.RootPath, Assert.Single(_ejectService.EjectedPaths));
        }

        [Fact]
        public async Task OpenDevice_WithTheKeyboardEditor_FiresItsLoadExactlyOnce()
        {
            // The shell is device-agnostic: it fires DeviceEditorViewModel.LoadAsync and forgets
            // it, which is the only reason the keyboard editor ever reads its profile.
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            var editor = Assert.IsType<KeyboardEditorViewModel>(_shell.Editor);

            await WaitForLoadAsync(editor);

            Assert.Equal(1, _profiles.LoadCallCount);
            Assert.NotNull(editor.Layout);
            Assert.NotEmpty(editor.Layers);
        }

        [Fact]
        public void OpenDevice_WithADeviceThatHasNoEditorOfItsOwn_SwapsInThePlaceholder()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.IsType<EditorPlaceholderViewModel>(_shell.Editor);

            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.IsType<EditorPlaceholderViewModel>(_shell.Editor);
        }

        [Fact]
        public async Task HomeCommand_WithTheKeyboardEditorOpen_DisposesItAndStopsCapture()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));

            var editor = Assert.IsType<KeyboardEditorViewModel>(_shell.Editor);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Equal(1, _capture.StopCount);
            Assert.False(_capture.HasSubscribers);

            // Disposal is idempotent, so the shell may safely have done it already.
            editor.Dispose();

            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public void OpenDevice_TwiceInARow_DisposesTheEditorItReplaces()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.IsType<EditorPlaceholderViewModel>(_shell.Editor);
            Assert.False(_capture.HasSubscribers);
            Assert.Equal(1, _capture.StopCount);
        }

        [Fact]
        public void OpenDevice_WhenConfiguring_ShowsAndHidesTheDeviceLoadingCaption()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Equal(new string?[] { "Loading TKO...", null }, _notifications.LoadingHistory);
            Assert.Null(_notifications.LoadingCaption);
        }

        [Fact]
        public void OpenDevice_WithUnwritableDrive_EntersDemoMode()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Advantage2, VDriveConnectionStatus.CannotAccess));

            Assert.True(_shell.IsDemoMode);
            Assert.True(_shell.Editor!.IsDemoMode);
            Assert.Equal("Demo Mode", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Warning, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void ConfigureRequested_FromTheDashboard_OpensTheDevice()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Refresh();

            _dashboard.Devices[0].ConfigureCommand.Execute(null);

            Assert.NotNull(_shell.Editor);
            Assert.Equal(DeviceId.Tko, _shell.Editor!.Device.DeviceId);
        }

        [Fact]
        public async Task HomeCommand_AfterConfiguringAConnectedDevice_ClosesTheEditorAndEjects()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            _shell.OpenDevice(snapshot);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_sessions.Active);
            Assert.Equal(snapshot.Location!.RootPath, Assert.Single(_ejectService.EjectedPaths));
        }

        [Fact]
        public async Task HomeCommand_InDemoMode_DoesNotEject()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess));

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Empty(_ejectService.EjectedPaths);
            Assert.Empty(_notifications.Toasts);
            Assert.Null(_shell.Editor);
            Assert.False(_shell.IsDemoMode);
        }

        [Fact]
        public async Task HomeCommand_WhenTheEditorCloses_ShowsTheDashboardWithoutInterruptingPolling()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Start();
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.True(_monitor.IsPolling);
            Assert.Single(_dashboard.Devices);
            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Ok, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void OpenDevice_WhileEditing_KeepsTheDetectionLoopRunning()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Start();

            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            // specs/10-apps-and-ui.md: the open editor "re-verifies the device's version file
            // every tick" to drive its status indicator, so the loop cannot be stopped for the
            // session.
            Assert.True(_monitor.IsPolling);
        }

        [Fact]
        public void StatusIndicator_WhenTheVersionFileStopsBeingReadableMidSession_FlipsToVDriveError()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Refresh();
            _shell.OpenDevice(_dashboard.Devices[0].Snapshot);
            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);

            _fileService.SetUnreadable(TestDevices.CreateLocation(DeviceId.Tko).VersionFilePath);
            _monitor.Refresh();

            Assert.Equal("v-Drive Error", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Error, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_WhenTheVersionFileRecoversMidSession_FlipsBackToVDriveOk()
        {
            SetDrive(DeviceId.Tko);
            _fileService.SetUnreadable(TestDevices.CreateLocation(DeviceId.Tko).VersionFilePath);
            _monitor.Refresh();
            _shell.OpenDevice(_dashboard.Devices[0].Snapshot);
            Assert.Equal("v-Drive Error", _shell.StatusIndicatorText);

            SetDrive(DeviceId.Tko);
            _monitor.Refresh();

            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Ok, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_WhenTheDriveDisappearsMidSession_ReportsVDriveError()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Refresh();
            _shell.OpenDevice(_dashboard.Devices[0].Snapshot);

            _scanner.SetResult();
            _monitor.Refresh();

            Assert.Equal("v-Drive Error", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Error, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_WhenADemoSessionsDriveBecomesWritable_StaysInDemoMode()
        {
            SetDrive(DeviceId.Tko, isWritable: false);
            _monitor.Refresh();
            _shell.OpenDevice(_dashboard.Devices[0].Snapshot);

            SetDrive(DeviceId.Tko);
            _monitor.Refresh();

            // Demo mode is decided once, when Configure opens the device (specs/10-apps-and-ui.md).
            Assert.True(_shell.IsDemoMode);
            Assert.True(_sessions.Active!.IsDemoMode);
            Assert.Equal("Demo Mode", _shell.StatusIndicatorText);
            Assert.IsType<ReadOnlyNotificationSuppressionStore>(_sessions.Active.SuppressionStore);
        }

        [Fact]
        public async Task OpenDevice_WhileHomeIsEjecting_IsIgnored()
        {
            var gate = new TaskCompletionSource();
            _ejectService.Gate = gate;
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            var home = _shell.HomeCommand.ExecuteAsync(null);
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.True(_shell.IsBusy);
            Assert.Null(_shell.Editor);
            Assert.Null(_sessions.Active);
            Assert.False(_shell.HomeCommand.CanExecute(null));

            gate.SetResult();
            await home;

            Assert.False(_shell.IsBusy);
            Assert.Null(_shell.Editor);
            Assert.Same(_dashboard, _shell.CurrentView);
        }

        [Fact]
        public void StatusIndicator_WithoutAnyDrive_ReportsDemoMode()
        {
            _monitor.Refresh();

            Assert.Equal("Demo Mode", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Warning, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_WithAConnectedHealthyDrive_ReportsVDriveOk()
        {
            SetDrive(DeviceId.Tko);

            _monitor.Refresh();

            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Ok, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_WithAnUnreadableVersionFile_ReportsVDriveError()
        {
            _scanner.SetResult(TestDevices.CreateLocation(DeviceId.Tko));

            _monitor.Refresh();

            Assert.Equal("v-Drive Error", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Error, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_WhileEditingAHealthyDevice_ReportsVDriveOk()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);
        }

        [Fact]
        public void StatusIndicator_WhileEditingADeviceWithAVersionFileError_ReportsVDriveError()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko, health: VDriveHealth.Error));

            Assert.Equal("v-Drive Error", _shell.StatusIndicatorText);
        }

        [Fact]
        public void SettingsCommand_And_HelpCommand_AreUnavailableUntilTheirDialogsExist()
        {
            // Nothing consumes SettingsRequested/HelpRequested in production yet, so the buttons
            // report themselves as unavailable rather than swallowing clicks.
            Assert.False(_shell.SettingsCommand.CanExecute(null));
            Assert.False(_shell.HelpCommand.CanExecute(null));
        }

        [Fact]
        public void SettingsCommand_And_HelpCommand_KeepTheirPlaceholderRequestsAsHooks()
        {
            var raised = new List<string>();
            _shell.SettingsRequested += () => raised.Add("settings");
            _shell.HelpRequested += () => raised.Add("help");

            _shell.SettingsCommand.Execute(null);
            _shell.HelpCommand.Execute(null);

            Assert.Equal(new[] { "settings", "help" }, raised);
        }

        /// <summary>
        /// Waits for the editor's fire-and-forget load to finish. The shell never awaits it, so
        /// there is no task to hand back to the test.
        /// </summary>
        private static async Task WaitForLoadAsync(KeyboardEditorViewModel editor)
        {
            for (var attempt = 0; attempt < 500 && editor.IsLoading; attempt++)
            {
                await Task.Delay(10);
            }

            Assert.False(editor.IsLoading);
        }

        private void SetPedalFile(DeviceSnapshot snapshot, params string[] lines)
        {
            _fileService.SetFile(Path.Combine(snapshot.Location!.RootPath, "active", "pedals.txt"), lines);
        }

        private void SetDrive(DeviceId deviceId, bool isWritable = true)
        {
            var location = TestDevices.CreateLocation(deviceId, isWritable);
            _fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(deviceId));
            _scanner.SetResult(location);
        }

        public void Dispose()
        {
            _shell.Dispose();
            _dashboard.Dispose();
            _monitor.Dispose();
            _capture.Dispose();
        }
    }
}
