using System.Globalization;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// Serializes a <see cref="KeyboardSettings"/> into the key/value pairs the app writes for
    /// one device, per specs/08-settings.md §2. Only keys the device's
    /// <see cref="SettingsCapability"/> designates, and that are actually set (non-null) in the
    /// model, are emitted — reserved keys are never written. Value casing follows the spec 08
    /// §2 notes: booleans are uppercase <c>ON</c>/<c>OFF</c> except <c>v_drive</c>
    /// (<c>auto</c>/<c>manual</c>) and the Advantage2 <c>v_drive_open_on_startup</c> false
    /// value, which is literally lowercase <c>off</c>. Pure data-out — the pairs are applied
    /// through <c>IVDriveFileService.UpdateSettingsFile</c> so unmanaged lines survive.
    /// </summary>
    public static class KeyboardSettingsSerializer
    {
        private const string OnValue = "ON";
        private const string OffValue = "OFF";
        private const string LowercaseOffValue = "off";
        private const string AutoValue = "auto";
        private const string ManualValue = "manual";
        private const string LayoutFilePrefix = "layout";
        private const string LedFilePrefix = "led";
        private const string FileSuffix = ".txt";

        /// <summary>
        /// Emits the pairs to write, in the spec 08 §2 table order. Values are validated
        /// against the spec's ranges and forms: profile number 1-9, macro speed 0-9 (0 =
        /// disabled on every device; the per-device minimum of
        /// <see cref="SettingsCapability.MacroSpeedMinimum"/> is the UI slider floor), status
        /// play speed 0-4, <c>led_mode</c> per its <see cref="LedModeKind"/>. Out-of-range
        /// numbers throw <see cref="ArgumentOutOfRangeException"/>; malformed led modes throw
        /// <see cref="ArgumentException"/>.
        /// </summary>
        public static IReadOnlyList<KeyValuePair<string, string>> Serialize(SettingsCapability capability, KeyboardSettings settings)
        {
            ArgumentNullException.ThrowIfNull(capability);
            ArgumentNullException.ThrowIfNull(settings);

            var pairs = new List<KeyValuePair<string, string>>();

            AppendStartupSetting(pairs, capability, settings);
            AppendLedMode(pairs, capability, settings);
            AppendMacroSpeed(pairs, capability, settings);
            AppendStatusPlaySpeed(pairs, capability, settings);
            AppendVDriveSetting(pairs, capability, settings);
            AppendFlag(pairs, capability.WritesGameMode, SettingsKeys.GameMode, settings.IsGameModeEnabled);
            AppendFlag(pairs, capability.WritesProgramLock, SettingsKeys.Lock, settings.IsProgramLockEnabled);
            AppendFlag(pairs, capability.WritesKeyClickTone, SettingsKeys.KeyClickTone, settings.IsKeyClickToneEnabled);
            AppendFlag(pairs, capability.WritesToggleTone, SettingsKeys.ToggleTone, settings.IsToggleToneEnabled);

            return pairs;
        }

        private static void AppendStartupSetting(
            List<KeyValuePair<string, string>> pairs,
            SettingsCapability capability,
            KeyboardSettings settings)
        {
            if (capability.StartupSetting == StartupSettingKind.None || settings.StartupProfileNumber is not int profileNumber)
            {
                return;
            }

            if (profileNumber < SettingsValueRanges.MinimumProfileNumber || profileNumber > SettingsValueRanges.MaximumProfileNumber)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    profileNumber,
                    $"The startup profile number must be {SettingsValueRanges.MinimumProfileNumber}-{SettingsValueRanges.MaximumProfileNumber} (specs/08-settings.md §2).");
            }

            var profileText = profileNumber.ToString(CultureInfo.InvariantCulture);

            if (capability.StartupSetting == StartupSettingKind.LayoutFileName)
            {
                pairs.Add(KeyValuePair.Create(SettingsKeys.StartupFile, LayoutFilePrefix + profileText + FileSuffix));
                return;
            }

            pairs.Add(KeyValuePair.Create(SettingsKeys.Profile, profileText));
        }

        private static void AppendLedMode(
            List<KeyValuePair<string, string>> pairs,
            SettingsCapability capability,
            KeyboardSettings settings)
        {
            if (capability.LedMode == LedModeKind.None || settings.LedMode is not string ledMode)
            {
                return;
            }

            // The mode-string domain (0-9, P, B) is LedModeValues': the settings panel's picker
            // builds its options from the same list, so it cannot offer a value refused here.
            var value = capability.LedMode == LedModeKind.LedFileName
                ? FormatLedFileName(ledMode)
                : LedModeValues.Normalize(ledMode);

            if (value is null)
            {
                var expectedForm = capability.LedMode == LedModeKind.LedFileName
                    ? "a led file name led<N>.txt with N 1-9"
                    : "a brightness 0-9, P (pitch black) or B (breathe)";

                throw new ArgumentException(
                    $"The led_mode value '{ledMode}' is not {expectedForm} (specs/08-settings.md §2).",
                    nameof(settings));
            }

            pairs.Add(KeyValuePair.Create(SettingsKeys.LedMode, value));
        }

        private static string? FormatLedFileName(string ledMode)
        {
            var trimmedValue = ledMode.Trim();
            var expectedLength = LedFilePrefix.Length + 1 + FileSuffix.Length;

            if (trimmedValue.Length == expectedLength
                && trimmedValue.StartsWith(LedFilePrefix, StringComparison.OrdinalIgnoreCase)
                && trimmedValue.EndsWith(FileSuffix, StringComparison.OrdinalIgnoreCase)
                && trimmedValue[LedFilePrefix.Length] is >= '1' and <= '9')
            {
                return LedFilePrefix + trimmedValue[LedFilePrefix.Length] + FileSuffix;
            }

            return null;
        }

        private static void AppendMacroSpeed(
            List<KeyValuePair<string, string>> pairs,
            SettingsCapability capability,
            KeyboardSettings settings)
        {
            if (!capability.WritesMacroSpeed || settings.MacroSpeed is not int macroSpeed)
            {
                return;
            }

            if (macroSpeed < SettingsValueRanges.MinimumMacroSpeed || macroSpeed > SettingsValueRanges.MaximumMacroSpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    macroSpeed,
                    $"The macro speed must be {SettingsValueRanges.MinimumMacroSpeed}-{SettingsValueRanges.MaximumMacroSpeed}, 0 meaning playback disabled (specs/08-settings.md §2).");
            }

            pairs.Add(KeyValuePair.Create(SettingsKeys.MacroSpeed, macroSpeed.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AppendStatusPlaySpeed(
            List<KeyValuePair<string, string>> pairs,
            SettingsCapability capability,
            KeyboardSettings settings)
        {
            if (capability.StatusSetting == StatusSettingKind.None || settings.StatusPlaySpeed is not int statusPlaySpeed)
            {
                return;
            }

            if (statusPlaySpeed < SettingsValueRanges.MinimumStatusPlaySpeed || statusPlaySpeed > SettingsValueRanges.MaximumStatusPlaySpeed)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(settings),
                    statusPlaySpeed,
                    $"The status play speed must be {SettingsValueRanges.MinimumStatusPlaySpeed}-{SettingsValueRanges.MaximumStatusPlaySpeed}, 0 meaning disabled (specs/08-settings.md §2).");
            }

            var key = capability.StatusSetting == StatusSettingKind.StatusKey
                ? SettingsKeys.Status
                : SettingsKeys.StatusPlaySpeed;

            pairs.Add(KeyValuePair.Create(key, statusPlaySpeed.ToString(CultureInfo.InvariantCulture)));
        }

        private static void AppendVDriveSetting(
            List<KeyValuePair<string, string>> pairs,
            SettingsCapability capability,
            KeyboardSettings settings)
        {
            if (capability.VDriveSetting == VDriveSettingKind.AutoManual
                && settings.IsVDriveAutoMountEnabled is bool isAutoMountEnabled)
            {
                pairs.Add(KeyValuePair.Create(SettingsKeys.VDrive, isAutoMountEnabled ? AutoValue : ManualValue));
                return;
            }

            if (capability.VDriveSetting == VDriveSettingKind.OpenOnStartup
                && settings.IsVDriveOpenOnStartupEnabled is bool isOpenOnStartupEnabled)
            {
                pairs.Add(KeyValuePair.Create(
                    SettingsKeys.VDriveOpenOnStartup,
                    isOpenOnStartupEnabled ? OnValue : LowercaseOffValue));
            }
        }

        private static void AppendFlag(
            List<KeyValuePair<string, string>> pairs,
            bool isWritten,
            string key,
            bool? value)
        {
            if (isWritten && value is bool flag)
            {
                pairs.Add(KeyValuePair.Create(key, flag ? OnValue : OffValue));
            }
        }
    }
}
