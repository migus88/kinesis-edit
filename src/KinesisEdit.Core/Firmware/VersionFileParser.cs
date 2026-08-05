using System.Text.RegularExpressions;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Parses the text lines of a device version file into a <see cref="VersionFileInfo"/> per
    /// specs/09-firmware.md §1.1: each line is prefix-matched case-insensitively against the
    /// device's line prefixes, exactly one separator character after the prefix is skipped
    /// (the ":" or "="), and the trimmed remainder is the value. The Savant Elite 2 file is
    /// free text (specs/12-savant-elite.md §1) and is scanned for the first dotted numeric
    /// token instead. Pure text-in/data-out — file I/O lives in drive discovery, not here.
    /// </summary>
    public static partial class VersionFileParser
    {
        private const string FourMegabyteMarker = "4MB";
        private const string ModelNamePrefix = "model name";
        private const string FirmwareVersionPrefix = "firmware version";
        private const string KeyboardFirmwarePrefix = "kbd firmware";
        private const string LedFirmwarePrefix = "led firmware";
        private const string Advantage360ModelPrefix = "model";
        private const string Advantage360KeyboardFirmwarePrefix = "kbd_fw_r";
        private const int SeparatorLength = 1;

        /// <summary>
        /// Parses <paramref name="lines"/> using the prefix set of <paramref name="deviceId"/>
        /// (specs/09-firmware.md §1). Devices without version data (CROSSFIRE keypad,
        /// Advantage 360 Professional) and unknown ids return <see cref="VersionFileInfo.Empty"/>.
        /// </summary>
        public static VersionFileInfo Parse(DeviceId deviceId, IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            return deviceId switch
            {
                DeviceId.FreestyleEdge or DeviceId.FreestylePro or DeviceId.Advantage2 =>
                    ParseWithPrefixes(lines, ModelNamePrefix, FirmwareVersionPrefix, ledFirmwarePrefix: null),
                DeviceId.FreestyleEdgeRgb or DeviceId.Tko =>
                    ParseWithPrefixes(lines, ModelNamePrefix, KeyboardFirmwarePrefix, LedFirmwarePrefix),
                DeviceId.Advantage360 =>
                    ParseWithPrefixes(lines, Advantage360ModelPrefix, Advantage360KeyboardFirmwarePrefix, ledFirmwarePrefix: null),
                DeviceId.SavantElite2 => ParseFreeText(lines),
                _ => VersionFileInfo.Empty
            };
        }

        private static VersionFileInfo ParseWithPrefixes(
            IEnumerable<string> lines,
            string modelPrefix,
            string keyboardFirmwarePrefix,
            string? ledFirmwarePrefix)
        {
            var modelName = string.Empty;
            var keyboardFirmwareText = string.Empty;
            var ledFirmwareText = string.Empty;
            var hasFourMegabyteMarker = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                hasFourMegabyteMarker |= ContainsFourMegabyteMarker(line);

                if (TryReadPrefixedValue(line, modelPrefix, out var modelValue))
                {
                    modelName = modelValue;
                    continue;
                }

                if (TryReadPrefixedValue(line, keyboardFirmwarePrefix, out var keyboardValue))
                {
                    keyboardFirmwareText = keyboardValue;
                    continue;
                }

                if (ledFirmwarePrefix is not null && TryReadPrefixedValue(line, ledFirmwarePrefix, out var ledValue))
                {
                    ledFirmwareText = ledValue;
                }
            }

            return new VersionFileInfo
            {
                ModelName = modelName,
                KeyboardFirmware = ParseVersion(keyboardFirmwareText),
                KeyboardFirmwareText = keyboardFirmwareText,
                LedFirmware = ParseVersion(ledFirmwareText),
                LedFirmwareText = ledFirmwareText,
                HasFourMegabyteMarker = hasFourMegabyteMarker
            };
        }

        private static VersionFileInfo ParseFreeText(IEnumerable<string> lines)
        {
            FirmwareVersion? keyboardFirmware = null;
            var keyboardFirmwareText = string.Empty;
            var hasFourMegabyteMarker = false;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                hasFourMegabyteMarker |= ContainsFourMegabyteMarker(line);

                if (keyboardFirmware is not null)
                {
                    continue;
                }

                var match = DottedVersionRegex().Match(line);

                if (match.Success && FirmwareVersion.TryParse(match.Value, out var version))
                {
                    keyboardFirmware = version;
                    keyboardFirmwareText = match.Value;
                }
            }

            return new VersionFileInfo
            {
                KeyboardFirmware = keyboardFirmware,
                KeyboardFirmwareText = keyboardFirmwareText,
                HasFourMegabyteMarker = hasFourMegabyteMarker
            };
        }

        private static bool TryReadPrefixedValue(string line, string prefix, out string value)
        {
            value = string.Empty;
            var trimmedLine = line.TrimStart();

            if (!trimmedLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var valueStart = prefix.Length + SeparatorLength;

            if (valueStart < trimmedLine.Length)
            {
                value = trimmedLine[valueStart..].Trim();
            }

            return true;
        }

        private static bool ContainsFourMegabyteMarker(string line)
        {
            return line.Contains(FourMegabyteMarker, StringComparison.OrdinalIgnoreCase);
        }

        private static FirmwareVersion? ParseVersion(string text)
        {
            return FirmwareVersion.TryParse(text, out var version) ? version : null;
        }

        [GeneratedRegex(@"\d+(\.\d+)+")]
        private static partial Regex DottedVersionRegex();
    }
}
