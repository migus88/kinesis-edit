using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// Which parameter panels one lighting mode shows (specs/07-lighting.md §3, "Which parameter
    /// panels each mode shows"). Every answer is derived from the mode catalog and
    /// <see cref="LightingAvailability"/> rather than from a list of mode names, so a mode row
    /// gaining a speed or a direction is picked up here for free.
    /// <para>
    /// It is a value, recomputed whenever the mode or the layer changes, which is what keeps
    /// <see cref="LightingTabViewModel"/> free of seven parallel boolean properties.
    /// </para>
    /// </summary>
    public sealed record LightingPanelVisibility
    {
        /// <summary>Nothing shown — the shape of <see cref="LightingMode.Disabled"/> and of "no layer yet".</summary>
        public static LightingPanelVisibility None { get; } = new();

        /// <summary>
        /// The panels <paramref name="mode"/> shows on <paramref name="deviceId"/>.
        /// <paramref name="isLayerCustomizationAvailable"/> is the
        /// <see cref="Core.Firmware.FirmwareFeature.LightingLayerCustomization"/> verdict: §3's
        /// base-color row is "on RGB only shown when LED firmware ≥ 1.0.44", the same gate the Fn
        /// layer sits behind.
        /// </summary>
        public static LightingPanelVisibility For(
            DeviceId deviceId,
            LightingMode mode,
            bool isLayerCustomizationAvailable)
        {
            var definition = LightingModeCatalog.Find(mode);

            return new LightingPanelVisibility
            {
                // §3's effect-color row is the eight modes that write one into the file plus the
                // two per-key modes, whose "effect color" is the color a click paints with (§4).
                ShowsEffectColor = definition.WritesEffectColor || definition.HasPerKeyColors,
                ShowsBaseColor = definition.HasBaseMonoLine && isLayerCustomizationAvailable,
                ShowsSpeed = definition.WritesSpeed,

                // Device-aware on purpose: Fireball carries a direction in the file but has no
                // direction panel on the RGB (docs/app/lighting.md).
                ShowsDirection = LightingAvailability.GetKeyBacklightDirections(deviceId, mode).Count > 0,
                ShowsPerKeyColors = definition.HasPerKeyColors
            };
        }

        /// <summary>Whether the effect-color swatch is shown.</summary>
        public bool ShowsEffectColor { get; init; }

        /// <summary>Whether the base-color swatch is shown (two-line effects, firmware-gated).</summary>
        public bool ShowsBaseColor { get; init; }

        /// <summary>Whether the 1-9 speed control is shown.</summary>
        public bool ShowsSpeed { get; init; }

        /// <summary>Whether the direction control is shown.</summary>
        public bool ShowsDirection { get; init; }

        /// <summary>Whether the keyboard picture is offered for per-key coloring (Freestyle, Breathe).</summary>
        public bool ShowsPerKeyColors { get; init; }

        /// <summary>Whether the zone buttons are shown — §3: "Freestyle and Breathe only".</summary>
        public bool ShowsZones => ShowsPerKeyColors;

        /// <summary>Whether "Reset All" is offered — §4: "visible only for Freestyle/Breathe".</summary>
        public bool ShowsResetAll => ShowsPerKeyColors;

        /// <summary>Whether any color swatch is shown, i.e. whether the picker has a target.</summary>
        public bool ShowsAnyColor => ShowsEffectColor || ShowsBaseColor;
    }
}
