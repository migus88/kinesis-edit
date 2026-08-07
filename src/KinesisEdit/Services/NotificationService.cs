namespace KinesisEdit.Services
{
    /// <summary>
    /// Suppression policy over an <see cref="IMessageBoxPresenter"/>, plus the toast and loading
    /// surfaces. A request carrying a <see cref="MessageBoxRequest.SuppressionKey"/> that the
    /// active device's store reports hidden is never presented (specs/08-settings.md §3:
    /// <c>on</c> means "hide this notification"), and a "Hide this notification?" answer is
    /// written back to that store — which in demo mode is
    /// <see cref="NullAppPreferencesStore"/>, so nothing is persisted. A request may narrow that
    /// write-back to one answer with <see cref="MessageBoxRequest.SuppressionResult"/>.
    /// </summary>
    public sealed class NotificationService : INotificationService
    {
        /// <summary>The caption of the loading indicator while it is up; null when it is hidden.</summary>
        public string? LoadingCaption { get; private set; }

        /// <summary>Raised when a toast should appear.</summary>
        public event Action<ToastRequest>? ToastRequested;

        /// <summary>Raised whenever the loading indicator's caption changes; null means "hide it".</summary>
        public event Action<string?>? LoadingChanged;

        private readonly IMessageBoxPresenter _presenter;
        private readonly IDeviceSessionAccessor _sessionAccessor;

        /// <summary>Creates the service over the view layer's presenter and the active-session accessor.</summary>
        public NotificationService(IMessageBoxPresenter presenter, IDeviceSessionAccessor sessionAccessor)
        {
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _sessionAccessor = sessionAccessor ?? throw new ArgumentNullException(nameof(sessionAccessor));
        }

        /// <summary>
        /// Presents <paramref name="request"/> unless it is suppressed, then persists a
        /// "Hide this notification?" answer for the active device.
        /// </summary>
        public async Task<MessageBoxOutcome> ShowMessageBoxAsync(MessageBoxRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var store = _sessionAccessor.Active?.SuppressionStore ?? NullAppPreferencesStore.Instance;
            var suppressionKey = request.SuppressionKey;

            if (request.HasSuppressionOption && store.IsHidden(suppressionKey!))
            {
                return MessageBoxOutcome.ForSuppressed(request);
            }

            var outcome = await _presenter.PresentAsync(request).ConfigureAwait(true);

            // A ticked box is only written back when the answer it was ticked beside can keep the
            // promise it makes. Issue #52's checkbox reads "always save on leaving", so ticking it
            // and then pressing Discard or Cancel must not arm auto-save — that would turn one
            // "throw this away" into every future one. A request that names no
            // MessageBoxRequest.SuppressionResult records on any answer, which is what every
            // "Don't ask this again" box has always done.
            if (outcome.SuppressRequested
                && request.HasSuppressionOption
                && (request.SuppressionResult is null || outcome.Result == request.SuppressionResult))
            {
                store.SetHidden(suppressionKey!, true);
            }

            return outcome;
        }

        /// <summary>Raises <see cref="ToastRequested"/> for the view layer to host.</summary>
        public void ShowToast(ToastRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            ToastRequested?.Invoke(request);
        }

        /// <summary>Shows the loading indicator; a null caption uses <see cref="LoadingCaptions.Default"/>.</summary>
        public void ShowLoading(string? caption = null)
        {
            LoadingCaption = string.IsNullOrWhiteSpace(caption) ? LoadingCaptions.Default : caption;

            LoadingChanged?.Invoke(LoadingCaption);
        }

        /// <summary>Hides the loading indicator; safe when it is not up.</summary>
        public void HideLoading()
        {
            if (LoadingCaption is null)
            {
                return;
            }

            LoadingCaption = null;

            LoadingChanged?.Invoke(null);
        }
    }
}
