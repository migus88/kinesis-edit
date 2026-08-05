namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The state of one Advantage360 indicator LED (specs/07-lighting.md §5): a function
    /// assignment and one color per layer. Non-layer functions use the <see cref="IndicatorLayer.Base"/>
    /// slot ("layer 0 except for the <c>[lay*]</c> tokens"); the Layer function uses all five.
    /// Reset state: function Disabled, all colors black.
    /// </summary>
    public sealed class IndicatorState
    {
        /// <summary>Number of per-layer color slots (base, keypad, fn1, fn2, fn3; §5).</summary>
        public const int LayerColorCount = 5;

        /// <summary>The indicator's function assignment; Disabled in the reset state (§5).</summary>
        public IndicatorFunction Function { get; set; }

        private readonly LedColor[] _layerColors = new LedColor[LayerColorCount];

        /// <summary>Returns the color stored for <paramref name="layer"/>.</summary>
        public LedColor GetColor(IndicatorLayer layer)
        {
            return _layerColors[(int)layer];
        }

        /// <summary>Stores <paramref name="color"/> for <paramref name="layer"/>.</summary>
        public void SetColor(IndicatorLayer layer, LedColor color)
        {
            _layerColors[(int)layer] = color;
        }

        /// <summary>Restores the reset state: function Disabled, all colors black (§5).</summary>
        public void Reset()
        {
            Function = IndicatorFunction.Disabled;
            Array.Fill(_layerColors, LedColor.Black);
        }

        /// <summary>Whether this state and <paramref name="other"/> hold the same values.</summary>
        public bool IsEquivalentTo(IndicatorState? other)
        {
            if (other is null || Function != other.Function)
            {
                return false;
            }

            for (var index = 0; index < LayerColorCount; index++)
            {
                if (_layerColors[index] != other._layerColors[index])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
