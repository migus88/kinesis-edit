using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The key inspector's locked-key panel (mockup <c>2h</c>), bound to
    /// <see cref="ViewModels.LockedKeyPanelViewModel"/>. Everything it shows is bound and nothing
    /// here is interactive except one of the editor's own commands, so there is no code — which is
    /// the point: the panel exists to explain a position, not to edit one.
    /// </summary>
    public partial class LockedKeyPanelView : UserControl
    {
        /// <summary>Creates the locked-key panel.</summary>
        public LockedKeyPanelView()
        {
            InitializeComponent();
        }
    }
}
