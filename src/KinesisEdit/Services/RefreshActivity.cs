namespace KinesisEdit.Services
{
    /// <summary>
    /// The refresh state of <see cref="DeviceMonitorService"/>, extracted so the service stays a
    /// detection loop instead of also being a state machine. It owns two things that belong
    /// together because they change at the same three moments:
    /// <list type="bullet">
    /// <item>the gate that serializes refreshes — a call arriving while one runs marks the running
    /// one to <em>repeat</em> and returns, because <c>VDriveMonitor.Poll</c> discards an
    /// overlapping poll outright;</item>
    /// <item>the two facts the dashboard renders — whether a refresh is in flight (the "Scanning"
    /// card state) and when the last one completed (the "refreshed 0.4s ago" readout).</item>
    /// </list>
    /// <see cref="Changed"/> is raised outside the lock, on whatever thread caused the transition —
    /// usually the thread-pool timer. Marshaling it onto the UI thread is the service's job.
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

        /// <summary>The last value passed to <see cref="Stamp"/>; null before the first one.</summary>
        public DateTimeOffset? LastRefreshedUtc
        {
            get
            {
                lock (_syncRoot)
                {
                    return _lastRefreshedUtc;
                }
            }
        }

        /// <summary>
        /// How many refreshes have run to completion since this instance was created — every call
        /// to <see cref="Stamp"/>, and nothing else.
        /// <para>
        /// <b>It counts completed passes, not requests and not timer ticks.</b> A
        /// <c>Refresh()</c> arriving while one runs is coalesced into a repeat of the running pass
        /// (see <see cref="TryBegin"/>), so two callers can share one increment; a pass that threw
        /// unwinds through <see cref="End"/> and never reaches <see cref="Stamp"/>, so it adds
        /// nothing. It is what the empty state's "rescanned N times" reads, which is a claim about
        /// scans that actually happened.
        /// </para>
        /// </summary>
        public int CompletedRefreshCount
        {
            get
            {
                lock (_syncRoot)
                {
                    return _completedRefreshCount;
                }
            }
        }

        /// <summary>Raised after <see cref="IsRefreshing"/> or <see cref="LastRefreshedUtc"/> changes.</summary>
        public event Action? Changed;

        private readonly object _syncRoot = new();
        private bool _isRefreshing;
        private bool _isRepeatRequested;
        private DateTimeOffset? _lastRefreshedUtc;
        private int _completedRefreshCount;

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

        /// <summary>
        /// Records that a refresh ran to completion at <paramref name="timestamp"/>. The time and
        /// the count move together, under one lock, because they are the same fact seen twice:
        /// <em>a pass finished</em>.
        /// </summary>
        public void Stamp(DateTimeOffset timestamp)
        {
            lock (_syncRoot)
            {
                _lastRefreshedUtc = timestamp;
                _completedRefreshCount++;
            }

            Changed?.Invoke();
        }
    }
}
