using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The legend row under the keyboard canvas (mockups 1e/2a), bound to
    /// <see cref="ViewModels.BoardLegendViewModel"/>. Everything it shows is bound; there is no
    /// code here at all, which is the point — the row is a read-out plus two of the editor's own
    /// commands.
    /// </summary>
    public partial class BoardLegendView : UserControl
    {
        /// <summary>Creates the legend row.</summary>
        public BoardLegendView()
        {
            InitializeComponent();
        }
    }
}
