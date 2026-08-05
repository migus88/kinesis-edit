using Avalonia.Controls;
using KinesisEdit.ViewModels;
using KinesisEdit.Views;

namespace KinesisEdit.Services
{
    /// <summary>
    /// The view layer's <see cref="IMessageBoxPresenter"/>: shows a <see cref="MessageBoxWindow"/>
    /// modally over the shell and completes with the user's answer. The owner is resolved lazily
    /// through a callback because the presenter is built before the main window exists — the
    /// notification service it feeds is a constructor dependency of the shell view model.
    /// </summary>
    public sealed class MessageBoxPresenter : IMessageBoxPresenter
    {
        private readonly Func<Window?> _ownerAccessor;

        /// <summary>Creates the presenter; <paramref name="ownerAccessor"/> returns the modal owner, or null when there is none yet.</summary>
        public MessageBoxPresenter(Func<Window?> ownerAccessor)
        {
            _ownerAccessor = ownerAccessor ?? throw new ArgumentNullException(nameof(ownerAccessor));
        }

        /// <summary>Presents <paramref name="request"/> and completes once the dialog is answered or closed.</summary>
        public async Task<MessageBoxOutcome> PresentAsync(MessageBoxRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var viewModel = new MessageBoxViewModel(request);
            var window = new MessageBoxWindow(viewModel);
            var owner = _ownerAccessor();

            if (owner is null)
            {
                await ShowStandaloneAsync(window).ConfigureAwait(true);
            }
            else
            {
                await window.ShowDialog(owner).ConfigureAwait(true);
            }

            // The window always records an outcome before it closes; the fallback only guards a
            // dialog that never opened at all.
            return viewModel.Outcome ?? viewModel.Complete(viewModel.EscapeResult);
        }

        private static Task ShowStandaloneAsync(Window window)
        {
            var completion = new TaskCompletionSource();

            window.Closed += (_, _) => completion.TrySetResult();
            window.Show();

            return completion.Task;
        }
    }
}
