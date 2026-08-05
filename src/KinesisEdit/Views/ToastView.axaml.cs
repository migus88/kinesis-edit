using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>One self-dismissing notice (specs/11-feature-dialogs.md §11.9, "Info dialog").</summary>
    public partial class ToastView : UserControl
    {
        /// <summary>Creates the toast view.</summary>
        public ToastView()
        {
            InitializeComponent();
        }
    }
}
