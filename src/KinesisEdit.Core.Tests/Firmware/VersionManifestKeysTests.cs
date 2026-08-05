using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the JSON key names of the published-versions payload (specs/09-firmware.md §3
    /// steps 3-4) verbatim — they are wire format, so a rename breaks the endpoint contract.
    /// </summary>
    public class VersionManifestKeysTests
    {
        [Theory]
        [InlineData(VersionManifestKeys.KeyboardVersion, "keyboard_ver")]
        [InlineData(VersionManifestKeys.LightingVersion, "lighting_ver")]
        [InlineData(VersionManifestKeys.WindowsGamingAppVersion, "app_ver")]
        [InlineData(VersionManifestKeys.WindowsOfficeAppVersion, "pc_app_version")]
        [InlineData(VersionManifestKeys.MacGamingAppVersion, "mac_app_ver")]
        [InlineData(VersionManifestKeys.MacOfficeAppVersion, "mac_app_version")]
        [InlineData(VersionManifestKeys.TkoKeyboardVersion, "tko_keyboard_version")]
        [InlineData(VersionManifestKeys.TkoLightingVersion, "tko_lighting_version")]
        [InlineData(VersionManifestKeys.Advantage360Version, "kb360_version")]
        public void Keys_Always_MatchTheSpecKeyNames(string key, string expectedKey)
        {
            Assert.Equal(expectedKey, key);
        }
    }
}
