namespace KinesisEdit.Services
{
    /// <summary>
    /// The demo-mode store: every notification is shown and nothing is ever persisted, because
    /// <c>app_settings.txt</c> is never written in demo mode (specs/08-settings.md §3,
    /// specs/03-vdrive-and-files.md §3.5). Also the fallback when no device session is active.
    /// </summary>
    public sealed class NullNotificationSuppressionStore : INotificationSuppressionStore
    {
        /// <summary>Shared instance; the type is stateless.</summary>
        public static NullNotificationSuppressionStore Instance { get; } = new();

        /// <summary>Always false — nothing is hidden in demo mode.</summary>
        public bool IsHidden(string key)
        {
            return false;
        }

        /// <summary>Discards the preference; demo mode never writes to the v-Drive.</summary>
        public void SetHidden(string key, bool hidden)
        {
        }
    }
}
