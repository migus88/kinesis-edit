using Avalonia.Controls;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The device dashboard: a two-column grid of cards, or the troubleshoot empty state of
    /// specs/11-feature-dialogs.md §11.8 when nothing is detected.
    /// <para>
    /// Markup only. It used to defer refreshes while the pointer or keyboard focus was inside the
    /// card grid, because a 2 s loop could otherwise move a control under the cursor; scanning is
    /// manual now, so nothing arrives unbidden and there is no click left to steal.
    /// </para>
    /// </summary>
    public partial class DashboardView : UserControl
    {
        /// <summary>Creates the dashboard view.</summary>
        public DashboardView()
        {
            InitializeComponent();
        }
    }
}
