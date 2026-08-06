using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Savant Elite2 editor's view (specs/12-savant-elite.md §5), resolved from
    /// <see cref="KinesisEdit.ViewModels.SavantElitePedalViewModel"/> by <see cref="ViewLocator"/>.
    /// Everything it shows is bound; there is no code-behind at all.
    /// <para>
    /// Deliberately <b>no Escape handler</b>, unlike <see cref="KeyboardEditorView"/>. The entry box
    /// never leaves its recording state on a keystroke, so an Escape reaching a handler here would
    /// both record an Escape keystroke (capture resolves it like any other key,
    /// docs/app/keystroke-capture.md) and then throw the whole entry away. Cancel is the button.
    /// </para>
    /// </summary>
    public partial class SavantElitePedalView : UserControl
    {
        /// <summary>Creates the pedal view.</summary>
        public SavantElitePedalView()
        {
            InitializeComponent();
        }
    }
}
