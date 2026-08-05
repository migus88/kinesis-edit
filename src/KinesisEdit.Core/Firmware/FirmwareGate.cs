using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// One row of the minimum-firmware gate table (specs/09-firmware.md §2): the requirements a
    /// device's firmware must meet before <see cref="Feature"/> is available. Requirement
    /// properties left null (or empty) are not part of the rule; every declared requirement must
    /// hold. All instances live in <see cref="FirmwareGateCatalog"/>; evaluation belongs to
    /// <see cref="FirmwareGateService"/>.
    /// </summary>
    public sealed record FirmwareGate
    {
        /// <summary>The device the gate applies to.</summary>
        public required DeviceId Device { get; init; }

        /// <summary>The feature the gate unlocks.</summary>
        public required FirmwareFeature Feature { get; init; }

        /// <summary>Minimum keyboard firmware (inclusive); null when the rule has no keyboard minimum.</summary>
        public FirmwareVersion? MinimumKeyboardFirmware { get; init; }

        /// <summary>Minimum LED firmware (inclusive); null when the rule has no LED minimum.</summary>
        public FirmwareVersion? MinimumLedFirmware { get; init; }

        /// <summary>Keyboard firmware the rule requires exactly (TKO 1.0.0 warning); null otherwise.</summary>
        public FirmwareVersion? ExactKeyboardFirmware { get; init; }

        /// <summary>LED firmware versions of which exactly one must match (RGB expansion-pack offer); empty otherwise.</summary>
        public IReadOnlyList<FirmwareVersion> ExactLedFirmwareVersions { get; init; } = [];

        /// <summary>
        /// Dialog text the legacy app shows for the gate — the refusal prompt of a
        /// below-minimum gate, or the startup warning of the TKO exact-match gate — where
        /// specs 09 §2 / 11 §11.1 give the wording; null elsewhere.
        /// </summary>
        public string? Message { get; init; }
    }
}
