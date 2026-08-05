namespace KinesisEdit.Services
{
    /// <summary>
    /// Everything needed to show one message box (specs/11-feature-dialogs.md §11.9). A
    /// <see cref="SuppressionKey"/> turns on the "Hide this notification?" checkbox and makes the
    /// request subject to the suppression policy of <see cref="NotificationService"/>.
    /// </summary>
    public sealed record MessageBoxRequest
    {
        /// <summary>Dialog title.</summary>
        public required string Title { get; init; }

        /// <summary>Dialog message.</summary>
        public required string Message { get; init; }

        /// <summary>Dialog type, selecting the icon.</summary>
        public MessageBoxIcon Icon { get; init; } = MessageBoxIcon.Information;

        /// <summary>Standard button set.</summary>
        public MessageBoxButtons Buttons { get; init; } = MessageBoxButtons.Ok;

        /// <summary>Additional custom buttons, shown after the standard ones.</summary>
        public IReadOnlyList<MessageBoxButton> CustomButtons { get; init; } = [];

        /// <summary>
        /// The <c>*_msg</c> key of specs/08-settings.md §3 this dialog can be suppressed with;
        /// null when the dialog is always shown (see <see cref="NotificationKeys"/>).
        /// </summary>
        public string? SuppressionKey { get; init; }

        /// <summary>Result reported when the dialog is suppressed and therefore never shown.</summary>
        public MessageBoxResult SuppressedResult { get; init; } = MessageBoxResult.Ok;

        /// <summary>Whether the "Hide this notification?" checkbox belongs on this dialog.</summary>
        public bool HasSuppressionOption => !string.IsNullOrWhiteSpace(SuppressionKey);
    }
}
