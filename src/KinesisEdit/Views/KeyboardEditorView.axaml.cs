using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The keyboard editor view, resolved from <see cref="KeyboardEditorViewModel"/> by
    /// <see cref="ViewLocator"/>. Everything it shows is bound; the only code here is the Escape
    /// route out of an open feature panel and out of the remap's listening state, plus the two
    /// selection handlers that turn a segment or a tab being chosen back into the command the
    /// buttons they replaced used to run.
    /// </summary>
    public partial class KeyboardEditorView : UserControl
    {
        /// <summary>Creates the editor view.</summary>
        public KeyboardEditorView()
        {
            InitializeComponent();

            // Tunneling, as in MessageBoxWindow: Escape must leave the listening state whatever
            // has focus, instead of being swallowed by the focused key cap. handledEventsToo is
            // set because the keystroke-capture service previews the same event on the window
            // above us and marks it handled while a key is listening
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

        /// <summary>
        /// The layer switch is a <see cref="ListBox"/>, so choosing a layer is a selection rather
        /// than a click — but the editor's rules for switching (cancel the listening key, stop a
        /// recording, move the macro trigger) all live in
        /// <see cref="KeyboardEditorViewModel.SelectLayerCommand"/>, so that is still what runs.
        /// The command is idempotent, which is what makes it safe to fire on the selection the
        /// control makes for itself while the profile is loading.
        /// </summary>
        private void OnLayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel
                || (sender as SelectingItemsControl)?.SelectedItem is not KeyboardLayerViewModel layer)
            {
                return;
            }

            viewModel.SelectLayerCommand.Execute(layer);
        }

        /// <inheritdoc cref="OnLayerSelectionChanged" />
        private void OnTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not KeyboardEditorViewModel viewModel || sender is not SelectingItemsControl strip)
            {
                return;
            }

            if (strip.SelectedItem is EditorTabViewModel tab)
            {
                viewModel.SelectTabCommand.Execute(tab);
            }

            // A tab with nothing behind it stays shut whichever way it is asked for, so the strip is
            // put back on the section that is actually open rather than left showing one that never
            // opened. Guarded by the comparison, which is what stops the assignment re-entering.
            if ((strip.SelectedItem as EditorTabViewModel)?.Tab != viewModel.SelectedTab)
            {
                strip.SelectedValue = viewModel.SelectedTab;
            }
        }
    }
}
