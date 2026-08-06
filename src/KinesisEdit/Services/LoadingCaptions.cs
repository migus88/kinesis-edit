namespace KinesisEdit.Services
{
    /// <summary>
    /// Captions of the loading indicator (specs/11-feature-dialogs.md §11.9: "progress window
    /// with a caller-supplied title/message. Default caption 'Loading...'"). The per-device form
    /// is shown while Configure opens an editor (specs/10-apps-and-ui.md, "shows a loading
    /// splash"); the spec does not quote its text.
    /// <para>
    /// The ellipsis is the single character U+2026, not three periods: mockup 1k draws
    /// "Loading Advantage 360…", as the dashboard's "Scanning for v-Drive…" already does. The
    /// spec's own casing is Latin-1 era and the redesign supersedes it.
    /// </para>
    /// </summary>
    public static class LoadingCaptions
    {
        /// <summary>The default caption, with mockup 1k's one-character ellipsis.</summary>
        public const string Default = "Loading…";

        /// <summary>Caption shown while the editor of <paramref name="deviceName"/> opens.</summary>
        public static string ForDevice(string deviceName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

            return $"Loading {deviceName}…";
        }
    }
}
