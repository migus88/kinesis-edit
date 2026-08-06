using Avalonia.Controls;
using Avalonia.Threading;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The dashboard's empty state (docs/design/mockups.md §1d) — the "Keyboard not detected"
    /// troubleshoot panel of specs/11-feature-dialogs.md §11.8, rebuilt as a device picker whose
    /// selection drives the connection steps, the support link and the demo-mode target.
    /// <para>
    /// Everything it shows is bound. The one behaviour here is the toolkit's, and could not be:
    /// a single-selection <see cref="ListBox"/> deselects the row that is already selected when it
    /// is Ctrl/Cmd-clicked, and there is no way to say "one of these is always picked" that keeps
    /// the pick — <c>SelectionMode="AlwaysSelected"</c> re-selects the *first* row instead.
    /// </para>
    /// </summary>
    public partial class NoDeviceView : UserControl
    {
        /// <summary>Creates the troubleshoot view.</summary>
        public NoDeviceView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Puts the pick back when the list drops it. This screen has no "nothing picked" state —
        /// the title, the steps, the demo target and the support link all describe one board — and
        /// <see cref="NoDeviceViewModel.SelectedOption"/> refuses the null the binding writes, so
        /// without this the list would sit with no row lit while every panel beside it still
        /// described a device.
        /// </summary>
        private void OnDeviceSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is not ListBox { SelectedItem: null } list || DataContext is not NoDeviceViewModel picker)
            {
                return;
            }

            // Posted, not assigned: a SelectingItemsControl ignores a selection written from
            // inside its own SelectionChanged, so the restore has to land on the next pass.
            var option = picker.SelectedOption;

            Dispatcher.UIThread.Post(() =>
            {
                if (list.SelectedItem is null)
                {
                    list.SelectedItem = option;
                }
            });
        }
    }
}
