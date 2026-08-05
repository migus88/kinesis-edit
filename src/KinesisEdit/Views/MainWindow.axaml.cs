using Avalonia.Controls;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The single-window shell: a top bar carrying Home / Settings / Help and the v-Drive status
    /// indicator, a content host that swaps between the dashboard and the open editor, and the
    /// non-modal notification overlay floating above both.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>Parameterless constructor for the XAML designer.</summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>Creates the shell window and starts hosting <paramref name="notifications"/>.</summary>
        public MainWindow(MainWindowViewModel viewModel, INotificationService notifications) : this()
        {
            ArgumentNullException.ThrowIfNull(viewModel);
            ArgumentNullException.ThrowIfNull(notifications);

            DataContext = viewModel;

            Notifications.Attach(notifications);
        }
    }
}
