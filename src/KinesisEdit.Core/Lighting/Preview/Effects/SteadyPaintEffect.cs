namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// The paint layer, held still — <see cref="LightingMode.Freestyle"/>, which is per-key
    /// colouring and nothing else, and <see cref="LightingMode.FrozenWave"/>, which is per-LED
    /// colours that deliberately do not animate (specs/07-lighting.md §3). A key with no paint on
    /// file stays hatched: nothing painted means nothing lit.
    /// </summary>
    internal sealed class SteadyPaintEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            return LightingCellComposer.ComposePaint(context.State, key.KeyCode, 1.0);
        }
    }
}
