namespace KinesisEdit.Core.Devices
{
    /// <summary>
    /// Tap-and-hold support and limits per specs/11-feature-dialogs.md §11.1 and the limits table
    /// of specs/04-layout-file-format.md §5.3: at most 10 tap-and-hold keys per layout, a timing
    /// delay of 1-999 ms defaulting to 250 ms (150 ms on the Advantage 360), and the per-app
    /// firmware minimum. Firmware minimums are carried as data only; gate evaluation belongs to
    /// specs/09-firmware.md.
    /// </summary>
    public sealed record TapAndHoldCapability
    {
        /// <summary>A capability for devices without tap-and-hold support (SE2 pedal, CROSSFIRE, Adv360 Professional).</summary>
        public static TapAndHoldCapability None { get; } = new();

        /// <summary>Whether the device supports tap-and-hold assignments.</summary>
        public bool IsSupported { get; init; }

        /// <summary>Tap-and-hold keys allowed per layout/profile (10 on every supporting device; 04 §5.3, 11 §11.1).</summary>
        public int? MaxPerLayout { get; init; }

        /// <summary>
        /// Timing-delay range in milliseconds and the delay applied to a key that has none yet
        /// (1-999, default 250; Adv360 defaults to 150; 11 §11.1).
        /// </summary>
        public ValueRange? DelayMilliseconds { get; init; }

        /// <summary>Delay in milliseconds applied to a key with no delay yet (250; Adv360: 150; 11 §11.1).</summary>
        public int? DefaultDelayMilliseconds => DelayMilliseconds?.Default;

        /// <summary>
        /// Minimum keyboard firmware for the feature (Adv2 1.0.516, FS Edge/Pro 1.0.480, FS Edge RGB
        /// 1.0.1; 11 §11.1, 09 §2). Null where the spec lists no gate (TKO, Adv360). Data only, not
        /// evaluated here.
        /// </summary>
        public FirmwareVersion? MinimumFirmware { get; init; }
    }
}
