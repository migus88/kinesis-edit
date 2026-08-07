namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Keys twinkling out of step with one another — <see cref="LightingMode.Starlight"/> ("§3:
    /// twinkles random keys"). An effect colour over a base (§2.2), never the paint. Each key's
    /// offset into the cycle is a stable hash of its key code, so the pattern is pseudo-random but
    /// identical on every call.
    /// </summary>
    internal sealed class TwinkleEffect : ILightingEffect
    {
        private const double Sharpness = 3.0;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var offset = LightingPreviewMath.UnitFrom(key.KeyCode);
            var position = LightingPreviewMath.Fraction(context.Phase + offset);
            var intensity = Math.Pow(Math.Sin(Math.PI * position), Sharpness);

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
