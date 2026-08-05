using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
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
        /// the device (see <see cref="OpenDevice"/>); the shell only ever uses what
        /// <see cref="DeviceEditorViewModel"/> declares.
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
        private readonly PedalFileService _pedalFiles;
        private ViewModelBase _currentView;
        private DeviceEditorViewModel? _editor;
        private string _statusIndicatorText = DemoModeIndicator;
        private StatusSeverity _statusIndicatorSeverity = StatusSeverity.Warning;
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
            PedalFileService pedalFiles)
        {
            Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            _notifications = notifications ?? throw new ArgumentNullException(nameof(notifications));
            _ejectNotifier = ejectNotifier ?? throw new ArgumentNullException(nameof(ejectNotifier));
            _pedalFiles = pedalFiles ?? throw new ArgumentNullException(nameof(pedalFiles));
            _currentView = dashboard;

            HomeCommand = new AsyncRelayCommand(GoHomeAsync, () => IsEditorOpen && !IsBusy);
            SettingsCommand = new RelayCommand(() => SettingsRequested?.Invoke(), () => false);
            HelpCommand = new RelayCommand(() => HelpRequested?.Invoke(), () => false);

            Dashboard.ConfigureRequested += OpenDevice;
            _monitor.Updated += OnMonitorUpdated;

            UpdateStatusIndicator();
        }

        /// <summary>
        /// Opens <paramref name="device"/> in the editor, in the order specs/10-apps-and-ui.md
        /// prescribes for Configure: set demo mode from the device's connected/writable state,
        /// record the active device, show the loading splash, swap the view in, initialize. Which
        /// editor is built is decided here and nowhere else — the Savant Elite2 has its own
        /// (read-only) pedal view, every other device still gets the placeholder of issues #14-#16.
        /// </summary>
        public void OpenDevice(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            if (IsBusy)
            {
                return;
            }

            IsDemoMode = device.IsDemoMode;

            _sessions.Begin(device);

            _notifications.ShowLoading(LoadingCaptions.ForDevice(device.DisplayName));

            // CreateEditor reads from the v-Drive for the pedal, so the splash is hidden in a
            // finally: an unexpected I/O failure must not leave the shell wedged behind a loading
            // overlay with no editor and a disabled Home button.
            try
            {
                var editor = CreateEditor(device);

                Editor = editor;
                CurrentView = editor;

                // The detection loop keeps running: specs/10-apps-and-ui.md requires the open editor
                // to "re-verify the device's version file every tick" to drive the v-Drive OK /
                // v-Drive Error indicator, and here one loop serves both the dashboard and the editor.
                UpdateStatusIndicator();
            }
            finally
            {
                _notifications.HideLoading();
            }
        }

        private DeviceEditorViewModel CreateEditor(DeviceSnapshot device)
        {
            // The pedal is the one device with a real editor so far, and it reads its file itself
            // (specs/12-savant-elite.md §4.6: pedals.txt is the whole model, so there is nothing
            // for the detection loop to carry). Everything else stays on the placeholder.
            return device.DeviceId == DeviceId.SavantElite2
                ? new SavantElitePedalViewModel(device, _pedalFiles)
                : new EditorPlaceholderViewModel(device);
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
                var session = _sessions.Active;

                _sessions.End();

                Editor = null;
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
                    StatusIndicatorText = DemoModeIndicator;
                    StatusIndicatorSeverity = StatusSeverity.Warning;
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

        /// <summary>Unsubscribes from the dashboard and the detection loop. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            Dashboard.ConfigureRequested -= OpenDevice;
            _monitor.Updated -= OnMonitorUpdated;
        }
    }
}
