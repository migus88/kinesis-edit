namespace KinesisEdit.Services
{
    /// <summary>
    /// Captions of the loading indicator (specs/11-feature-dialogs.md §11.9: "Non-modal
    /// borderless progress window with a caller-supplied title/message. Default caption
    /// 'Loading...'"). The per-device form is shown while Configure opens an editor
    /// (specs/10-apps-and-ui.md, "shows a loading splash"); the spec does not quote its text.
    /// </summary>
    public static class LoadingCaptions
    {
        /// <summary>The spec's default caption.</summary>
        public const string Default = "Loading...";

        /// <summary>Caption shown while the editor of <paramref name="deviceName"/> opens.</summary>
        public static string ForDevice(string deviceName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(deviceName);

            return $"Loading {deviceName}...";
        }
    }
}
