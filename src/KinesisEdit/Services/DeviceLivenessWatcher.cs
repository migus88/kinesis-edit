namespace KinesisEdit.Services
{
    /// <summary>
    /// Watches the drives a completed scan already found, and asks
    /// <see cref="DeviceMonitorService.Refresh"/> for a fresh pass the moment one of them stops
    /// being mounted — an eject, an unplug, a volume unmounted from the Finder.
    /// <para>
    /// <b>This is a liveness check, never discovery.</b> Invariant 5 of docs/app/app-shell.md is
    /// unchanged: nothing here looks for a drive the app has not already seen, so plugging a board
    /// in while the app is open still does nothing until the user scans. All this closes is the
    /// opposite direction — a card, a snapshot list and an open <see cref="DeviceSession"/> that go
    /// on describing a volume the operating system removed. Nothing in the app may say the list
    /// keeps itself up to date, because it still does not (deviation 25).
    /// </para>
    /// <para>
    /// <b>It has no <c>Start</c>, no <c>Stop</c> and no <c>IsPolling</c>.</b> Construction and
    /// <see cref="Dispose"/> are the whole public surface: it arms itself off the monitor's own
    /// <see cref="DeviceMonitorService.Updated"/> when a pass reports a connected drive, and
    /// disarms when the last one is gone. With nothing connected — the state the app spends most of
    /// its life in — no timer is armed, nothing ticks, and <see cref="IVolumeLivenessProbe"/> is
    /// never called.
    /// </para>
    /// <para>
    /// <b>A tick that finds everything present does nothing at all.</b> That matters: a
    /// <see cref="DeviceMonitorService.Refresh"/> lights <c>IsRefreshing</c>, which puts every
    /// dashboard card into its Scanning face, so a watcher that refreshed on a schedule would make
    /// the whole grid flicker twice a minute. It refreshes only when a drive has actually vanished,
    /// which is a moment the user is about to see the consequences of anyway.
    /// </para>
    /// <para>
    /// It lives at the composition root rather than on the dashboard, so it runs with an editor open
    /// too: <c>MainWindowViewModel</c> already re-points the open session from every pass, which
    /// flips <see cref="DeviceSession.Health"/> to <see cref="VDriveHealth.Error"/> and turns the
    /// editor's status chip to <c>v-Drive Error</c>. No editor code is involved, and there is still
    /// no "Keyboard Connection Lost" dialog (that stays with the editor issues).
    /// </para>
    /// </summary>
    public sealed class DeviceLivenessWatcher : IDisposable
    {
        private static readonly TimeSpan _defaultInterval = TimeSpan.FromSeconds(2);

        private readonly DeviceMonitorService _monitor;
        private readonly IVolumeLivenessProbe _probe;
        private readonly IVDriveWriteActivity _writeActivity;
        private readonly TimeSpan _interval;
        private readonly object _syncRoot = new();
        private readonly object _tickGate = new();
        private readonly List<string> _watchedPaths = [];
        private Timer? _timer;
        private bool _isDisposed;

        /// <summary>
        /// Watches the drives <paramref name="monitor"/> reports, checking each through
        /// <paramref name="probe"/> every <paramref name="interval"/> (2 seconds by default) and
        /// skipping any tick during which <paramref name="writeActivity"/> reports a write in
        /// flight. Nothing is armed here — the first <see cref="DeviceMonitorService.Updated"/> that
        /// carries a connected drive does that.
        /// </summary>
        public DeviceLivenessWatcher(
            DeviceMonitorService monitor,
            IVolumeLivenessProbe probe,
            IVDriveWriteActivity writeActivity,
            TimeSpan? interval = null)
        {
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _probe = probe ?? throw new ArgumentNullException(nameof(probe));
            _writeActivity = writeActivity ?? throw new ArgumentNullException(nameof(writeActivity));
            _interval = interval ?? _defaultInterval;

            _monitor.Updated += OnMonitorUpdated;
        }

        private void OnMonitorUpdated(DeviceMonitorUpdate update)
        {
            if (_isDisposed)
            {
                return;
            }

            var watched = new List<string>();

            foreach (var snapshot in update.Snapshots)
            {
                // Detected is the whole test, and it is exactly the right one: a demo snapshot is
                // NotDetected by construction, so this excludes every fixture drive without naming
                // demo mode — which matters, because probing the synthetic kinesis-edit://demo/
                // root of a fixture would stat a path that is not a path, forever.
                //
                // Deliberately NOT filtered on IsDemoMode. That property is "not connected OR not
                // writable" (specs/03-vdrive-and-files.md §3.5), so testing it here would drop the
                // one case detection already accounts for: a CannotAccess drive is physically
                // mounted, has a real RootPath and draws a real card, and unplugging it must lose
                // that card like any other. Writability is a question about editing, not presence.
                if (snapshot.IsDetected && snapshot.Location is not null)
                {
                    watched.Add(snapshot.Location.RootPath);
                }
            }

            lock (_syncRoot)
            {
                _watchedPaths.Clear();
                _watchedPaths.AddRange(watched);

                if (_watchedPaths.Count > 0)
                {
                    _timer ??= new Timer(OnTimerTick, null, _interval, _interval);
                }
                else
                {
                    _timer?.Dispose();
                    _timer = null;
                }
            }
        }

        private void OnTimerTick(object? state)
        {
            try
            {
                RunTick();
            }
            catch (Exception)
            {
                // A liveness check must never take down the process from a timer thread; the next
                // tick simply retries. This is the shape DeviceMonitorService's own deleted tick
                // had, kept for the same reason.
            }
        }

        private void RunTick()
        {
            // A tick that overruns its interval — a refresh runs inside one — must not be joined by
            // the next, or one vanished drive would ask for two scans. The same gate
            // VDriveMonitor.Poll uses, and for the same reason: the overlapping tick is dropped
            // rather than queued, because it would have asked exactly what this one is asking.
            if (!Monitor.TryEnter(_tickGate))
            {
                return;
            }

            try
            {
                ProbeWatchedPaths();
            }
            finally
            {
                Monitor.Exit(_tickGate);
            }
        }

        private void ProbeWatchedPaths()
        {
            if (_isDisposed || _writeActivity.IsWriting)
            {
                // A save is mid-flight: the drive is demonstrably there, and a scan that landed on
                // top of one would re-read the very files being rewritten.
                return;
            }

            string[] watched;

            lock (_syncRoot)
            {
                if (_watchedPaths.Count == 0)
                {
                    return;
                }

                watched = [.. _watchedPaths];
            }

            foreach (var rootPath in watched)
            {
                if (_probe.IsPresent(rootPath))
                {
                    continue;
                }

                // One refresh for the whole tick, however many drives vanished at once. Refresh is
                // already serialized and republishes Snapshots itself, and its Updated re-derives
                // the watched set above — which is what disarms this timer when the last drive is
                // gone. Called outside the lock: the update can arrive on this very thread.
                _monitor.Refresh();

                return;
            }
        }

        /// <summary>
        /// Unsubscribes and disarms, in the reverse of construction. Idempotent; a tick already in
        /// flight returns without probing.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _monitor.Updated -= OnMonitorUpdated;

            lock (_syncRoot)
            {
                _timer?.Dispose();
                _timer = null;
                _watchedPaths.Clear();
            }
        }
    }
}
