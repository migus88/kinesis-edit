namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The lighting state of one layer in one configuration context — key backlight, or TKO
    /// edge (specs/07-lighting.md §1.5): a mode, effect and base colors, speed, direction, and
    /// a per-key color map keyed by key code. Reset defaults per §6: mode Disabled, effect
    /// color lime green, base color black, speed 5, direction left, no per-key colors.
    /// </summary>
    public sealed class LayerLightingState
    {
        /// <summary>The default speed (specs/07-lighting.md §6).</summary>
        public const int DefaultSpeed = 5;

        /// <summary>Lowest valid speed (specs/07-lighting.md §2.1).</summary>
        public const int MinimumSpeed = 1;

        /// <summary>Highest valid speed (specs/07-lighting.md §2.1).</summary>
        public const int MaximumSpeed = 9;

        /// <summary>The LED mode of the layer.</summary>
        public LightingMode Mode { get; set; }

        /// <summary>The effect color; defaults to lime green RGB(0,255,0) (§6).</summary>
        public LedColor EffectColor { get; set; }

        /// <summary>The base/background color of the two-line effects; defaults to black (§6).</summary>
        public LedColor BaseColor { get; set; }

        /// <summary>The effect speed, 1-9; defaults to 5 (§6).</summary>
        public int Speed { get; set; }

        /// <summary>The effect direction; defaults to left (§6).</summary>
        public LightingDirection Direction { get; set; }

        /// <summary>
        /// Per-key colors keyed by key code (specs/07-lighting.md §4). Black is the "no color"
        /// value, so the map only ever holds non-black colors — keys with no color are omitted
        /// on save (§2.2).
        /// </summary>
        public IReadOnlyDictionary<int, LedColor> KeyColors => _keyColors;

        private readonly Dictionary<int, LedColor> _keyColors = new();

        /// <summary>Creates a layer state in the reset defaults of specs/07-lighting.md §6.</summary>
        public LayerLightingState()
        {
            Reset();
        }

        /// <summary>
        /// Assigns <paramref name="color"/> to <paramref name="keyCode"/>. Assigning black
        /// clears the key — black is the "no color" value (specs/07-lighting.md §2.1).
        /// </summary>
        public void SetKeyColor(int keyCode, LedColor color)
        {
            if (color.IsBlack)
            {
                _keyColors.Remove(keyCode);
            }
            else
            {
                _keyColors[keyCode] = color;
            }
        }

        /// <summary>Erases every per-key color assignment (specs/07-lighting.md §4 "Reset").</summary>
        public void ClearKeyColors()
        {
            _keyColors.Clear();
        }

        /// <summary>Restores the reset defaults of specs/07-lighting.md §6.</summary>
        public void Reset()
        {
            Mode = LightingMode.Disabled;
            EffectColor = LedColor.DefaultEffectColor;
            BaseColor = LedColor.Black;
            Speed = DefaultSpeed;
            Direction = LightingDirection.Left;
            _keyColors.Clear();
        }

        /// <summary>Whether this state and <paramref name="other"/> hold the same values.</summary>
        public bool IsEquivalentTo(LayerLightingState? other)
        {
            if (other is null)
            {
                return false;
            }

            if (Mode != other.Mode
                || EffectColor != other.EffectColor
                || BaseColor != other.BaseColor
                || Speed != other.Speed
                || Direction != other.Direction
                || _keyColors.Count != other._keyColors.Count)
            {
                return false;
            }

            foreach (var (keyCode, color) in _keyColors)
            {
                if (!other._keyColors.TryGetValue(keyCode, out var otherColor) || color != otherColor)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
