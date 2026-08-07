namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// A shot travelling along one row with a trail behind it — <see cref="LightingMode.Fireball"/>
    /// ("§3: shoots across a row"). The board is cut into <see cref="RowCount"/> bands and each
    /// cycle picks one by a stable hash of the cycle index, so every shot runs along a real row of
    /// keys instead of possibly falling between two; the trail is read off the travel axis, so
    /// left and right are mirror images.
    /// </summary>
    internal sealed class FireballEffect : ILightingEffect
    {
        private const int RowSeed = 3;
        private const int RowCount = 6;
        private const double RowHalfHeight = 0.6 / RowCount;
        private const double TrailLength = 0.35;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var intensity = 0.0;
            var row = LightingPreviewMath.Hash(RowSeed, context.CycleIndex) % RowCount;
            var rowCentre = (row + 0.5) / RowCount;

            if (Math.Abs(key.Y - rowCentre) <= RowHalfHeight)
            {
                var axis = LightingPreviewMath.AxisCoordinate(context.Direction, key.X, key.Y);
                var behindHead = LightingPreviewMath.Fraction(context.Phase - axis);

                if (behindHead < TrailLength)
                {
                    intensity = 1.0 - (behindHead / TrailLength);
                }
            }

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
