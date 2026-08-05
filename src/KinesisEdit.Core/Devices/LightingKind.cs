namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// The kind of lighting hardware a device carries, per the master device table
    /// in specs/02-devices.md.
    /// </summary>
    public enum LightingKind
    {
        /// <summary>No lighting (SE2, Advantage2, Freestyle Pro).</summary>
        None = 0,

        /// <summary>Single-color blue backlight with brightness levels (Freestyle Edge).</summary>
        BlueBacklight = 1,

        /// <summary>Per-key RGB with effect modes (Freestyle Edge RGB, TKO).</summary>
        PerKeyRgb = 2,

        /// <summary>RGB indicator LEDs mappable to status functions, not per-key (Advantage 360).</summary>
        IndicatorLeds = 3
    }
}
