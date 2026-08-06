using CommunityToolkit.Mvvm.Input;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// A self-dismissing notice (specs/11-feature-dialogs.md §11.9, "Info dialog";
    /// docs/design/mockups.md, mockup 1k): the view hosts it and closes it after
    /// <see cref="Timeout"/>, which defaults to the spec's 5 seconds, and the user can close it
    /// sooner with the card's <c>×</c>.
    /// </summary>
    public sealed class ToastViewModel : ViewModelBase
    {
        /// <summary>Optional title; null shows the message alone.</summary>
        public string? Title => _request.Title;

        /// <summary>The notice text.</summary>
        public string Message => _request.Message;

        /// <summary>How long the notice stays up.</summary>
        public TimeSpan Timeout => _request.Timeout;

        /// <summary>Where the view places the notice.</summary>
        public ToastPosition Position => _request.Position;

        /// <summary>Which of mockup 1k's two toasts this is; the view maps it to a style class.</summary>
        public ToastSeverity Severity => _request.Severity;

        /// <summary>Whether the notice has a title to render.</summary>
        public bool HasTitle => !string.IsNullOrWhiteSpace(_request.Title);

        /// <summary>
        /// Whether the notice is currently on screen, as opposed to arriving or leaving. The host
        /// raises it once the view exists and lowers it when <see cref="Timeout"/> elapses or the
        /// user dismisses it; the view binds it to the class that runs the 180 ms fade, so the
        /// toast is never removed mid-animation. It starts false so the first frame is the
        /// "arriving" one.
        /// </summary>
        public bool IsShown
        {
            get => _isShown;
            set => SetProperty(ref _isShown, value);
        }

        /// <summary>Closes the notice early — the <c>×</c> of mockup 1k.</summary>
        public IRelayCommand DismissCommand { get; }

        /// <summary>
        /// Raised at most once, when the user dismisses the notice. The host owns both timers, so
        /// it — not this view model — is what cancels the pending dwell and schedules the removal.
        /// </summary>
        public event Action<ToastViewModel>? DismissRequested;

        private readonly ToastRequest _request;

        private bool _isShown;
        private bool _isDismissed;

        /// <summary>Creates a view model for <paramref name="request"/>.</summary>
        public ToastViewModel(ToastRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));

            DismissCommand = new RelayCommand(Dismiss);
        }

        /// <summary>
        /// Asks the host to take this notice down now. Idempotent: a second click on a <c>×</c>
        /// that is already fading must not schedule a second removal.
        /// </summary>
        public void Dismiss()
        {
            if (_isDismissed)
            {
                return;
            }

            _isDismissed = true;

            DismissRequested?.Invoke(this);
        }
    }
}
