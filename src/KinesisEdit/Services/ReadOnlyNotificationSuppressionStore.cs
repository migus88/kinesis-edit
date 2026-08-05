namespace KinesisEdit.Services
{
    /// <summary>
    /// A suppression store that reads but never writes — the demo-mode store for a device whose
    /// v-Drive is present but not writable. specs/08-settings.md §3 says <c>app_settings.txt</c>
    /// is "never saved in demo mode"; it says nothing about reading it, so notifications the user
    /// hid on this drive stay hidden instead of reappearing.
    /// </summary>
    public sealed class ReadOnlyNotificationSuppressionStore : INotificationSuppressionStore
    {
        private readonly INotificationSuppressionStore _source;

        /// <summary>Wraps <paramref name="source"/>, keeping its reads and discarding its writes.</summary>
        public ReadOnlyNotificationSuppressionStore(INotificationSuppressionStore source)
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
    }
}
