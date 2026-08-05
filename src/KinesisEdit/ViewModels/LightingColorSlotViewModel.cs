using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One of the lighting tab's two color swatches — the effect color and the base color of
    /// specs/07-lighting.md §3. The slot carries a reader/writer pair onto
    /// <see cref="LayerLightingState"/>, so it addresses the model without knowing which of the
    /// two it is, exactly like the settings rows address settings keys
    /// (docs/app/keyboard-editor.md, "The Settings tab").
    /// <para>
    /// The picker edits whichever slot is <see cref="IsSelected"/>; visibility is
    /// <see cref="LightingPanelVisibility"/>'s answer for the current mode.
    /// </para>
    /// </summary>
    public sealed class LightingColorSlotViewModel : ViewModelBase
    {
        /// <summary>Caption of the effect-color swatch (specs/07-lighting.md §3).</summary>
        public const string EffectColorCaption = "Effect Color";

        /// <summary>Caption of the base-color swatch (§3), shown for the two-line effects only.</summary>
        public const string BaseColorCaption = "Base Color";

        /// <summary>The swatch over <see cref="LayerLightingState.EffectColor"/>.</summary>
        public static LightingColorSlotViewModel CreateEffectColor()
        {
            return new LightingColorSlotViewModel(
                EffectColorCaption,
                state => state.EffectColor,
                (state, color) => state.EffectColor = color);
        }

        /// <summary>The swatch over <see cref="LayerLightingState.BaseColor"/>.</summary>
        public static LightingColorSlotViewModel CreateBaseColor()
        {
            return new LightingColorSlotViewModel(
                BaseColorCaption,
                state => state.BaseColor,
                (state, color) => state.BaseColor = color);
        }

        /// <summary>The swatch's caption.</summary>
        public string Caption { get; }

        /// <summary>The color the slot currently holds.</summary>
        public LedColor Color
        {
            get => _color;
            private set
            {
                if (SetProperty(ref _color, value))
                {
                    OnPropertyChanged(nameof(ColorHex));
                }
            }
        }

        /// <summary>The slot's color as the <c>#RRGGBB</c> string the view paints with.</summary>
        public string ColorHex => KeyColorOverlay.ToHex(_color);

        /// <summary>Whether the current mode has this slot at all (§3).</summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>Whether the picker is currently editing this slot.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private readonly Func<LayerLightingState, LedColor> _reader;
        private readonly Action<LayerLightingState, LedColor> _writer;
        private LedColor _color = LedColor.DefaultEffectColor;
        private bool _isVisible;
        private bool _isSelected;

        /// <summary>Creates a swatch over one color of a layer's lighting state.</summary>
        public LightingColorSlotViewModel(
            string caption,
            Func<LayerLightingState, LedColor> reader,
            Action<LayerLightingState, LedColor> writer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Caption = caption;
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
        }

        /// <summary>Re-reads the slot from <paramref name="state"/> — what a layer switch ends with.</summary>
        public void ReadFrom(LayerLightingState? state)
        {
            Color = state is null ? LedColor.DefaultEffectColor : _reader(state);
        }

        /// <summary>Writes <paramref name="color"/> into the slot and through into the model.</summary>
        public void Assign(LayerLightingState? state, LedColor color)
        {
            Color = color;

            if (state is not null)
            {
                _writer(state, color);
            }
        }
    }
}
