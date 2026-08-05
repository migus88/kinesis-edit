using System.Globalization;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Parses keyboard-settings file lines into a <see cref="KeyboardSettings"/> per
    /// specs/08-settings.md §2. Parsing is common to all devices and always case-insensitive
    /// (spec 08 §1-§2): every known key is read tolerantly regardless of which device wrote
    /// the file; unparseable values — including numbers outside the
    /// <see cref="SettingsValueRanges"/> of spec 08 §2 — yield null, never an error, so a save
    /// skips the key and the original line survives verbatim. Pure text-in/data-out — file
    /// I/O lives in <c>SettingsService</c>, not here.
    /// </summary>
    public static class KeyboardSettingsParser
    {
        private const string OnValue = "on";
        private const string AutoValue = "auto";
        private const string TrueValue = "true";

        /// <summary>
        /// Parses <paramref name="lines"/> into a typed model. Missing keys stay null; the
        /// startup profile number is taken from <c>profile</c> when present, else from
        /// <c>startup_file</c>, both via the shared file-number logic (spec 08 §2); the status
        /// play speed is taken from <c>status_play_speed</c> when present, else from the
        /// Advantage 360 short key <c>status</c> (spec 08 §2).
        /// </summary>
        public static KeyboardSettings Parse(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            return new KeyboardSettings
            {
                StartupProfileNumber =
                    ParseFileNumber(SettingsLineReader.FindValue(lines, SettingsKeys.Profile))
                    ?? ParseFileNumber(SettingsLineReader.FindValue(lines, SettingsKeys.StartupFile)),
                LedMode = ParseText(SettingsLineReader.FindValue(lines, SettingsKeys.LedMode)),
                MacroSpeed = ParseMacroSpeed(SettingsLineReader.FindValue(lines, SettingsKeys.MacroSpeed)),
                StatusPlaySpeed =
                    ParseStatusPlaySpeed(SettingsLineReader.FindValue(lines, SettingsKeys.StatusPlaySpeed))
                    ?? ParseStatusPlaySpeed(SettingsLineReader.FindValue(lines, SettingsKeys.Status)),
                IsVDriveAutoMountEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.VDrive), AutoValue),
                IsVDriveOpenOnStartupEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.VDriveOpenOnStartup), OnValue),
                IsGameModeEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.GameMode), OnValue),
                IsProgramLockEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.Lock), OnValue),
                IsKeyClickToneEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.KeyClickTone), OnValue),
                IsToggleToneEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.ToggleTone), OnValue),
                ThumbMode = ParseText(SettingsLineReader.FindValue(lines, SettingsKeys.ThumbMode)),
                IsMacroDisabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.MacroDisable), OnValue),
                IsPowerUser = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.PowerUser), TrueValue),
                Country = ParseText(SettingsLineReader.FindValue(lines, SettingsKeys.Country)),
                IsProgramKeyLockEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.ProgramKeyLock), OnValue),
                IsProfileSyncModeEnabled = ParseFlag(SettingsLineReader.FindValue(lines, SettingsKeys.ProfileSyncMode), OnValue)
            };
        }

        private static bool? ParseFlag(string? value, string trueValue)
        {
            if (value is null)
            {
                return null;
            }

            return string.Equals(value.Trim(), trueValue, StringComparison.OrdinalIgnoreCase);
        }

        private static int? ParseMacroSpeed(string? value)
        {
            return ParseIntegerInRange(value, SettingsValueRanges.MinimumMacroSpeed, SettingsValueRanges.MaximumMacroSpeed);
        }

        private static int? ParseStatusPlaySpeed(string? value)
        {
            return ParseIntegerInRange(value, SettingsValueRanges.MinimumStatusPlaySpeed, SettingsValueRanges.MaximumStatusPlaySpeed);
        }

        private static int? ParseIntegerInRange(string? value, int minimum, int maximum)
        {
            if (value is null)
            {
                return null;
            }

            if (!int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                return null;
            }

            return parsed >= minimum && parsed <= maximum ? parsed : null;
        }

        private static int? ParseFileNumber(string? value)
        {
            if (value is null)
            {
                return null;
            }

            var digitsStart = -1;
            var trimmedValue = value.Trim();

            for (var index = 0; index < trimmedValue.Length; index++)
            {
                if (char.IsAsciiDigit(trimmedValue[index]))
                {
                    if (digitsStart < 0)
                    {
                        digitsStart = index;
                    }

                    continue;
                }

                if (digitsStart >= 0)
                {
                    return ParseProfileNumber(trimmedValue[digitsStart..index]);
                }
            }

            return digitsStart >= 0 ? ParseProfileNumber(trimmedValue[digitsStart..]) : null;
        }

        private static int? ParseProfileNumber(string digits)
        {
            return ParseIntegerInRange(digits, SettingsValueRanges.MinimumProfileNumber, SettingsValueRanges.MaximumProfileNumber);
        }

        private static string? ParseText(string? value)
        {
            if (value is null)
            {
                return null;
            }

            var trimmedValue = value.Trim();

            return trimmedValue.Length > 0 ? trimmedValue : null;
        }
    }
}
