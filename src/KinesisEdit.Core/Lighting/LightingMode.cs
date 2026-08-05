namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The full LED mode/effect set of the lighting system (specs/07-lighting.md §1.5, §3).
    /// Token and per-context availability data for each mode lives in
    /// <see cref="LightingModeCatalog"/>.
    /// </summary>
    public enum LightingMode
    {
        /// <summary>Lighting off; nothing is written for the layer (specs/07-lighting.md §2.2).</summary>
        Disabled = 0,

        /// <summary>Per-key coloring; the fallback interpretation for bare per-key files (§2.4).</summary>
        Freestyle = 1,

        /// <summary>Single color across the board, token <c>[mono]</c>.</summary>
        Monochrome = 2,

        /// <summary>Fading per-key colors, token <c>[breathe]</c>.</summary>
        Breathe = 3,

        /// <summary>Board-wide hue cycle, token <c>[spectrum]</c>.</summary>
        Spectrum = 4,

        /// <summary>Scrolling rainbow, token <c>[wave]</c>.</summary>
        Wave = 5,

        /// <summary>Static per-LED rainbow, edge-only token <c>[frozenwave_edge]</c> (§2.3).</summary>
        FrozenWave = 6,

        /// <summary>Keys light on key-press, token <c>[reactive]</c>.</summary>
        Reactive = 7,

        /// <summary>Expanding rings, token <c>[ripple]</c>; firmware-gated on the RGB (§3).</summary>
        Ripple = 8,

        /// <summary>A shot across a row, token <c>[fireball]</c>; firmware-gated on the RGB (§3).</summary>
        Fireball = 9,

        /// <summary>Twinkling random keys, token <c>[star]</c> — not <c>[starlight]</c>.</summary>
        Starlight = 10,

        /// <summary>Bouncing bar, token <c>[rebound]</c>.</summary>
        Rebound = 11,

        /// <summary>Directional sweep, token <c>[loop]</c>.</summary>
        Loop = 12,

        /// <summary>Whole-board fade, token <c>[pulse]</c>.</summary>
        Pulse = 13,

        /// <summary>Random falling columns, token <c>[rain]</c>.</summary>
        Rain = 14,

        /// <summary>
        /// Reserved token <c>[black]</c> — the legacy RGB/TKO apps neither write nor read it
        /// (specs/07-lighting.md §2.2); the engine serializes it as nothing.
        /// </summary>
        PitchBlack = 15
    }
}
