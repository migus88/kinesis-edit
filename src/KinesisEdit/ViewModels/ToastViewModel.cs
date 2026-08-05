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

        private readonly ToastRequest _request;

        /// <summary>Creates a view model for <paramref name="request"/>.</summary>
        public ToastViewModel(ToastRequest request)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
        }
    }
}
