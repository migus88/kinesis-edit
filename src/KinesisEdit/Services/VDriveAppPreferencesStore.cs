using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The session's preferences backed by <c>app_settings.txt</c> in the v-Drive's settings folder
    /// (specs/08-settings.md §3), for a connected, writable drive. Demo sessions get
    /// <see cref="ReadOnlyAppPreferencesStore"/> or <see cref="NullAppPreferencesStore"/>.
    /// <para>
    /// <b>The file is read once and then lives in memory.</b> Every consumer — the colour picker's
    /// twelve slots, the settings screen's seventeen preferences, a message box's "Don't ask this
    /// again" — reads <see cref="Current"/> and writes through <see cref="Update"/>, so a write by
    /// one is visible to the others immediately and no consumer can serve a stale file.
    /// </para>
    /// <para>
    /// Every read and write goes through <see cref="ISettingsService"/>, i.e. through Core's
    /// <c>AppSettingsParser</c>/<c>AppSettingsSerializer</c> and its read-modify-write merge, so
    /// this file has exactly one reader and one writer. A second, line-based implementation here
    /// would silently clobber the state Core writes and would resolve the file's path differently
    /// for the Advantage2, whose keyboard settings live in <c>active/</c> while
    /// <c>app_settings.txt</c> always lives in <c>settings/</c> (spec 08 §3,
    /// specs/03-vdrive-and-files.md §6).
    /// </para>
    /// </summary>
    public sealed class VDriveAppPreferencesStore : IAppPreferencesStore
    {
        /// <summary>
        /// The <c>app_settings.txt</c> path of a drive. Delegates to Core so that the path this
        /// store touches and the path <see cref="ISettingsService"/> touches can never diverge.
        /// </summary>
        public static string GetFilePath(VDriveLocation location)
        {
            ArgumentNullException.ThrowIfNull(location);

            return SettingsService.GetAppSettingsFilePath(location);
        }

        /// <summary>
        /// The drive's preferences, read on first access and held from then on. An unreadable file
        /// yields <see cref="AppSettings.Empty"/> and is not retried — a session reads the file
        /// once, and a preference is never worth a second round of I/O failures.
        /// </summary>
        public AppSettings Current
        {
            get
            {
                EnsureLoaded();

                return _current;
            }
        }

        /// <summary>Always true; the read-only and null stores are the ones that refuse writes.</summary>
        public bool IsWritable => true;

        /// <summary>Raised after every <see cref="Update"/>, including one whose save failed.</summary>
        public event Action? Changed;

        private readonly ISettingsService _settings;
        private readonly VDriveLocation _location;
        private AppSettings _current = AppSettings.Empty;
        private bool _hasLoaded;

        /// <summary>Creates a store over the <c>app_settings.txt</c> file of <paramref name="location"/>.</summary>
        public VDriveAppPreferencesStore(ISettingsService settings, VDriveLocation location)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _location = location ?? throw new ArgumentNullException(nameof(location));
        }

        /// <summary>
        /// True only when the stored value of <paramref name="key"/> is <c>on</c>. A missing file,
        /// a missing key, <c>off</c>, an unreadable file, a key outside
        /// <see cref="AppPreferenceCatalog"/>, or a key the catalog models as a <b>display</b>
        /// preference rather than a suppression flag all mean "show".
        /// </summary>
        public bool IsHidden(string key)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var descriptor = AppPreferenceCatalog.Find(key);

            if (descriptor is null || !descriptor.IsSuppression)
            {
                return false;
            }

            // The catalog's GetValue is the user-facing option ("ask me"); hidden is its negation.
            return !descriptor.GetValue(Current);
        }

        /// <summary>
        /// Writes <c>&lt;key&gt;=on</c> or <c>&lt;key&gt;=off</c>, preserving every other line and
        /// creating <c>app_settings.txt</c> when the drive does not have one yet. A key the
        /// catalog does not model as a suppression flag is never written — an unmodelled key has
        /// no property to carry it, and <c>advisory_detail</c> is not a thing a dialog may hide.
        /// </summary>
        public void SetHidden(string key, bool hidden)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            var descriptor = AppPreferenceCatalog.Find(key);

            if (descriptor is null || !descriptor.IsSuppression)
            {
                return;
            }

            Update(settings => descriptor.SetValue(settings, !hidden));
        }

        /// <summary>
        /// Applies <paramref name="mutate"/>, saves the result and raises <see cref="Changed"/>.
        /// I/O failures are swallowed; the in-memory value still moves so the rest of the session
        /// agrees with what the user just did.
        /// </summary>
        public void Update(Func<AppSettings, AppSettings> mutate)
        {
            Apply(mutate, settings => _settings.SaveAppSettings(_location, settings));
        }

        /// <summary>
        /// Applies <paramref name="mutate"/> and writes <b>only</b> the macro names of
        /// <paramref name="profileNumber"/>, through Core's own per-profile entry point. Flags,
        /// colours and every other profile's names are untouched, and the same read-modify-write
        /// merge keeps unknown lines verbatim (spec 08 §1).
        /// </summary>
        public void UpdateMacroNames(int profileNumber, Func<AppSettings, AppSettings> mutate)
        {
            Apply(mutate, settings => _settings.SaveMacroNames(_location, settings, profileNumber));
        }

        /// <summary>
        /// The one write path: mutate in memory, try to persist through <paramref name="save"/>,
        /// announce. I/O failures are swallowed — a preference that cannot be written must not
        /// break a screen — but the in-memory value still moves, so the rest of the session agrees
        /// with what the user just did.
        /// </summary>
        private void Apply(Func<AppSettings, AppSettings> mutate, Action<AppSettings> save)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            EnsureLoaded();

            _current = mutate(_current)
                ?? throw new InvalidOperationException("An app-preferences mutation must return settings, not null.");

            try
            {
                save(_current);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
            }

            Changed?.Invoke();
        }

        private void EnsureLoaded()
        {
            if (_hasLoaded)
            {
                return;
            }

            _hasLoaded = true;

            try
            {
                _current = _settings.LoadAppSettings(_location);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                _current = AppSettings.Empty;
            }
        }
    }
}
