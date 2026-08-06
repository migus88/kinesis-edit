using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The editor's Settings tab (specs/08-settings.md §5): one row per control the device's
    /// capability designates, rendered by row kind. It contains no device knowledge — see
    /// <see cref="ViewModels.KeyboardSettingsViewModel"/>.
    /// </summary>
    public partial class KeyboardSettingsView : UserControl
    {
        /// <summary>Creates the keyboard-settings view.</summary>
        public KeyboardSettingsView()
        {
            InitializeComponent();
        }
    }
}
