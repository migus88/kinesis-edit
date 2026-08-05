namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The function assigned to an Advantage360 indicator LED (specs/07-lighting.md §5).
    /// File tokens and color rules per function live in <see cref="IndicatorFunctionCatalog"/>.
    /// </summary>
    public enum IndicatorFunction
    {
        /// <summary>Indicator off, token <c>[null]</c>; the reset state (specs/07-lighting.md §5).</summary>
        Disabled = 0,

        /// <summary>NKRO mode indicator, token <c>[nkro]</c>.</summary>
        NkroMode = 1,

        /// <summary>Scroll Lock indicator, token <c>[sclk]</c>.</summary>
        ScrollLock = 2,

        /// <summary>Num Lock indicator, token <c>[nmlk]</c>.</summary>
        NumLock = 3,

        /// <summary>Caps Lock indicator, token <c>[caps]</c>.</summary>
        CapsLock = 4,

        /// <summary>
        /// Active-layer indicator, written as the five lines <c>[layd]</c>/<c>[layk]</c>/
        /// <c>[lay1]</c>/<c>[lay2]</c>/<c>[lay3]</c> with one color per layer.
        /// </summary>
        Layer = 5,

        /// <summary>Active-profile indicator, token <c>[prof]</c>.</summary>
        Profile = 6,

        /// <summary>Battery indicator, token <c>[batt]</c>; carries no color.</summary>
        Battery = 7
    }
}
