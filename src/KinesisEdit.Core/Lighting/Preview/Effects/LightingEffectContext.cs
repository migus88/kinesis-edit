namespace KinesisEdit.Core.Lighting.Preview.Effects
{
    /// <summary>
    /// Everything an effect needs about the instant being sampled, resolved once per frame by
    /// <see cref="LightingEffectSampler"/> so no effect recomputes it per key.
    /// </summary>
    /// <param name="State">The layer being previewed; effects read its colours and per-key paint.</param>
    /// <param name="Direction">
    /// The travel direction the mode actually uses: <see cref="LightingDirection.None"/> when the
    /// mode has none, otherwise the layer's direction, or left when the layer holds a value the
    /// mode does not accept (the same left fallback specs/07-lighting.md §2.4 uses on read).
    /// </param>
    /// <param name="Phase">Where in the current cycle the instant falls, 0..1.</param>
    /// <param name="CycleIndex">How many complete cycles have elapsed; the seed for per-cycle randomness.</param>
    /// <param name="ElapsedSeconds">The raw elapsed time, for effects that need it unquantised.</param>
    internal readonly record struct LightingEffectContext(
        LayerLightingState State,
        LightingDirection Direction,
        double Phase,
        long CycleIndex,
        double ElapsedSeconds);
}
