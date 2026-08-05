using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Immutable result of parsing a device version file (specs/09-firmware.md §1). Raw value
    /// texts are kept alongside the parsed versions because parsing keeps only the
    /// major/minor/revision tokens, so only the raw text carries the full value the file
    /// stated, e.g. "1.0.1709.us (4MB), 03/08/2019". Produced by
    /// <see cref="VersionFileParser"/>; carries data plus the spec 02 model resolution only.
    /// </summary>
    public sealed record VersionFileInfo
    {
        private const string FreestyleProModelName = "FS PRO";

        /// <summary>
        /// The result for empty input and for devices without version data
        /// (CROSSFIRE keypad, Advantage 360 Professional).
        /// </summary>
        public static VersionFileInfo Empty { get; } = new();

        /// <summary>Raw model name from the model line (e.g. "FS Edge RGB"); empty when absent.</summary>
        public string ModelName { get; init; } = string.Empty;

        /// <summary>Parsed keyboard firmware version; null when absent or unparseable.</summary>
        public FirmwareVersion? KeyboardFirmware { get; init; }

        /// <summary>Raw keyboard firmware text after the prefix (e.g. "1.0.1709.us (4MB), 03/08/2019"); empty when absent.</summary>
        public string KeyboardFirmwareText { get; init; } = string.Empty;

        /// <summary>Parsed LED firmware version (RGB/TKO only); null when absent or unparseable.</summary>
        public FirmwareVersion? LedFirmware { get; init; }

        /// <summary>Raw LED firmware text after the prefix (RGB/TKO only); empty when absent.</summary>
        public string LedFirmwareText { get; init; } = string.Empty;

        /// <summary>
        /// Whether any line contains the literal marker "4MB" (specs/09-firmware.md §1.1). Drives
        /// Advantage2 settings-editing enablement; absence means the app treats the board as 2MB.
        /// </summary>
        public bool HasFourMegabyteMarker { get; init; }

        /// <summary>
        /// Resolves the Freestyle model actually connected, per specs/02-devices.md: a model
        /// name of "FS PRO" (case-insensitive) selects the Freestyle Pro; anything else —
        /// including an empty model name — selects the Freestyle Edge.
        /// </summary>
        public DeviceId ResolveFreestyleModel()
        {
            var isFreestylePro = string.Equals(ModelName.Trim(), FreestyleProModelName, StringComparison.OrdinalIgnoreCase);

            return isFreestylePro ? DeviceId.FreestylePro : DeviceId.FreestyleEdge;
        }
    }
}
