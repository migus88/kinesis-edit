using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The key inspector's Tap &amp; hold panel (mockup <c>2h</c>, specs/11-feature-dialogs.md
    /// §11.1), bound to <see cref="ViewModels.TapAndHoldPanelViewModel"/>.
    /// <para>
    /// There is no code here on purpose. The two fields are filled by the editor's single keystroke
    /// subscription — never by this view — so nothing in it handles a key event, and a
    /// <c>KeyDown</c> handler on a panel that arms capture is precisely the second capture owner
    /// docs/app/keyboard-editor.md forbids.
    /// </para>
    /// </summary>
    public partial class TapAndHoldPanelView : UserControl
    {
        /// <summary>Creates the Tap and Hold panel.</summary>
        public TapAndHoldPanelView()
        {
            InitializeComponent();
        }
    }
}
