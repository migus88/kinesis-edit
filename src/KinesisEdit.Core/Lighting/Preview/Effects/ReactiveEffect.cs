namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Splashes that flare and fade — <see cref="LightingMode.Reactive"/> ("§3: lights keys on
    /// key-press"). An effect colour over a base (§2.2), never the paint. Nothing is really being
    /// typed during a preview, so a stable hash of key code and cycle index stands in for the
    /// presses: roughly one key in <see cref="PressedKeyRatio"/> flares each cycle and decays
    /// across it.
    /// </summary>
    internal sealed class ReactiveEffect : ILightingEffect
    {
        private const int PressedKeyRatio = 8;

        /// <inheritdoc />
        public LightingPreviewCell? Sample(in LightingEffectContext context, in LightingPreviewKey key)
        {
            var isPressed = LightingPreviewMath.Hash(key.KeyCode, context.CycleIndex) % PressedKeyRatio == 0;
            var intensity = isPressed ? 1.0 - context.Phase : 0.0;

            return LightingCellComposer.Compose(context.State.EffectColor, context.State.BaseColor, intensity);
        }
    }
}
