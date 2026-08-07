using KinesisEdit.Core.Lighting.Preview.Effects;

namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// Samples a layer's lighting mode at an instant, producing the frame the board draws.
    /// <para>
    /// <b>These are representative approximations, not simulations of the firmware.</b> specs/
    /// documents the led file format exhaustively and the effect <i>algorithms</i> not at all —
    /// §3 gives one sentence per mode ("Wave scrolls a rainbow across the board", "Starlight
    /// twinkles random keys") and §7 only says the legacy app animated a preview. Nothing here is
    /// derived from the hardware's timing or maths, and nothing here should ever be cited as
    /// though it were. What the preview must get right is the <i>reading</i>: which way the effect
    /// travels, how fast it goes relative to the speed knob, which colours are in play, and which
    /// modes consume the paint layer. Everything else is a drawing.
    /// </para>
    /// <para>
    /// <b>Deterministic.</b> No clock, no <c>Random</c>, no state carried between calls: the same
    /// <paramref name="state"/>, keys and elapsed time always produce byte-identical cells. The
    /// effects that need to look random — Starlight's twinkles, Ripple's origins, Reactive's
    /// splashes, Rain's columns — draw from a stable hash of the key code and the cycle index
    /// instead of a generator, so a test can assert an exact cell and an animation can be scrubbed
    /// backwards.
    /// </para>
    /// </summary>
    public sealed class LightingEffectSampler
    {
        /// <summary>
        /// What <paramref name="keys"/> show at <paramref name="elapsedSeconds"/> into
        /// <paramref name="state"/>'s effect.
        /// <para>
        /// The frame holds a cell only for the keys the effect lights (see
        /// <see cref="LightingPreviewCell"/>), together with the opacity the paint layer should be
        /// drawn at beneath it. <paramref name="elapsedSeconds"/> is time since the animation
        /// started, in seconds; it may be any finite value, including one that spans many cycles,
        /// and a non-finite value is treated as zero.
        /// </para>
        /// </summary>
        public LightingEffectFrame Sample(
            LayerLightingState state,
            IReadOnlyList<LightingPreviewKey> keys,
            double elapsedSeconds)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(keys);

            var effect = LightingEffectCatalog.For(state.Mode);
            var context = BuildContext(state, elapsedSeconds);
            var cells = new Dictionary<int, LightingPreviewCell>(keys.Count);

            foreach (var key in keys)
            {
                var cell = effect.Sample(in context, in key);

                if (cell is { } lit && lit.Intensity >= LightingPreviewCell.MinimumVisibleIntensity)
                {
                    cells[key.KeyCode] = lit;
                }
            }

            return new LightingEffectFrame(cells, LightingPaintModes.PaintOpacityFor(state.Mode));
        }

        private static LightingEffectContext BuildContext(LayerLightingState state, double elapsedSeconds)
        {
            var seconds = double.IsFinite(elapsedSeconds) ? elapsedSeconds : 0.0;
            var cycles = seconds / LightingSpeedScale.PeriodSecondsFor(state.Speed);

            return new LightingEffectContext(
                state,
                ResolveDirection(state),
                LightingPreviewMath.Fraction(cycles),
                (long)Math.Floor(cycles),
                seconds);
        }

        private static LightingDirection ResolveDirection(LayerLightingState state)
        {
            // The catalog's device-agnostic set is the right one here: it is what the file may
            // carry, and the preview animates what is on file rather than what a panel offers.
            var directions = LightingModeCatalog.Find(state.Mode).KeyBacklightDirections;

            if (directions.Count == 0)
            {
                return LightingDirection.None;
            }

            return directions.Contains(state.Direction) ? state.Direction : LightingDirection.Left;
        }
    }
}
