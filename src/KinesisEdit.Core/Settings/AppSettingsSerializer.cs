namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Serializes an <see cref="AppSettings"/> into the key/value pairs the app writes to
    /// app_settings.txt, per specs/08-settings.md §3. Hide flags are persisted lowercase
    /// <c>on</c>/<c>off</c> (<c>on</c> = hide); flags that were never set (null) and unset
    /// colors are skipped — an unset color's key is never written empty. Pure data-out — the
    /// pairs are applied through <c>IVDriveFileService</c> so unmanaged lines survive.
    /// </summary>
    public static class AppSettingsSerializer
    {
        private const string OnValue = "on";
        private const string OffValue = "off";

        /// <summary>Emits the pairs to write: the set hide flags in spec 08 §3 table order, then the set colors.</summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Serialize(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var pairs = new List<KeyValuePair<string, string>>();

            AppendHideFlag(pairs, SettingsKeys.AppIntroMessage, settings.IsAppIntroMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.SaveAsMessage, settings.IsSaveAsMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.SaveMessage, settings.IsSaveMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.MultiplayMessage, settings.IsMultiplayMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.SpeedMessage, settings.IsSpeedMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.CopyMacroMessage, settings.IsCopyMacroMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.ResetKeyMessage, settings.IsResetKeyMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.FirmwareCheckMessage, settings.IsFirmwareCheckMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.SaveLightingMessage, settings.IsSaveLightingMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.SaveSettingsMessage, settings.IsSaveSettingsMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.WindowsCombinationMessage, settings.IsWindowsCombinationMessageHidden);
            AppendHideFlag(pairs, SettingsKeys.UpDownKeystrokeMessage, settings.IsUpDownKeystrokeMessageHidden);

            for (var slotNumber = 1; slotNumber <= AppSettings.CustomColorCount; slotNumber++)
            {
                if (settings.CustomColors[slotNumber - 1] is SettingsColor color)
                {
                    pairs.Add(KeyValuePair.Create(SettingsKeys.GetCustomColorKey(slotNumber), color.ToString()));
                }
            }

            return pairs;
        }

        private static void AppendHideFlag(List<KeyValuePair<string, string>> pairs, string key, bool? isHidden)
        {
            if (isHidden is bool flag)
            {
                pairs.Add(KeyValuePair.Create(key, flag ? OnValue : OffValue));
            }
        }
    }
}
