using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the lighting tab's layer switch: the layer's own
    /// <see cref="LayerLightingState"/> (mode, colors, speed, direction and per-key colors are
    /// fully independent per layer — specs/07-lighting.md §4 "Layer-specific lighting") together
    /// with the keyboard picture that layer paints on.
    /// <para>
    /// The Fn entry is disabled below the <c>LightingLayerCustomization</c> gate (LED firmware
    /// 1.0.44 on the RGB, §3): the firmware cannot read <c>fn </c> lines, so offering the layer
    /// would invite the user to write a file the board ignores.
    /// </para>
    /// </summary>
    public sealed class LightingLayerViewModel : ViewModelBase
    {
        /// <summary>The layer's caption, taken from the keyboard picture's own layer caption.</summary>
        public string Caption { get; }

        /// <summary>Layer identity: 0 = top, 1 = Fn (the two layers a led file describes, §1.5).</summary>
        public int Index { get; }

        /// <summary>The lighting state this entry edits.</summary>
        public LayerLightingState State { get; }

        /// <summary>The keyboard picture of the same layer, or null when the profile has none.</summary>
        public KeyboardLayerViewModel? Board { get; }

        /// <summary>Whether the layer may be edited at all (the Fn firmware gate).</summary>
        public bool IsEnabled { get; }

        /// <summary>Whether this is the layer the tab is showing.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Joins one lighting layer state to the picture it paints on.</summary>
        public LightingLayerViewModel(
            int index,
            string caption,
            LayerLightingState state,
            KeyboardLayerViewModel? board,
            bool isEnabled)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Index = index;
            Caption = caption;
            State = state ?? throw new ArgumentNullException(nameof(state));
            Board = board;
            IsEnabled = isEnabled;
        }
    }
}
