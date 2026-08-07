namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// A band sweeping across the board and wrapping — <see cref="LightingMode.Loop"/> ("§3:
    /// sweeps in the chosen direction"). Read off the travel axis, so reversing the direction
    /// reverses the sweep.
    /// </summary>
    internal sealed class SweepEffect : ILightingEffect
    {
        private const double BandWidth = 0.3;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var axis = LightingPreviewMath.AxisCoordinate(context.Direction, key.X, key.Y);
            var behindHead = LightingPreviewMath.Fraction(context.Phase - axis);
            var intensity = 1.0 - LightingPreviewMath.Clamp01(behindHead / BandWidth);

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
