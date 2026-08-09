namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// The one place that turns the led file's speed value — <see cref="LayerLightingState.Speed"/>,
    /// <see cref="LayerLightingState.MinimumSpeed"/> 1 … <see cref="LayerLightingState.MaximumSpeed"/> 9
    /// (specs/07-lighting.md §2.1) — into the period of one preview cycle.
    /// <para>
    /// <b>The anchor is a measurement of the hardware.</b> On a Freestyle Edge RGB, Rebound at the
    /// default speed 5 takes <b>4.0 s</b> to carry its bar edge to edge across the whole board —
    /// both halves and the gutter. Rebound's travel is a triangle wave
    /// (<c>Effects.ReboundEffect</c>), so one edge-to-edge pass is <i>half</i> a cycle: the period
    /// at speed 5 is <b>8.0 s</b>. Everything below is pinned to that one number, because it is the
    /// only one there is — specs/ documents the led file format exhaustively and the firmware's
    /// effect timings not at all (see <see cref="LightingEffectSampler"/>).
    /// </para>
    /// <para>
    /// <b>The curve.</b> Geometric, anchored at <see cref="SlowestPeriodSeconds"/> = 24 s for
    /// speed 1 and <see cref="FastestPeriodSeconds"/> = 24/9 s ≈ 2.67 s for speed 9, so each step
    /// of the knob is the same multiplier (9<sup>-1/8</sup> ≈ 0.76×, i.e. about 32% faster per
    /// step) rather than the same number of milliseconds — a linear ramp would make the fast half
    /// of the knob feel dead. Speed 5 sits in the geometric middle of that 9:1 span, which is
    /// 24 × (1/9)<sup>1/2</sup> = exactly 8.0 s: the measurement above. Change one anchor and the
    /// other has to move with it, or speed 5 stops matching the hardware.
    /// </para>
    /// <para>
    /// <b>The accepted caveat: a "cycle" does not mean the same thing in every mode.</b> For
    /// Rebound it is a <i>round trip</i> — out and back — while for Wave, Loop, Fireball and Rain
    /// it is a <i>single sweep</i> across the board. Calibrating one shared clock on a round trip
    /// therefore leaves those modes running roughly 2× slower than the hardware, and that is
    /// knowingly accepted. A per-mode cycle-length factor was considered and <b>deliberately
    /// rejected</b>: there is a single measured data point, so a table of per-mode divisors derived
    /// from it would be a guess presented as hardware behaviour — exactly what
    /// <see cref="LightingEffectSampler"/>'s "representative approximations, not simulations" rule
    /// forbids. Measure the sweeping modes and the factor can arrive with evidence behind it.
    /// </para>
    /// </summary>
    public static class LightingSpeedScale
    {
        /// <summary>
        /// How much slower <see cref="LayerLightingState.MinimumSpeed"/> is than
        /// <see cref="LayerLightingState.MaximumSpeed"/>. 9:1 is what puts the measured 8.0 s
        /// exactly on the middle of the knob.
        /// </summary>
        public const double SpeedSpanRatio = 9.0;

        /// <summary>Seconds per cycle at <see cref="LayerLightingState.MinimumSpeed"/>.</summary>
        public const double SlowestPeriodSeconds = 24.0;

        /// <summary>Seconds per cycle at <see cref="LayerLightingState.MaximumSpeed"/>.</summary>
        public const double FastestPeriodSeconds = SlowestPeriodSeconds / SpeedSpanRatio;

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
