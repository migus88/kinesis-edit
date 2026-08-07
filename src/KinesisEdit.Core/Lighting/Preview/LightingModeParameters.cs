using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Lighting.Preview
{
    /// <summary>
    /// What one lighting mode accepts on one device in one firmware state — the descriptor the
    /// mode rail draws a row from, and the same answer the parameter controls below the board use
    /// to decide what to show.
    /// <para>
    /// <b>Every fact is derived</b> from <see cref="LightingModeCatalog"/> and
    /// <see cref="LightingAvailability"/>, i.e. from the file format and the firmware gates, never
    /// from the design's per-mode parameter list. Where the two disagree the catalog wins: mockup
    /// 2f gives Wave left/right where specs/07-lighting.md §3 gives the RGB all four, drops the
    /// direction from Loop, which has all four, and gives Rain up/down, which has none.
    /// </para>
    /// </summary>
    public sealed record LightingModeParameters
    {
        /// <summary>The <see cref="Summary"/> of a mode that takes no parameters at all.</summary>
        public const string NoParametersSummary = "—";

        /// <summary>Nothing accepted — the shape of "no layer selected yet".</summary>
        public static LightingModeParameters None { get; } = new()
        {
            Mode = LightingMode.Disabled,
            Summary = NoParametersSummary
        };

        /// <summary>
        /// The parameters <paramref name="mode"/> accepts on <paramref name="deviceId"/>.
        /// <paramref name="isLayerCustomizationAvailable"/> is the
        /// <see cref="Core.Firmware.FirmwareFeature.LightingLayerCustomization"/> verdict — §3's
        /// base-color row is "on RGB only shown when LED firmware ≥ 1.0.44", the same gate the Fn
        /// layer sits behind — so a board below the gate reports no base colour.
        /// </summary>
        public static LightingModeParameters For(
            DeviceId deviceId,
            LightingMode mode,
            bool isLayerCustomizationAvailable)
        {
            var definition = LightingModeCatalog.Find(mode);

            // §3's effect-color row is the modes that write one into the file plus the per-key
            // modes, whose "effect color" is the color a click paints with (§4).
            var acceptsEffectColor = definition.WritesEffectColor || definition.HasPerKeyColors;
            var acceptsBaseColor = definition.HasBaseMonoLine && isLayerCustomizationAvailable;

            // Device-aware on purpose: Fireball carries a direction in the file but has no
            // direction panel on the RGB (docs/app/lighting.md).
            var directions = LightingAvailability.GetKeyBacklightDirections(deviceId, mode);

            return new LightingModeParameters
            {
                Mode = mode,
                AcceptsEffectColor = acceptsEffectColor,
                AcceptsBaseColor = acceptsBaseColor,
                AcceptsSpeed = definition.WritesSpeed,
                HasPerKeyColors = definition.HasPerKeyColors,
                Directions = directions,
                RendersPaintDirectly = LightingPaintModes.RendersPaintDirectly(mode),
                Summary = LightingModeSummaryBuilder.Build(
                    acceptsEffectColor,
                    acceptsBaseColor,
                    definition.WritesSpeed,
                    directions)
            };
        }

        /// <summary>The mode this descriptor answers for.</summary>
        public required LightingMode Mode { get; init; }

        /// <summary>Whether an effect colour applies — the colour written to the file, or painted with (§4).</summary>
        public bool AcceptsEffectColor { get; init; }

        /// <summary>Whether a base colour applies: a two-line effect, and the firmware gate open.</summary>
        public bool AcceptsBaseColor { get; init; }

        /// <summary>Whether the 1-9 speed knob applies.</summary>
        public bool AcceptsSpeed { get; init; }

        /// <summary>
        /// Whether the mode's body is per-key colour lines — which is also §3's rule for the zone
        /// buttons ("Freestyle and Breathe only") and §4's for "Reset All".
        /// </summary>
        public bool HasPerKeyColors { get; init; }

        /// <summary>
        /// The directions the mode offers on this device, in catalog order; empty when it takes
        /// none. Always equal to <see cref="LightingAvailability.GetKeyBacklightDirections"/>.
        /// </summary>
        public IReadOnlyList<LightingDirection> Directions { get; init; } = [];

        /// <summary>
        /// Whether the preview draws the layer's per-key painted colours rather than ignoring
        /// them — <see cref="LightingPaintModes.RenderedDirectly"/>.
        /// </summary>
        public bool RendersPaintDirectly { get; init; }

        /// <summary>
        /// The mode rail's second line: a compact caption of everything above, in the design's
        /// grammar — colours first, then speed, then directions, joined with " · ".
        /// <para>
        /// The colour fragment is "2 colors" when an effect <i>and</i> a base colour apply,
        /// "color" when one does, and is omitted when neither does; the speed fragment is "spd";
        /// the direction fragment is one letter per entry of <see cref="Directions"/> —
        /// D / L / U / R, in that list's order — joined with "/", so a mode with all four reads
        /// "D/L/U/R" and Rebound's horizontal/vertical pair reads "L/U". A mode that accepts
        /// nothing reads <see cref="NoParametersSummary"/>. Letters rather than arrows: the rail
        /// draws the arrows as icons from <see cref="Directions"/> itself, so this line's only job
        /// is to be an accurate, compact caption.
        /// </para>
        /// </summary>
        public required string Summary { get; init; }

        /// <summary>Whether any colour swatch applies, i.e. whether the colour picker has a target.</summary>
        public bool AcceptsAnyColor => AcceptsEffectColor || AcceptsBaseColor;

        /// <summary>Whether the direction control applies at all.</summary>
        public bool AcceptsDirection => Directions.Count > 0;
    }
}
