namespace KinesisEdit.Services
{
    /// <summary>
    /// Owns the one active editing session of the single-window shell: opening a device records
    /// it (specs/10-apps-and-ui.md, "the Configure button sets demo mode from the device's
    /// connected/writable state, records the active device"), Home ends it, and every detection
    /// pass re-points it at the device's latest snapshot. Chooses the device's
    /// notification-preference store: the v-Drive's <c>app_settings.txt</c> for a connected drive,
    /// a read-only view of that same file for a demo session over a drive that is present but not
    /// writable, and the null store when there is no drive at all (specs/08-settings.md §3).
    /// </summary>
    public sealed class DeviceSessionManager : IDeviceSessionAccessor
    {
        /// <summary>The session currently open, or null while the dashboard is showing.</summary>
        public DeviceSession? Active { get; private set; }

        /// <summary>Raised whenever <see cref="Active"/> changes, with the new session or null.</summary>
        public event Action<DeviceSession?>? ActiveChanged;

        private readonly ISettingsService _settings;

        /// <summary>Creates the manager; <paramref name="settings"/> backs v-Drive suppression stores.</summary>
        public DeviceSessionManager(ISettingsService settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>Opens a session for <paramref name="device"/>, replacing any session already active.</summary>
        public DeviceSession Begin(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var session = new DeviceSession(device, CreateSuppressionStore(device));
            Active = session;

            ActiveChanged?.Invoke(session);

            return session;
        }

        /// <summary>
        /// Re-points the active session at this refresh's snapshot of its device, matched by the
        /// scanned catalog slot so a Freestyle board that re-resolves mid-session still matches.
        /// Demo mode and the suppression store stay as they were when the session opened; only the
        /// live drive state moves. Does nothing when no session is open or the device is absent
        /// from <paramref name="snapshots"/>.
        /// </summary>
        public void UpdateActive(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            ArgumentNullException.ThrowIfNull(snapshots);

            var session = Active;

            if (session is null)
            {
                return;
            }

            foreach (var snapshot in snapshots)
            {
                if (snapshot.ScannedDeviceId == session.OpenedDevice.ScannedDeviceId)
                {
                    session.Update(snapshot);

                    return;
                }
            }
        }

        /// <summary>Ends the active session; safe to call when none is open.</summary>
        public void End()
        {
            if (Active is null)
            {
                return;
            }

            Active = null;

            ActiveChanged?.Invoke(null);
        }

        private INotificationSuppressionStore CreateSuppressionStore(DeviceSnapshot device)
        {
            if (device.Location is null)
            {
                return NullNotificationSuppressionStore.Instance;
            }

            var store = new VDriveNotificationSuppressionStore(_settings, device.Location);

            // The file is still there and still readable when the drive is merely not writable,
            // and specs/08-settings.md §3 bans saving in demo mode, not loading — so a demo
            // session over a real drive keeps the notifications the user hid on it.
            return device.IsDemoMode ? new ReadOnlyNotificationSuppressionStore(store) : store;
        }
    }
}
