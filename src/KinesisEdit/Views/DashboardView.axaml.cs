using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The device dashboard: a card per detected device, or the troubleshoot empty state of
    /// specs/11-feature-dialogs.md §11.8 when the scanner found nothing.
    /// </summary>
    public partial class DashboardView : UserControl
    {
        /// <summary>Creates the dashboard view.</summary>
        public DashboardView()
        {
            InitializeComponent();
        }
    }
}
