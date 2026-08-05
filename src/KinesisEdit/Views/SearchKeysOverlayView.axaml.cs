using Avalonia.Controls;
using Avalonia.Input;
using KinesisEdit.Core.Keys;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Search Keys picker (specs/11-feature-dialogs.md §11.6), resolved from
    /// <see cref="SearchKeysOverlayViewModel"/> by <see cref="ViewLocator"/>. Everything it shows
    /// is bound; the only code here is §11.6's "double-clicking an item accepts immediately",
    /// which is a pointer gesture and therefore the view's — the view model takes the row it
    /// picked as a command parameter and never learns what a double click is.
    /// </summary>
    public partial class SearchKeysOverlayView : UserControl
    {
        /// <summary>Creates the picker.</summary>
        public SearchKeysOverlayView()
        {
            InitializeComponent();
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
    }
}
