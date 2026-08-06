using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Assign Tap and Hold Action panel (specs/11-feature-dialogs.md §11.1), resolved from
    /// <see cref="TapAndHoldOverlayViewModel"/> by <see cref="ViewLocator"/>. Everything it shows
    /// is bound; there is no logic here.
    /// </summary>
    public partial class TapAndHoldOverlayView : UserControl
    {
        /// <summary>Creates the panel.</summary>
        public TapAndHoldOverlayView()
        {
            InitializeComponent();
        }
    }
}
