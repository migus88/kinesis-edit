namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Parses app_settings.txt lines into an <see cref="AppSettings"/> per
    /// specs/08-settings.md §3. Case-insensitive throughout (spec 08 §1); a hide flag is true
    /// iff its value is <c>on</c>, and a missing key stays null (= <c>off</c> = show). Custom
    /// colors that are missing, empty, or malformed parse as unset. Pure text-in/data-out.
    /// </summary>
    public static class AppSettingsParser
    {
        private const string OnValue = "on";

        /// <summary>Parses <paramref name="lines"/> into a typed model; missing keys stay null.</summary>
        public static AppSettings Parse(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var customColors = new SettingsColor?[AppSettings.CustomColorCount];

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                var value = SettingsLineReader.FindValue(lines, SettingsKeys.GetCustomColorKey(slotNumber));

                if (SettingsColor.TryParse(value, out var color))
                {
                    customColors[slotNumber - 1] = color;
                }
            }

            return new AppSettings
            {
                IsAppIntroMessageHidden = ParseHideFlag(lines, SettingsKeys.AppIntroMessage),
                IsSaveAsMessageHidden = ParseHideFlag(lines, SettingsKeys.SaveAsMessage),
                IsSaveMessageHidden = ParseHideFlag(lines, SettingsKeys.SaveMessage),
                IsMultiplayMessageHidden = ParseHideFlag(lines, SettingsKeys.MultiplayMessage),
                IsSpeedMessageHidden = ParseHideFlag(lines, SettingsKeys.SpeedMessage),
                IsCopyMacroMessageHidden = ParseHideFlag(lines, SettingsKeys.CopyMacroMessage),
                IsResetKeyMessageHidden = ParseHideFlag(lines, SettingsKeys.ResetKeyMessage),
                IsFirmwareCheckMessageHidden = ParseHideFlag(lines, SettingsKeys.FirmwareCheckMessage),
                IsSaveLightingMessageHidden = ParseHideFlag(lines, SettingsKeys.SaveLightingMessage),
                IsSaveSettingsMessageHidden = ParseHideFlag(lines, SettingsKeys.SaveSettingsMessage),
                IsWindowsCombinationMessageHidden = ParseHideFlag(lines, SettingsKeys.WindowsCombinationMessage),
                IsUpDownKeystrokeMessageHidden = ParseHideFlag(lines, SettingsKeys.UpDownKeystrokeMessage),
                CustomColors = customColors
            };
        }

        private static bool? ParseHideFlag(IReadOnlyList<string> lines, string key)
        {
            var value = SettingsLineReader.FindValue(lines, key);

            if (value is null)
            {
                return null;
            }

            return string.Equals(value.Trim(), OnValue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
