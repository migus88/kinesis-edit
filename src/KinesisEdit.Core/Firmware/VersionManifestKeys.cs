namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The JSON key names of the published-versions endpoint payload
    /// (specs/09-firmware.md §3 steps 3-4). Every key is optional and carries a version
    /// string; which key a row reads is decided by <see cref="UpdateCheckKeys"/>.
    /// </summary>
    public static class VersionManifestKeys
    {
        /// <summary>Edge RGB keyboard firmware.</summary>
        public const string KeyboardVersion = "keyboard_ver";

        /// <summary>Edge RGB lighting firmware.</summary>
        public const string LightingVersion = "lighting_ver";

        /// <summary>SmartSet app version, Windows build of the gaming master app.</summary>
        public const string WindowsGamingAppVersion = "app_ver";

        /// <summary>SmartSet app version, Windows build of the office master app.</summary>
        public const string WindowsOfficeAppVersion = "pc_app_version";

        /// <summary>SmartSet app version, macOS build of the gaming master app.</summary>
        public const string MacGamingAppVersion = "mac_app_ver";

        /// <summary>SmartSet app version, macOS build of the office master app.</summary>
        public const string MacOfficeAppVersion = "mac_app_version";

        /// <summary>TKO keyboard firmware.</summary>
        public const string TkoKeyboardVersion = "tko_keyboard_version";

        /// <summary>TKO lighting firmware.</summary>
        public const string TkoLightingVersion = "tko_lighting_version";

        /// <summary>Advantage 360 firmware; the same value serves its keyboard and lighting rows.</summary>
        public const string Advantage360Version = "kb360_version";
    }
}
