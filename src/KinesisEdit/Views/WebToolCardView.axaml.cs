using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The dashboard card for a board this app does not edit — the Advantage 360 Professional,
    /// configured in Kinesis' web tool (docs/design/mockups.md §1b). Same grid cell and same height
    /// as a device card, with one sentence and one action.
    /// </summary>
    public partial class WebToolCardView : UserControl
    {
        /// <summary>Creates the card view.</summary>
        public WebToolCardView()
        {
            InitializeComponent();
        }
    }
}
