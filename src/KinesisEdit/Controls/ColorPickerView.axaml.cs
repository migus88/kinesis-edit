using Avalonia.Controls;

namespace KinesisEdit.Controls
{
    /// <summary>
    /// The colour picker of specs/07-lighting.md §4: Avalonia's <c>ColorView</c> (HSV ring, R/G/B
    /// sliders, hex field) plus the spec's premixed swatch row and the twelve custom slots.
    /// <para>
    /// It binds a <see cref="ViewModels.ColorPickerViewModel"/> directly rather than being
    /// resolved through <c>ViewLocator</c>, exactly like <see cref="KeyboardView"/>: it is a
    /// component hosted by a panel, not a screen. The <c>#RRGGBB</c> ↔ <c>Color</c> conversion is
    /// the one in <c>Converters/HexColorToColorConverter</c>, so no Avalonia colour ever reaches
    /// the view model.
    /// </para>
    /// </summary>
    public partial class ColorPickerView : UserControl
    {
        /// <summary>Creates the colour picker.</summary>
        public ColorPickerView()
        {
            InitializeComponent();
        }
    }
}
