using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The device dashboard (specs/10-apps-and-ui.md, "SmartSetMaster … dashboard apps";
    /// docs/design/mockups.md §1b): a header, a grid of cards, and the troubleshoot empty state of
    /// specs/11-feature-dialogs.md §11.8 when there is nothing to show.
    /// <para>
    /// <b>The roster is what the last scan found.</b> A card exists for every detected,
    /// programmable device and for nothing else: plug a board in and scan, and its card inserts at
    /// the end of the list; unplug it and scan, and the card goes. When the last one goes
    /// <see cref="HasDevices"/> turns false and the empty state renders again.
    /// </para>
    /// <para>
    /// The card list is homogeneous — one kind, <see cref="DeviceCardViewModel"/>. A board this app
    /// cannot edit is not on this screen at all, which is the design's own rule that an absent
    /// feature is not rendered rather than shown as a card with nothing to configure.
    /// </para>
    /// </summary>
    public sealed class DashboardViewModel : ViewModelBase, IDisposable
    {
        /// <summary>Heading over the card grid (docs/design/mockups.md §1b).</summary>
        public const string HeaderTitleText = "Devices";

        /// <summary>Caption of the header's rescan button (docs/design/mockups.md §1b).</summary>
        public const string ScanAllActionCaption = "Scan all";

        // "3 of 7 known devices present" (docs/design/mockups.md §1b, minus its "list updates
        // itself" clause — scanning is manual). The noun is pluralised on the detected count, not
        // on the catalog total.
        private const string SubtitleTemplate = "{0} of {1} known device{2} present";

        /// <summary>
        /// How many devices this app can serve — the denominator of the header subtitle. Every
        /// programmable catalog entry counts, whether or not it was ever seen.
        /// </summary>
        public static int KnownDeviceCount { get; } = DeviceCatalog.All.Count(IsDashboardDevice);

        /// <summary>
        /// Whether the dashboard is about <paramref name="device"/> at all. One predicate serves
        /// both halves of "N of 7": the subtitle's denominator counts the catalog through it and
        /// its numerator counts snapshots through it, and it is what decides whether a detected
        /// drive gets a card.
        /// <para>
        /// It has to be one predicate because the two sets are not the same by construction. The
        /// scanner walks the whole catalog, and the never-shipped CROSSFIRE keypad is not
        /// programmable but <em>is</em> detectable — it carries a volume label. Counted by
        /// detection alone it would produce an eighth card with an empty meta line beside a
        /// subtitle reading "1 of 7".
        /// </para>
        /// </summary>
        public static bool IsDashboardDevice(DeviceDefinition device)
        {
            ArgumentNullException.ThrowIfNull(device);

            return device.IsProgrammable;
        }

        /// <summary>
        /// Formats the header subtitle. Static and public so the wording is asserted without
        /// standing a dashboard up.
        /// </summary>
        public static string FormatSubtitle(int detectedCount, int knownCount)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                SubtitleTemplate,
                detectedCount,
                knownCount,
                detectedCount == 1 ? string.Empty : "s");
        }

        /// <summary>Every card in render order — one kind, one collection, no pinned trailing item.</summary>
        public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];

        /// <summary>The troubleshoot panel shown while nothing is detected.</summary>
        public NoDeviceViewModel EmptyState { get; }

        /// <summary>Heading over the card grid.</summary>
        public string HeaderTitle => HeaderTitleText;

        /// <summary>"N of 7 known devices present".</summary>
        public string HeaderSubtitle => FormatSubtitle(_detectedCount, KnownDeviceCount);

        /// <summary>Caption of the header's rescan button.</summary>
        public string ScanAllCaption => ScanAllActionCaption;

        /// <summary>Whether any device is present, and so whether there are cards to show.</summary>
        public bool HasDevices => Devices.Count > 0;

        /// <summary>Whether the troubleshoot empty state should be shown instead of the cards.</summary>
        public bool IsEmpty => !HasDevices;

        /// <summary>Whether a detection pass is in flight; the cards render it as their Scanning state.</summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set => SetProperty(ref _isRefreshing, value);
        }

        /// <summary>Re-runs v-Drive detection (the header's 'Scan all' and every card's scan button).</summary>
        public IAsyncRelayCommand ScanCommand { get; }

        /// <summary>Raised when the user asks to open a device — Configure, or the empty state's demo button.</summary>
        public event Action<DeviceSnapshot>? ConfigureRequested;

        private readonly DeviceMonitorService _monitor;
        private readonly VDriveEjectNotifier _ejectNotifier;
        private bool _isRefreshing;
        private int _detectedCount;
        private bool _isDisposed;

        /// <summary>Creates the dashboard over the detection loop and the shared eject flow.</summary>
        public DashboardViewModel(
            DeviceMonitorService monitor,
            VDriveEjectNotifier ejectNotifier,
            IUrlLauncher urlLauncher)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _ejectNotifier = ejectNotifier ?? throw new ArgumentNullException(nameof(ejectNotifier));

            ArgumentNullException.ThrowIfNull(urlLauncher);

            EmptyState = new NoDeviceViewModel(urlLauncher, RequestConfigure, ScanAsync);
            ScanCommand = new AsyncRelayCommand(ScanAsync);

            _monitor.Updated += OnMonitorUpdated;
            _monitor.RefreshActivityChanged += OnRefreshActivityChanged;

            Apply(_monitor.Snapshots);
            UpdateRefreshActivity();
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
        /// Reconciles the card list with <paramref name="snapshots"/>: a card exists for every
        /// device detected <em>now</em>, keyed by the scanned catalog slot and updated in place so
        /// selection and scroll position survive a scan. A device that has gone loses its card, and
        /// losing the last one brings the empty state back.
        /// </summary>
        public void Apply(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            ArgumentNullException.ThrowIfNull(snapshots);

            var roster = snapshots
                .Where(snapshot => snapshot.IsDetected && IsDashboardDevice(snapshot.Device))
                .ToList();

            RemoveMissingCards(roster);
            MergeCards(roster);

            _detectedCount = roster.Count;

            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void RemoveMissingCards(IReadOnlyList<DeviceSnapshot> roster)
        {
            for (var index = Devices.Count - 1; index >= 0; index--)
            {
                var card = Devices[index];

                if (!roster.Any(snapshot => snapshot.ScannedDeviceId == card.ScannedDeviceId))
                {
                    Devices.RemoveAt(index);
                }
            }
        }

        private void MergeCards(IReadOnlyList<DeviceSnapshot> roster)
        {
            // New cards land at the end of the list and existing cards never move: the design
            // animates a device appearing as an insertion at the end, and re-sorting into catalog
            // order would shift every card below it instead. The first pass therefore fixes the
            // order, and the snapshots arrive in catalog order.
            foreach (var snapshot in roster)
            {
                var existingIndex = IndexOf(snapshot.ScannedDeviceId);

                if (existingIndex >= 0)
                {
                    Devices[existingIndex].Update(snapshot);

                    continue;
                }

                Devices.Add(CreateCard(snapshot));
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
            return new DeviceCardViewModel(snapshot, _ejectNotifier, RequestConfigure, ScanAsync)
            {
                IsScanning = _isRefreshing
            };
        }

        private void OnMonitorUpdated(DeviceMonitorUpdate update)
        {
            Apply(update.Snapshots);
        }

        private void OnRefreshActivityChanged()
        {
            UpdateRefreshActivity();
        }

        private void UpdateRefreshActivity()
        {
            IsRefreshing = _monitor.IsRefreshing;

            // The empty state renders the same fact the cards do — a pass being in flight — and is
            // fed the same way, pushed down rather than subscribed, so there is one subscription to
            // the loop on this screen.
            EmptyState.SetRefreshActivity(_monitor.IsRefreshing);

            foreach (var card in Devices)
            {
                card.IsScanning = _isRefreshing;
            }
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
            _monitor.RefreshActivityChanged -= OnRefreshActivityChanged;
        }
    }
}
