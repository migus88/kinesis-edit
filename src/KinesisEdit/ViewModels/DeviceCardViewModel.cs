using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One drive-backed device card of the dashboard (specs/10-apps-and-ui.md, "SmartSetMaster …
    /// dashboard apps"; docs/design/mockups.md §1b, §2e): device name, the catalog-composed meta
    /// line, a status line, the mono mount path, a per-state explanation, and the Configure /
    /// secondary / Eject actions. Everything the view needs is a string, a bool or an enum —
    /// colours and classes are resolved in XAML.
    /// <para>
    /// The card renders a <see cref="DeviceCardState"/>, not a
    /// <see cref="VDriveConnectionStatus"/>: the drive fact plus whether a detection pass is in
    /// flight. Which <em>actions</em> exist follows the drive alone, deliberately — a 2 s refresh
    /// puts every card through <see cref="DeviceCardState.Scanning"/>, and buttons that appeared,
    /// vanished or changed caption for the duration would shift the layout under the cursor.
    /// </para>
    /// </summary>
    public sealed class DeviceCardViewModel : DashboardCardViewModel
    {
        /// <summary>Status caption for a connected, writable drive.</summary>
        public const string ConnectedStatusText = "Connected";

        /// <summary>Status caption when no drive was found — the resting state, not an error.</summary>
        public const string NotDetectedStatusText = "Not Detected";

        /// <summary>Status caption when the drive was found but is not writable.</summary>
        public const string CannotAccessStatusText = "Cannot Access";

        /// <summary>Status caption while a detection pass is in flight (docs/design/mockups.md §1b, §2e).</summary>
        public const string ScanningStatusText = "Scanning for v-Drive…";

        /// <summary>Main-button caption when the device can be opened against a mounted drive.</summary>
        public const string ConfigureActionCaption = "Configure";

        /// <summary>Main-button caption when the device would open without a drive at all.</summary>
        public const string DemoModeActionCaption = "Demo Mode";

        /// <summary>Caption of the rescan button (specs/11-feature-dialogs.md §11.8).</summary>
        public const string ScanActionCaption = "Scan for v-Drive";

        /// <summary>Caption the rescan button takes over a drive that is mounted but unwritable (mockup §1b).</summary>
        public const string RetryAccessActionCaption = "Retry access";

        /// <summary>Caption the secondary button holds while the pass it would start is already running (mockup §1b).</summary>
        public const string ScanningActionCaption = "Scanning";

        /// <summary>Caption of the eject button (specs/10-apps-and-ui.md).</summary>
        public const string EjectActionCaption = "Eject";

        /// <summary>Explanation under a resting card (docs/design/mockups.md §2e, verbatim).</summary>
        public const string RestingExplanationText = "Known device, no drive mounted. Idle and quiet — no red, no spinner. This is the resting state, not an error.";

        /// <summary>
        /// Explanation under an unwritable card (docs/design/mockups.md §1b, verbatim except that
        /// the drive name is templated instead of the mockup's literal "TKO" — the sentence names
        /// the volume the user is looking at, and every device has a different one.
        /// </summary>
        public const string CannotAccessExplanationTemplate = "Drive {0} is visible but not writable. Another app may have a file open, or the volume mounted read-only.";

        /// <summary>
        /// The face the card wears: the drive fact, overridden by
        /// <see cref="DeviceCardState.Scanning"/> while a detection pass is in flight.
        /// </summary>
        public static DeviceCardState GetState(VDriveConnectionStatus status, bool isScanning)
        {
            if (isScanning)
            {
                return DeviceCardState.Scanning;
            }

            return status switch
            {
                VDriveConnectionStatus.Connected => DeviceCardState.Connected,
                VDriveConnectionStatus.CannotAccess => DeviceCardState.CannotAccess,
                _ => DeviceCardState.Resting
            };
        }

        /// <summary>Maps a card state to its status caption.</summary>
        public static string GetStatusText(DeviceCardState state)
        {
            return state switch
            {
                DeviceCardState.Connected => ConnectedStatusText,
                DeviceCardState.CannotAccess => CannotAccessStatusText,
                DeviceCardState.Scanning => ScanningStatusText,
                _ => NotDetectedStatusText
            };
        }

        /// <summary>
        /// Maps a card state to its colour severity. <see cref="DeviceCardState.Resting"/> is
        /// <see cref="StatusSeverity.Unknown"/> and emphatically not
        /// <see cref="StatusSeverity.Error"/>: a known device whose drive is simply not mounted is
        /// not broken, and the status chip has no <c>.unknown</c> face on purpose — the absence of
        /// a chip colour <em>is</em> the resting treatment (docs/design/mockups.md §2e). Scanning
        /// is transient and gets no colour either; its indeterminate bar is what reads as motion.
        /// </summary>
        public static StatusSeverity GetStatusSeverity(DeviceCardState state)
        {
            return state switch
            {
                DeviceCardState.Connected => StatusSeverity.Ok,
                DeviceCardState.CannotAccess => StatusSeverity.Error,
                _ => StatusSeverity.Unknown
            };
        }

        /// <summary>The device state this card renders.</summary>
        public DeviceSnapshot Snapshot => _snapshot;

        /// <summary>The scanned catalog slot; the identity the dashboard keys cards by.</summary>
        public DeviceId ScannedDeviceId => _snapshot.ScannedDeviceId;

        /// <summary>Resolved device id; what this card shows, which can change between refreshes.</summary>
        public DeviceId DeviceId => _snapshot.DeviceId;

        /// <summary>Card identity: the scanned slot, never the resolved id (invariant 4).</summary>
        public override string Key => _snapshot.ScannedDeviceId.ToString();

        /// <summary>Device name shown on the card.</summary>
        public override string DisplayName => _snapshot.DisplayName;

        /// <summary>Hardware summary composed from the catalog — no per-device code lives here.</summary>
        public override string MetaLine => DeviceMetaLine.Describe(_snapshot.Device);

        /// <summary>Whether a detection pass is in flight; pushed in by the dashboard.</summary>
        public bool IsScanning
        {
            get => _isScanning;
            set
            {
                if (_isScanning == value)
                {
                    return;
                }

                _isScanning = value;

                OnPropertyChanged(nameof(IsScanning));
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusSeverity));
                OnPropertyChanged(nameof(SecondaryActionCaption));
                OnPropertyChanged(nameof(CanRunSecondaryAction));

                SecondaryActionCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>The face the card currently wears.</summary>
        public DeviceCardState State => GetState(_snapshot.Status, _isScanning);

        /// <summary>Connection status caption.</summary>
        public string StatusText => GetStatusText(State);

        /// <summary>Severity the view maps to a colour; <see cref="StatusSeverity.Unknown"/> paints nothing.</summary>
        public StatusSeverity StatusSeverity => GetStatusSeverity(State);

        /// <summary>Whether a drive was found at all — what the card's actions are chosen from.</summary>
        public bool IsDetected => _snapshot.IsDetected;

        /// <summary>Whether opening this device would enter demo mode (03 §3.5).</summary>
        public bool IsDemoMode => _snapshot.IsDemoMode;

        /// <summary>
        /// The mount point, shown in mono because it exists verbatim on the machine. Null when no
        /// drive is mounted.
        /// </summary>
        public string? MountPath => _snapshot.Location?.RootPath;

        /// <summary>Whether the mount path has something to show (Connected and Cannot Access).</summary>
        public bool HasMountPath => MountPath is not null;

        /// <summary>
        /// The volume the card is talking about — the mounted folder name, falling back to the
        /// catalog's primary label and then to the device name.
        /// </summary>
        public string DriveName => GetDriveName(_snapshot);

        /// <summary>Per-state explanation copy; empty where the status line says everything.</summary>
        public string ExplanationText => GetExplanationText(State, DriveName);

        /// <summary>Whether there is an explanation to render at all.</summary>
        public bool HasExplanation => ExplanationText.Length > 0;

        /// <summary>
        /// Caption of the main button. Chosen from the drive rather than the card state: an
        /// unwritable drive still opens its editor (in demo mode), so it reads <c>Configure</c>
        /// like a connected one, and only a device with no drive at all offers
        /// <c>Demo Mode</c>. A scan in flight never changes it, so the caption cannot flicker
        /// twice a second under the pointer.
        /// </summary>
        public string PrimaryActionCaption => IsDetected ? ConfigureActionCaption : DemoModeActionCaption;

        /// <summary>
        /// Caption of the secondary button: <c>Retry access</c> over a mounted-but-unwritable
        /// drive, <c>Scanning</c> while the pass it would start is already running, and
        /// <c>Scan for v-Drive</c> otherwise. The button always exists — it holds its position
        /// through a scan instead of disappearing (docs/design/mockups.md §2e).
        /// </summary>
        public string SecondaryActionCaption
        {
            get
            {
                if (_isScanning)
                {
                    return ScanningActionCaption;
                }

                return _snapshot.Status == VDriveConnectionStatus.CannotAccess
                    ? RetryAccessActionCaption
                    : ScanActionCaption;
            }
        }

        /// <summary>Whether the secondary button can start a pass — false while one is already running.</summary>
        public bool CanRunSecondaryAction => !_isScanning;

        /// <summary>
        /// Whether the Eject button is rendered at all. Follows the drive, not the card state, so
        /// a card that goes Scanning keeps its buttons exactly where they were.
        /// </summary>
        public bool ShowsEject => IsDetected;

        /// <summary>
        /// Whether the Eject button is usable: a platform that can eject, and a mounted drive.
        /// A <c>Cannot Access</c> drive counts — it is mounted, and unmounting it is precisely the
        /// remedy the card's own explanation points at.
        /// </summary>
        public bool CanEject => _ejectNotifier.IsSupported && IsDetected && _snapshot.Location is not null;

        /// <summary>Opens this device in the editor (Configure / Demo Mode).</summary>
        public IRelayCommand ConfigureCommand { get; }

        /// <summary>The secondary button: re-runs detection.</summary>
        public IAsyncRelayCommand SecondaryActionCommand { get; }

        /// <summary>Flushes and releases the drive, with the spec's progress notices.</summary>
        public IAsyncRelayCommand EjectCommand { get; }

        private readonly VDriveEjectNotifier _ejectNotifier;
        private readonly Action<DeviceSnapshot> _configureRequested;
        private readonly Func<Task> _scanRequested;
        private DeviceSnapshot _snapshot;
        private bool _isScanning;

        /// <summary>Creates a card for <paramref name="snapshot"/>.</summary>
        public DeviceCardViewModel(
            DeviceSnapshot snapshot,
            VDriveEjectNotifier ejectNotifier,
            Action<DeviceSnapshot> configureRequested,
            Func<Task> scanRequested)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _ejectNotifier = ejectNotifier ?? throw new ArgumentNullException(nameof(ejectNotifier));
            _configureRequested = configureRequested ?? throw new ArgumentNullException(nameof(configureRequested));
            _scanRequested = scanRequested ?? throw new ArgumentNullException(nameof(scanRequested));

            ConfigureCommand = new RelayCommand(Configure);
            SecondaryActionCommand = new AsyncRelayCommand(RunSecondaryActionAsync, () => CanRunSecondaryAction);
            EjectCommand = new AsyncRelayCommand(EjectAsync, () => CanEject);
        }

        /// <summary>Re-points the card at a newer snapshot of the same device, notifying every derived value.</summary>
        public void Update(DeviceSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (_snapshot.Equals(snapshot))
            {
                return;
            }

            _snapshot = snapshot;

            OnPropertyChanged(nameof(Snapshot));
            OnPropertyChanged(nameof(ScannedDeviceId));
            OnPropertyChanged(nameof(DeviceId));
            OnPropertyChanged(nameof(Key));
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(MetaLine));
            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusSeverity));
            OnPropertyChanged(nameof(IsDetected));
            OnPropertyChanged(nameof(IsDemoMode));
            OnPropertyChanged(nameof(MountPath));
            OnPropertyChanged(nameof(HasMountPath));
            OnPropertyChanged(nameof(DriveName));
            OnPropertyChanged(nameof(ExplanationText));
            OnPropertyChanged(nameof(HasExplanation));
            OnPropertyChanged(nameof(PrimaryActionCaption));
            OnPropertyChanged(nameof(SecondaryActionCaption));
            OnPropertyChanged(nameof(ShowsEject));
            OnPropertyChanged(nameof(CanEject));

            EjectCommand.NotifyCanExecuteChanged();
        }

        private static string GetExplanationText(DeviceCardState state, string driveName)
        {
            return state switch
            {
                DeviceCardState.Resting => RestingExplanationText,
                DeviceCardState.CannotAccess => string.Format(CannotAccessExplanationTemplate, driveName),
                _ => string.Empty
            };
        }

        private static string GetDriveName(DeviceSnapshot snapshot)
        {
            var rootPath = snapshot.Location?.RootPath;

            if (!string.IsNullOrEmpty(rootPath))
            {
                var mountedName = Path.GetFileName(
                    rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

                if (!string.IsNullOrEmpty(mountedName))
                {
                    return mountedName;
                }
            }

            var labels = snapshot.Device.VolumeLabels;

            return labels.Count > 0 ? labels[0] : snapshot.DisplayName;
        }

        private void Configure()
        {
            _configureRequested(_snapshot);
        }

        private Task RunSecondaryActionAsync()
        {
            return _scanRequested();
        }

        private async Task EjectAsync()
        {
            var location = _snapshot.Location;

            if (location is null)
            {
                return;
            }

            await _ejectNotifier.EjectAsync(location.RootPath).ConfigureAwait(true);
        }
    }
}
