namespace KinesisEdit.Services
{
    /// <summary>
    /// The device currently being edited: the snapshot it was opened from, the snapshot the
    /// detection loop last saw for it, whether editing runs in demo mode
    /// (specs/03-vdrive-and-files.md §3.5), and this device's <c>app_settings.txt</c> preferences
    /// (specs/08-settings.md §3).
    /// <para>
    /// Demo mode and the preferences store are fixed when the session opens — specs/10-apps-and-ui.md
    /// has Configure "set demo mode from the device's connected/writable state", once. Only
    /// <see cref="Device"/> and therefore <see cref="Health"/> follow the polling loop, which is
    /// what drives the editor's `v-Drive OK` / `v-Drive Error` indicator.
    /// </para>
    /// </summary>
    public sealed class DeviceSession
    {
        /// <summary>State of the device at the moment the session was opened.</summary>
        public DeviceSnapshot OpenedDevice { get; }

        /// <summary>The device being edited, re-pointed at every refresh by <see cref="Update"/>.</summary>
        public DeviceSnapshot Device { get; private set; }

        /// <summary>Whether this session runs without a connected, writable drive; fixed at open time.</summary>
        public bool IsDemoMode => OpenedDevice.IsDemoMode;

        /// <summary>
        /// Outcome of the latest version-file re-check for this session. A demo session has no
        /// drive to verify; a live session whose drive disappeared cannot pass the re-check, so it
        /// reports <see cref="VDriveHealth.Error"/> rather than falling back to 'Demo Mode'.
        /// </summary>
        public VDriveHealth Health
        {
            get
            {
                if (IsDemoMode)
                {
                    return VDriveHealth.Unknown;
                }

                return Device.IsDetected ? Device.Health : VDriveHealth.Error;
            }
        }

        /// <summary>
        /// The session's single <c>app_settings.txt</c>: preferences, custom swatches and
        /// "Don't ask this again" answers, loaded once and shared by every consumer. Anything in
        /// the app that reads or writes that file goes through here.
        /// </summary>
        public IAppPreferencesStore Preferences { get; }

        /// <summary>
        /// The same object as <see cref="Preferences"/>, seen through the narrow interface
        /// <see cref="NotificationService"/> depends on. Kept so the notification pipeline never
        /// learns about swatches or display preferences.
        /// </summary>
        public INotificationSuppressionStore SuppressionStore => Preferences;

        /// <summary>Creates a session for <paramref name="device"/> backed by <paramref name="preferences"/>.</summary>
        public DeviceSession(DeviceSnapshot device, IAppPreferencesStore preferences)
        {
            ArgumentNullException.ThrowIfNull(device);

            OpenedDevice = device;
            Device = device;
            Preferences = preferences ?? throw new ArgumentNullException(nameof(preferences));
        }

        /// <summary>Re-points the session at a newer snapshot of the same device.</summary>
        public void Update(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            Device = device;
        }
    }
}
