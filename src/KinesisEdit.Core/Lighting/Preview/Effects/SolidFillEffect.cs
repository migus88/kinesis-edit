namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// The whole board held on one colour — <see cref="LightingMode.Monochrome"/>, the design's
    /// "Solid". The mode writes a single effect colour and no per-key lines
    /// (specs/07-lighting.md §2.2), so the preview fills every key with
    /// <see cref="LayerLightingState.EffectColor"/> and never reads the paint.
    /// </summary>
    internal sealed class SolidFillEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            return new LightingPreviewCell(context.State.EffectColor, 1.0);
        }
    }
}
