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
    /// <see cref="Core.Lighting.Preview.LightingModeParameters"/>'s answer for the current mode.
    /// </para>
    /// </summary>
    public sealed class LightingColorSlotViewModel : ViewModelBase
    {
        /// <summary>Caption of the effect-color swatch (specs/07-lighting.md §3).</summary>
        public const string EffectColorCaption = "Effect Color";

        /// <summary>
        /// What the same swatch is called in a mode where it is <b>not effect colour at all</b>.
        /// specs/07-lighting.md §2.2 writes no effect-colour line for Freestyle, Breathe or Frozen
        /// Wave: their file body is per-key colour lines, so the swatch is literally "the colour
        /// you paint with" (§4: "clicking a key applies the currently selected picker color") and
        /// calling it "Effect Color" there names a line the file will never carry.
        /// </summary>
        public const string PaintColorCaption = "Paint Color";

        /// <summary>Caption of the base-color swatch (§3), shown for the two-line effects only.</summary>
        public const string BaseColorCaption = "Base Color";

        /// <summary>
        /// What the effect-colour swatch is called in <paramref name="mode"/>. It asks the catalog
        /// whether the mode writes an effect-colour line at all
        /// (<see cref="LightingModeDefinition.WritesEffectColor"/>, i.e. §2.2's own grammar) rather
        /// than restating which modes those are — the per-mode table lives in Core and nowhere
        /// else.
        /// </summary>
        public static string EffectCaptionFor(LightingMode mode)
        {
            return LightingModeCatalog.Find(mode).WritesEffectColor ? EffectColorCaption : PaintColorCaption;
        }

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

        /// <summary>
        /// The swatch's caption. Settable, because the effect swatch is renamed per mode — see
        /// <see cref="EffectCaptionFor"/>.
        /// </summary>
        public string Caption
        {
            get => _caption;
            set => SetProperty(ref _caption, value);
        }

        /// <summary>
        /// The one line printed under the swatch saying what this colour <i>means</i> in the
        /// selected mode (<see cref="LightingHintCatalog"/>). It is per mode, which is the point:
        /// "Effect Color" and "Base Color" are indistinguishable names until something says that
        /// one is the flash and the other is what the key rests at.
        /// </summary>
        public string Hint
        {
            get => _hint;
            set => SetProperty(ref _hint, value);
        }

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
        private string _caption;
        private string _hint = string.Empty;
        private bool _isVisible;
        private bool _isSelected;

        /// <summary>Creates a swatch over one color of a layer's lighting state.</summary>
        public LightingColorSlotViewModel(
            string caption,
            Func<LayerLightingState, LedColor> reader,
            Action<LayerLightingState, LedColor> writer)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            _caption = caption;
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
