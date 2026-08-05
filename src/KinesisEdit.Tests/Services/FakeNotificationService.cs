using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="INotificationService"/> recording toasts, message boxes, and the
    /// loading indicator's caption history.
    /// </summary>
    internal sealed class FakeNotificationService : INotificationService
    {
        public string? LoadingCaption { get; private set; }

        public MessageBoxOutcome OutcomeToReturn { get; set; } = new MessageBoxOutcome
        {
            Result = MessageBoxResult.Ok
        };

        /// <summary>
        /// Invoked from inside <see cref="ShowMessageBoxAsync"/>: the seam for asserting what the
        /// caller had already done by the time the (blocking) box went up.
        /// </summary>
        public Action<MessageBoxRequest>? MessageBoxShowing { get; set; }

        /// <summary>
        /// When set, <see cref="ShowMessageBoxAsync"/> throws it — a box the real presenter could
        /// not put on screen, e.g. because its owner window is already closed.
        /// </summary>
        public Exception? MessageBoxExceptionToThrow { get; set; }

        public List<ToastRequest> Toasts { get; } = [];

        public List<MessageBoxRequest> MessageBoxes { get; } = [];

        public List<string?> LoadingHistory { get; } = [];

        public event Action<ToastRequest>? ToastRequested;

        public event Action<string?>? LoadingChanged;

        public Task<MessageBoxOutcome> ShowMessageBoxAsync(MessageBoxRequest request)
        {
            MessageBoxes.Add(request);

            MessageBoxShowing?.Invoke(request);

            if (MessageBoxExceptionToThrow is not null)
            {
                return Task.FromException<MessageBoxOutcome>(MessageBoxExceptionToThrow);
            }

            return Task.FromResult(OutcomeToReturn);
        }

        public void ShowToast(ToastRequest request)
        {
            Toasts.Add(request);

            ToastRequested?.Invoke(request);
        }

        public void ShowLoading(string? caption = null)
        {
            LoadingCaption = caption ?? LoadingCaptions.Default;
            LoadingHistory.Add(LoadingCaption);

            LoadingChanged?.Invoke(LoadingCaption);
        }

        public void HideLoading()
        {
            LoadingCaption = null;
            LoadingHistory.Add(null);

            LoadingChanged?.Invoke(null);
        }
    }
}
