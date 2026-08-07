using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace KinesisEdit.Views
{
    /// <summary>
    /// The Lighting tab's mode rail (design mockup 2f): the mode list, the parameters the selected
    /// mode accepts, and the colour picker disclosed in place of the list. It holds no lighting
    /// rule — every row, every parameter and every command is
    /// <see cref="ViewModels.LightingTabViewModel"/>'s.
    /// <para>
    /// The one thing it owns is <b>presentation state</b>: whether the picker is disclosed. That is
    /// not a fact about the lighting model — nothing is written by opening or closing it, and the
    /// file cannot tell the difference — so it lives here rather than in the view model, exactly as
    /// the dashboard's refresh deferral lives in <c>DashboardView</c>.
    /// </para>
    /// </summary>
    public partial class LightingModeRailView : UserControl
    {
        /// <summary>The section label over the two colour swatches (design mockup 2f: "Color").</summary>
        public const string ColorLabel = "COLOR";

        /// <summary>The section label over the nine speed bars (2f: "Speed 6 / 9").</summary>
        public const string SpeedLabel = "SPEED";

        /// <summary>The section label over the four arrows (2f: "Direction").</summary>
        public const string DirectionLabel = "DIRECTION";

        /// <summary>The label over the disclosed picker, naming what is being edited.</summary>
        public const string PickerLabel = "PICK A COLOR";

        /// <summary>
        /// What closes the disclosed picker. Named for what it does to the rail rather than for what
        /// it does to the colour: nothing here is cancellable — a colour is written into the model
        /// the moment it is picked, and the board shows it — so "Cancel" would be a lie.
        /// </summary>
        public const string ClosePickerCaption = "Done";

        /// <summary>
        /// Whether the colour picker is showing in place of the mode list. Written by a swatch in
        /// the footer and by <see cref="ClosePickerCaption"/>; read by both halves of the rail's
        /// body row.
        /// </summary>
        public static readonly StyledProperty<bool> IsPickerOpenProperty =
            AvaloniaProperty.Register<LightingModeRailView, bool>(nameof(IsPickerOpen));

        /// <inheritdoc cref="IsPickerOpenProperty" />
        public bool IsPickerOpen
        {
            get => GetValue(IsPickerOpenProperty);
            set => SetValue(IsPickerOpenProperty, value);
        }

        /// <summary>Creates the mode rail.</summary>
        public LightingModeRailView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// A swatch was clicked. The button's own <c>Command</c> has already pointed the picker at
        /// that slot; this is the other half of the same gesture — showing the picker it now edits.
        /// </summary>
        private void OnColorSlotClicked(object? sender, RoutedEventArgs e)
        {
            IsPickerOpen = true;
        }

        /// <summary>Closes the picker and brings the mode list back.</summary>
        private void OnClosePickerClicked(object? sender, RoutedEventArgs e)
        {
            IsPickerOpen = false;
        }
    }
}
