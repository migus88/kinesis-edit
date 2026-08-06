using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The v-Drive status chip, shared by the shell's app bar and by an editor that draws its own
    /// toolbar. Everything it shows is bound to an <see cref="ViewModels.IShellChrome"/>; there is
    /// no code here on purpose.
    /// </summary>
    public partial class StatusChipView : UserControl
    {
        /// <summary>Creates the chip.</summary>
        public StatusChipView()
        {
            InitializeComponent();
        }
    }
}
