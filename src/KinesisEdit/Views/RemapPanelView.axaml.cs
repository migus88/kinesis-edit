using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The key inspector's Remap panel (mockups <c>1e</c>/<c>2a</c>), resolved from
    /// <see cref="RemapPanelViewModel"/> by <see cref="ViewLocator"/>.
    /// <para>
    /// There is deliberately no code here. The one gesture the panel needs — a double click that
    /// takes a row — belongs to the picker it hosts, and so does the caret: focus lives with the
    /// field, in <see cref="TokenPickerView"/>. Everything else is bound.
    /// </para>
    /// </summary>
    public partial class RemapPanelView : UserControl
    {
        /// <summary>Creates the panel.</summary>
        public RemapPanelView()
        {
            InitializeComponent();
        }
    }
}
