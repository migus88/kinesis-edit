using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The device dashboard (specs/10-apps-and-ui.md, "SmartSetMaster … dashboard apps"): one
    /// card per device whose v-Drive was found, refreshed by the detection loop. The legacy
    /// dashboards carried a fixed two-card registry including "Coming Soon" placeholders; this
    /// app serves the whole catalog, so it shows detected devices only and falls back to the
    /// troubleshoot empty state of specs/11-feature-dialogs.md §11.8 when there are none.
    /// </summary>
    public sealed class DashboardViewModel : ViewModelBase, IDisposable
    {
        /// <summary>Cards for the currently detected devices, in device-catalog order.</summary>
        public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];

        /// <summary>The troubleshoot panel shown while no device is detected.</summary>
        public NoDeviceViewModel EmptyState { get; }

        /// <summary>Whether any device is currently detected.</summary>
        public bool HasDevices => Devices.Count > 0;

        /// <summary>Whether the troubleshoot empty state should be shown instead of the cards.</summary>
        public bool IsEmpty => !HasDevices;

        /// <summary>Re-runs v-Drive detection (the 'Scan for v-Drive' buttons).</summary>
        public IAsyncRelayCommand ScanCommand { get; }

        /// <summary>Raised when the user asks to open a device — Configure, Demo Mode, or the empty state's demo button.</summary>
        public event Action<DeviceSnapshot>? ConfigureRequested;

        private readonly DeviceMonitorService _monitor;
        private readonly VDriveEjectNotifier _ejectNotifier;
        private readonly IFirmwareUpdatePresenter _updatePresenter;
        private bool _isDisposed;

        /// <summary>Creates the dashboard over the detection loop, the shared eject flow, and the firmware update dialog.</summary>
        public DashboardViewModel(
            DeviceMonitorService monitor,
            VDriveEjectNotifier ejectNotifier,
            IFirmwareUpdatePresenter updatePresenter,
            IUrlLauncher urlLauncher)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _ejectNotifier = ejectNotifier ?? throw new ArgumentNullException(nameof(ejectNotifier));
            _updatePresenter = updatePresenter ?? throw new ArgumentNullException(nameof(updatePresenter));

            ArgumentNullException.ThrowIfNull(urlLauncher);

            EmptyState = new NoDeviceViewModel(urlLauncher, RequestConfigure, ScanAsync);
            ScanCommand = new AsyncRelayCommand(ScanAsync);

            _monitor.Updated += OnMonitorUpdated;

            Apply(_monitor.Snapshots);
        }

        /// <summary>
        /// Runs one detection pass now, on a thread-pool thread: a scan enumerates volumes and
        /// reads every present device's version file, and a stalled mount must not freeze the
        /// window. The results come back on the UI thread through the monitor's dispatcher.
        /// </summary>
        public Task ScanAsync()
        {
            return Task.Run(_monitor.Refresh);
        }

        /// <summary>
        /// Reconciles the card list with <paramref name="snapshots"/>: cards exist for detected
        /// devices only, are keyed by the scanned catalog slot, and are updated in place so the
        /// selection and scroll position survive a refresh.
        /// </summary>
        public void Apply(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            ArgumentNullException.ThrowIfNull(snapshots);

            var detected = snapshots.Where(snapshot => snapshot.IsDetected).ToList();

            RemoveMissingCards(detected);
            MergeCards(detected);

            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void RemoveMissingCards(IReadOnlyList<DeviceSnapshot> detected)
        {
            for (var index = Devices.Count - 1; index >= 0; index--)
            {
                if (!detected.Any(snapshot => snapshot.ScannedDeviceId == Devices[index].ScannedDeviceId))
                {
                    Devices.RemoveAt(index);
                }
            }
        }

        private void MergeCards(IReadOnlyList<DeviceSnapshot> detected)
        {
            // Cards below placedCount already sit at their final index. Counting placements
            // instead of trusting the snapshot index keeps the collection operations in range
            // even if two snapshots ever shared a key: the second one just updates the card the
            // first one placed rather than moving it past the end of the list.
            var placedCount = 0;

            foreach (var snapshot in detected)
            {
                var existingIndex = IndexOf(snapshot.ScannedDeviceId);

                if (existingIndex < 0)
                {
                    Devices.Insert(placedCount, CreateCard(snapshot));
                    placedCount++;

                    continue;
                }

                Devices[existingIndex].Update(snapshot);

                if (existingIndex < placedCount)
                {
                    continue;
                }

                if (existingIndex != placedCount)
                {
                    Devices.Move(existingIndex, placedCount);
                }

                placedCount++;
            }
        }

        private int IndexOf(DeviceId scannedDeviceId)
        {
            for (var index = 0; index < Devices.Count; index++)
            {
                if (Devices[index].ScannedDeviceId == scannedDeviceId)
                {
                    return index;
                }
            }

            return -1;
        }

        private DeviceCardViewModel CreateCard(DeviceSnapshot snapshot)
        {
            return new DeviceCardViewModel(snapshot, _ejectNotifier, _updatePresenter, RequestConfigure, ScanAsync);
        }

        private void OnMonitorUpdated(DeviceMonitorUpdate update)
        {
            Apply(update.Snapshots);
        }

        private void RequestConfigure(DeviceSnapshot snapshot)
        {
            ConfigureRequested?.Invoke(snapshot);
        }

        /// <summary>Unsubscribes from the detection loop. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _monitor.Updated -= OnMonitorUpdated;
        }
    }
}
