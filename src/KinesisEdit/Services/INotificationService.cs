namespace KinesisEdit.Services
{
    /// <summary>
    /// The app-wide notification surface of specs/11-feature-dialogs.md §11.9: modal message
    /// boxes (with the "Hide this notification?" policy of specs/08-settings.md §3),
    /// self-dismissing toasts, and the loading indicator. View models depend on this interface
    /// so they stay free of any UI toolkit.
    /// </summary>
    public interface INotificationService
    {
        /// <summary>The caption of the loading indicator while it is up; null when it is hidden.</summary>
        string? LoadingCaption { get; }

        /// <summary>Raised when a toast should appear; the view layer hosts it.</summary>
        event Action<ToastRequest>? ToastRequested;

        /// <summary>Raised whenever the loading indicator's caption changes; null means "hide it".</summary>
        event Action<string?>? LoadingChanged;

        /// <summary>
        /// Shows a message box unless its notification is hidden for the active device, and
        /// persists the user's "Hide this notification?" answer.
        /// </summary>
        Task<MessageBoxOutcome> ShowMessageBoxAsync(MessageBoxRequest request);

        /// <summary>Shows a self-dismissing notice.</summary>
        void ShowToast(ToastRequest request);

        /// <summary>Shows the loading indicator; a null caption uses <see cref="LoadingCaptions.Default"/>.</summary>
        void ShowLoading(string? caption = null);

        /// <summary>Hides the loading indicator; safe when it is not up.</summary>
        void HideLoading();
    }
}
