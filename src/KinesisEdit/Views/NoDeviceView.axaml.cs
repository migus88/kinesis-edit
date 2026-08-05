using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The dashboard's empty state — the "Keyboard not detected" troubleshoot panel of
    /// specs/11-feature-dialogs.md §11.8, with a device picker driving its text and links.
    /// </summary>
    public partial class NoDeviceView : UserControl
    {
        /// <summary>Creates the troubleshoot view.</summary>
        public NoDeviceView()
        {
            InitializeComponent();
        }
    }
}
