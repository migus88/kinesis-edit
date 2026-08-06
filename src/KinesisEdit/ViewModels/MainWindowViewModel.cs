using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The single-window shell: it hosts the dashboard and swaps in an editor when a device is
    /// opened, exactly like the legacy dashboards embedded an editor form in their content panel
    /// (specs/10-apps-and-ui.md, "Opening a device" and "Home"). It also owns the status
    /// indicator — green 'v-Drive OK', red 'v-Drive Error', or 'Demo Mode'.
    /// </summary>
    public sealed class MainWindowViewModel : ViewModelBase, IDisposable
    {
        /// <summary>Indicator text while the version file re-check succeeds.</summary>
        public const string VDriveOkIndicator = "v-Drive OK";

        /// <summary>Indicator text while the version file cannot be read.</summary>
        public const string VDriveErrorIndicator = "v-Drive Error";

        /// <summary>Indicator text while editing without a connected, writable drive (03 §3.5).</summary>
        public const string DemoModeIndicator = "Demo Mode";

        /// <summary>Title of the box raised when a device could not be opened at all.</summary>
        public const string OpenFailureTitle = "Open Device";

        /// <summary>Message prefix of that box; the exception's message follows it.</summary>
        public const string OpenFailureMessagePrefix = "The device could not be opened: ";

        /// <summary>The dashboard; also the view shown whenever no editor is open.</summary>
        public DashboardViewModel Dashboard { get; }

        /// <summary>The view currently filling the window: the dashboard or the open editor.</summary>
        public ViewModelBase CurrentView
        {
            get => _currentView;
            private set => SetProperty(ref _currentView, value);
        }

        /// <summary>
        /// The open editor, or null while the dashboard is showing. Which editor it is depends on
        /// the device and is decided by <see cref="IEditorViewModelFactory"/>; the shell only ever
        /// uses what <see cref="DeviceEditorViewModel"/> declares.
        /// </summary>
        public DeviceEditorViewModel? Editor
        {
            get => _editor;
            private set
            {
                if (SetProperty(ref _editor, value))
                {
                    OnPropertyChanged(nameof(IsEditorOpen));

                    HomeCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Whether an editor is open — the Home button is only usable then.</summary>
        public bool IsEditorOpen => _editor is not null;

        /// <summary>Whether the open session runs in demo mode.</summary>
        public bool IsDemoMode
        {
            get => _isDemoMode;
            private set => SetProperty(ref _isDemoMode, value);
        }

        /// <summary>
        /// Whether a navigation is in flight. Home awaits the eject, so without this both
        /// directions stay clickable during it: Configure would open a second session under
        /// Home's continuation.
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    HomeCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Status indicator caption.</summary>
        public string StatusIndicatorText
        {
            get => _statusIndicatorText;
            private set => SetProperty(ref _statusIndicatorText, value);
        }

        /// <summary>Severity the view maps to the indicator's color.</summary>
        public StatusSeverity StatusIndicatorSeverity
        {
            get => _statusIndicatorSeverity;
            private set => SetProperty(ref _statusIndicatorSeverity, value);
        }

        /// <summary>Closes the editor and ejects the drive when not in demo mode.</summary>
        public IAsyncRelayCommand HomeCommand { get; }

        /// <summary>
        /// The top bar's Settings button (specs/10-apps-and-ui.md). Permanently disabled until
        /// the app-settings dialog exists: nothing consumes <see cref="SettingsRequested"/> yet,
        /// and a button that silently does nothing is worse than a visibly unavailable one.
        /// </summary>
        public IRelayCommand SettingsCommand { get; }

        /// <summary>The top bar's Help button; disabled on the same terms as <see cref="SettingsCommand"/>.</summary>
        public IRelayCommand HelpCommand { get; }

        /// <summary>Raised by <see cref="SettingsCommand"/> until the settings dialog exists.</summary>
        public event Action? SettingsRequested;

        /// <summary>Raised by <see cref="HelpCommand"/> until the help dialog exists.</summary>
        public event Action? HelpRequested;

        private readonly DeviceMonitorService _monitor;
        private readonly DeviceSessionManager _sessions;
        private readonly INotificationService _notifications;
        private readonly VDriveEjectNotifier _ejectNotifier;
        private readonly IEditorViewModelFactory _editors;
        private ViewModelBase _currentView;
        private DeviceEditorViewModel? _editor;
        private string _statusIndicatorText = DemoModeIndicator;
        private StatusSeverity _statusIndicatorSeverity = StatusSeverity.Demo;
        private bool _isDemoMode;
        private bool _isBusy;
        private bool _isDisposed;

        /// <summary>Creates the shell over the dashboard, the detection loop, and the session services.</summary>
        public MainWindowViewModel(
            DashboardViewModel dashboard,
            DeviceMonitorService monitor,
            DeviceSessionManager sessions,
            INotificationService notifications,
            VDriveEjectNotifier ejectNotifier,
            IEditorViewModelFactory editors)
        {
            Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _ejectNotifier = ejectNotifier ?? throw new ArgumentNullException(nameof(ejectNotifier));
            _editors = editors ?? throw new ArgumentNullException(nameof(editors));
            _currentView = dashboard;

            HomeCommand = new AsyncRelayCommand(GoHomeAsync, () => IsEditorOpen && !IsBusy);
            SettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(), () => false);
            HelpCommand = new RelayCommand(() => HelpRequested?.Invoke(), () => false);

            Dashboard.ConfigureRequested += OpenDevice;
            _monitor.Updated += OnMonitorUpdated;

            UpdateStatusIndicator();
        }

        /// <summary>
        /// Opens <paramref name="device"/> in the editor and forgets the task — the form the
        /// dashboard's <c>ConfigureRequested</c> event needs, since it hands out no place to await
        /// one. Everything the navigation does is inside <see cref="OpenDeviceAsync"/>, which is
        /// total.
        /// </summary>
        public void OpenDevice(DeviceSnapshot device)
        {
            _ = OpenDeviceAsync(device);
        }

        /// <summary>
        /// Opens <paramref name="device"/> in the editor, in the order specs/10-apps-and-ui.md
        /// prescribes for Configure: ask the open editor whether it may be closed, set demo mode
        /// from the device's connected/writable state, record the active device, show the loading
        /// splash, swap the view in, initialize. Which editor is built is
        /// <see cref="IEditorViewModelFactory"/>'s decision, so the shell stays device-agnostic
        /// however many editors exist.
        /// <para>
        /// <b>Total.</b> <see cref="OpenDevice"/> forgets the task it returns, so a failure anywhere
        /// in the navigation — the session manager, an editor's constructor — would otherwise become
        /// an unobserved exception leaving the shell on the dashboard with a session open, demo mode
        /// already flipped and nothing said. Instead the half-done navigation is undone and the
        /// failure is shown.
        /// </para>
        /// </summary>
        public async Task OpenDeviceAsync(DeviceSnapshot device)
        {
            if (IsBusy)
            {
                return;
            }

            DeviceEditorViewModel? editor = null;

            // The confirmation may put a modal question on screen, so the navigation is busy from
            // here: without it a second Configure could open a session underneath the answer.
            IsBusy = true;

            try
            {
                // Deliberately outside the guarded region below: a confirmation that fails must
                // abort the navigation, not abandon the editor it was asking about — dropping that
                // editor would discard the very unsaved changes the question is there to protect.
                if (!await ConfirmOpenAsync(device).ConfigureAwait(true))
                {
                    return;
                }

                try
                {
                    IsDemoMode = device.IsDemoMode;

                    _sessions.Begin(device);

                    // Creating an editor may still fail, so the splash is hidden in a finally: an
                    // unexpected failure must not leave the shell wedged behind a loading overlay
                    // with no editor and a disabled Home button.
                    try
                    {
                        _notifications.ShowLoading(LoadingCaptions.ForDevice(device.DisplayName));

                        CloseEditor();

                        editor = CreateEditor(device);

                        Editor = editor;
                        CurrentView = editor;

                        // The detection loop keeps running: specs/10-apps-and-ui.md requires the
                        // open editor to "re-verify the device's version file every tick" to drive
                        // the v-Drive OK / v-Drive Error indicator, and here one loop serves both
                        // the dashboard and the editor.
                        UpdateStatusIndicator();
                    }
                    finally
                    {
                        _notifications.HideLoading();
                    }
                }
                catch (Exception exception)
                {
                    editor = null;

                    AbandonOpenDevice();

                    await ReportOpenFailureAsync(exception).ConfigureAwait(true);
                }
            }
            finally
            {
                IsBusy = false;
            }

            // What the editor still has to do — every editor reads its files here — happens after
            // the view is on screen, against the editor's own loading state: the shell's splash
            // covers the swap, not the drive read. LoadAsync never throws, which is what makes
            // forgetting the task safe.
            if (editor is not null)
            {
                _ = editor.LoadAsync();
            }
        }

        private DeviceEditorViewModel CreateEditor(DeviceSnapshot device)
        {
            return _editors.Create(device);
        }

        /// <summary>
        /// Whether the navigation may go ahead: there is a device, and the open editor agreed to be
        /// closed. A confirmation that throws is reported and answered with "no", which leaves the
        /// open editor on screen with everything it was holding — the alternative, treating it as a
        /// failed open, would discard its unsaved changes without asking and skip the eject.
        /// </summary>
        private async Task<bool> ConfirmOpenAsync(DeviceSnapshot device)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(device);

                return await ConfirmEditorCloseAsync().ConfigureAwait(true);
            }
            catch (Exception exception)
            {
                await ReportOpenFailureAsync(exception).ConfigureAwait(true);

                return false;
            }
        }

        /// <summary>
        /// Puts the shell back where a failed open found it: no session, no editor, the dashboard on
        /// screen and the indicator recomputed. Every step is idempotent, so it is safe whether the
        /// navigation failed before or after the session was recorded.
        /// <para>
        /// Every step is also guarded on its own. This runs inside the <c>catch</c> of a method whose
        /// contract is that it never throws, with the error box still to come — and the steps can
        /// throw for real: <see cref="CloseEditor"/> disposes the editor, which for the pedal editor
        /// stops the app-wide keystroke capture service. One failing step must neither swallow the
        /// report nor stop the rest from putting the shell back together.
        /// </para>
        /// </summary>
        private void AbandonOpenDevice()
        {
            RunGuarded(() => _sessions.End());
            RunGuarded(CloseEditor);
            RunGuarded(() =>
            {
                CurrentView = Dashboard;
                IsDemoMode = false;
            });
            RunGuarded(UpdateStatusIndicator);
        }

        /// <summary>Runs one undo step, swallowing whatever it throws (see <see cref="AbandonOpenDevice"/>).</summary>
        private static void RunGuarded(Action step)
        {
            try
            {
                step();
            }
            catch (Exception)
            {
                // Deliberately ignored: the failure being reported is the one that matters.
            }
        }

        /// <summary>
        /// Tells the user why the device did not open. Swallows a box that cannot be shown — the
        /// window may already be gone, and a failure to report a failure must not escape a method
        /// whose whole point is that it never throws.
        /// </summary>
        private async Task ReportOpenFailureAsync(Exception exception)
        {
            try
            {
                await _notifications.ShowMessageBoxAsync(new MessageBoxRequest
                {
                    Title = OpenFailureTitle,
                    Message = OpenFailureMessagePrefix + exception.Message,
                    Icon = MessageBoxIcon.Error
                }).ConfigureAwait(true);
            }
            catch (Exception)
            {
                // Nothing left to try: the failure is already on its way to being invisible.
            }
        }

        private async Task GoHomeAsync()
        {
            if (IsBusy)
            {
                return;
            }

            IsBusy = true;

            try
            {
                if (!await ConfirmEditorCloseAsync().ConfigureAwait(true))
                {
                    return;
                }

                var session = _sessions.Active;

                _sessions.End();

                CloseEditor();

                CurrentView = Dashboard;
                IsDemoMode = false;

                // Demo mode never touches the drive, so there is nothing to eject (03 §3.5).
                if (session is not null && !session.IsDemoMode && session.Device.Location is not null)
                {
                    await _ejectNotifier.EjectAsync(session.Device.Location.RootPath).ConfigureAwait(true);
                }
            }
            finally
            {
                IsBusy = false;
            }

            UpdateStatusIndicator();
        }

        /// <summary>
        /// Asks the open editor whether it may be closed (unsaved work, an edit in progress). The
        /// shell knows nothing about what is at stake — only whether the navigation may go ahead.
        /// </summary>
        private Task<bool> ConfirmEditorCloseAsync()
        {
            return Editor?.ConfirmCloseAsync() ?? Task.FromResult(true);
        }

        /// <summary>
        /// Drops the open editor, disposing it first. An editor may hold the app-wide keystroke
        /// capture service, which would otherwise keep swallowing keystrokes from the dashboard
        /// behind it (docs/app/keystroke-capture.md). The reference is dropped even when the
        /// disposal fails: an editor that could not be torn down cleanly is still gone from the
        /// screen, and keeping it here would leave Home pointing at it.
        /// </summary>
        private void CloseEditor()
        {
            try
            {
                if (Editor is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            finally
            {
                Editor = null;
            }
        }

        private void UpdateStatusIndicator()
        {
            var session = _sessions.Active;

            if (session is not null)
            {
                SetStatusIndicator(session.Health);

                return;
            }

            SetStatusIndicator(GetDashboardHealth());
        }

        private VDriveHealth GetDashboardHealth()
        {
            var health = VDriveHealth.Unknown;

            foreach (var snapshot in _monitor.Snapshots)
            {
                if (snapshot.Status == VDriveConnectionStatus.Connected && snapshot.Health == VDriveHealth.Ok)
                {
                    return VDriveHealth.Ok;
                }

                if (snapshot.IsDetected)
                {
                    health = VDriveHealth.Error;
                }
            }

            return health;
        }

        private void SetStatusIndicator(VDriveHealth health)
        {
            switch (health)
            {
                case VDriveHealth.Ok:
                    StatusIndicatorText = VDriveOkIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Ok;
                    break;
                case VDriveHealth.Error:
                    StatusIndicatorText = VDriveErrorIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Error;
                    break;
                default:
                    // Demo mode, not an advisory: no drive is being written to, which the design
                    // gives its own colour rather than folding into amber.
                    StatusIndicatorText = DemoModeIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Demo;
                    break;
            }
        }

        private void OnMonitorUpdated(DeviceMonitorUpdate update)
        {
            // The open session follows the loop, so a drive that disappears or stops answering
            // mid-session flips the indicator to 'v-Drive Error' instead of freezing on the state
            // it had when the editor opened. The deferred "Keyboard Connection Lost" dialog that
            // update.HasConnectionLoss also feeds is save/load-time behavior owned by a later issue.
            _sessions.UpdateActive(update.Snapshots);

            UpdateStatusIndicator();
        }

        /// <summary>
        /// Unsubscribes from the dashboard and the detection loop and closes any open editor.
        /// Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            Dashboard.ConfigureRequested -= OpenDevice;
            _monitor.Updated -= OnMonitorUpdated;

            CloseEditor();
        }
    }
}
