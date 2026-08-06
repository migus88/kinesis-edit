using System.Globalization;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Settings
{
    /// <summary>
    /// The startup-profile ↔ led-file pairing of specs/08-settings.md §5.1 and
    /// specs/07-lighting.md §1.2: pointing a device at profile N writes the startup-profile key
    /// <b>and</b>, on devices whose <c>led_mode</c> holds a file name, the paired
    /// <c>led_mode=led&lt;N&gt;.txt</c>. Pure model-to-model transform — the settings panel's
    /// active-profile slider and <c>ProfileSession.SaveAs</c> share this one implementation so
    /// the pair can never drift apart. Serialization (and the 1-9 range check) still belongs to
    /// <see cref="KeyboardSettingsSerializer"/>.
    /// </summary>
    public static class StartupProfileSettings
    {
        private const string LedFileNamePrefix = "led";
        private const string LedFileNameSuffix = ".txt";

        /// <summary>
        /// Returns <paramref name="settings"/> pointed at profile <paramref name="profileNumber"/>:
        /// <see cref="KeyboardSettings.StartupProfileNumber"/> is set (the capability decides
        /// whether it persists as <c>startup_file=layout&lt;N&gt;.txt</c> or <c>profile=&lt;N&gt;</c>),
        /// and <see cref="KeyboardSettings.LedMode"/> is set to the paired led file name when
        /// <see cref="SettingsCapability.LedMode"/> is <see cref="LedModeKind.LedFileName"/>.
        /// Devices with <see cref="StartupSettingKind.None"/> have no startup-profile key at all
        /// (FS Edge/Pro, Advantage2), so the settings come back unchanged.
        /// </summary>
        public static KeyboardSettings ApplyStartupProfile(
            SettingsCapability capability,
            KeyboardSettings settings,
            int profileNumber)
        {
            ArgumentNullException.ThrowIfNull(capability);
            ArgumentNullException.ThrowIfNull(settings);

            if (capability.StartupSetting == StartupSettingKind.None)
            {
                return settings;
            }

            var updated = settings with { StartupProfileNumber = profileNumber };

            if (capability.LedMode == LedModeKind.LedFileName)
            {
                updated = updated with { LedMode = GetLedFileName(profileNumber) };
            }

            return updated;
        }

        /// <summary>The led file paired with a profile number, <c>led&lt;N&gt;.txt</c> (spec 07 §1.2).</summary>
        public static string GetLedFileName(int profileNumber)
        {
            return LedFileNamePrefix + profileNumber.ToString(CultureInfo.InvariantCulture) + LedFileNameSuffix;
        }
    }
}
