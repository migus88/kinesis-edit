using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Settings tab's "App &amp; notifications" section and the custom-swatch strip under it
    /// (docs/design/mockups.md §1j). Markup only — the seventeen preferences, their two on-disk
    /// polarities and the twelve colour slots are all
    /// <see cref="ViewModels.AppPreferencesViewModel"/>'s.
    /// </summary>
    public partial class AppPreferencesView : UserControl
    {
        /// <summary>Creates the app-preferences section.</summary>
        public AppPreferencesView()
        {
            InitializeComponent();
        }
    }
}
