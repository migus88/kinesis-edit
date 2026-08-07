using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Macros tab's macro editor (specs/10-apps-and-ui.md, specs/06-macros.md), resolved from
    /// <see cref="MacroPanelViewModel"/> by <see cref="ViewLocator"/>.
    /// <para>
    /// There is deliberately no code here. Recording is armed by a command and fed by the editor's
    /// single keystroke subscription — never by this view — so nothing in it handles a key event,
    /// and a <c>KeyDown</c> handler on a panel that records keystrokes is precisely the second
    /// capture owner docs/app/keyboard-editor.md forbids.
    /// </para>
    /// </summary>
    public partial class MacroPanelView : UserControl
    {
        /// <summary>Creates the macro panel.</summary>
        public MacroPanelView()
        {
            InitializeComponent();
        }
    }
}
