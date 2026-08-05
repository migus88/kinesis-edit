using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The modal message box of specs/11-feature-dialogs.md §11.9. It adds the keyboard contract
    /// the spec states — "Enter activates OK/Yes; Escape cancels" — and guarantees an outcome
    /// even when the window is closed from the title bar, so the awaiting presenter always
    /// completes.
    /// </summary>
    public partial class MessageBoxWindow : Window
    {
        private readonly MessageBoxViewModel? _viewModel;

        /// <summary>Parameterless constructor for the XAML designer.</summary>
        public MessageBoxWindow()
        {
            InitializeComponent();
        }

        /// <summary>Creates the dialog over <paramref name="viewModel"/>.</summary>
        public MessageBoxWindow(MessageBoxViewModel viewModel) : this()
        {
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            DataContext = viewModel;

            viewModel.Completed += OnCompleted;

            // Tunneling: the spec's Enter/Escape contract must win over the focused button, which
            // would otherwise turn Enter into "activate whatever has focus".
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        }

        /// <summary>Records a dismissal when the window is closed without a button being used.</summary>
        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel is not null)
            {
                _viewModel.Completed -= OnCompleted;
                _viewModel.Complete(_viewModel.EscapeResult);
            }

            base.OnClosed(e);
        }

        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (_viewModel is null)
            {
                return;
            }

            // Enter is left unhandled when the dialog has no OK/Yes button to activate, so a
            // custom-button-only dialog cannot be closed with a result none of its buttons offer.
            if (e.Key == Key.Enter && _viewModel.HasAcceptButton)
            {
                e.Handled = true;
                _viewModel.Complete(_viewModel.AcceptResult);

                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                _viewModel.Complete(_viewModel.EscapeResult);
            }
        }

        private void OnCompleted(MessageBoxOutcome outcome)
        {
            Close();
        }
    }
}
