namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// The one place that turns the led file's speed value — <see cref="LayerLightingState.Speed"/>,
    /// <see cref="LayerLightingState.MinimumSpeed"/> 1 … <see cref="LayerLightingState.MaximumSpeed"/> 9
    /// (specs/07-lighting.md §2.1) — into the period of one preview cycle.
    /// <para>
    /// <b>Every number here is a measurement of the hardware.</b> On a Freestyle Edge RGB, Rebound
    /// carries its bar edge to edge across the whole board — both halves and the gutter — in
    /// <b>5 s at speed 1, 3 s at speed 5 and 1 s at speed 9</b>. Rebound's travel is a triangle wave
    /// (<c>Effects.ReboundEffect</c>), so one edge-to-edge pass is <i>half</i> a cycle and the
    /// periods those three readings imply are <b>10 s, 6 s and 2 s</b>. specs/ documents the led
    /// file format exhaustively and the firmware's effect timings not at all (see
    /// <see cref="LightingEffectSampler"/>), so measurement is the only source there is.
    /// </para>
    /// <para>
    /// <b>The curve is linear, and the measurements are what decide that.</b>
    /// <c>period = 11 − speed</c> passes through all three readings exactly. This replaced a
    /// geometric ramp between the same end points: geometric was defensible while speed 5 was the
    /// only reading — a same-multiplier-per-step knob keeps its fast half from feeling dead — but a
    /// geometric curve anchored at 10 s and 2 s puts speed 5 at 4.47 s, and the board says 6 s. Three
    /// points settle the shape of the curve as well as its ends, and an argument from feel does not
    /// outrank them. Move any one anchor and the others have to be re-measured with it.
    /// </para>
    /// <para>
    /// <b>The accepted caveat: a "cycle" does not mean the same thing in every mode.</b> For
    /// Rebound it is a <i>round trip</i> — out and back — while for Wave, Loop, Fireball and Rain
    /// it is a <i>single sweep</i> across the board. Calibrating one shared clock on a round trip
    /// therefore leaves those modes running roughly 2× slower than the hardware, and that is
    /// knowingly accepted. A per-mode cycle-length factor was considered and <b>deliberately
    /// rejected</b>: every reading there is was taken on Rebound, so a table of per-mode divisors
    /// derived from it would be a guess presented as hardware behaviour — exactly what
    /// <see cref="LightingEffectSampler"/>'s "representative approximations, not simulations" rule
    /// forbids. Measure the sweeping modes and the factor can arrive with evidence behind it.
    /// </para>
    /// </summary>
    public static class LightingSpeedScale
    {
        /// <summary>
        /// Seconds per cycle at <see cref="LayerLightingState.MinimumSpeed"/> — twice the measured
        /// 5 s edge-to-edge traverse.
        /// </summary>
        public const double SlowestPeriodSeconds = 10.0;

        /// <summary>
        /// Seconds per cycle at <see cref="LayerLightingState.MaximumSpeed"/> — twice the measured
        /// 1 s edge-to-edge traverse.
        /// </summary>
        public const double FastestPeriodSeconds = 2.0;

        /// <summary>
        /// Seconds one preview cycle takes at <paramref name="speed"/>. Values outside 1..9 are
        /// clamped to the bounds <see cref="LayerLightingState"/> itself declares.
        /// </summary>
        public static double PeriodSecondsFor(int speed)
        {
            var clamped = Math.Clamp(speed, LayerLightingState.MinimumSpeed, LayerLightingState.MaximumSpeed);
            var steps = (double)(clamped - LayerLightingState.MinimumSpeed)
                / (LayerLightingState.MaximumSpeed - LayerLightingState.MinimumSpeed);

            return SlowestPeriodSeconds + ((FastestPeriodSeconds - SlowestPeriodSeconds) * steps);
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
