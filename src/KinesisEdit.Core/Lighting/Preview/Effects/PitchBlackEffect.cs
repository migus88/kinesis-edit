namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Lights every key black at full intensity — <see cref="LightingMode.PitchBlack"/>. Black is
    /// a colour a key can legitimately be lit, and the board must read differently from
    /// <see cref="LightingMode.Disabled"/>, which is hatched.
    /// </summary>
    internal sealed class PitchBlackEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            return new LightingPreviewCell(LedColor.Black, 1.0);
        }
    }
}
