using Avalonia.Controls;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The key inspector rail (mockups <c>1e</c>/<c>2a</c>), bound to
    /// <see cref="KeyInspectorViewModel"/>. Everything it shows is bound; the only code here is the
    /// one handler that turns a mode tab being chosen back into the command a row of buttons would
    /// have run — the same shape <see cref="KeyboardEditorView"/> uses for its section strip and
    /// its layer switcher.
    /// <para>
    /// <b>There is no Escape handler here.</b> The editor owns the whole keyboard grammar in one
    /// place (<see cref="KeyboardEditorView"/>), because the order Escape unwinds in — feature
    /// panel, then capture, then an armed copy, then the inspector — is a decision no single
    /// surface can make on its own.
    /// </para>
    /// </summary>
    public partial class KeyInspectorView : UserControl
    {
        /// <summary>Creates the rail.</summary>
        public KeyInspectorView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// A mode tab was chosen. The strip's own selection is bound one-way, so the view model
        /// stays the single decision-maker: a tab it refuses — a dead one on a locked position —
        /// leaves <see cref="KeyInspectorViewModel.SelectedMode"/> where it was.
        /// </summary>
        private void OnModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not KeyInspectorViewModel inspector)
            {
                return;
            }

            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not KeyInspectorTabViewModel tab)
            {
                return;
            }

            inspector.SelectModeCommand.Execute(tab);
        }
    }
}
