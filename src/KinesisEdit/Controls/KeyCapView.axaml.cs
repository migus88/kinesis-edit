using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// One key cap of the keyboard picture, over a <see cref="ViewModels.KeyboardKeyViewModel"/>.
    /// Everything it draws comes from that view model and the <c>keyCap</c> style classes, so it
    /// carries no code of its own.
    /// </summary>
    public partial class KeyCapView : UserControl
    {
        /// <summary>Creates the key cap.</summary>
        public KeyCapView()
        {
            InitializeComponent();
        }
    }
}
