using System.Globalization;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// The post-save "Profile n Saved" wording per device family, quoted verbatim from
    /// specs/03-vdrive-and-files.md §5.3, specs/07-lighting.md §1.3, and
    /// specs/10-apps-and-ui.md — the same "plain data, no UI framework" pattern as
    /// <c>KinesisEdit.Core.Firmware.FirmwareGateCatalog</c>'s gate messages. RGB/TKO/Adv360 pick
    /// one of two wordings depending on whether the saved profile is the startup profile;
    /// FS Edge/Pro use one combined wording (that app has no per-profile startup concept in the
    /// settings capability, spec 08 §2's write column) regardless.
    /// </summary>
    public static class ProfileSaveMessageCatalog
    {
        private const string FreestyleMessage =
            "…use the Refresh Shortcut (SmartSet + Layout) or simply close the v-Drive (SmartSet + F8). "
            + "To load this layout to the keyboard press SmartSet + {0}.";

        private const string RgbStartupMessage =
            "Use the Refresh Shortcut (SmartSet + Profile) to preview your Layout and Lighting updates "
            + "or simply Eject the \"FS EDGE RGB\" drive in File Explorer and then disconnect the v-Drive (SmartSet + F8).";

        private const string RgbNonStartupMessage =
            "To load Profile {0} to the keyboard, hold the SmartSet key and tap the {0} key.";

        private const string TkoStartupMessage =
            "Use the Refresh Shortcut (SmartSet + Right Shift + B) to preview your Layout and Lighting updates "
            + "or simply Eject the \"TKO\" drive in File Explorer and then disconnect the v-Drive (SmartSet + Right Shift + V).";

        private const string TkoNonStartupMessage =
            "To load Profile {0} to the keyboard, hold the SmartSet key + Right Shift and tap the {0} key.";

        private const string Advantage360StartupMessage =
            "Use the Refresh Shortcut (SmartSet + 'Refresh')…";

        private const string Advantage360NonStartupMessage =
            "To load Profile {0}…, hold the SmartSet key and tap the {0} key.";

        /// <summary>
        /// The post-save message for <paramref name="device"/> saving <paramref name="profileNumber"/>,
        /// per the startup-profile branch <paramref name="isStartupProfile"/> (07 §1.3; 03 §5.3;
        /// 10 "SmartSetFSEdgePro"/"SmartSetTKO"/"SmartSetAdv360" save workflows).
        /// </summary>
        public static string GetMessage(DeviceId device, int profileNumber, bool isStartupProfile)
        {
            return device switch
            {
                DeviceId.FreestyleEdge or DeviceId.FreestylePro =>
                    Format(FreestyleMessage, profileNumber),

                DeviceId.FreestyleEdgeRgb =>
                    isStartupProfile ? RgbStartupMessage : Format(RgbNonStartupMessage, profileNumber),

                DeviceId.Tko =>
                    isStartupProfile ? TkoStartupMessage : Format(TkoNonStartupMessage, profileNumber),

                DeviceId.Advantage360 =>
                    isStartupProfile ? Advantage360StartupMessage : Format(Advantage360NonStartupMessage, profileNumber),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(device), device, "No post-save message is defined for this device.")
            };
        }

        private static string Format(string template, int profileNumber)
        {
            return string.Format(CultureInfo.InvariantCulture, template, profileNumber);
        }
    }
}
