namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// A bar bouncing from edge to edge — <see cref="LightingMode.Rebound"/> ("§3: bounces a
    /// bar"). Its two directions are axes rather than headings: left means horizontal and up means
    /// vertical (§3), so the bar's travel is a triangle wave along X or along Y.
    /// </summary>
    internal sealed class ReboundEffect : ILightingEffect
    {
        private const double BarHalfWidth = 0.16;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var axis = context.Direction == LightingDirection.Up ? key.Y : key.X;
            var barCentre = LightingPreviewMath.Triangle(context.Phase);
            var intensity = 1.0 - LightingPreviewMath.Clamp01(Math.Abs(axis - barCentre) / BarHalfWidth);

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
