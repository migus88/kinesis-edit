namespace KinesisEdit.Services
{
    /// <summary>
    /// Shows a message box and awaits the user's answer. Implemented by the view layer (the
    /// modal window of specs/11-feature-dialogs.md §11.9); the services and view models depend
    /// on this interface only, so the suppression policy can be tested without a UI toolkit.
    /// </summary>
    public interface IMessageBoxPresenter
    {
        /// <summary>Presents <paramref name="request"/> modally and completes with the user's outcome.</summary>
        Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request);
    }
}
