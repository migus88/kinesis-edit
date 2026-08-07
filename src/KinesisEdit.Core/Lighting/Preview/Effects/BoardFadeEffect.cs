namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// The whole board fading in and out together — <see cref="LightingMode.Pulse"/> ("§3:
    /// Breathe and Pulse fade the whole board in and out"; Breathe fades the painted colours
    /// instead, see <see cref="PaintFadeEffect"/>). Pulse writes no base <c>[mono]</c> line, so
    /// the fade runs against black rather than against whatever base colour the layer happens to
    /// be carrying in memory.
    /// </summary>
    internal sealed class BoardFadeEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var intensity = 0.5 * (1.0 - Math.Cos(2.0 * Math.PI * context.Phase));

            return LightingCellComposer.Compose(context.State.EffectColor, LedColor.Black, intensity);
        }
    }
}
