namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Lights nothing — <see cref="LightingMode.Disabled"/>. The board is entirely hatched,
    /// because off is hatched and never black.
    /// </summary>
    internal sealed class UnlitEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            return null;
        }
    }
}
