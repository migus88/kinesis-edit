using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// A self-dismissing notice (specs/11-feature-dialogs.md §11.9, "Info dialog"): the view
    /// hosts it and closes it after <see cref="Timeout"/>, which defaults to the spec's
    /// 5 seconds.
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

        /// <summary>Whether the notice has a title to render.</summary>
        public bool HasTitle => !string.IsNullOrWhiteSpace(_request.Title);

        /// <summary>
        /// Whether the notice is currently on screen, as opposed to arriving or leaving. The host
        /// raises it once the view exists and lowers it when <see cref="Timeout"/> elapses; the
        /// view binds it to the class that runs the 180 ms fade, so the toast is never removed
        /// mid-animation. It starts false so the first frame is the "arriving" one.
        /// </summary>
        public bool IsShown
        {
            get => _isShown;
            set => SetProperty(ref _isShown, value);
        }

        private readonly ToastRequest _request;

        private bool _isShown;

        /// <summary>Creates a view model for <paramref name="request"/>.</summary>
        public ToastViewModel(ToastRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }
    }
}
