namespace KinesisEdit.Services
{
    /// <summary>
    /// The refresh state of <see cref="DeviceMonitorService"/>, extracted so the service stays a
    /// detection loop instead of also being a state machine. It owns two things that are the same
    /// fact seen from two sides:
    /// <list type="bullet">
    /// <item>the gate that serializes refreshes — a call arriving while one runs marks the running
    /// one to <em>repeat</em> and returns, because <c>VDriveMonitor.Poll</c> discards an
    /// overlapping poll outright;</item>
    /// <item>whether a refresh is in flight, which is what the dashboard renders as its "Scanning"
    /// card state and the empty state as its "Scanning" button caption.</item>
    /// </list>
    /// <see cref="Changed"/> is raised outside the lock, on whatever thread caused the transition —
    /// a scan runs on a thread-pool thread, because a stalled mount must not freeze the window.
    /// Marshaling it onto the UI thread is the service's job.
    /// <para>
    /// It records nothing about <em>past</em> passes. Scanning is manual (see
    /// <see cref="DeviceMonitorService"/>), so no surface counts completed passes or ages against
    /// the last one — the app never claims to be watching on its own.
    /// </para>
    /// </summary>
    internal sealed class RefreshActivity
    {
        /// <summary>Whether a refresh is running right now.</summary>
        public bool IsRefreshing
        {
            get
            {
                lock (_syncRoot)
                {
                    return _isRefreshing;
                }
            }
        }

        /// <summary>Raised after <see cref="IsRefreshing"/> changes.</summary>
        public event Action? Changed;

        private readonly object _syncRoot = new();
        private bool _isRefreshing;
        private bool _isRepeatRequested;

        /// <summary>
        /// Claims the gate for a refresh. Returns false when one is already running, having marked
        /// it to repeat so the caller's request is honoured by that pass instead of being dropped.
        /// </summary>
        public bool TryBegin()
        {
            lock (_syncRoot)
            {
                if (_isRefreshing)
                {
                    _isRepeatRequested = true;

                    return false;
                }

                _isRefreshing = true;
                _isRepeatRequested = false;
            }

            Changed?.Invoke();

            return true;
        }

        /// <summary>
        /// Takes a pending repeat request, or releases the gate when there is none and reports
        /// false. <paramref name="isAllowed"/> is false once the service is disposed: the request
        /// is then dropped and the gate released rather than starting another pass.
        /// </summary>
        public bool TryRepeat(bool isAllowed)
        {
            lock (_syncRoot)
            {
                // Taking the request and releasing the running flag happen under the same lock,
                // so a request arriving now either wins this pass or starts a fresh refresh.
                if (_isRepeatRequested && isAllowed)
                {
                    _isRepeatRequested = false;

                    return true;
                }

                _isRefreshing = false;
                _isRepeatRequested = false;
            }

            Changed?.Invoke();

            return false;
        }

        /// <summary>Releases the gate unconditionally — the path a throwing refresh unwinds through.</summary>
        public void End()
        {
            bool hasChanged;

            lock (_syncRoot)
            {
                hasChanged = _isRefreshing;
                _isRefreshing = false;
                _isRepeatRequested = false;
            }

            if (hasChanged)
            {
                Changed?.Invoke();
            }
        }
    }
}
