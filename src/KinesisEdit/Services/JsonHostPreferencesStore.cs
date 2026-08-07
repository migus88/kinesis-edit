namespace KinesisEdit.Services
{
    /// <summary>
    /// The <see cref="IHostPreferencesStore"/> backed by a JSON file in the per-user configuration
    /// directory (<see cref="HostPreferencesPathProvider"/>).
    /// <para>
    /// <b>The file is read once and then lives in memory</b>, like the device store it mirrors: the
    /// Settings screen, the window's geometry handler and the two appliers all read
    /// <see cref="Current"/> and write through <see cref="Update"/>, so no consumer can serve a
    /// stale file and the file is neither re-read on every property access nor held open.
    /// </para>
    /// <para>
    /// <b>Nothing here throws for a filesystem problem.</b> A read that fails yields
    /// <see cref="HostPreferences.Default"/>; a write that fails is dropped and the in-memory value
    /// still moves, so the app stays consistent with what the user just did even when the disk
    /// refused. Serialization happens <i>outside</i> the guard on purpose — a bug in the serializer
    /// is not an I/O failure and must not be swallowed with one.
    /// </para>
    /// <para>
    /// <b>It resolves no path of its own.</b> There is deliberately no
    /// <c>CreateForCurrentPlatform</c> here: the real location is reachable only by naming
    /// <see cref="HostPreferencesPathProvider.CreateForCurrentPlatform"/>, which the composition
    /// root does and the test suite never does.
    /// </para>
    /// </summary>
    public sealed class JsonHostPreferencesStore : IHostPreferencesStore
    {
        /// <summary>
        /// The user's preferences, read on first access and held from then on. An absent or
        /// unreadable file yields <see cref="HostPreferences.Default"/> and is not retried.
        /// </summary>
        public HostPreferences Current
        {
            get
            {
                lock (_gate)
                {
                    EnsureLoaded();

                    return _current;
                }
            }
        }

        /// <inheritdoc />
        public event Action? Changed;

        private readonly IHostPreferencesPathProvider _paths;

        private readonly object _gate = new();

        private HostPreferences _current = HostPreferences.Default;

        private bool _hasLoaded;

        /// <summary>Creates a store over the file <paramref name="paths"/> names.</summary>
        public JsonHostPreferencesStore(IHostPreferencesPathProvider paths)
        {
            _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        }

        /// <summary>
        /// Applies <paramref name="mutate"/>, saves the result and raises <see cref="Changed"/>.
        /// <b>A mutation that returns an equal record does nothing at all</b> — no write, no event
        /// — because <see cref="HostPreferences"/> is a record and equality is by value: a Settings
        /// screen re-asserting the theme it is already showing must not churn the file or re-enter
        /// every binding.
        /// </summary>
        public void Update(Func<HostPreferences, HostPreferences> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);

            lock (_gate)
            {
                EnsureLoaded();

                var updated = mutate(_current)
                    ?? throw new InvalidOperationException("A host-preferences mutation must return preferences, not null.");

                if (updated == _current)
                {
                    return;
                }

                _current = updated;

                Save(updated);
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

            _current = HostPreferencesParser.Parse(ReadFile());
        }

        private string? ReadFile()
        {
            try
            {
                var path = _paths.GetFilePath();

                return File.Exists(path) ? File.ReadAllText(path) : null;
            }
            catch (Exception)
            {
                // Deliberately broad, and deliberately silent. This runs on the startup path,
                // before any window exists, and the set of failures a filesystem plus a platform
                // path lookup can produce is not worth enumerating: a preferences file that cannot
                // be read means the user gets the defaults, never a failed launch.
                return null;
            }
        }

        private void Save(HostPreferences preferences)
        {
            // Outside the guard below: serialization is pure, and a defect in it is a bug rather
            // than an I/O failure. Swallowing it here would hide it forever.
            var content = HostPreferencesSerializer.Serialize(preferences);

            try
            {
                var path = _paths.GetFilePath();
                var directory = Path.GetDirectoryName(path);

                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, content);
            }
            catch (Exception)
            {
                // Same reasoning as ReadFile, from the other side: the callers are a preferences
                // toggle and a window-close handler, and neither may fail because a read-only home
                // directory, a full disk or a locked file refused a preference.
            }
        }
    }
}
