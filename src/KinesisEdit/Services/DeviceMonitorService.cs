using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The app-side detection loop of specs/10-apps-and-ui.md ("Detection loop" and
    /// "Startup / v-Drive scan / demo mode"): each refresh runs one
    /// <see cref="VDriveMonitor.Poll"/> and then re-reads and re-parses every present device's
    /// version file, producing an immutable <see cref="DeviceSnapshot"/> list plus the status
    /// transitions of that poll. The version file is re-read on every tick on purpose — it is
    /// both the "v-Drive OK / v-Drive Error" probe and the Freestyle Edge/Pro model resolution.
    /// Both events — <see cref="Updated"/> and <see cref="RefreshActivityChanged"/> — are marshaled
    /// through an <see cref="IUiDispatcher"/> because the monitor polls on a thread-pool timer.
    /// Serializing the refreshes, and the refresh state the dashboard renders
    /// (<see cref="IsRefreshing"/>, <see cref="LastRefreshedUtc"/>), live in
    /// <see cref="RefreshActivity"/>.
    /// </summary>
    public sealed class DeviceMonitorService : IDisposable
    {
        private static readonly TimeSpan _defaultRefreshInterval = TimeSpan.FromSeconds(2);

        /// <summary>The snapshots produced by the most recent refresh; empty before the first one.</summary>
        public IReadOnlyList<DeviceSnapshot> Snapshots { get; private set; } = [];

        /// <summary>
        /// Whether the polling timer is armed. The loop runs for as long as the app does — an
        /// open editor keeps it running, because specs/10-apps-and-ui.md requires the editor to
        /// "re-verify the device's version file every tick" to drive its status indicator.
        /// </summary>
        public bool IsPolling
        {
            get
            {
                lock (_syncRoot)
                {
                    return _timer is not null;
                }
            }
        }

        /// <summary>
        /// Whether a refresh is in flight right now — what the dashboard renders as its "Scanning"
        /// card state. Visual only: nothing cancels, waits on, or refuses a running refresh.
        /// </summary>
        public bool IsRefreshing => _activity.IsRefreshing;

        /// <summary>
        /// When the most recent refresh that ran to completion finished, read from
        /// <see cref="ISystemClock"/>; null until the first one does. This is what the dashboard's
        /// "refreshed 0.4s ago" readout ages against. A pass that found no drives, or that read
        /// nothing but unreadable version files, still counts — it looked, and that is what the
        /// readout claims. A pass that threw does not, so the readout keeps ageing while the loop
        /// is failing instead of reporting a scan that never happened.
        /// </summary>
        public DateTimeOffset? LastRefreshedUtc => _activity.LastRefreshedUtc;

        /// <summary>Raised once per refresh, on the UI thread, with that refresh's snapshots and changes.</summary>
        public event Action<DeviceMonitorUpdate>? Updated;

        /// <summary>
        /// Raised on the UI thread whenever <see cref="IsRefreshing"/> or
        /// <see cref="LastRefreshedUtc"/> changes. Separate from <see cref="Updated"/> because a
        /// refresh <em>starting</em> publishes no snapshots and yet must light the Scanning state,
        /// and because a coalesced repeat changes neither. Marshaled through the same
        /// <see cref="IUiDispatcher"/>, so a handler may touch view-model state directly.
        /// </summary>
        public event Action? RefreshActivityChanged;

        private readonly VDriveMonitor _monitor;
        private readonly IVDriveFileService _fileService;
        private readonly IUiDispatcher _dispatcher;
        private readonly ISystemClock _clock;
        private readonly TimeSpan _refreshInterval;
        private readonly object _syncRoot = new();
        private readonly RefreshActivity _activity = new();
        private readonly List<VDriveStatusChange> _pendingChanges = [];
        private Timer? _timer;
        private bool _isDisposed;

        /// <summary>
        /// Creates the service over an existing <paramref name="monitor"/> (owned and disposed by
        /// this service), the file service used to re-read version files, the dispatcher that
        /// marshals <see cref="Updated"/> and <see cref="RefreshActivityChanged"/> onto the UI
        /// thread, and the clock that stamps <see cref="LastRefreshedUtc"/>.
        /// </summary>
        public DeviceMonitorService(
            VDriveMonitor monitor,
            IVDriveFileService fileService,
            IUiDispatcher dispatcher,
            ISystemClock clock,
            TimeSpan? refreshInterval = null)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _refreshInterval = refreshInterval ?? _defaultRefreshInterval;

            _monitor.StatusChanged += OnStatusChanged;
            _activity.Changed += OnActivityChanged;
        }

        /// <summary>
        /// Runs one detection pass: poll the drives, re-read every present device's version file,
        /// and raise <see cref="Updated"/>. Never throws for a device whose version file is
        /// missing, unreadable, or unparseable — that device reports
        /// <see cref="VDriveHealth.Error"/> instead. A call after <see cref="Dispose"/> is a no-op.
        /// <para>
        /// Refreshes are serialized without blocking the caller: a call arriving while another
        /// refresh runs marks that refresh to repeat instead of scanning concurrently. Scanning
        /// concurrently is not an option — <see cref="VDriveMonitor.Poll"/> discards an
        /// overlapping poll outright, which would turn a user's 'Scan for v-Drive' landing on a
        /// timer tick into a no-op. Repeating means the explicit scan still produces a scan that
        /// observes the drives as of the request, and no status change is dropped on the way.
        /// </para>
        /// </summary>
        public void Refresh()
        {
            if (_isDisposed || !_activity.TryBegin())
            {
                return;
            }

            var isRefreshing = true;

            try
            {
                while (isRefreshing)
                {
                    RunRefresh();

                    isRefreshing = _activity.TryRepeat(!_isDisposed);
                }
            }
            finally
            {
                if (isRefreshing)
                {
                    _activity.End();
                }
            }
        }

        /// <summary>
        /// Runs one synchronous <see cref="Refresh"/> and then arms the polling timer, so
        /// <see cref="Snapshots"/> is populated when this returns. Starting twice is a no-op.
        /// </summary>
        public void Start()
        {
            if (_isDisposed)
            {
                return;
            }

            var started = false;

            lock (_syncRoot)
            {
                if (_timer is null)
                {
                    _timer = new Timer(OnTimerTick, null, _refreshInterval, _refreshInterval);
                    started = true;
                }
            }

            if (started)
            {
                Refresh();
            }
        }

        /// <summary>Disarms the polling timer; the last <see cref="Snapshots"/> stay available. Safe when not started.</summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        private void RunRefresh()
        {
            _monitor.Poll();

            var snapshots = CreateSnapshots();

            List<VDriveStatusChange> changes;

            lock (_syncRoot)
            {
                changes = [.. _pendingChanges];
            }

            Snapshots = snapshots;

            // Stamped only once the pass has produced its snapshots, and before they are
            // published: an Updated handler reading LastRefreshedUtc must never see the previous
            // pass's time, and a pass that threw on the way here must not claim to have happened.
            _activity.Stamp(_clock.UtcNow);

            var update = new DeviceMonitorUpdate
            {
                Snapshots = snapshots,
                Changes = changes
            };

            _dispatcher.Post(() => Updated?.Invoke(update));

            // Drop exactly what was just published, and only once it has been: anything the
            // monitor raises later stays queued for the next pass instead of vanishing.
            lock (_syncRoot)
            {
                _pendingChanges.RemoveRange(0, changes.Count);
            }
        }

        private IReadOnlyList<DeviceSnapshot> CreateSnapshots()
        {
            var statuses = _monitor.Statuses;
            var snapshots = new List<DeviceSnapshot>(statuses.Count);

            foreach (var device in DeviceCatalog.All)
            {
                if (statuses.TryGetValue(device.Id, out var status))
                {
                    snapshots.Add(CreateSnapshot(device, status));
                }
            }

            return snapshots;
        }

        private DeviceSnapshot CreateSnapshot(DeviceDefinition device, VDriveStatus status)
        {
            if (status.Location is null)
            {
                return DeviceSnapshot.CreateDemo(device) with
                {
                    Status = status.Status
                };
            }

            // A version file that cannot be read, or that carries no recognizable firmware
            // version, is the "v-Drive Error" state of specs/10-apps-and-ui.md.
            var parsed = ReadVersionFile(device, status.Location);
            var health = parsed?.KeyboardFirmware is null ? VDriveHealth.Error : VDriveHealth.Ok;
            var versionFile = parsed ?? VersionFileInfo.Empty;

            // Demo mode is "not connected OR not writable" (specs/03-vdrive-and-files.md §3.5),
            // so a CannotAccess drive opens the editor in demo mode just like no drive at all.
            var isDemoMode = status.Status != VDriveConnectionStatus.Connected;

            return new DeviceSnapshot
            {
                ScannedDeviceId = device.Id,
                Device = ResolveDevice(device, versionFile, health),
                Status = status.Status,
                Location = status.Location,
                VersionFile = versionFile,
                Firmware = FirmwareState.FromVersionFile(versionFile, isDemoMode),
                IsDemoMode = isDemoMode,
                Health = health
            };
        }

        private VersionFileInfo? ReadVersionFile(DeviceDefinition device, VDriveLocation location)
        {
            try
            {
                var lines = _fileService.ReadAllLines(location.VersionFilePath);

                return VersionFileParser.Parse(device.Id, lines);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                return null;
            }
        }

        private static DeviceDefinition ResolveDevice(DeviceDefinition device, VersionFileInfo versionFile, VDriveHealth health)
        {
            var isFreestyle = device.Id is DeviceId.FreestyleEdge or DeviceId.FreestylePro;

            if (!isFreestyle || health != VDriveHealth.Ok)
            {
                // Without a readable version file the label match of the scanner is the best
                // information available; re-deriving from an empty file would report an Edge.
                return device;
            }

            return DeviceCatalog.GetById(versionFile.ResolveFreestyleModel());
        }

        private void OnStatusChanged(VDriveStatusChange change)
        {
            lock (_syncRoot)
            {
                _pendingChanges.Add(change);
            }
        }

        private void OnActivityChanged()
        {
            // The transition happens on the polling thread; invariant 7 of docs/app/app-shell.md
            // makes marshaling this the service's job, exactly as it is for Updated.
            _dispatcher.Post(() => RefreshActivityChanged?.Invoke());
        }

        private void OnTimerTick(object? state)
        {
            try
            {
                Refresh();
            }
            catch (Exception)
            {
                // A refresh failure must never take down the process from the timer thread;
                // the next tick simply retries.
            }
        }

        /// <summary>Stops polling and releases the monitor. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            Stop();

            _activity.Changed -= OnActivityChanged;
            _monitor.StatusChanged -= OnStatusChanged;
            _monitor.Dispose();
        }
    }
}
