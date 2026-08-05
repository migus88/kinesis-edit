using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The keyboard editor view, resolved from <see cref="KeyboardEditorViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the Escape
    /// route out of the remap's listening state.
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

        private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || DataContext is not KeyboardEditorViewModel viewModel)
            {
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
