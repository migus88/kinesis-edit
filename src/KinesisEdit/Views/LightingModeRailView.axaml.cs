using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Lighting tab's rail: the mode dropdown, and under it <b>only</b> the properties the
    /// selected mode has — its colours, the picker that edits them, its speed, its direction — each
    /// with one line saying what it means in that mode. It holds no lighting rule: every option,
    /// every parameter, every hint and every command is
    /// <see cref="ViewModels.LightingTabViewModel"/>'s.
    /// <para>
    /// It owns no presentation state either, since issue #128. It used to carry
    /// <c>IsPickerOpen</c> — whether the colour picker was disclosed <i>in place of</i> the mode
    /// list, which was the only part of the rail tall enough to hold it. There is no list to
    /// displace now, so the picker is on screen wherever the mode can be painted at all
    /// (<see cref="Core.Lighting.Preview.LightingModeParameters.AcceptsPaint"/>) and the toggle
    /// went with the reason for it.
    /// </para>
    /// </summary>
    public partial class LightingModeRailView : UserControl
    {
        /// <summary>The section label over the two colour swatches (design mockup 2f: "Color").</summary>
        public const string ColorLabel = "COLOR";

        /// <summary>The section label over the nine speed bars (2f: "Speed 6 / 9").</summary>
        public const string SpeedLabel = "SPEED";

        /// <summary>The section label over the direction arrows (2f: "Direction").</summary>
        public const string DirectionLabel = "DIRECTION";

        /// <summary>The label over the picker, naming what it is for.</summary>
        public const string PickerLabel = "PICK A COLOR";

        /// <summary>Creates the rail.</summary>
        public LightingModeRailView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// The mode picker is a <see cref="ComboBox"/>, so choosing a mode is a selection rather
        /// than a click — but everything picking a mode means (writing it into the layer, recomputing
        /// what the mode accepts, normalising a direction it cannot use, redrawing the board and
        /// announcing the write) lives in <see cref="LightingTabViewModel.SelectModeCommand"/>, so
        /// that is still what runs.
        /// <para>
        /// The command is idempotent by identity — re-selecting the mode already open returns
        /// without writing — which is what makes it safe to fire on the selection the control makes
        /// for itself while it binds, and on every re-show of a tab that is hidden rather than
        /// unloaded.
        /// </para>
        /// </summary>
        private void OnModeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not LightingTabViewModel viewModel || sender is not SelectingItemsControl picker)
            {
                return;
            }

            if (picker.SelectedItem is LightingModeViewModel mode)
            {
                viewModel.SelectModeCommand.Execute(mode);
            }

            // The panel can refuse — there is no layer attached yet while the editor is loading —
            // so the control is put back on whatever the panel kept rather than left showing a mode
            // nothing switched to. Guarded by the comparison, which is what stops the assignment
            // re-entering this handler.
            if (!ReferenceEquals(picker.SelectedItem, viewModel.SelectedModeOption))
            {
                picker.SelectedItem = viewModel.SelectedModeOption;
            }
        }

        /// <summary>
        /// A swatch was clicked. The button's own <c>Command</c> has already pointed the picker at
        /// that slot; this is the other half of the same gesture — putting the picker it now edits
        /// on screen.
        /// <para>
        /// It is a <b>scroll</b>, not a disclosure: the picker is drawn in every mode that can be
        /// painted, and it is the last block of the rail because it is ~460 px tall and would
        /// otherwise push Speed and Direction off the bottom of a 680 px window. So it can be below
        /// the fold, and a swatch that re-targeted an editor nobody could see would look dead.
        /// </para>
        /// <para>
        /// <b>It needs no guard for the collapsed picker</b>, even though the section is hidden in
        /// Off and Pitch Black since the properties-panel fix:
        /// <see cref="Core.Lighting.Preview.LightingModeParameters.AcceptsAnyColor"/> implies
        /// <see cref="Core.Lighting.Preview.LightingModeParameters.AcceptsPaint"/> — the two modes
        /// that write no file body write no colour line either (specs/07-lighting.md §2.2) — so a
        /// swatch, whose own block is gated on the former, cannot be on screen to be clicked while
        /// the picker is not. The implication is pinned in
        /// <c>LightingModeParametersTests</c> rather than assumed here.
        /// </para>
        /// </summary>
        private void OnColorSlotClicked(object? sender, RoutedEventArgs e)
        {
            PickerSection.BringIntoView();
        }
    }
}
