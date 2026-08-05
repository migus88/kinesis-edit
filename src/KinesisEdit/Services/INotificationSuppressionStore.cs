namespace KinesisEdit.Services
{
    /// <summary>
    /// Per-message "don't show again" preferences — the "Hide this notification?" checkbox of
    /// specs/11-feature-dialogs.md §11.9, persisted per device as described in
    /// specs/08-settings.md §3. Implementations never throw for I/O problems: a preference that
    /// cannot be read or written must not break a dialog.
    /// </summary>
    public interface INotificationSuppressionStore
    {
        /// <summary>Whether the notification identified by <paramref name="key"/> is hidden (stored value <c>on</c>).</summary>
        bool IsHidden(string key);

        /// <summary>Persists the hidden/shown state of <paramref name="key"/> as <c>on</c>/<c>off</c>.</summary>
        void SetHidden(string key, bool hidden);
    }
}
