namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// A rainbow scrolling across the board — <see cref="LightingMode.Wave"/> ("§3: scrolls a
    /// rainbow"). The hue is read off the travel axis, so reversing the direction reverses the
    /// gradient. The mode carries no colours of its own, and ignores the paint.
    /// </summary>
    internal sealed class TravellingRainbowEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var axis = LightingPreviewMath.AxisCoordinate(context.Direction, key.X, key.Y);
            var hue = LightingPreviewMath.Fraction(axis - context.Phase);

            return new LightingPreviewCell(LightingPreviewMath.FromHue(hue), 1.0);
        }
    }
}
