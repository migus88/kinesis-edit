using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The shell's Help screen — the link table and the about card of
    /// <see cref="ViewModels.HelpScreenViewModel"/>. It knows no URL of its own: every row is data
    /// the view model composed from the device catalog.
    /// </summary>
    public partial class HelpScreenView : UserControl
    {
        /// <summary>Creates the help screen.</summary>
        public HelpScreenView()
        {
            InitializeComponent();
        }
    }
}
