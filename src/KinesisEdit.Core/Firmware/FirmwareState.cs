using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The firmware knowledge a gate query runs against: keyboard and LED versions (null when
    /// unknown or not applicable) plus the demo-mode flag of specs/03-vdrive-and-files.md §3.5.
    /// Demo mode makes every gate pass (specs/09-firmware.md §2).
    /// </summary>
    public readonly record struct FirmwareState
    {
        /// <summary>Creates a state from a parsed version file, carrying its keyboard and LED versions.</summary>
        public static FirmwareState FromVersionFile(VersionFileInfo versionFile, bool isDemoMode = false)
        {
            ArgumentNullException.ThrowIfNull(versionFile);

            return new FirmwareState
            {
                KeyboardFirmware = versionFile.KeyboardFirmware,
                LedFirmware = versionFile.LedFirmware,
                IsDemoMode = isDemoMode
            };
        }

        /// <summary>Keyboard firmware version; null when unknown.</summary>
        public FirmwareVersion? KeyboardFirmware { get; init; }

        /// <summary>LED firmware version (RGB/TKO); null when unknown or not applicable.</summary>
        public FirmwareVersion? LedFirmware { get; init; }

        /// <summary>Whether the app runs in demo mode (no connected, writable v-Drive).</summary>
        public bool IsDemoMode { get; init; }
    }
}
