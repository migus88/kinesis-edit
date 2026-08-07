namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Parses app_settings.txt lines into an <see cref="AppSettings"/> per
    /// specs/08-settings.md §3. Case-insensitive throughout (spec 08 §1); a hide flag is true
    /// iff its value is <c>on</c>, and a missing key stays null (= <c>off</c> = show). Custom
    /// colors that are missing, empty, or malformed parse as unset. Pure text-in/data-out.
    /// <para>
    /// Five keys are this app's additions rather than spec 08's (see <see cref="AppSettings"/>).
    /// Four are ordinary hide flags; <c>advisory_detail</c> is a display preference where
    /// <c>on</c> means "expand". <see cref="ParseOnFlag"/> only reports whether the stored value
    /// was <c>on</c> — <b>what <c>on</c> means is the property's business, not the parser's</b>,
    /// which is why there is one flag reader and not a "hide" reader.
    /// </para>
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
                IsAppIntroMessageHidden = ParseOnFlag(lines, SettingsKeys.AppIntroMessage),
                IsSaveAsMessageHidden = ParseOnFlag(lines, SettingsKeys.SaveAsMessage),
                IsSaveMessageHidden = ParseOnFlag(lines, SettingsKeys.SaveMessage),
                IsMultiplayMessageHidden = ParseOnFlag(lines, SettingsKeys.MultiplayMessage),
                IsSpeedMessageHidden = ParseOnFlag(lines, SettingsKeys.SpeedMessage),
                IsCopyMacroMessageHidden = ParseOnFlag(lines, SettingsKeys.CopyMacroMessage),
                IsResetKeyMessageHidden = ParseOnFlag(lines, SettingsKeys.ResetKeyMessage),
                IsFirmwareCheckMessageHidden = ParseOnFlag(lines, SettingsKeys.FirmwareCheckMessage),
                IsSaveLightingMessageHidden = ParseOnFlag(lines, SettingsKeys.SaveLightingMessage),
                IsSaveSettingsMessageHidden = ParseOnFlag(lines, SettingsKeys.SaveSettingsMessage),
                IsWindowsCombinationMessageHidden = ParseOnFlag(lines, SettingsKeys.WindowsCombinationMessage),
                IsUpDownKeystrokeMessageHidden = ParseOnFlag(lines, SettingsKeys.UpDownKeystrokeMessage),
                IsUnsavedChangesWarningHidden = ParseOnFlag(lines, SettingsKeys.UnsavedChangesMessage),
                IsResetLayerConfirmationHidden = ParseOnFlag(lines, SettingsKeys.ResetLayerMessage),
                IsCaptureSummaryHidden = ParseOnFlag(lines, SettingsKeys.CaptureSummaryMessage),
                IsSwitchVariantConfirmationHidden = ParseOnFlag(lines, SettingsKeys.SwitchVariantMessage),
                IsAdvisoryDetailExpanded = ParseOnFlag(lines, SettingsKeys.AdvisoryDetail),
                CustomColors = customColors,
                MacroNames = ParseMacroNames(lines)
            };
        }

        /// <summary>
        /// Reads the <c>macro_name_*</c> family by prefix scan (their full key names are dynamic,
        /// see <see cref="MacroNameKey"/>). Keys that do not parse as the prefix plus four unsigned
        /// decimals are ignored — a foreign line this app must not claim. Later lines win, matching
        /// <see cref="SettingsLineReader.FindValue"/>'s last-one-wins rule (08 §1). Values are
        /// trimmed; a blank one is kept as a <b>tombstone</b> so the next save deletes the key
        /// rather than leaving a name-less line behind.
        /// </summary>
        private static Dictionary<MacroNameKey, string> ParseMacroNames(IReadOnlyList<string> lines)
        {
            var names = new Dictionary<MacroNameKey, string>();

            foreach (var pair in SettingsLineReader.FindPrefixedValues(lines, SettingsKeys.MacroNamePrefix))
            {
                if (MacroNameKey.TryParseSuffix(pair.Key, out var key))
                {
                    names[key] = pair.Value.Trim();
                }
            }

            return names;
        }

        // Reports whether the stored value is literally "on", nothing more: the hide flags read it
        // as "hide", advisory_detail reads it as "expand", and conflating the two inverts a screen.
        private static bool? ParseOnFlag(IReadOnlyList<string> lines, string key)
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
