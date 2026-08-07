namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// The one place that turns the led file's speed value — <see cref="LayerLightingState.Speed"/>,
    /// <see cref="LayerLightingState.MinimumSpeed"/> 1 … <see cref="LayerLightingState.MaximumSpeed"/> 9
    /// (specs/07-lighting.md §2.1) — into the period of one preview cycle.
    /// <para>
    /// <b>The curve.</b> Geometric, anchored at <see cref="SlowestPeriodSeconds"/> = 3.6 s for
    /// speed 1 and <see cref="FastestPeriodSeconds"/> = 0.4 s for speed 9, so each step of the
    /// knob is the same multiplier (9<sup>-1/8</sup> ≈ 0.76×, i.e. about 32% faster per step)
    /// rather than the same number of milliseconds — a linear ramp would make the fast half of
    /// the knob feel dead. The default speed 5 lands exactly in the geometric middle at 1.2 s,
    /// which is a legible breathing rate and a wave that crosses the board in a bit over a second.
    /// </para>
    /// <para>
    /// The numbers are a <i>reading</i> of the hardware, not a measurement of it: specs/ documents
    /// no firmware timings (see <see cref="LightingEffectSampler"/>). What they must get right is
    /// that 1 is slow, 9 is fast, and every step in between is felt.
    /// </para>
    /// </summary>
    public static class LightingSpeedScale
    {
        /// <summary>Seconds per cycle at <see cref="LayerLightingState.MinimumSpeed"/>.</summary>
        public const double SlowestPeriodSeconds = 3.6;

        /// <summary>Seconds per cycle at <see cref="LayerLightingState.MaximumSpeed"/>.</summary>
        public const double FastestPeriodSeconds = 0.4;

        /// <summary>
        /// Seconds one preview cycle takes at <paramref name="speed"/>. Values outside 1..9 are
        /// clamped to the bounds <see cref="LayerLightingState"/> itself declares.
        /// </summary>
        public static double PeriodSecondsFor(int speed)
        {
            var clamped = Math.Clamp(speed, LayerLightingState.MinimumSpeed, LayerLightingState.MaximumSpeed);
            var steps = (double)(clamped - LayerLightingState.MinimumSpeed)
                / (LayerLightingState.MaximumSpeed - LayerLightingState.MinimumSpeed);

            return SlowestPeriodSeconds * Math.Pow(FastestPeriodSeconds / SlowestPeriodSeconds, steps);
        }

        /// <summary>One preview cycle at <paramref name="speed"/>, as a <see cref="TimeSpan"/>.</summary>
        public static TimeSpan PeriodFor(int speed)
        {
            return TimeSpan.FromSeconds(PeriodSecondsFor(speed));
        }

        /// <summary>How many preview cycles a second at <paramref name="speed"/>.</summary>
        public static double CyclesPerSecond(int speed)
        {
            return 1.0 / PeriodSecondsFor(speed);
        }
    }
}
