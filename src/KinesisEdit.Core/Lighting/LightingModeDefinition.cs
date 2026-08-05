using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// One row of the mode catalog (specs/07-lighting.md §2.2, §2.3, §3): a mode's file tokens,
    /// line-shape flags, per-context direction rules, menu availability, and firmware gate.
    /// All instances live in <see cref="LightingModeCatalog"/>.
    /// </summary>
    public sealed record LightingModeDefinition
    {
        /// <summary>The mode this row describes.</summary>
        public required LightingMode Mode { get; init; }

        /// <summary>
        /// The mode's UI caption, quoted from the "Mode (UI name)" column of
        /// specs/07-lighting.md §3 (and its "UI captions (RGB app)" list) — e.g. "Disable" for
        /// <see cref="LightingMode.Disabled"/> and "Starlight" for the <c>[star]</c> token.
        /// </summary>
        public required string DisplayName { get; init; }

        /// <summary>Key-backlight file token without brackets (§2.2); null when the mode has none.</summary>
        public string? KeyBacklightToken { get; init; }

        /// <summary>Edge-lighting file token without brackets (§2.3); null when not an edge mode.</summary>
        public string? EdgeToken { get; init; }

        /// <summary>
        /// Whether the token is reserved: <c>[black]</c> is neither written nor read by the
        /// RGB/TKO apps (§2.2), so mode detection never matches it.
        /// </summary>
        public bool IsReserved { get; init; }

        /// <summary>Whether the mode line carries a <c>[spdN]</c> token (§3 speed column).</summary>
        public bool WritesSpeed { get; init; }

        /// <summary>Whether the mode line carries an effect color (Monochrome and the two-line effects).</summary>
        public bool WritesEffectColor { get; init; }

        /// <summary>
        /// Whether the mode writes a base-color <c>[mono]</c>/<c>[mono_edge]</c> line before the
        /// effect line (the two-line effects, §2.2).
        /// </summary>
        public bool HasBaseMonoLine { get; init; }

        /// <summary>Whether the mode's body is per-key/per-LED color lines (Freestyle, Breathe, Frozen Wave).</summary>
        public bool HasPerKeyColors { get; init; }

        /// <summary>
        /// Valid directions in the key-backlight context (§2.4, §3); empty when the mode writes
        /// no direction token there. Invalid parsed values fall back to left.
        /// </summary>
        public IReadOnlyList<LightingDirection> KeyBacklightDirections { get; init; } = [];

        /// <summary>
        /// Valid directions in the TKO edge context (§2.3); empty when the mode writes no
        /// direction token there (Rebound writes none on the edge).
        /// </summary>
        public IReadOnlyList<LightingDirection> EdgeDirections { get; init; } = [];

        /// <summary>Whether the RGB key-backlight mode menu offers the mode (§3).</summary>
        public bool OffersInRgbKeyBacklightMenu { get; init; }

        /// <summary>Whether the TKO key-backlight mode menu offers the mode (§3).</summary>
        public bool OffersInTkoKeyBacklightMenu { get; init; }

        /// <summary>Whether the TKO edge mode menu offers the mode (§3).</summary>
        public bool OffersInTkoEdgeMenu { get; init; }

        /// <summary>
        /// The firmware feature gating the mode (Ripple/Fireball on the RGB, §3), evaluated via
        /// <see cref="FirmwareGateService"/>; <see cref="FirmwareFeature.None"/> when ungated.
        /// Devices without a catalog gate row (TKO) pass automatically.
        /// </summary>
        public FirmwareFeature GatedByFeature { get; init; } = FirmwareFeature.None;
    }
}
