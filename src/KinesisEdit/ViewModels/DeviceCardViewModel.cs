using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One device card of the dashboard (specs/10-apps-and-ui.md, "SmartSetMaster …
    /// dashboard apps"): device name, connection status, an Eject and a Scan button, and the
    /// main Configure / Demo Mode button. Status wording is the spec's verbatim
    /// 'Connected' / 'Not Detected' / 'Cannot Access'; the color comes from
    /// <see cref="StatusSeverity"/> and is resolved in XAML.
    /// </summary>
    public sealed class DeviceCardViewModel : ViewModelBase
    {
        /// <summary>Status caption for a connected, writable drive.</summary>
        public const string ConnectedStatusText = "Connected";

        /// <summary>Status caption when no drive was found.</summary>
        public const string NotDetectedStatusText = "Not Detected";

        /// <summary>Status caption when the drive was found but is not writable.</summary>
        public const string CannotAccessStatusText = "Cannot Access";

        /// <summary>Main-button caption when the device can be edited against its drive.</summary>
        public const string ConfigureActionCaption = "Configure";

        /// <summary>Main-button caption when the device would open without a usable drive.</summary>
        public const string DemoModeActionCaption = "Demo Mode";

        /// <summary>Caption of the rescan button (specs/11-feature-dialogs.md §11.8).</summary>
        public const string ScanActionCaption = "Scan for v-Drive";

        /// <summary>
        /// Caption the rescan button takes on a connected device that offers the firmware update
        /// dialog (specs/10-apps-and-ui.md: connected cards "swap the scan button to
        /// 'Check for Updates'", and "the same card button opens the shared firmware-update
        /// dialog instead").
        /// </summary>
        public const string CheckForUpdatesActionCaption = "Check for Updates";

        /// <summary>Caption of the eject button (specs/10-apps-and-ui.md).</summary>
        public const string EjectActionCaption = "Eject";

        /// <summary>Maps a connection state to the spec's card caption.</summary>
        public static string GetStatusText(VDriveConnectionStatus status)
        {
            return status switch
            {
                VDriveConnectionStatus.Connected => ConnectedStatusText,
                VDriveConnectionStatus.CannotAccess => CannotAccessStatusText,
                _ => NotDetectedStatusText
            };
        }

        /// <summary>Maps a connection state to its color severity: connected is green, everything else red.</summary>
        public static StatusSeverity GetStatusSeverity(VDriveConnectionStatus status)
        {
            return status == VDriveConnectionStatus.Connected ? StatusSeverity.Ok : StatusSeverity.Error;
        }

        /// <summary>The device state this card renders.</summary>
        public DeviceSnapshot Snapshot => _snapshot;

        /// <summary>The scanned catalog slot; the identity the dashboard keys cards by.</summary>
        public DeviceId ScannedDeviceId => _snapshot.ScannedDeviceId;

        /// <summary>Resolved device id; what this card shows, which can change between refreshes.</summary>
        public DeviceId DeviceId => _snapshot.DeviceId;

        /// <summary>Device name shown on the card.</summary>
        public string DisplayName => _snapshot.DisplayName;

        /// <summary>Connection status caption ('Connected' / 'Not Detected' / 'Cannot Access').</summary>
        public string StatusText => GetStatusText(_snapshot.Status);

        /// <summary>Severity the view maps to a color.</summary>
        public StatusSeverity StatusSeverity => GetStatusSeverity(_snapshot.Status);

        /// <summary>Whether opening this device would enter demo mode (03 §3.5).</summary>
        public bool IsDemoMode => _snapshot.IsDemoMode;

        /// <summary>Caption of the main button: Configure for a usable drive, Demo Mode otherwise.</summary>
        public string PrimaryActionCaption => IsDemoMode ? DemoModeActionCaption : ConfigureActionCaption;

        /// <summary>
        /// Whether the secondary button checks for updates instead of rescanning: the drive must
        /// be connected — specs/09-firmware.md §3 "requires a connected device" — and the device
        /// must be one of the three that offer the dialog at all (§4: the FS Edge/Pro,
        /// Advantage2 and Savant Elite 2 apps have none).
        /// </summary>
        public bool CanCheckForUpdates => _snapshot.Status == VDriveConnectionStatus.Connected
            && UpdateCheckEligibility.IsSupported(DeviceId);

        /// <summary>Caption of the secondary button: 'Check for Updates' when it can, 'Scan for v-Drive' otherwise.</summary>
        public string SecondaryActionCaption => CanCheckForUpdates ? CheckForUpdatesActionCaption : ScanActionCaption;

        /// <summary>Whether the Eject button is usable: a connected drive on a platform that can eject.</summary>
        public bool CanEject => _ejectNotifier.IsSupported
            && _snapshot.Status == VDriveConnectionStatus.Connected
            && _snapshot.Location is not null;

        /// <summary>Opens this device in the editor (Configure / Demo Mode).</summary>
        public IRelayCommand ConfigureCommand { get; }

        /// <summary>
        /// The secondary button: re-runs detection, or opens the firmware update dialog when
        /// <see cref="CanCheckForUpdates"/> — spec 10 gives the card one button for both.
        /// </summary>
        public IAsyncRelayCommand SecondaryActionCommand { get; }

        /// <summary>Flushes and releases the drive, with the spec's progress notices.</summary>
        public IAsyncRelayCommand EjectCommand { get; }

        private readonly VDriveEjectNotifier _ejectNotifier;
        private readonly IFirmwareUpdatePresenter _updatePresenter;
        private readonly Action<DeviceSnapshot> _configureRequested;
        private readonly Func<Task> _scanRequested;
        private DeviceSnapshot _snapshot;

        /// <summary>Creates a card for <paramref name="snapshot"/>.</summary>
        public DeviceCardViewModel(
            DeviceSnapshot snapshot,
            VDriveEjectNotifier ejectNotifier,
            IFirmwareUpdatePresenter updatePresenter,
            Action<DeviceSnapshot> configureRequested,
            Func<Task> scanRequested)
        {
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _ejectNotifier = ejectNotifier ?? throw new ArgumentNullException(nameof(ejectNotifier));
            _updatePresenter = updatePresenter ?? throw new ArgumentNullException(nameof(updatePresenter));
            _configureRequested = configureRequested ?? throw new ArgumentNullException(nameof(configureRequested));
            _scanRequested = scanRequested ?? throw new ArgumentNullException(nameof(scanRequested));

            ConfigureCommand = new RelayCommand(Configure);
            SecondaryActionCommand = new AsyncRelayCommand(RunSecondaryActionAsync);
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
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusSeverity));
            OnPropertyChanged(nameof(IsDemoMode));
            OnPropertyChanged(nameof(PrimaryActionCaption));
            OnPropertyChanged(nameof(CanCheckForUpdates));
            OnPropertyChanged(nameof(SecondaryActionCaption));
            OnPropertyChanged(nameof(CanEject));

            EjectCommand.NotifyCanExecuteChanged();
        }

        private void Configure()
        {
            _configureRequested(_snapshot);
        }

        private Task RunSecondaryActionAsync()
        {
            if (CanCheckForUpdates)
            {
                return _updatePresenter.PresentAsync(_snapshot);
            }

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
