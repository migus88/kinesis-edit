using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The macro-insertion token picker (specs/11-feature-dialogs.md §11.6), resolved from
    /// <see cref="TokenPickerOverlayViewModel"/> by <see cref="ViewLocator"/>.
    /// <para>
    /// There is deliberately no code here — including no focus call. The overlay's view model asks
    /// for the caret in its own constructor, and <see cref="TokenPickerView"/> answers the request
    /// as it attaches; putting a second focus grab here would take the caret away from a field the
    /// picker has already claimed.
    /// </para>
    /// </summary>
    public partial class TokenPickerOverlayView : UserControl
    {
        /// <summary>Creates the panel.</summary>
        public TokenPickerOverlayView()
        {
            InitializeComponent();
        }
    }
}
