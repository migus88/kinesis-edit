using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// Read-only view of the Savant Elite2's <c>active/pedals.txt</c>
    /// (specs/12-savant-elite.md §5). XAML only — everything it shows comes from
    /// <see cref="ViewModels.SavantElitePedalViewModel"/>.
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
