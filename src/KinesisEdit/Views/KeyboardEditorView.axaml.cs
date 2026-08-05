using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The keyboard editor view, resolved from <see cref="KeyboardEditorViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the Escape
    /// route out of an open feature panel and out of the remap's listening state.
    /// </summary>
    public partial class KeyboardEditorView : UserControl
    {
        /// <summary>Creates the editor view.</summary>
        public KeyboardEditorView()
        {
            InitializeComponent();

            // Tunneling, as in MessageBoxWindow and FirmwareUpdateWindow: Escape must leave the
            // listening state whatever has focus, instead of being swallowed by the focused key
            // cap. handledEventsToo is set because the keystroke-capture service previews the same
            // event on the window above us and marks it handled while a key is listening
            // (docs/app/keystroke-capture.md).
            AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        /// <summary>
        /// Escape is a remappable key, not a shortcut: while a key is listening — or while a Tap
        /// and Hold field is armed — the capture service consumes this event on the window above
        /// us and assigns it.
        /// <para>
        /// An open feature panel is therefore dismissed on Escape <b>whatever <c>e.Handled</c>
        /// says</b>, unless the panel itself is the thing waiting for the keystroke
        /// (<see cref="KeyboardEditorViewModel.IsOverlayAwaitingKeystroke"/>) — capture may be
        /// running for something else entirely, and a panel a user cannot close with the keyboard
        /// is worse than an Escape that also fills an armed field. An armed field disarms as it
        /// takes the key, so the next Escape closes the panel.
        /// </para>
        /// </summary>
        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || DataContext is not KeyboardEditorViewModel viewModel)
            {
                return;
            }

            if (!viewModel.IsOverlayAwaitingKeystroke && viewModel.CloseOverlayCommand.CanExecute(null))
            {
                e.Handled = true;

                viewModel.CloseOverlayCommand.Execute(null);

                return;
            }

            if (!viewModel.CancelRemapCommand.CanExecute(null))
            {
                return;
            }

            e.Handled = true;

            viewModel.CancelRemapCommand.Execute(null);
        }
    }
}
