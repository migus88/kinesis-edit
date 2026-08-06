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
    /// <b>The roster is dynamic.</b> A card exists once its device has been detected at least once
    /// this session, and stays — as <see cref="DeviceCardState.Resting"/> — if the drive later
    /// goes away. So the dashboard starts empty (the empty state still renders), cards genuinely
    /// insert mid-session, and Resting is the state a card <em>falls to</em> rather than the state
    /// every catalog device is born in.
    /// </para>
    /// <para>
    /// The card list is heterogeneous: <see cref="DeviceCardViewModel"/>s first, then at most one
    /// <see cref="WebToolCardViewModel"/> pinned strictly last. <see cref="HasDevices"/> counts
    /// device cards only — counting the web-tool card would make it permanently true and the empty
    /// state unreachable.
    /// </para>
    /// </summary>
    public sealed class DashboardViewModel : ViewModelBase, IDisposable
    {
        /// <summary>Heading over the card grid (docs/design/mockups.md §1b).</summary>
        public const string HeaderTitleText = "Devices";

        /// <summary>Caption of the header's rescan button (docs/design/mockups.md §1b).</summary>
        public const string ScanAllActionCaption = "Scan all";

        // "3 of 7 known devices present · list updates itself" (docs/design/mockups.md §1b). The
        // noun is pluralised on the detected count, not on the catalog total.
        private const string SubtitleTemplate = "{0} of {1} known device{2} present · list updates itself";

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

        /// <summary>
        /// Every card in render order: the device cards, then the web-tool card last. Typed to the
        /// base because the two kinds have different anatomies and different templates.
        /// </summary>
        public ObservableCollection<DashboardCardViewModel> Devices { get; } = [];

        /// <summary>The drive-backed cards alone, in render order.</summary>
        public IReadOnlyList<DeviceCardViewModel> DeviceCards => [.. Devices.OfType<DeviceCardViewModel>()];

        /// <summary>
        /// How many drive-backed cards there are. The device cards occupy the front of
        /// <see cref="Devices"/> and the web-tool card, when shown, is the single trailing item.
        /// </summary>
        public int DeviceCardCount => _isWebToolCardShown ? Devices.Count - 1 : Devices.Count;

        /// <summary>The troubleshoot panel shown while no device has ever been detected.</summary>
        public NoDeviceViewModel EmptyState { get; }

        /// <summary>Heading over the card grid.</summary>
        public string HeaderTitle => HeaderTitleText;

        /// <summary>"N of 7 known devices present · list updates itself".</summary>
        public string HeaderSubtitle => FormatSubtitle(_detectedCount, KnownDeviceCount);

        /// <summary>Caption of the header's rescan button.</summary>
        public string ScanAllCaption => ScanAllActionCaption;

        /// <summary>Whether any device card exists — the web-tool card deliberately does not count.</summary>
        public bool HasDevices => DeviceCardCount > 0;

        /// <summary>Whether the troubleshoot empty state should be shown instead of the cards.</summary>
        public bool IsEmpty => !HasDevices;

        /// <summary>Whether a detection pass is in flight; the cards render it as their Scanning state.</summary>
        public bool IsRefreshing
        {
            get => _isRefreshing;
            private set => SetProperty(ref _isRefreshing, value);
        }

        /// <summary>Whether incoming refreshes are being parked instead of applied.</summary>
        public bool IsRefreshSuspended => _isRefreshSuspended;

        /// <summary>Re-runs v-Drive detection (the header's 'Scan all' and every card's scan button).</summary>
        public IAsyncRelayCommand ScanCommand { get; }

        /// <summary>Raised when the user asks to open a device — Configure, Demo Mode, or the empty state's demo button.</summary>
        public event Action<DeviceSnapshot>? ConfigureRequested;

        private readonly DeviceMonitorService _monitor;
        private readonly VDriveEjectNotifier _ejectNotifier;
        private readonly HashSet<DeviceId> _everDetected = [];
        private readonly WebToolCardViewModel? _webToolCard;
        private IReadOnlyList<DeviceSnapshot>? _pendingSnapshots;
        private bool _hasPendingRefreshActivity;
        private bool _isRefreshSuspended;
        private bool _isWebToolCardShown;
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

            var webToolDevice = WebToolCardViewModel.WebToolDevices().FirstOrDefault();

            _webToolCard = webToolDevice is null ? null : new WebToolCardViewModel(webToolDevice, urlLauncher);

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
        /// Parks incoming refreshes instead of applying them. Called by the view while a card's own
        /// button is under the pointer or holds keyboard focus, so a 2 s refresh can never move,
        /// relabel or disable the control the user is about to click.
        /// <para>
        /// The view owns that detection because pointer and focus are Avalonia's business and view
        /// models stay toolkit-free (docs/app/app-shell.md, invariant 8). Calling this while
        /// already suspended is a no-op, deliberately: a single <see cref="ResumeRefresh"/> must
        /// always be enough to un-stick the list, whatever the view's own bookkeeping did.
        /// </para>
        /// </summary>
        public void SuspendRefresh()
        {
            if (_isRefreshSuspended)
            {
                return;
            }

            _isRefreshSuspended = true;

            OnPropertyChanged(nameof(IsRefreshSuspended));
        }

        /// <summary>
        /// Resumes applying refreshes and applies whatever arrived meanwhile. Only the newest
        /// parked snapshot list is applied — earlier ones describe drives that have since been
        /// re-scanned — and nothing is lost, because the newest list is a complete picture rather
        /// than a delta. Resuming with nothing parked changes nothing.
        /// <para>
        /// The one thing a complete picture does <em>not</em> subsume is the set of devices ever
        /// detected this session, which is why <see cref="Apply"/> accumulates that set even while
        /// parked rather than leaving it to the list that finally lands here.
        /// </para>
        /// </summary>
        public void ResumeRefresh()
        {
            if (!_isRefreshSuspended)
            {
                return;
            }

            _isRefreshSuspended = false;

            OnPropertyChanged(nameof(IsRefreshSuspended));

            var pendingSnapshots = _pendingSnapshots;
            var hasPendingRefreshActivity = _hasPendingRefreshActivity;

            _pendingSnapshots = null;
            _hasPendingRefreshActivity = false;

            if (pendingSnapshots is not null)
            {
                ApplySnapshots(pendingSnapshots);
            }

            if (hasPendingRefreshActivity)
            {
                UpdateRefreshActivity();
            }
        }

        /// <summary>
        /// Reconciles the card list with <paramref name="snapshots"/>: a card exists for every
        /// device that is detected now or was detected earlier this session, keyed by the scanned
        /// catalog slot and updated in place so selection and scroll position survive a refresh.
        /// While suspended the list is parked instead (see <see cref="SuspendRefresh"/>).
        /// </summary>
        public void Apply(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            ArgumentNullException.ThrowIfNull(snapshots);

            // Recorded before the suspend check, and deliberately outside the parking that
            // supersedes: "detected at least once this session" is an accumulation, and a newer
            // list does not subsume its predecessors for it the way it does for every other field.
            // A device plugged in and pulled out again entirely inside one suspend window would
            // otherwise never enter the set, and would end up with no Resting card at all — while
            // the same sequence with the pointer elsewhere leaves one.
            RecordEverDetected(snapshots);

            if (_isRefreshSuspended)
            {
                _pendingSnapshots = snapshots;

                return;
            }

            ApplySnapshots(snapshots);
        }

        private void RecordEverDetected(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            foreach (var snapshot in snapshots)
            {
                if (snapshot.IsDetected && IsDashboardDevice(snapshot.Device))
                {
                    _everDetected.Add(snapshot.ScannedDeviceId);
                }
            }
        }

        private void ApplySnapshots(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            var detectedCount = snapshots.Count(
                snapshot => snapshot.IsDetected && IsDashboardDevice(snapshot.Device));

            var roster = snapshots
                .Where(snapshot => _everDetected.Contains(snapshot.ScannedDeviceId))
                .ToList();

            RemoveMissingCards(roster);
            MergeCards(roster);
            UpdateWebToolCard();

            _detectedCount = detectedCount;

            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(DeviceCardCount));
            OnPropertyChanged(nameof(DeviceCards));
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(IsEmpty));
        }

        private void RemoveMissingCards(IReadOnlyList<DeviceSnapshot> roster)
        {
            // Bounded by the device-card region: the web-tool card is not keyed by a scanned slot
            // and is never a candidate for removal here.
            for (var index = DeviceCardCount - 1; index >= 0; index--)
            {
                var card = (DeviceCardViewModel)Devices[index];

                if (!roster.Any(snapshot => snapshot.ScannedDeviceId == card.ScannedDeviceId))
                {
                    Devices.RemoveAt(index);
                }
            }
        }

        private void MergeCards(IReadOnlyList<DeviceSnapshot> roster)
        {
            // New cards land at the end of the device-card region and existing cards never move:
            // the design animates a device appearing mid-session as an insertion at the end of the
            // list, and re-sorting into catalog order would shift every card below it instead.
            // The first pass therefore fixes the order, and the snapshots arrive in catalog order.
            foreach (var snapshot in roster)
            {
                var existingIndex = IndexOf(snapshot.ScannedDeviceId);

                if (existingIndex >= 0)
                {
                    ((DeviceCardViewModel)Devices[existingIndex]).Update(snapshot);

                    continue;
                }

                // Insert rather than Add: the web-tool card, when shown, is the trailing item and
                // stays pinned there.
                Devices.Insert(DeviceCardCount, CreateCard(snapshot));
            }
        }

        private void UpdateWebToolCard()
        {
            if (_webToolCard is null)
            {
                return;
            }

            // Shown only alongside at least one device card: on its own it would fill a dashboard
            // that has nothing to configure and hide the troubleshoot empty state behind it.
            var shouldShow = DeviceCardCount > 0;

            if (shouldShow == _isWebToolCardShown)
            {
                return;
            }

            if (shouldShow)
            {
                _isWebToolCardShown = true;

                Devices.Add(_webToolCard);

                return;
            }

            _isWebToolCardShown = false;

            Devices.Remove(_webToolCard);
        }

        private int IndexOf(DeviceId scannedDeviceId)
        {
            var deviceCardCount = DeviceCardCount;

            for (var index = 0; index < deviceCardCount; index++)
            {
                if (((DeviceCardViewModel)Devices[index]).ScannedDeviceId == scannedDeviceId)
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
            // Deferred with the snapshots, and for the same reason: the Scanning state relabels and
            // disables a card's own scan button, so letting it through while the pointer is on that
            // button is exactly the stolen click deferral exists to prevent.
            if (_isRefreshSuspended)
            {
                _hasPendingRefreshActivity = true;

                return;
            }

            UpdateRefreshActivity();
        }

        private void UpdateRefreshActivity()
        {
            IsRefreshing = _monitor.IsRefreshing;

            for (var index = 0; index < DeviceCardCount; index++)
            {
                ((DeviceCardViewModel)Devices[index]).IsScanning = _isRefreshing;
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
