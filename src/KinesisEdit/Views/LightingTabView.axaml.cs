using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The editor's Lighting tab (specs/07-lighting.md §3, §4): layer switch, mode menu, per-mode
    /// parameter panels, colour picker, zone buttons and the per-key keyboard picture. It contains
    /// no lighting knowledge — see <see cref="ViewModels.LightingTabViewModel"/>.
    /// </summary>
    public partial class LightingTabView : UserControl
    {
        /// <summary>Creates the lighting view.</summary>
        public LightingTabView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The layer switch is a <see cref="ListBox"/>, so choosing a layer is a selection rather
        /// than a click — but everything switching a layer means (re-reading the mode, the colours,
        /// the speed and the direction off the new layer's state) lives in
        /// <see cref="LightingTabViewModel.SelectLayerCommand"/>, so that is still what runs.
        /// </summary>
        private void OnLayerSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not LightingTabViewModel viewModel || sender is not SelectingItemsControl list)
            {
                return;
            }

            if (list.SelectedItem is LightingLayerViewModel layer)
            {
                viewModel.SelectLayerCommand.Execute(layer);
            }

            // The panel refuses a layer the firmware gates off — the Fn layer below LED 1.0.44 (§3)
            // — so the control is put back on whatever it kept rather than left showing a layer
            // nothing switched to. Guarded by the comparison, which is what stops the assignment
            // re-entering this handler.
            if (!ReferenceEquals(list.SelectedItem, viewModel.SelectedLayer))
            {
                list.SelectedItem = viewModel.SelectedLayer;
            }
        }
    }
}
