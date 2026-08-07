using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One card of the device dashboard (specs/10-apps-and-ui.md, "SmartSetMaster … dashboard
    /// apps"; docs/design/mockups.md §1b, §2e): device name, the catalog-composed meta line, a
    /// status line, the mono mount path, a per-state explanation, and the Configure / secondary /
    /// Eject actions. Everything the view needs is a string, a bool or an enum — colours and
    /// classes are resolved in XAML.
    /// <para>
    /// <b>A card exists only while its drive does.</b> The dashboard's roster is what the last scan
    /// found, so a card is always backed by a mounted drive — either
    /// <see cref="VDriveConnectionStatus.Connected"/> or
    /// <see cref="VDriveConnectionStatus.CannotAccess"/>. There is one card kind, and it is this
    /// one; a board this app cannot edit gets no card at all.
    /// </para>
    /// <para>
    /// The card renders a <see cref="DeviceCardState"/>, not a
    /// <see cref="VDriveConnectionStatus"/>: the drive fact plus whether a detection pass is in
    /// flight. Which <em>actions</em> exist follows the drive alone, deliberately — a scan puts
    /// every card through <see cref="DeviceCardState.Scanning"/>, and buttons that appeared,
    /// vanished or changed caption for the duration would shift the layout under the cursor.
    /// </para>
    /// </summary>
    public sealed class DeviceCardViewModel : ViewModelBase
    {
        /// <summary>Status caption for a connected, writable drive.</summary>
        public const string ConnectedStatusText = "Connected";

        /// <summary>Status caption when the drive was found but is not writable.</summary>
        public const string CannotAccessStatusText = "Cannot Access";

        /// <summary>Status caption while a detection pass is in flight (docs/design/mockups.md §1b, §2e).</summary>
        public const string ScanningStatusText = "Scanning for v-Drive…";

        /// <summary>Main-button caption; every card has a mounted drive to open.</summary>
        public const string ConfigureActionCaption = "Configure";

        /// <summary>Caption the secondary button takes over a drive that is mounted but unwritable (mockup §1b).</summary>
        public const string RetryAccessActionCaption = "Retry access";

        /// <summary>Caption the secondary button holds while the pass it would start is already running (mockup §1b).</summary>
        public const string ScanningActionCaption = "Scanning";

        /// <summary>Caption of the eject button (specs/10-apps-and-ui.md).</summary>
        public const string EjectActionCaption = "Eject";

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

            return status == VDriveConnectionStatus.CannotAccess
                ? DeviceCardState.CannotAccess
                : DeviceCardState.Connected;
        }

        /// <summary>Maps a card state to its status caption.</summary>
        public static string GetStatusText(DeviceCardState state)
        {
            return state switch
            {
                DeviceCardState.CannotAccess => CannotAccessStatusText,
                DeviceCardState.Scanning => ScanningStatusText,
                _ => ConnectedStatusText
            };
        }

        /// <summary>
        /// Maps a card state to its colour severity. <see cref="DeviceCardState.Scanning"/> is
        /// <see cref="StatusSeverity.Unknown"/> and emphatically not
        /// <see cref="StatusSeverity.Error"/>: a scan in flight is transient, gets no colour, and
        /// its indeterminate bar is what reads as motion. The status chip has no <c>.unknown</c>
        /// face on purpose — the absence of a chip colour <em>is</em> that treatment
        /// (docs/design/mockups.md §2e).
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

        /// <summary>
        /// Stable identity of this card within one session: the scanned slot, never the resolved id
        /// — Freestyle resolution makes that many-to-one (docs/app/app-shell.md, invariant 4).
        /// </summary>
        public string Key => _snapshot.ScannedDeviceId.ToString();

        /// <summary>Device name shown on the card.</summary>
        public string DisplayName => _snapshot.DisplayName;

        /// <summary>
        /// The hardware summary under the title — "Split flat · 2 layers · per-key RGB". Always
        /// composed by <see cref="DeviceMetaLine"/> from catalog data, so no card and no view ever
        /// carries per-device code.
        /// </summary>
        public string MetaLine => DeviceMetaLine.Describe(_snapshot.Device);

        /// <summary>
        /// Whether this card has already been on screen once. The design animates a card in when
        /// its device is <em>newly detected</em>; a card view is also rebuilt from scratch every
        /// time the shell swaps the dashboard back in, and an existing card being re-hosted is not
        /// an insertion — without this the whole grid would grow in from zero on every trip Home.
        /// <para>
        /// A bool rather than anything from the toolkit: the card knows whether it has been shown,
        /// the view decides what to do about it (docs/app/app-shell.md, invariant 8).
        /// </para>
        /// </summary>
        public bool HasBeenPresented { get; private set; }

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
                OnPropertyChanged(nameof(ShowsSecondaryAction));
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
        /// Caption of the main button, which is always <c>Configure</c>: a card exists only for a
        /// mounted drive, and an unwritable one still opens its editor (in demo mode). Demo mode
        /// without hardware is reached from the empty state's own button, never from a card.
        /// </summary>
        public string PrimaryActionCaption => ConfigureActionCaption;

        /// <summary>
        /// Caption of the secondary button: <c>Scanning</c> while the pass it would start is
        /// already running, and <c>Retry access</c> otherwise. It is only rendered over a
        /// mounted-but-unwritable drive (see <see cref="ShowsSecondaryAction"/>), so those are the
        /// only two captions it can hold.
        /// </summary>
        public string SecondaryActionCaption => _isScanning ? ScanningActionCaption : RetryAccessActionCaption;

        /// <summary>
        /// Whether the secondary button is rendered at all: only over a drive that is mounted but
        /// unwritable, where re-reading it is the one thing that can help. A connected card carries
        /// <c>Configure</c> and <c>Eject</c> and nothing else — re-scanning a drive the app is
        /// already talking to changes nothing.
        /// <para>
        /// <b>Gated on the drive, never on <see cref="State"/></b>, exactly as
        /// <see cref="ShowsEject"/> is: <see cref="GetState"/> reports
        /// <see cref="DeviceCardState.Scanning"/> for every card while a pass is in flight, so a
        /// state-based test would flash the button onto a connected card mid-scan.
        /// </para>
        /// </summary>
        public bool ShowsSecondaryAction => _snapshot.Status == VDriveConnectionStatus.CannotAccess;

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

        /// <summary>Records that a view for this card has joined the visual tree.</summary>
        public void MarkPresented()
        {
            HasBeenPresented = true;
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
            OnPropertyChanged(nameof(ShowsSecondaryAction));
            OnPropertyChanged(nameof(ShowsEject));
            OnPropertyChanged(nameof(CanEject));

            EjectCommand.NotifyCanExecuteChanged();
        }

        private static string GetExplanationText(DeviceCardState state, string driveName)
        {
            return state == DeviceCardState.CannotAccess
                ? string.Format(CannotAccessExplanationTemplate, driveName)
                : string.Empty;
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
