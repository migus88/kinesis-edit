using Avalonia.Controls;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The one way this app puts a dialog window on screen: modally over its owner, or standalone
    /// when there is no owner yet — the presenters are built before the shell window exists.
    /// Shared by <see cref="MessageBoxPresenter"/> and <see cref="FirmwareUpdatePresenter"/> so
    /// the ownerless fallback cannot drift between them.
    /// </summary>
    internal static class DialogWindowHost
    {
        /// <summary>
        /// Shows <paramref name="window"/> over <paramref name="owner"/>, or without an owner when
        /// there is none, and completes once the window closes.
        /// </summary>
        public static Task ShowAsync(Window window, Window? owner)
        {
            ArgumentNullException.ThrowIfNull(window);

            if (owner is not null)
            {
                return window.ShowDialog(owner);
            }

            var completion = new TaskCompletionSource();

            window.Closed += (_, _) => completion.TrySetResult();
            window.Show();

            return completion.Task;
        }
    }
}
