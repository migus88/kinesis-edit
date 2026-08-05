using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Export files panel (specs/11-feature-dialogs.md §11.5), resolved from
    /// <see cref="ExportOverlayViewModel"/> by <see cref="ViewLocator"/>. Everything it shows is
    /// bound; there is no logic here.
    /// </summary>
    public partial class ExportOverlayView : UserControl
    {
        /// <summary>Creates the panel.</summary>
        public ExportOverlayView()
        {
            InitializeComponent();
        }
    }
}
