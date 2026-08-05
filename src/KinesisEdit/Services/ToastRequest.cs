namespace KinesisEdit.Services
{
    /// <summary>
    /// A self-dismissing notice — the "Info dialog" of specs/11-feature-dialogs.md §11.9:
    /// "Self-dismissing toast-style notification with a title, message, position, and timeout;
    /// a timer closes it after the given number of seconds (default 5)". Rendering is left to
    /// the view layer.
    /// </summary>
    public sealed record ToastRequest
    {
        /// <summary>The spec's default lifetime of a toast (5 seconds).</summary>
        public static TimeSpan DefaultTimeout { get; } = TimeSpan.FromSeconds(5);

        /// <summary>Optional title; null shows the message alone.</summary>
        public string? Title { get; init; }

        /// <summary>The notice text.</summary>
        public required string Message { get; init; }

        /// <summary>How long the toast stays up before dismissing itself.</summary>
        public TimeSpan Timeout { get; init; } = DefaultTimeout;

        /// <summary>Where the notice appears; the spec's two placements, defaulting to bottom-right.</summary>
        public ToastPosition Position { get; init; } = ToastPosition.BottomRight;
    }
}
