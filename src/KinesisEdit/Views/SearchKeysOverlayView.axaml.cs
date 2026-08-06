using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using KinesisEdit.Core.Keys;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Search Keys picker (specs/11-feature-dialogs.md §11.6), resolved from
    /// <see cref="SearchKeysOverlayViewModel"/> by <see cref="ViewLocator"/>. Everything it shows
    /// is bound; the only code here is §11.6's "double-clicking an item accepts immediately",
    /// which is a pointer gesture and therefore the view's — the view model takes the row it
    /// picked as a command parameter and never learns what a double click is — plus putting the
    /// caret in the filter box when the panel appears.
    /// </summary>
    public partial class SearchKeysOverlayView : UserControl
    {
        /// <summary>Creates the picker.</summary>
        public SearchKeysOverlayView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// A type-to-filter picker opens with its caret in the filter box — which is the whole of
        /// the grammar's "⌘F focuses the token search" (docs/design/mockups.md <c>2b</c>), and is
        /// right for every other way this panel opens too.
        /// <para>
        /// It is done here rather than by whoever opened the panel because the panel's own view is
        /// the only thing that knows its field, and because the panel is materialised by
        /// <see cref="ViewLocator"/> a layout pass after the command that asked for it — the caller
        /// has nothing to focus at the moment it runs. Focusing the box also stands keystroke
        /// capture down through the adapter's text-input judgement, on top of the explicit
        /// suspension <c>EditorOverlayHost</c> already applies to a text-entry panel.
        /// </para>
        /// </summary>
        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (TryFocusQueryField())
            {
                return;
            }

            // A window that has not laid out yet refuses focus; the first idle pass after it has.
            Dispatcher.UIThread.Post(() => TryFocusQueryField(), DispatcherPriority.Loaded);
        }

        private void OnRowDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (sender is not Control { DataContext: KeySearchEntry entry }
                || DataContext is not SearchKeysOverlayViewModel viewModel)
            {
                return;
            }

            e.Handled = true;

            viewModel.ChooseCommand.Execute(entry);
        }

        /// <summary>
        /// Focuses the one text field this panel declares. It is found by type rather than by name
        /// so nothing here depends on the markup's shape, and it is the panel's own content, never
        /// somebody's template part.
        /// </summary>
        private bool TryFocusQueryField()
        {
            return this.GetVisualDescendants().OfType<TextBox>().FirstOrDefault() is { } field
                   && field.Focus();
        }
    }
}
