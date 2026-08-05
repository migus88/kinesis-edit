using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Macro Timing Delays panel (specs/11-feature-dialogs.md §11.3), resolved from
    /// <see cref="MacroDelayOverlayViewModel"/> by <see cref="ViewLocator"/>. Everything it shows
    /// is bound; there is no logic here.
    /// </summary>
    public partial class MacroDelayOverlayView : UserControl
    {
        /// <summary>Creates the panel.</summary>
        public MacroDelayOverlayView()
        {
            InitializeComponent();
        }
    }
}
