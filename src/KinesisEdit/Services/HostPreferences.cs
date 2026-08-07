namespace KinesisEdit.Services
{
    /// <summary>
    /// <b>Everything the app remembers about this machine's user</b> — theme, motion and the shell
    /// window's last geometry. Held by <see cref="IHostPreferencesStore"/> and persisted to a JSON
    /// file in the per-user configuration directory.
    /// <para>
    /// <b>This is not <c>app_settings.txt</c>.</b> That file lives on a keyboard's v-Drive, is
    /// per-device, travels with the board, and is reached through <see cref="IAppPreferencesStore"/>
    /// — its own screen section even says "stored per device". Nothing here is about any keyboard,
    /// which is exactly why it cannot live there: a preference stored on a drive is unreadable with
    /// no drive attached, and the window has to open before any drive is scanned. See
    /// docs/app/host-preferences.md.
    /// </para>
    /// <para>
    /// A record, and deliberately toolkit-free: only <see cref="ThemeApplier"/> and
    /// <see cref="MotionPreferenceApplier"/> — the two bridging services — turn these values into
    /// Avalonia state (app-shell.md invariant 8).
    /// </para>
    /// </summary>
    public sealed record HostPreferences
    {
        /// <summary>
        /// A fresh install: follow the OS for both theme and motion, and remember no window. Every
        /// tolerant-read failure resolves to this — an absent file, an unparseable one, a file full
        /// of keys or types this app does not model.
        /// </summary>
        public static HostPreferences Default { get; } = new();

        /// <summary>Which light/dark face the app wears. Defaults to <see cref="AppThemePreference.FollowSystem"/>.</summary>
        public AppThemePreference Theme { get; init; } = AppThemePreference.FollowSystem;

        /// <summary>Which motion budget the app animates with. Defaults to <see cref="MotionPreference.FollowSystem"/>.</summary>
        public MotionPreference Motion { get; init; } = MotionPreference.FollowSystem;

        /// <summary>
        /// The shell window's last size and position, or <c>null</c> when nothing usable is
        /// stored — a fresh install, or a stored geometry that did not survive
        /// <see cref="WindowGeometry.TryCreate"/>. Null means "let the window pick its own size".
        /// </summary>
        public WindowGeometry? Window { get; init; }
    }
}
