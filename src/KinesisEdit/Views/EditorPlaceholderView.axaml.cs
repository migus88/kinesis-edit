using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// Stand-in for the per-device editors of issues #14-#16. It only proves the shell's
    /// navigation contract (specs/10-apps-and-ui.md, "Opening a device" / "Home").
    /// </summary>
    public partial class EditorPlaceholderView : UserControl
    {
        /// <summary>Creates the placeholder editor view.</summary>
        public EditorPlaceholderView()
        {
            InitializeComponent();
        }
    }
}
