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
        private readonly FakeSystemClock _clock = new();
        private readonly ISettingsService _settings;
        private readonly DeviceSessionManager _sessions;
        private readonly DeviceMonitorService _monitor;
        private readonly VDriveEjectNotifier _ejectNotifier;
        private readonly DashboardViewModel _dashboard;
        private readonly MainWindowViewModel _shell;

        public MainWindowViewModelTests()
        {
            var ejectNotifier = new VDriveEjectNotifier(_ejectService, _notifications);
            _ejectNotifier = ejectNotifier;
            _settings = TestDevices.CreateSettingsService(_fileService);
            _sessions = new DeviceSessionManager(_settings);
            _monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _fileService,
                new FakeUiDispatcher(),
                _clock,
                _neverPolls);

            // The real factory over fake collaborators: which editor a device resolves to is part
            // of what OpenDevice has to get right.
            var editors = new EditorViewModelFactory(
                _profiles,
                _settings,
                () => _capture,
                _notifications,
                new PedalFileService(_fileService),
                new FakeFolderPickerService(),
                new FakeFilePickerService(),
                _fileService,
                new FakeUrlLauncher());

            _dashboard = new DashboardViewModel(_monitor, ejectNotifier, new FakeUrlLauncher());
            _shell = CreateShell(editors);
        }

        [Fact]
        public void CurrentView_Initially_IsTheDashboard()
        {
            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_shell.Editor);
            Assert.False(_shell.IsEditorOpen);
        }

        /// <summary>
        /// Home is a navigation, not "leave the editor", and it stays runnable on the dashboard —
        /// where it does nothing at all. That is load-bearing rather than lax: the nav pill's
        /// selected face is written `.selected:not(:disabled)`, and a Button takes its enablement
        /// from its command, so gating Home on an open editor makes it disabled in exactly the
        /// state <see cref="MainWindowViewModel.IsHomeSelected"/> is true and the active face
        /// unreachable. The dashboard then draws Home identically to the two pills that are not
        /// implemented yet.
        /// </summary>
        [Fact]
        public async Task HomeCommand_OnTheDashboard_IsRunnableAndDoesNothing()
        {
            Assert.True(_shell.IsHomeSelected);
            Assert.True(_shell.HomeCommand.CanExecute(null));

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_shell.Editor);
            Assert.Null(_sessions.Active);
            Assert.False(_shell.IsBusy);
            Assert.Empty(_ejectService.EjectedPaths);
            Assert.Empty(_notifications.Toasts);
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
        public async Task OpenDevice_WithTheSavantElite2_SwapsInThePedalEditor()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");

            _shell.OpenDevice(snapshot);

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_shell.Editor);

            await WaitForLoadAsync(() => pedal.IsLoading);

            Assert.Same(pedal, _shell.CurrentView);
            Assert.Equal(PedalLoadState.Loaded, pedal.LoadState);
            Assert.Equal(7, pedal.Inputs.Count);
            Assert.Equal("[lmouse]", pedal.Inputs[0].AssignmentText);
            Assert.Same(snapshot, _sessions.Active!.Device);
            Assert.True(_shell.IsEditorOpen);
            Assert.True(_shell.HomeCommand.CanExecute(null));
        }

        [Fact]
        public async Task OpenDevice_WithTheSavantElite2InDemoMode_ReadsNothingFromTheDrive()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2, VDriveConnectionStatus.CannotAccess);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");

            _shell.OpenDevice(snapshot);

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_shell.Editor);

            await WaitForLoadAsync(() => pedal.IsLoading);

            Assert.Equal(0, _fileService.ReadCount);
            Assert.Equal(PedalLoadState.DemoMode, pedal.LoadState);
            Assert.True(_shell.IsDemoMode);
        }

        [Fact]
        public async Task HomeCommand_WhenTheOpenEditorRefusesToClose_LeavesItOnScreen()
        {
            // The shell knows nothing about what is at stake — only that the editor said no
            // (docs/app/savant-elite.md: the pedal asks about unsaved changes).
            var pedal = await OpenPedalWithUnsavedChangesAsync();

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Cancel };

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Same(pedal, _shell.Editor);
            Assert.Same(pedal, _shell.CurrentView);
            Assert.NotNull(_sessions.Active);
            Assert.Empty(_ejectService.EjectedPaths);
            Assert.False(_shell.IsBusy);

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Same(_dashboard, _shell.CurrentView);
        }

        [Fact]
        public async Task HomeCommand_WhileTheOpenEditorIsSaving_IsRefusedAndEjectsNothing()
        {
            // The loading overlay does not take clicks and the top bar sits outside the editor, so
            // Home is reachable during a save. Leaving would dispose the editor and eject the
            // volume out from under a write still in flight — and tell the user their changes could
            // not be saved, which is the opposite of what is happening.
            var pedal = await OpenPedalWithUnsavedChangesAsync();
            var gate = new TaskCompletionSource();

            _fileService.WriteGate = gate;

            var save = pedal.SaveCommand.ExecuteAsync(null);

            Assert.True(pedal.IsBusy);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Same(pedal, _shell.Editor);
            Assert.Same(pedal, _shell.CurrentView);
            Assert.NotNull(_sessions.Active);
            Assert.Empty(_ejectService.EjectedPaths);
            Assert.Empty(_notifications.MessageBoxes);

            // Home is a live button here — the shell gates it on its own IsBusy, not the editor's —
            // so the refusal has to be visible. Without the toast the click is a silent no-op.
            var toast = Assert.Single(_notifications.Toasts);

            Assert.Equal(SavantElitePedalViewModel.SaveInProgressTitle, toast.Title);
            Assert.Equal(SavantElitePedalViewModel.SaveInProgressMessage, toast.Message);

            gate.SetResult();

            await save;

            // Once the write is done the navigation goes through, with nothing left to ask.
            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.Single(_ejectService.EjectedPaths);
        }

        [Fact]
        public async Task OpenDeviceAsync_WhenTheOpenEditorsConfirmationThrows_KeepsThatEditor()
        {
            // The confirmation is asked outside the guarded region: treating a failed question as a
            // failed open would drop the editor — discarding the very unsaved changes the question
            // exists to protect, and skipping the eject Home would have done.
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseExceptionToThrow = new InvalidOperationException("no window")
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var session = _sessions.Active;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.Same(editor, shell.Editor);
            Assert.Same(editor, shell.CurrentView);
            Assert.Equal(0, editor.DisposeCount);
            Assert.Same(session, _sessions.Active);
            Assert.False(shell.IsBusy);

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(MainWindowViewModel.OpenFailureTitle, box.Title);
            Assert.Contains("no window", box.Message);

            // Nothing is wedged: once the editor can answer again, the navigation goes through.
            editor.ConfirmCloseExceptionToThrow = null;
            editors.EditorToReturn = null;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.IsType<EditorPlaceholderViewModel>(shell.Editor);
            Assert.Equal(1, editor.DisposeCount);
        }

        [Fact]
        public async Task OpenDeviceAsync_WhenDisposingTheOpenEditorThrows_StillReportsTheFailure()
        {
            // AbandonOpenDevice runs inside the catch of a method that must not throw, and its
            // first real hazard is CloseEditor -> the editor's Dispose. An escape there would be
            // exactly the unobserved exception the totality is for, with no error box shown.
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                DisposeExceptionToThrow = new InvalidOperationException("dispose failed")
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            editors.EditorToReturn = null;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(MainWindowViewModel.OpenFailureTitle, box.Title);
            Assert.Contains("dispose failed", box.Message);

            Assert.Null(shell.Editor);
            Assert.Same(_dashboard, shell.CurrentView);
            Assert.Null(_sessions.Active);
            Assert.False(shell.IsDemoMode);
            Assert.False(shell.IsBusy);
            Assert.Null(_notifications.LoadingCaption);

            // Still usable afterwards.
            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.IsType<EditorPlaceholderViewModel>(shell.Editor);
        }

        [Fact]
        public async Task OpenDeviceAsync_WhenTheEditorCannotBeBuilt_ReportsItAndStaysOnTheDashboard()
        {
            // OpenDevice forgets the task, so a throw here would be an unobserved exception leaving
            // the shell on the dashboard with a session open, demo mode already flipped, an editor
            // that is null and nothing said.
            var editors = new FakeEditorViewModelFactory
            {
                ExceptionToThrow = new InvalidOperationException("no editor for you")
            };

            using var shell = CreateShell(editors);

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess));

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(MainWindowViewModel.OpenFailureTitle, box.Title);
            Assert.StartsWith(MainWindowViewModel.OpenFailureMessagePrefix, box.Message);
            Assert.Contains("no editor for you", box.Message);
            Assert.Equal(MessageBoxIcon.Error, box.Icon);

            Assert.Null(shell.Editor);
            Assert.False(shell.IsEditorOpen);
            Assert.Same(_dashboard, shell.CurrentView);
            Assert.Null(_sessions.Active);
            Assert.False(shell.IsDemoMode);
            Assert.False(shell.IsBusy);
            Assert.True(shell.IsHomeSelected);
            Assert.Null(_notifications.LoadingCaption);
            Assert.Equal(MainWindowViewModel.DemoModeIndicator, shell.StatusIndicatorText);

            // The shell is usable afterwards: the next device opens normally.
            editors.ExceptionToThrow = null;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.IsType<EditorPlaceholderViewModel>(shell.Editor);
        }

        [Fact]
        public void OpenDevice_WhenTheEditorCannotBeBuilt_SwallowsNothingAndThrowsNothing()
        {
            // The fire-and-forget entry point the dashboard's ConfigureRequested event needs.
            var editors = new FakeEditorViewModelFactory
            {
                ExceptionToThrow = new InvalidOperationException("no editor for you")
            };

            using var shell = CreateShell(editors);

            shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Single(_notifications.MessageBoxes);
            Assert.Null(shell.Editor);
        }

        [Fact]
        public async Task OpenDeviceAsync_WithoutADevice_ReportsItInsteadOfThrowing()
        {
            using var shell = CreateShell(new FakeEditorViewModelFactory());

            await shell.OpenDeviceAsync(null!);

            Assert.Single(_notifications.MessageBoxes);
            Assert.Null(shell.Editor);
            Assert.Null(_sessions.Active);
            Assert.False(shell.IsBusy);
        }

        [Fact]
        public async Task OpenDeviceAsync_WhenTheOpenEditorRefusesToClose_OpensNothing()
        {
            var pedal = await OpenPedalWithUnsavedChangesAsync();
            var session = _sessions.Active;

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.Cancel };

            await _shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Same(pedal, _shell.Editor);
            Assert.Same(session, _sessions.Active);

            _notifications.OutcomeToReturn = new MessageBoxOutcome { Result = MessageBoxResult.No };

            await _shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.IsType<EditorPlaceholderViewModel>(_shell.Editor);
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

            await WaitForLoadAsync(() => editor.IsLoading);

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
            Assert.Equal(StatusSeverity.Demo, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void ConfigureRequested_FromTheDashboard_OpensTheDevice()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Refresh();

            _dashboard.DeviceCards[0].ConfigureCommand.Execute(null);

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
            Assert.Single(_dashboard.DeviceCards);
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
            _shell.OpenDevice(_dashboard.DeviceCards[0].Snapshot);
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
            _shell.OpenDevice(_dashboard.DeviceCards[0].Snapshot);
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
            _shell.OpenDevice(_dashboard.DeviceCards[0].Snapshot);

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
            _shell.OpenDevice(_dashboard.DeviceCards[0].Snapshot);

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
            Assert.Equal(StatusSeverity.Demo, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_BeforeTheFirstScan_AlreadyReportsDemoMode()
        {
            // The field initialisers are the state the chip renders in the instant between the
            // shell being constructed and the detection loop first answering, so they carry the
            // same pair the demo branch below sets rather than a fourth, transient one.
            Assert.Equal("Demo Mode", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Demo, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_DemoMode_IsNotAnAdvisory()
        {
            // docs/design/: amber is the *only* warning colour and means "advisory, never blocks".
            // Demo mode means "nothing is written", which is its own state with its own colour, so
            // it must never land on the amber ramp.
            _monitor.Refresh();

            Assert.NotEqual(StatusSeverity.Warning, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_AcrossEveryDriveState_UsesADistinctSeverityPerState()
        {
            // The four states of the design's vocabulary each mean one thing, so no two of the
            // shell's own indicator states may share a severity.
            var severities = new List<StatusSeverity>();

            // Nothing detected at all.
            _monitor.Refresh();
            severities.Add(_shell.StatusIndicatorSeverity);

            // A drive with no readable version file. This runs before SetDrive below, because the
            // fake file service keeps what it is given and a version file written once stays
            // readable for the rest of the test.
            _scanner.SetResult(TestDevices.CreateLocation(DeviceId.Tko));
            _monitor.Refresh();
            severities.Add(_shell.StatusIndicatorSeverity);

            // A drive whose version file parses.
            SetDrive(DeviceId.Tko);
            _monitor.Refresh();
            severities.Add(_shell.StatusIndicatorSeverity);

            Assert.Equal(new[] { StatusSeverity.Demo, StatusSeverity.Error, StatusSeverity.Ok }, severities);
            Assert.Equal(severities.Count, severities.Distinct().Count());
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

        [Fact]
        public void WindowTitle_OnTheDashboard_IsJustTheAppName()
        {
            Assert.Equal("KinesisEdit", _shell.WindowTitle);
            Assert.True(_shell.IsHomeSelected);
        }

        [Fact]
        public void WindowTitle_WhileEditing_NamesTheDeviceAheadOfTheApp()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Equal("TKO — KinesisEdit", _shell.WindowTitle);
            Assert.False(_shell.IsHomeSelected);
        }

        [Fact]
        public void WindowTitle_InDemoMode_SaysSoBetweenTheDeviceAndTheApp()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess));

            Assert.Equal("TKO (Demo) — KinesisEdit", _shell.WindowTitle);
        }

        [Fact]
        public async Task WindowTitle_AfterGoingHome_IsTheAppNameAgain()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Equal("KinesisEdit", _shell.WindowTitle);
            Assert.True(_shell.IsHomeSelected);
        }

        [Fact]
        public void LastRefreshedText_BeforeTheFirstCompletedPass_HasNothingToSay()
        {
            Assert.Null(_monitor.LastRefreshedUtc);
            Assert.False(_shell.HasLastRefreshed);
            Assert.Equal(string.Empty, _shell.LastRefreshedText);
        }

        [Fact]
        public void LastRefreshedText_AfterAPass_AgesWithOneDecimal()
        {
            _monitor.Refresh();

            Assert.True(_shell.HasLastRefreshed);
            Assert.Equal("refreshed 0.0s ago", _shell.LastRefreshedText);

            _clock.Advance(TimeSpan.FromMilliseconds(400));
            Assert.Equal("refreshed 0.4s ago", _shell.LastRefreshedText);

            _clock.Advance(TimeSpan.FromMilliseconds(800));
            Assert.Equal("refreshed 1.2s ago", _shell.LastRefreshedText);
        }

        [Fact]
        public void LastRefreshedText_WhenTheClockStepsBack_ReadsZeroRatherThanNegative()
        {
            _monitor.Refresh();

            _clock.Advance(TimeSpan.FromSeconds(-5));

            Assert.Equal("refreshed 0.0s ago", _shell.LastRefreshedText);
        }

        [Fact]
        public void LastRefreshedText_WhenAPassCompletes_IsRepublishedWithoutWaitingForTheTicker()
        {
            var changed = new List<string?>();
            _shell.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            _monitor.Refresh();

            Assert.Contains(nameof(MainWindowViewModel.LastRefreshedText), changed);
            Assert.Contains(nameof(MainWindowViewModel.HasLastRefreshed), changed);
        }

        [Theory]
        [InlineData(null, false, "KinesisEdit")]
        [InlineData("", false, "KinesisEdit")]
        [InlineData("Advantage 360", false, "Advantage 360 — KinesisEdit")]
        [InlineData("Advantage 360", true, "Advantage 360 (Demo) — KinesisEdit")]
        public void BuildWindowTitle_CoversTheThreeShapes(string? deviceName, bool isDemoMode, string expected)
        {
            Assert.Equal(expected, MainWindowViewModel.BuildWindowTitle(deviceName, isDemoMode));
        }

        /// <summary>
        /// Waits for the editor's fire-and-forget load to finish. The shell never awaits it, so
        /// there is no task to hand back to the test.
        /// </summary>
        private static async Task WaitForLoadAsync(Func<bool> isLoading)
        {
            for (var attempt = 0; attempt < 500 && isLoading(); attempt++)
            {
                await Task.Delay(10);
            }

            Assert.False(isLoading());
        }

        /// <summary>
        /// Opens the pedal editor and programs one input, so that the editor has something to
        /// refuse a close over.
        /// </summary>
        private async Task<SavantElitePedalViewModel> OpenPedalWithUnsavedChangesAsync()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);

            SetPedalFile(snapshot, "[lpedal]>");

            await _shell.OpenDeviceAsync(snapshot);

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_shell.Editor);

            await WaitForLoadAsync(() => pedal.IsLoading);
            await pedal.BeginEditCommand.ExecuteAsync(pedal.Inputs[0]);

            _capture.RaiseKeystroke(PedalTokenMap.Resolve("a")!);

            pedal.DoneCommand.Execute(null);

            Assert.True(pedal.HasUnsavedChanges);

            return pedal;
        }

        /// <summary>
        /// A shell over the shared collaborators, with an editor factory of its own. The readout
        /// ticker is parked: every test that cares about "refreshed Ns ago" moves the fake clock
        /// and reads the property, so a background timer would only add a race.
        /// </summary>
        private MainWindowViewModel CreateShell(IEditorViewModelFactory editors)
        {
            return new MainWindowViewModel(
                _dashboard,
                _monitor,
                _sessions,
                _notifications,
                _ejectNotifier,
                editors,
                _clock,
                new FakeUiDispatcher(),
                _neverPolls);
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
