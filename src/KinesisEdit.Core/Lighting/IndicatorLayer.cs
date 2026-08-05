namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The layer a stored indicator color belongs to (specs/07-lighting.md §5). Values match
    /// the Advantage360 layer indices of specs/05-key-model.md §1.4. Non-layer functions store
    /// their color at <see cref="Base"/> ("layer 0 except for the <c>[lay*]</c> tokens").
    /// </summary>
    public enum IndicatorLayer
    {
        /// <summary>Base layer color, token <c>[layd]</c>; also the color slot of non-layer functions.</summary>
        Base = 0,

        /// <summary>Keypad layer color, token <c>[layk]</c>.</summary>
        Keypad = 1,

        /// <summary>Fn1 layer color, token <c>[lay1]</c>.</summary>
        Fn1 = 2,

        /// <summary>Fn2 layer color, token <c>[lay2]</c>.</summary>
        Fn2 = 3,

        /// <summary>Fn3 layer color, token <c>[lay3]</c>.</summary>
        Fn3 = 4
    }
}
