using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The editor's advisory summary strip. Everything it shows is bound to
    /// <see cref="ViewModels.Advisories.AdvisoryStripViewModel"/> — the open section's count and
    /// sentence, and the <c>Review N</c> command — which the editor hands it through
    /// <c>KeyboardEditorViewModel.AdvisoryStrip</c>, so there is no code here on purpose.
    /// </summary>
    public partial class AdvisoryStripView : UserControl
    {
        /// <summary>Creates the strip.</summary>
        public AdvisoryStripView()
        {
            InitializeComponent();
        }
    }
}
