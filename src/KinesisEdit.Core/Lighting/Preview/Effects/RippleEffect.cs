namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// A ring expanding from a point that moves each cycle — <see cref="LightingMode.Ripple"/>
    /// ("§3: expands rings"). An effect colour over a base (§2.2), never the paint. The origin is
    /// a stable hash of the cycle index, so the ripple lands somewhere new each cycle and in the
    /// same place on every replay.
    /// </summary>
    internal sealed class RippleEffect : ILightingEffect
    {
        private const int OriginXSeed = 1;
        private const int OriginYSeed = 2;
        private const double MaximumRadius = 1.4;
        private const double RingWidth = 0.18;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var originX = LightingPreviewMath.UnitFrom(OriginXSeed, context.CycleIndex);
            var originY = LightingPreviewMath.UnitFrom(OriginYSeed, context.CycleIndex);
            var radius = context.Phase * MaximumRadius;
            var offsetX = key.X - originX;
            var offsetY = key.Y - originY;
            var distance = Math.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
            var intensity = 1.0 - LightingPreviewMath.Clamp01(Math.Abs(distance - radius) / RingWidth);

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
