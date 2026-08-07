using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Headless;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

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
        private readonly FakeHostPreferencesStore _preferences = new();
        private readonly ISettingsService _settings;
        private readonly DeviceSessionManager _sessions;
        private readonly DeviceMonitorService _monitor;
        private readonly DashboardViewModel _dashboard;
        private readonly MainWindowViewModel _shell;

        public MainWindowViewModelTests()
        {
            // Only the dashboard card ejects now that Home does not (docs/design/mockups.md §1l),
            // so the notifier reaches the dashboard and never the shell.
            var ejectNotifier = new VDriveEjectNotifier(_ejectService, _notifications);
            _settings = TestDevices.CreateSettingsService(_fileService);
            _sessions = new DeviceSessionManager(_settings);
            _monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _fileService,
                new FakeUiDispatcher());

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
        public void IsShellChromeVisible_OnTheDashboard_IsTrue()
        {
            Assert.True(_shell.IsShellChromeVisible);
        }

        [Fact]
        public async Task IsShellChromeVisible_WhileAnEditorDrawsItsOwnBar_IsFalseAndComesBackOnHome()
        {
            // The mockups draw exactly one 46px bar while editing, and the keyboard editor's bar
            // carries the same Home / device / Save / status pill this one does.
            var changes = new List<string?>();

            _shell.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));

            Assert.True(_shell.Editor!.ProvidesOwnChrome);
            Assert.False(_shell.IsShellChromeVisible);
            Assert.Contains(nameof(MainWindowViewModel.IsShellChromeVisible), changes);

            changes.Clear();

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.True(_shell.IsShellChromeVisible);
            Assert.Contains(nameof(MainWindowViewModel.IsShellChromeVisible), changes);
        }

        [Fact]
        public async Task IsShellChromeVisible_ForAnEditorWithoutItsOwnBar_StaysTrue()
        {
            // The Savant Elite2 pedal editor keeps the shell's bar until issue #53 gives it one.
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");

            _shell.OpenDevice(snapshot);

            var pedal = Assert.IsType<SavantElitePedalViewModel>(_shell.Editor);

            await WaitForLoadAsync(() => pedal.IsLoading);

            Assert.False(pedal.ProvidesOwnChrome);
            Assert.True(_shell.IsShellChromeVisible);
        }

        [Fact]
        public async Task OpenDevice_HandsTheEditorTheShellAndTakesItBackOnHome()
        {
            // The editor's own toolbar reaches Home and the status chip through IShellChrome, not
            // up the visual tree — so the hand-off is state the shell owns, and a closed editor
            // must not be left holding it.
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess));

            var editor = Assert.IsType<KeyboardEditorViewModel>(_shell.Editor);

            Assert.Same(_shell, editor.Shell);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(editor.Shell);
        }

        [Fact]
        public void StatusIndicator_IsTheShellChromeAnEditorBindsThrough()
        {
            IShellChrome chrome = _shell;

            Assert.Same(_shell.HomeCommand, chrome.HomeCommand);
            Assert.Equal(_shell.StatusIndicatorText, chrome.StatusIndicatorText);
            Assert.Equal(_shell.StatusIndicatorSeverity, chrome.StatusIndicatorSeverity);
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

            // Once the write is done the navigation goes through, with nothing left to ask — and
            // still nothing ejected: Home never ejects (docs/design/mockups.md §1l).
            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.Empty(_ejectService.EjectedPaths);
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

        /// <summary>
        /// Closing the window is the third way out of a session, and until now the only one that
        /// asked nothing — <c>Dispose</c> runs from <c>desktop.Exit</c> and cannot await a dialog,
        /// so quitting with unsaved edits dropped them silently. With nothing open there is still
        /// nothing to ask: the app closes at once and no box is raised.
        /// </summary>
        [Fact]
        public async Task ConfirmShutdownAsync_WithNoEditorOpen_ClosesWithoutAsking()
        {
            Assert.True(await _shell.ConfirmShutdownAsync());

            Assert.Empty(_notifications.MessageBoxes);
            Assert.False(_shell.IsBusy);
        }

        [Fact]
        public async Task ConfirmShutdownAsync_WithACleanEditor_ClosesWithoutAskingTheUser()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko));

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.True(await shell.ConfirmShutdownAsync());

            // The editor was asked — the shell never decides for it — and it had nothing to say.
            Assert.Equal(1, editor.ConfirmCloseCount);
            Assert.Empty(_notifications.MessageBoxes);

            // Answering is the whole job: the teardown is still Dispose's, so a close the platform
            // abandons after this leaves the session exactly as it was.
            Assert.Same(editor, shell.Editor);
            Assert.Equal(0, editor.DisposeCount);
            Assert.False(shell.IsBusy);
        }

        /// <summary>
        /// The case the whole guard exists for: a dirty editor says no, so the window stays open —
        /// and the app has to be as usable afterwards as it was before, not left half-shut with a
        /// busy flag stuck on.
        /// </summary>
        [Fact]
        public async Task ConfirmShutdownAsync_WhenTheEditorRefuses_KeepsTheWindowOpenAndTheShellUsable()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseResult = false
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var session = _sessions.Active;

            Assert.False(await shell.ConfirmShutdownAsync());

            Assert.Same(editor, shell.Editor);
            Assert.Same(editor, shell.CurrentView);
            Assert.Equal(0, editor.DisposeCount);
            Assert.Same(session, _sessions.Active);
            Assert.False(shell.IsBusy);
            Assert.True(shell.HomeCommand.CanExecute(null));

            // Still navigable: Configure opens another device once the editor lets go...
            editor.ConfirmCloseResult = true;
            editors.EditorToReturn = null;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.IsType<EditorPlaceholderViewModel>(shell.Editor);
            Assert.Equal(1, editor.DisposeCount);

            // ...Home still goes home...
            await shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(shell.Editor);
            Assert.Same(_dashboard, shell.CurrentView);

            // ...and the next close attempt is not poisoned by the refused one.
            Assert.True(await shell.ConfirmShutdownAsync());
        }

        /// <summary>
        /// A confirmation that throws must not close the app. This mirrors
        /// <c>ConfirmOpenAsync</c>: the failure is reported and answered "no", because losing work
        /// because the *question* failed is the exact outcome the guard exists to prevent — and it
        /// must not throw either, since the window's closing handler runs detached.
        /// </summary>
        [Fact]
        public async Task ConfirmShutdownAsync_WhenTheConfirmationThrows_KeepsTheWindowOpen()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseExceptionToThrow = new InvalidOperationException("no window")
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.False(await shell.ConfirmShutdownAsync());

            var box = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(MainWindowViewModel.CloseFailureTitle, box.Title);
            Assert.StartsWith(MainWindowViewModel.CloseFailureMessagePrefix, box.Message);
            Assert.Contains("no window", box.Message);
            Assert.Equal(MessageBoxIcon.Error, box.Icon);

            Assert.Same(editor, shell.Editor);
            Assert.Equal(0, editor.DisposeCount);
            Assert.False(shell.IsBusy);

            // Nothing is wedged: once the editor can answer again, the app closes.
            editor.ConfirmCloseExceptionToThrow = null;

            Assert.True(await shell.ConfirmShutdownAsync());
        }

        /// <summary>
        /// And a report that fails too is still not a reason to throw out of a method the closing
        /// handler awaits — the box the shell wants to raise lives in the window that is closing.
        /// </summary>
        [Fact]
        public async Task ConfirmShutdownAsync_WhenEvenTheFailureCannotBeReported_StillAnswersNo()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseExceptionToThrow = new InvalidOperationException("no window")
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException("no host either");

            Assert.False(await shell.ConfirmShutdownAsync());
            Assert.False(shell.IsBusy);
        }

        /// <summary>
        /// The branch a close makes reachable: <c>MessageBoxPresenter</c> answers immediately with
        /// the request's escape outcome when the host window is gone, which during a shutdown it
        /// may well be. That outcome is <c>Cancel</c>, every caller reads it as "the user did not
        /// answer", and the safe reading of "did not answer" here is <b>do not close</b>. Driven
        /// through the real presenter and the real notification service, because the value of the
        /// test is that the chain resolves that way rather than that a fake was told to.
        /// </summary>
        [Fact]
        public async Task ConfirmShutdownAsync_WhenTheQuestionNeverReachedTheScreen_KeepsTheWindowOpen()
        {
            var presenter = new MessageBoxPresenter(() => null);
            var notifications = new NotificationService(presenter, _sessions);
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors, notifications);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseNotifications = notifications
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.False(await shell.ConfirmShutdownAsync());

            Assert.Same(editor, shell.Editor);
            Assert.Equal(0, editor.DisposeCount);
            Assert.False(shell.IsBusy);
        }

        /// <summary>
        /// The window's guard cancels the close and re-issues it, and a user who clicks the close
        /// button again while the prompt is up must not get a second prompt behind the first.
        /// </summary>
        [Fact]
        public async Task ConfirmShutdownAsync_WhileTheFirstQuestionIsStillUp_AsksOnceAndAnswersNo()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var gate = new TaskCompletionSource();
            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseGate = gate
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var first = shell.ConfirmShutdownAsync();

            Assert.True(shell.IsBusy);
            Assert.False(await shell.ConfirmShutdownAsync());
            Assert.Equal(1, editor.ConfirmCloseCount);

            gate.SetResult();

            Assert.True(await first);
            Assert.False(shell.IsBusy);

            // And the second attempt, made after the first was answered, goes through normally.
            Assert.True(await shell.ConfirmShutdownAsync());
            Assert.Equal(2, editor.ConfirmCloseCount);
        }

        /// <summary>
        /// Everything above proves what <see cref="MainWindowViewModel.ConfirmShutdownAsync"/>
        /// <i>answers</i>. This proves the window <b>obeys</b> the answer, which is a different
        /// claim and the one issue #52 is actually about — and it is not reachable from a view
        /// model. <c>MainWindow.OnClosing</c> cancels the close, posts the question to the
        /// dispatcher, and lets the re-issued close through on a latch; each of those three is a
        /// place where the window could shut anyway, or refuse to shut forever, with
        /// <c>ConfirmShutdownAsync</c> answering perfectly correctly throughout.
        /// </summary>
        [AvaloniaFact]
        public async Task ClosingTheWindow_WhenTheEditorRefuses_LeavesItOpen()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseResult = false
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var window = new MainWindow(shell, _notifications);

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            window.Close();

            await SettleClosingAsync();

            Assert.True(window.IsVisible, "A refused close still shut the window.");
            Assert.Equal(1, editor.ConfirmCloseCount);
            Assert.Same(editor, shell.Editor);
            Assert.Equal(0, editor.DisposeCount);

            // Not a one-shot: the guard has to be there for the next attempt too.
            window.Close();

            await SettleClosingAsync();

            Assert.True(window.IsVisible);
            Assert.Equal(2, editor.ConfirmCloseCount);
        }

        /// <summary>
        /// The latch's job. An approved close re-enters <c>OnClosing</c>, and without the latch
        /// that second pass would cancel itself and ask again — a window that can never be shut.
        /// </summary>
        [AvaloniaFact]
        public async Task ClosingTheWindow_WhenTheEditorAgrees_ClosesItAndAsksExactlyOnce()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseResult = true
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var window = new MainWindow(shell, _notifications);

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            window.Close();

            await SettleClosingAsync();

            Assert.False(window.IsVisible, "An approved close left the window open.");
            Assert.Equal(1, editor.ConfirmCloseCount);
        }

        /// <summary>
        /// The path taken by everyone who never opened a device — and the reason the question is
        /// posted rather than started inline: a shell with nothing to ask answers synchronously, so
        /// an inline continuation would re-enter <c>Close</c> mid-cancellation.
        /// </summary>
        [AvaloniaFact]
        public async Task ClosingTheWindow_WithNoEditorOpen_AsksNobodyAndCloses()
        {
            var window = new MainWindow(_shell, _notifications);

            using var host = ThemedHost.Show(window, ThemeVariant.Dark);

            window.Close();

            await SettleClosingAsync();

            Assert.False(window.IsVisible);
            Assert.Empty(_notifications.MessageBoxes);
        }

        /// <summary>
        /// Drains the posted question and whatever it awaited. Nothing the caller holds awaits the
        /// close answer, so letting the loop run is the only way to observe it.
        /// </summary>
        private static async Task SettleClosingAsync()
        {
            for (var pass = 0; pass < 5; pass++)
            {
                Dispatcher.UIThread.RunJobs();

                await Task.Yield();
            }

            Dispatcher.UIThread.RunJobs();
        }

        /// <summary>
        /// The top bar and the dashboard stay live behind a modal, so Configure is reachable while
        /// the close prompt is up — and it must not open a session underneath the answer.
        /// </summary>
        [Fact]
        public async Task OpenDevice_WhileTheCloseQuestionIsUp_IsIgnored()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var gate = new TaskCompletionSource();
            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseGate = gate
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var session = _sessions.Active;
            var shutdown = shell.ConfirmShutdownAsync();

            editors.EditorToReturn = null;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.Same(editor, shell.Editor);
            Assert.Same(session, _sessions.Active);
            Assert.Equal(DeviceId.Tko, Assert.Single(editors.Requests).DeviceId);

            gate.SetResult();

            Assert.True(await shutdown);
        }

        /// <summary>
        /// The guard does not replace the teardown. <c>Dispose</c> keeps closing the editor — it
        /// has to stay correct for anything that tears the shell down without going through the
        /// window's closing event — and stays idempotent after an approved shutdown.
        /// </summary>
        [Fact]
        public async Task Dispose_AfterAnApprovedShutdown_ClosesTheEditorExactlyOnce()
        {
            var editors = new FakeEditorViewModelFactory();
            var shell = CreateShell(editors);
            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko));

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.True(await shell.ConfirmShutdownAsync());

            shell.Dispose();

            Assert.Equal(1, editor.DisposeCount);
            Assert.Null(shell.Editor);
            Assert.Null(editor.Shell);

            shell.Dispose();

            Assert.Equal(1, editor.DisposeCount);
        }

        [Fact]
        public async Task HomeCommand_AfterOpeningTheSavantElite2_ClosesTheEditorAndEjectsNothing()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.SavantElite2);
            SetPedalFile(snapshot, "[lpedal]>[lmouse]");
            _shell.OpenDevice(snapshot);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_sessions.Active);

            // The pedal's one live file is written by Save, not by leaving, and the drive stays
            // mounted until the user ejects it from the card (docs/design/mockups.md §1l).
            Assert.Empty(_ejectService.EjectedPaths);
            Assert.Empty(_notifications.Toasts);
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

            Assert.Equal(new string?[] { "Loading TKO…", null }, _notifications.LoadingHistory);
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

            _dashboard.Devices[0].ConfigureCommand.Execute(null);

            Assert.NotNull(_shell.Editor);
            Assert.Equal(DeviceId.Tko, _shell.Editor!.Device.DeviceId);
        }

        /// <summary>
        /// docs/design/mockups.md §1l: "Home just goes home — it never ejects. Ejecting is its own
        /// deliberate action on the dashboard card, so nothing is released behind the user's back."
        /// A connected, writable device is the case that used to eject, so it is the one that pins
        /// the rule: no eject call, and none of the two toasts <c>VDriveEjectNotifier</c> raises
        /// around one either.
        /// </summary>
        [Fact]
        public async Task HomeCommand_AfterConfiguringAConnectedDevice_ClosesTheEditorAndEjectsNothing()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            _shell.OpenDevice(snapshot);

            Assert.NotNull(snapshot.Location);

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Null(_shell.Editor);
            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.Null(_sessions.Active);
            Assert.Empty(_ejectService.EjectedPaths);
            Assert.Empty(_notifications.Toasts);
        }

        /// <summary>
        /// Demo mode no longer *decides* anything here — Home ejects in no mode at all — so this
        /// only pins that the demo path did not grow an eject of its own on the way to the same
        /// rule.
        /// </summary>
        [Fact]
        public async Task HomeCommand_InDemoMode_ClosesTheEditorAndEjectsNothing()
        {
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess));

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Empty(_ejectService.EjectedPaths);
            Assert.Empty(_notifications.Toasts);
            Assert.Null(_shell.Editor);
            Assert.False(_shell.IsDemoMode);
        }

        [Fact]
        public async Task HomeCommand_WhenTheEditorCloses_ShowsTheDashboardOverTheLastScansRoster()
        {
            SetDrive(DeviceId.Tko);
            _monitor.Refresh();
            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Single(_dashboard.Devices);
            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Ok, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void OpenDevice_WhileEditing_LeavesTheMonitorAvailableToRescan()
        {
            // specs/10-apps-and-ui.md: the open editor re-verifies the device's version file to
            // drive its status indicator. Scanning is manual now, so what the session must not do
            // is tear the monitor down — a scan asked for while an editor is open still lands.
            SetDrive(DeviceId.Tko);

            _shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Tko));

            _monitor.Refresh();

            Assert.NotEmpty(_monitor.Snapshots);
            Assert.Equal("v-Drive OK", _shell.StatusIndicatorText);
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
            Assert.IsType<ReadOnlyAppPreferencesStore>(_sessions.Active.SuppressionStore);
        }

        /// <summary>
        /// Home no longer ejects, so the eject is no longer what a navigation waits on — but the
        /// re-entrancy it used to demonstrate is still real and is now what the *question* creates:
        /// Home puts a modal box on screen and the top bar stays live behind it, so a Configure
        /// arriving in that window would open a second session underneath the answer.
        /// </summary>
        [Fact]
        public async Task OpenDevice_WhileHomeIsAskingToClose_IsIgnored()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var gate = new TaskCompletionSource();
            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseGate = gate
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            editors.EditorToReturn = null;

            var home = shell.HomeCommand.ExecuteAsync(null);

            shell.OpenDevice(TestDevices.CreateSnapshot(DeviceId.Advantage2));

            Assert.True(shell.IsBusy);
            Assert.Same(editor, shell.Editor);
            Assert.Equal(1, editor.ConfirmCloseCount);
            Assert.Equal(DeviceId.Tko, Assert.Single(editors.Requests).DeviceId);
            Assert.False(shell.HomeCommand.CanExecute(null));

            gate.SetResult();
            await home;

            Assert.False(shell.IsBusy);
            Assert.Null(shell.Editor);
            Assert.Null(_sessions.Active);
            Assert.Same(_dashboard, shell.CurrentView);
        }

        [Fact]
        public void StatusIndicator_WithoutAnyDrive_ReportsVDriveError()
        {
            // Mockup 1a defines v-Drive Error as "gone · unwritable", and 1d says outright that
            // the chip reads v-Drive Error while nothing is present: "gone" is exactly this state.
            // Demo Mode is not it — that means "nothing is written", which is a thing an open
            // session is doing, not a thing the dashboard found.
            _monitor.Refresh();

            Assert.Equal("v-Drive Error", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Error, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_BeforeTheFirstScan_ReportsDemoMode()
        {
            // "Gone" is a finding, and before the first pass there is none — so the pre-scan state
            // is deliberately NOT the error the same empty snapshot list produces afterwards. The
            // field initialisers carry the same pair, so the chip never flickers through a fourth,
            // transient value. In the shipped app this state is never on screen: the composition
            // root runs one synchronous Refresh() before the first frame.
            Assert.Empty(_monitor.Snapshots);
            Assert.Equal("Demo Mode", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Demo, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_InADemoSession_IsNotAnAdvisory()
        {
            // docs/design/: amber is the *only* warning colour and means "advisory, never blocks".
            // Demo mode means "nothing is written", which is its own state with its own colour, so
            // it must never land on the amber ramp.
            _shell.OpenDevice(DeviceSnapshot.CreateDemo(DeviceCatalog.GetById(DeviceId.Tko)));

            Assert.Equal("Demo Mode", _shell.StatusIndicatorText);
            Assert.Equal(StatusSeverity.Demo, _shell.StatusIndicatorSeverity);
            Assert.NotEqual(StatusSeverity.Warning, _shell.StatusIndicatorSeverity);
        }

        [Fact]
        public void StatusIndicator_AcrossEveryStateItCanReport_UsesTheThreeSeveritiesAndNeverAmber()
        {
            // The shell's indicator answers with exactly three of the design's four states, and
            // amber is not among them: an advisory is never a drive fact. Two drive states share
            // Error on purpose — a drive that is gone and a drive that cannot be read are both
            // "gone · unwritable" (mockup 1a) — so this pins the mapping, not distinctness.
            var severities = new List<StatusSeverity>();

            // Nothing looked at yet.
            severities.Add(_shell.StatusIndicatorSeverity);

            // A completed scan that found nothing.
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

            Assert.Equal(
                new[] { StatusSeverity.Demo, StatusSeverity.Error, StatusSeverity.Error, StatusSeverity.Ok },
                severities);
            Assert.DoesNotContain(StatusSeverity.Warning, severities);
            Assert.DoesNotContain(StatusSeverity.Unknown, severities);
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
        public void SettingsCommand_And_HelpCommand_AreRunnableEverywhereIncludingTheirOwnScreen()
        {
            // They used to be permanently CanExecute → false, with nothing behind them. Both are
            // real navigations now, and — like Home — neither may be gated on being somewhere
            // else: NavPill writes its selected setter as `.selected:not(:disabled)`, so a pill
            // disabled in exactly the state it is selected can never wear the active face
            // (docs/app/app-shell.md, invariant 11).
            Assert.True(_shell.SettingsCommand.CanExecute(null));
            Assert.True(_shell.HelpCommand.CanExecute(null));
        }

        [Fact]
        public async Task SettingsCommand_And_HelpCommand_SwapTheScreenIntoTheWindow()
        {
            // What replaced the two placeholder events: the screens the shell was handed are what
            // the pills navigate to, and each is the very object hosted in the content area.
            await _shell.SettingsCommand.ExecuteAsync(null);

            Assert.Same(_shell.SettingsScreen, _shell.CurrentView);

            await _shell.HelpCommand.ExecuteAsync(null);

            Assert.Same(_shell.HelpScreen, _shell.CurrentView);

            // And no editor was opened, no session begun and nothing ejected on the way.
            Assert.Null(_shell.Editor);
            Assert.Null(_sessions.Active);
            Assert.Empty(_ejectService.EjectedPaths);
        }

        [Fact]
        public async Task EachNavPill_WearsTheSelectedFace_OnlyOnItsOwnScreen()
        {
            Assert.True(_shell.IsHomeSelected);
            Assert.False(_shell.IsSettingsSelected);
            Assert.False(_shell.IsHelpSelected);

            await _shell.SettingsCommand.ExecuteAsync(null);

            Assert.False(_shell.IsHomeSelected);
            Assert.True(_shell.IsSettingsSelected);
            Assert.False(_shell.IsHelpSelected);

            await _shell.HelpCommand.ExecuteAsync(null);

            Assert.False(_shell.IsHomeSelected);
            Assert.False(_shell.IsSettingsSelected);
            Assert.True(_shell.IsHelpSelected);

            // The editor is the one view with no pill: none of the three reads as selected there.
            await _shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.False(_shell.IsHomeSelected);
            Assert.False(_shell.IsSettingsSelected);
            Assert.False(_shell.IsHelpSelected);
        }

        /// <summary>
        /// The three flags are raised from the <c>CurrentView</c> setter, which every navigation
        /// path ends at, rather than from each path — so a path added later cannot move the window
        /// and leave the bar showing the screen you left.
        /// </summary>
        [Fact]
        public async Task EveryNavigation_RaisesAllThreeSelectionFlags()
        {
            var changes = new List<string?>();

            _shell.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

            await _shell.SettingsCommand.ExecuteAsync(null);

            Assert.Contains(nameof(MainWindowViewModel.IsHomeSelected), changes);
            Assert.Contains(nameof(MainWindowViewModel.IsSettingsSelected), changes);
            Assert.Contains(nameof(MainWindowViewModel.IsHelpSelected), changes);

            changes.Clear();

            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Contains(nameof(MainWindowViewModel.IsHomeSelected), changes);
            Assert.Contains(nameof(MainWindowViewModel.IsSettingsSelected), changes);
            Assert.Contains(nameof(MainWindowViewModel.IsHelpSelected), changes);
        }

        [Fact]
        public async Task SettingsCommand_OnTheSettingsScreen_IsRunnableAndDoesNothing()
        {
            await _shell.SettingsCommand.ExecuteAsync(null);

            var screen = _shell.CurrentView;

            Assert.True(_shell.SettingsCommand.CanExecute(null));

            await _shell.SettingsCommand.ExecuteAsync(null);

            Assert.Same(screen, _shell.CurrentView);
            Assert.False(_shell.IsBusy);
        }

        [Fact]
        public async Task HomeCommand_FromSettingsAndFromHelp_ReturnsToTheDashboard()
        {
            // GoHomeAsync used to early-return unless an editor was open, which would have left
            // Home dead on both of these screens.
            await _shell.SettingsCommand.ExecuteAsync(null);
            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.True(_shell.IsHomeSelected);

            await _shell.HelpCommand.ExecuteAsync(null);
            await _shell.HomeCommand.ExecuteAsync(null);

            Assert.Same(_dashboard, _shell.CurrentView);
            Assert.True(_shell.IsHomeSelected);
        }

        [Fact]
        public async Task NavigatingToSettings_WithAnEditorOpen_AsksItFirstAndClosesIt()
        {
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseResult = true
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));
            await shell.SettingsCommand.ExecuteAsync(null);

            Assert.Equal(1, editor.ConfirmCloseCount);
            Assert.Same(shell.SettingsScreen, shell.CurrentView);
            Assert.Null(shell.Editor);
            Assert.Null(_sessions.Active);
            Assert.False(shell.IsDemoMode);

            // Still no eject, on this exit as on every other one (invariant 1).
            Assert.Empty(_ejectService.EjectedPaths);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ADirtyEditor_RefusingToClose_RefusesTheNavigationAndStaysOpen(bool toSettings)
        {
            // A nav pill is not a way to throw work away: Settings and Help go through the same
            // ConfirmCloseAsync gate Home and Configure do, and a refusal leaves everything alone.
            var editors = new FakeEditorViewModelFactory();

            using var shell = CreateShell(editors);

            var editor = new FakeDeviceEditorViewModel(TestDevices.CreateSnapshot(DeviceId.Tko))
            {
                ConfirmCloseResult = false
            };

            editors.EditorToReturn = editor;

            await shell.OpenDeviceAsync(TestDevices.CreateSnapshot(DeviceId.Tko));

            var session = _sessions.Active;
            var command = toSettings ? shell.SettingsCommand : shell.HelpCommand;

            await command.ExecuteAsync(null);

            Assert.Equal(1, editor.ConfirmCloseCount);
            Assert.Same(editor, shell.CurrentView);
            Assert.Same(editor, shell.Editor);
            Assert.Equal(0, editor.DisposeCount);
            Assert.Same(session, _sessions.Active);
            Assert.False(shell.IsBusy);

            // And the navigation is not wedged: once the editor agrees, the same click works.
            editor.ConfirmCloseResult = true;

            await command.ExecuteAsync(null);

            Assert.Same(toSettings ? shell.SettingsScreen : shell.HelpScreen, shell.CurrentView);
        }

        [Fact]
        public void Dispose_Always_DisposesTheSettingsScreenItWasGiven()
        {
            // The screen subscribes to the host-preferences store, which outlives it. It is built
            // once, so nothing else is in a position to unsubscribe it.
            var shell = CreateShell(new FakeEditorViewModelFactory());
            var screen = shell.SettingsScreen;

            shell.Dispose();

            var before = _preferences.ChangedCount;

            _preferences.Update(current => current with { Theme = AppThemePreference.Dark });

            Assert.Equal(before + 1, _preferences.ChangedCount);
            Assert.Equal(
                AppThemePreference.FollowSystem,
                screen.SelectedThemeOption.Value);
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

        /// <summary>
        /// Issue #118: the app bar's mono "refreshed 0.4s ago" readout is gone, and with it the
        /// 200 ms ticker that drove it. Scanning is manual, so an age against the last pass was a
        /// number the app had no business volunteering — and a readout that ages on its own is the
        /// one thing on a bar that keeps repainting a window nobody is touching.
        /// </summary>
        [Fact]
        public void MainWindowViewModel_Exposes_NoLastRefreshedReadout()
        {
            Assert.DoesNotContain(
                typeof(MainWindowViewModel).GetMembers(),
                member => member.Name.Contains("Refreshed", StringComparison.Ordinal));
        }

        /// <summary>
        /// The other half of the same removal: a completed pass republishes what the chip is made
        /// of, and nothing else. It used to also fire the two readout properties off
        /// <c>RefreshActivityChanged</c>, which the shell no longer subscribes to at all.
        /// </summary>
        [Fact]
        public void StatusIndicator_WhenAPassCompletes_IsRepublishedAndNothingElseIs()
        {
            SetDrive(DeviceId.Tko);
            var changed = new List<string?>();
            _shell.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            _monitor.Refresh();

            Assert.Contains(nameof(MainWindowViewModel.StatusIndicatorText), changed);
            Assert.DoesNotContain(changed, name => name?.Contains("Refreshed", StringComparison.Ordinal) == true);
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
        /// A shell over the shared collaborators, with an editor factory of its own and — for the
        /// one test that drives the real presenter — a notification service of its own.
        /// </summary>
        private MainWindowViewModel CreateShell(
            IEditorViewModelFactory editors,
            INotificationService? notifications = null)
        {
            return new MainWindowViewModel(
                _dashboard,
                _monitor,
                _sessions,
                notifications ?? _notifications,
                editors,
                new SettingsScreenViewModel(_preferences, _ => { }, _ => { }),
                new HelpScreenViewModel(new FakeUrlLauncher()));
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
