namespace KinesisEdit.Services
{
    /// <summary>
    /// Structured outcome of a message box: which button closed it, whether the user ticked
    /// "Hide this notification?", and the id of the custom button when one was used. Replaces
    /// the legacy "+100 modal result" encoding of specs/11-feature-dialogs.md §11.9.
    /// </summary>
    public sealed record MessageBoxOutcome
    {
        /// <summary>Outcome of a dialog that was suppressed and therefore never shown.</summary>
        public static MessageBoxOutcome ForSuppressed(MessageBoxRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return new MessageBoxOutcome
            {
                Result = request.SuppressedResult,
                WasPresented = false
            };
        }

        /// <summary>Which button closed the dialog.</summary>
        public required MessageBoxResult Result { get; init; }

        /// <summary>Whether the user asked never to see this notification again.</summary>
        public bool SuppressRequested { get; init; }

        /// <summary>Id of the clicked custom button; null unless <see cref="Result"/> is Custom.</summary>
        public string? CustomButtonId { get; init; }

        /// <summary>False when the dialog was skipped because the notification is hidden.</summary>
        public bool WasPresented { get; init; } = true;
    }
}
