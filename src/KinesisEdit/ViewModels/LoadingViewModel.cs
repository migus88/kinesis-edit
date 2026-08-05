using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The loading indicator of specs/11-feature-dialogs.md §11.9: a non-modal progress overlay
    /// with a caller-supplied caption, defaulting to 'Loading...'. Shown while Configure opens a
    /// device (specs/10-apps-and-ui.md, "shows a loading splash").
    /// </summary>
    public sealed class LoadingViewModel : ViewModelBase
    {
        /// <summary>Caption shown next to the progress indicator.</summary>
        public string Caption
        {
            get => _caption;
            set => SetProperty(ref _caption, string.IsNullOrWhiteSpace(value) ? LoadingCaptions.Default : value);
        }

        /// <summary>Whether the indicator is currently up.</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        private string _caption;
        private bool _isVisible;

        /// <summary>Creates the indicator; a null or blank caption uses <see cref="LoadingCaptions.Default"/>.</summary>
        public LoadingViewModel(string? caption = null)
        {
            _caption = string.IsNullOrWhiteSpace(caption) ? LoadingCaptions.Default : caption;
        }
    }
}
