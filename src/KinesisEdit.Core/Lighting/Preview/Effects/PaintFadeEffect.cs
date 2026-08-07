namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// The painted colours fading in and out together — <see cref="LightingMode.Breathe"/>, whose
    /// file body is per-key colour lines after a <c>[breathe]&gt;[spdN]</c> header
    /// (specs/07-lighting.md §2.2): the per-key colours are what breathes. A key with no paint on
    /// file has nothing to fade and stays hatched.
    /// </summary>
    internal sealed class PaintFadeEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var intensity = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * context.Phase));

            return LightingCellComposer.ComposePaint(context.State, key.KeyCode, intensity);
        }
    }
}
