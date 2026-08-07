namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Drops falling down a scattering of columns — <see cref="LightingMode.Rain"/> ("§3: drops
    /// random columns"). The board is cut into <see cref="ColumnCount"/> strips and roughly one in
    /// <see cref="ActiveColumnRatio"/> runs a drop each cycle, chosen by a stable hash of the
    /// column and the cycle index. The mode carries no direction (§3), so rain always falls.
    /// </summary>
    internal sealed class RainEffect : ILightingEffect
    {
        private const int ColumnCount = 16;
        private const int ActiveColumnRatio = 3;
        private const double TrailLength = 0.4;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var intensity = 0.0;
            var column = (int)Math.Clamp(Math.Floor(key.X * ColumnCount), 0.0, ColumnCount - 1.0);

            if (LightingPreviewMath.Hash(column, context.CycleIndex) % ActiveColumnRatio == 0)
            {
                var behindHead = context.Phase - key.Y;

                if (behindHead >= 0.0 && behindHead < TrailLength)
                {
                    intensity = 1.0 - (behindHead / TrailLength);
                }
            }

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
