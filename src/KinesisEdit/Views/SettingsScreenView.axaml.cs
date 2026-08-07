using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The shell's Settings screen — the host preferences of
    /// <see cref="ViewModels.SettingsScreenViewModel"/>. It knows nothing about keyboards; see the
    /// view model for why that is the point.
    /// </summary>
    public partial class SettingsScreenView : UserControl
    {
        /// <summary>Creates the settings screen.</summary>
        public SettingsScreenView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The theme switch is a <see cref="ListBox"/>, so picking a theme is a selection rather
        /// than a click — but everything picking one means (persisting it and repainting the running
        /// app) lives in <see cref="SettingsScreenViewModel.SelectThemeCommand"/>, so that is still
        /// what runs.
        /// </summary>
        private void OnThemeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not SettingsScreenViewModel viewModel || sender is not SelectingItemsControl list)
            {
                return;
            }

            if (list.SelectedItem is AppThemeOption option)
            {
                viewModel.SelectThemeCommand.Execute(option);
            }

            // The view model is the single owner of which segment is lit, so the control is put back
            // on whatever it kept rather than left showing something nothing switched to. Guarded by
            // the comparison, which is what stops the assignment re-entering this handler.
            if (!ReferenceEquals(list.SelectedItem, viewModel.SelectedThemeOption))
            {
                list.SelectedItem = viewModel.SelectedThemeOption;
            }
        }

        /// <summary>The motion switch, on exactly the same contract as the theme's.</summary>
        private void OnMotionSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not SettingsScreenViewModel viewModel || sender is not SelectingItemsControl list)
            {
                return;
            }

            if (list.SelectedItem is MotionOption option)
            {
                viewModel.SelectMotionCommand.Execute(option);
            }

            if (!ReferenceEquals(list.SelectedItem, viewModel.SelectedMotionOption))
            {
                list.SelectedItem = viewModel.SelectedMotionOption;
            }
        }
    }
}
