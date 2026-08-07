using KinesisEdit.Core.Settings;

namespace KinesisEdit.Services
{
    /// <summary>
    /// A preferences store that reads but never writes — the demo-mode store for a device whose
    /// v-Drive is present but not writable. specs/08-settings.md §3 says <c>app_settings.txt</c>
    /// is "never saved in demo mode"; it says nothing about reading it, so the drive's real
    /// preferences, hidden notifications and custom swatches are all served as they stand.
    /// <para>
    /// Writes are discarded outright rather than kept in memory: a preference that appeared to
    /// take and then vanished at the end of the session would be a lie told twice. The settings
    /// screen states this in words instead — <c>SettingsMessageCatalog.DemoModePreferencesCaveat</c>.
    /// </para>
    /// </summary>
    public sealed class ReadOnlyAppPreferencesStore : IAppPreferencesStore
    {
        /// <summary>The wrapped store's preferences, read straight from the drive.</summary>
        public AppSettings Current => _source.Current;

        /// <summary>Always false; this is the store that exists to refuse writes.</summary>
        public bool IsWritable => false;

        /// <summary>
        /// Forwarded to the wrapped store. Nothing this wrapper does raises it — it is here so a
        /// subscriber sees a change the source makes by any other route, rather than silently
        /// getting an event that can never fire.
        /// </summary>
        public event Action? Changed
        {
            add => _source.Changed += value;
            remove => _source.Changed -= value;
        }

        private readonly IAppPreferencesStore _source;

        /// <summary>Wraps <paramref name="source"/>, keeping its reads and discarding its writes.</summary>
        public ReadOnlyAppPreferencesStore(IAppPreferencesStore source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        /// <summary>Reads the preference from the wrapped store.</summary>
        public bool IsHidden(string key)
        {
            return _source.IsHidden(key);
        }

        /// <summary>Discards the preference; demo mode never writes to the v-Drive.</summary>
        public void SetHidden(string key, bool hidden)
        {
        }

        /// <summary>Discards the change; demo mode never writes to the v-Drive.</summary>
        public void Update(Func<AppSettings, AppSettings> mutate)
        {
            ArgumentNullException.ThrowIfNull(mutate);
        }
    }
}
