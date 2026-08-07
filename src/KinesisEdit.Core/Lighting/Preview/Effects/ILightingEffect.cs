namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// One mode's contribution to a preview frame. Implementations must be pure: same context,
    /// same key, same answer — see <see cref="LightingEffectSampler"/> for why.
    /// </summary>
    internal interface ILightingEffect
    {
        /// <summary>
        /// What <paramref name="key"/> shows at this instant, or null when the effect leaves it
        /// unlit (the frame then omits the key entirely).
        /// </summary>
        LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key);
    }
}
