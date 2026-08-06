using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The modal message box of specs/11-feature-dialogs.md §11.9, drawn inline over the shell's
    /// scrim (docs/design/mockups.md, mockup 1k). It adds the keyboard contract the spec states —
    /// "Enter activates OK/Yes; Escape cancels" — and takes focus when it arrives, because a
    /// card that never received focus would leave both keys landing on whatever is behind the
    /// scrim.
    /// </summary>
    public partial class MessageBoxView : UserControl
    {
        /// <summary>Creates the card; the view model arrives as the <c>DataContext</c>.</summary>
        public MessageBoxView()
        {
            InitializeComponent();

            // Tunneling: the spec's Enter/Escape contract must win over the focused button, which
            // would otherwise turn Enter into "activate whatever has focus".
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        }

        /// <summary>
        /// Moves focus into the card once it is on screen, so Enter and Escape reach it. On
        /// <c>Loaded</c> rather than on attach: the buttons' templates are applied and their
        /// <c>IsVisible</c> bindings evaluated by then, and an unrealised button cannot take focus.
        /// </summary>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);

            FocusTheAffirmative();
        }

        private void FocusTheAffirmative()
        {
            // The primary affirmative first — it is what Enter would activate anyway — then any
            // focusable control the card carries, which is the custom-button-only shape of §11.9.
            if (AcceptYes.IsVisible && AcceptYes.Focus())
            {
                return;
            }

            if (AcceptOk.IsVisible && AcceptOk.Focus())
            {
                return;
            }

            this.Focus();
        }

        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MessageBoxViewModel viewModel)
            {
                return;
            }

            // Enter is left unhandled when the dialog has no OK/Yes button to activate, so a
            // custom-button-only dialog cannot be closed with a result none of its buttons offer.
            if (e.Key == Key.Enter && viewModel.HasAcceptButton)
            {
                e.Handled = true;
                viewModel.Complete(viewModel.AcceptResult);

                return;
            }

            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                viewModel.Complete(viewModel.EscapeResult);
            }
        }
    }
}
