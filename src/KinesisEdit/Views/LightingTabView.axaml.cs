using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The editor's Lighting tab (specs/07-lighting.md §3, §4): layer switch, mode menu, per-mode
    /// parameter panels, colour picker, zone buttons and the per-key keyboard picture. It contains
    /// no lighting knowledge — see <see cref="ViewModels.LightingTabViewModel"/>.
    /// </summary>
    public partial class LightingTabView : UserControl
    {
        /// <summary>Creates the lighting view.</summary>
        public LightingTabView()
        {
            InitializeComponent();
        }
    }
}
