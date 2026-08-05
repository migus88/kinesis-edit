using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// One dashboard device card (specs/10-apps-and-ui.md, "SmartSetMaster … dashboard apps"):
    /// name, colored connection status, and the Configure / Scan / Eject actions.
    /// </summary>
    public partial class DeviceCardView : UserControl
    {
        /// <summary>Creates the card view.</summary>
        public DeviceCardView()
        {
            InitializeComponent();
        }
    }
}
