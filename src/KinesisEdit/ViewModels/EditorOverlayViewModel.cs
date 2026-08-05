using CommunityToolkit.Mvvm.Input;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Base class of the editor's inline feature panels — Tap and Hold, Macro Timing Delays,
    /// Search Keys and Export (spec 11). The legacy apps opened these as separate modal windows;
    /// here they render as an overlay inside the editor view, so keystroke capture never has to
    /// cross a window boundary and the editor stays the single owner of routing.
    /// </summary>
    /// <remarks>
    /// Accept is a two-step affair: <see cref="TryAccept"/> validates and, on failure, leaves the
    /// overlay open with <see cref="ErrorMessage"/> set to the spec's verbatim wording. Only a
    /// successful accept — or a cancel — raises <see cref="Closed"/>, which is what the editor
    /// listens to in order to tear the overlay down.
    /// </remarks>
    public abstract class EditorOverlayViewModel : ViewModelBase
    {
        /// <summary>The overlay's title, as passed by the caller (spec 11 titles are per-context).</summary>
        public string Title { get; }

        /// <summary>
        /// The validation message shown inside the overlay, or <c>null</c> when there is none.
        /// Set from the spec's verbatim strings.
        /// </summary>
        public string? ErrorMessage
        {
            get => _errorMessage;
            protected set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>Whether the overlay has already closed. A closed overlay ignores further commands.</summary>
        public bool IsClosed
        {
            get => _isClosed;
            private set => SetProperty(ref _isClosed, value);
        }

        /// <summary>Whether the overlay was dismissed by accepting rather than cancelling.</summary>
        public bool WasAccepted
        {
            get => _wasAccepted;
            private set => SetProperty(ref _wasAccepted, value);
        }

        /// <summary>Validates and applies the overlay's result; closes it when validation passes.</summary>
        public IRelayCommand AcceptCommand { get; }

        /// <summary>Dismisses the overlay without applying anything.</summary>
        public IRelayCommand CancelCommand { get; }

        /// <summary>Raised once, when the overlay closes for any reason.</summary>
        public event EventHandler? Closed;

        private string? _errorMessage;
        private bool _isClosed;
        private bool _wasAccepted;

        /// <summary>Creates an overlay with the given title.</summary>
        protected EditorOverlayViewModel(string title)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            Title = title;
            AcceptCommand = new RelayCommand(Accept);
            CancelCommand = new RelayCommand(Cancel);
        }

        /// <summary>Runs validation and, when it passes, applies the result and closes.</summary>
        public void Accept()
        {
            if (IsClosed)
            {
                return;
            }

            ErrorMessage = null;

            if (!TryAccept())
            {
                return;
            }

            WasAccepted = true;
            Close();
        }

        /// <summary>Closes the overlay without applying anything.</summary>
        public void Cancel()
        {
            if (IsClosed)
            {
                return;
            }

            Close();
        }

        /// <summary>
        /// Validates the overlay's current input and applies its effect. Returning <c>false</c>
        /// keeps the overlay open; implementations set <see cref="ErrorMessage"/> to explain why.
        /// </summary>
        protected abstract bool TryAccept();

        private void Close()
        {
            IsClosed = true;
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }
}
