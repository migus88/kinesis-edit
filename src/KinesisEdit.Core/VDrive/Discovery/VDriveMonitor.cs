using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.VDrive.Discovery
{
    /// <summary>
    /// Polls an <see cref="IVDriveScanner"/> on a timer and tracks a <see cref="VDriveStatus"/>
    /// for every detectable catalog device, mirroring the master-dashboard scan of
    /// specs/03-vdrive-and-files.md §3.3. The spec specifies timer polling without an interval;
    /// the default here is 2 seconds. Demo mode is not a monitor state: it is the app-level
    /// condition that no device is <see cref="VDriveConnectionStatus.Connected"/> (03 §3.5) and
    /// is derived by callers from <see cref="Statuses"/>.
    /// </summary>
    public sealed class VDriveMonitor : IDisposable
    {
        private static readonly TimeSpan _defaultPollInterval = TimeSpan.FromSeconds(2);

        /// <summary>
        /// Snapshot of the latest status per detectable device. Before the first poll every
        /// device reports <see cref="VDriveConnectionStatus.NotDetected"/>. Each read returns an
        /// independent copy, safe to enumerate while polling continues.
        /// </summary>
        public IReadOnlyDictionary<DeviceId, VDriveStatus> Statuses
        {
            get
            {
                lock (_syncRoot)
                {
                    return new Dictionary<DeviceId, VDriveStatus>(_statuses);
                }
            }
        }

        /// <summary>
        /// Raised once per device whose status value changed during a poll, after the
        /// <see cref="Statuses"/> snapshot has been updated.
        /// </summary>
        public event Action<VDriveStatusChange>? StatusChanged;

        private readonly IVDriveScanner _scanner;
        private readonly TimeSpan _pollInterval;
        private readonly object _syncRoot = new();
        private readonly object _pollGate = new();
        private readonly Dictionary<DeviceId, VDriveStatus> _statuses;
        private Timer? _timer;
        private bool _isDisposed;

        /// <summary>
        /// Creates a monitor over <paramref name="scanner"/>; <paramref name="pollInterval"/>
        /// overrides the 2-second default polling interval.
        /// </summary>
        public VDriveMonitor(IVDriveScanner scanner, TimeSpan? pollInterval = null)
        {
            _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
            _pollInterval = pollInterval ?? _defaultPollInterval;
            _statuses = new Dictionary<DeviceId, VDriveStatus>();

            foreach (var device in DeviceCatalog.All)
            {
                if (VDriveDetection.IsDetectable(device))
                {
                    _statuses[device.Id] = VDriveStatus.NotDetected;
                }
            }
        }

        /// <summary>
        /// Runs one scan pass and raises <see cref="StatusChanged"/> for every device whose
        /// status changed. A call that overlaps a running poll returns immediately instead of
        /// queuing. Callable directly (tests do); the timer started by <see cref="Start"/> calls
        /// the same method.
        /// </summary>
        public void Poll()
        {
            if (!Monitor.TryEnter(_pollGate))
            {
                return;
            }

            try
            {
                var locations = _scanner.Scan();
                List<VDriveStatusChange> changes;

                lock (_syncRoot)
                {
                    changes = ApplyScanResult(locations);
                }

                foreach (var change in changes)
                {
                    StatusChanged?.Invoke(change);
                }
            }
            finally
            {
                Monitor.Exit(_pollGate);
            }
        }

        /// <summary>
        /// Runs one synchronous initial <see cref="Poll"/> and then starts timer polling at the
        /// configured interval, so <see cref="Statuses"/> is populated when this returns.
        /// Calling Start while already started is a no-op.
        /// </summary>
        public void Start()
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            var started = false;

            lock (_syncRoot)
            {
                if (_timer is null)
                {
                    _timer = new Timer(OnTimerTick, null, _pollInterval, _pollInterval);
                    started = true;
                }
            }

            if (started)
            {
                Poll();
            }
        }

        /// <summary>Stops timer polling; <see cref="Statuses"/> keeps its last snapshot. Safe to call when not started.</summary>
        public void Stop()
        {
            lock (_syncRoot)
            {
                _timer?.Dispose();
                _timer = null;
            }
        }

        private List<VDriveStatusChange> ApplyScanResult(IReadOnlyList<VDriveLocation> locations)
        {
            var changes = new List<VDriveStatusChange>();
            var locationsByDeviceId = new Dictionary<DeviceId, VDriveLocation>();

            foreach (var location in locations)
            {
                locationsByDeviceId.TryAdd(location.Device.Id, location);
            }

            foreach (var deviceId in _statuses.Keys.ToArray())
            {
                var current = CreateStatus(locationsByDeviceId.GetValueOrDefault(deviceId));
                var previous = _statuses[deviceId];

                if (!current.Equals(previous))
                {
                    _statuses[deviceId] = current;

                    changes.Add(new VDriveStatusChange
                    {
                        DeviceId = deviceId,
                        Previous = previous,
                        Current = current
                    });
                }
            }

            return changes;
        }

        private static VDriveStatus CreateStatus(VDriveLocation? location)
        {
            if (location is null)
            {
                return VDriveStatus.NotDetected;
            }

            return new VDriveStatus
            {
                Status = location.IsWritable
                    ? VDriveConnectionStatus.Connected
                    : VDriveConnectionStatus.CannotAccess,
                Location = location
            };
        }

        private void OnTimerTick(object? state)
        {
            try
            {
                Poll();
            }
            catch (Exception)
            {
                // A scanner failure must never take down the process from the timer thread;
                // the next tick simply retries. IVDriveScanner implementations should not throw.
            }
        }

        /// <summary>Stops polling and releases the timer. Safe to call multiple times.</summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            Stop();
        }
    }
}
