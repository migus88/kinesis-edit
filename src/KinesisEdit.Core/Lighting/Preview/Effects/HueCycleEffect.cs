namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// The whole board on one hue, cycling — <see cref="LightingMode.Spectrum"/> ("§3: cycles hue
    /// board-wide"). No direction, no colours of its own, and the paint is ignored.
    /// </summary>
    internal sealed class HueCycleEffect : ILightingEffect
    {
        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            return new LightingPreviewCell(LightingPreviewMath.FromHue(context.Phase), 1.0);
        }
    }
}
