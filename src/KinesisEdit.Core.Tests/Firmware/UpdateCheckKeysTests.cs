using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the manifest key table of specs/09-firmware.md §3 step 4: the per-device keyboard and
    /// lighting keys, and the app key per master app × operating system — including the deliberate
    /// deviation that Linux reuses the macOS keys (the legacy app had no Linux build).
    /// </summary>
    public class UpdateCheckKeysTests
    {
        [Theory]
        [InlineData(DeviceId.Tko, "tko_keyboard_version")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "keyboard_ver")]
        [InlineData(DeviceId.Advantage360, "kb360_version")]
        [InlineData(DeviceId.Advantage360Professional, "kb360_version")]
        public void FindKeyboardKey_ForDeviceInTheTable_MatchesSpec09Section3(DeviceId deviceId, string expectedKey)
        {
            Assert.Equal(expectedKey, UpdateCheckKeys.FindKeyboardKey(deviceId));
        }

        [Theory]
        [InlineData(DeviceId.Tko, "tko_lighting_version")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "lighting_ver")]
        [InlineData(DeviceId.Advantage360, "kb360_version")]
        [InlineData(DeviceId.Advantage360Professional, "kb360_version")]
        public void FindLightingKey_ForDeviceInTheTable_MatchesSpec09Section3(DeviceId deviceId, string expectedKey)
        {
            Assert.Equal(expectedKey, UpdateCheckKeys.FindLightingKey(deviceId));
        }

        [Fact]
        public void FindLightingKey_ForAdvantage360_RepeatsItsKeyboardKey()
        {
            Assert.Equal(UpdateCheckKeys.FindKeyboardKey(DeviceId.Advantage360), UpdateCheckKeys.FindLightingKey(DeviceId.Advantage360));
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.CrossfireKeypad)]
        public void FindFirmwareKeys_ForDeviceOutsideTheTable_ReturnNull(DeviceId deviceId)
        {
            Assert.Null(UpdateCheckKeys.FindKeyboardKey(deviceId));
            Assert.Null(UpdateCheckKeys.FindLightingKey(deviceId));
        }

        [Theory]
        [InlineData(ServingApp.SmartSetMasterGaming, UpdateCheckPlatform.Windows, "app_ver")]
        [InlineData(ServingApp.SmartSetMasterOffice, UpdateCheckPlatform.Windows, "pc_app_version")]
        [InlineData(ServingApp.SmartSetMasterGaming, UpdateCheckPlatform.MacOs, "mac_app_ver")]
        [InlineData(ServingApp.SmartSetMasterOffice, UpdateCheckPlatform.MacOs, "mac_app_version")]
        [InlineData(ServingApp.SmartSetMasterGaming, UpdateCheckPlatform.Linux, "mac_app_ver")]
        [InlineData(ServingApp.SmartSetMasterOffice, UpdateCheckPlatform.Linux, "mac_app_version")]
        public void FindAppKey_PerMasterAppAndPlatform_MatchesSpec09Section3(ServingApp servingApp, UpdateCheckPlatform platform, string expectedKey)
        {
            Assert.Equal(expectedKey, UpdateCheckKeys.FindAppKey(servingApp, platform));
        }

        [Theory]
        [InlineData(ServingApp.SmartSetMasterGaming)]
        [InlineData(ServingApp.SmartSetMasterOffice)]
        public void FindAppKey_WithUnknownPlatform_ReturnsNull(ServingApp servingApp)
        {
            Assert.Null(UpdateCheckKeys.FindAppKey(servingApp, UpdateCheckPlatform.None));
        }

        [Theory]
        [InlineData(ServingApp.None)]
        [InlineData(ServingApp.StandaloneOnly)]
        public void FindAppKey_ForAppWithoutUpdateDialog_ReturnsNull(ServingApp servingApp)
        {
            Assert.Null(UpdateCheckKeys.FindAppKey(servingApp, UpdateCheckPlatform.Windows));
            Assert.Null(UpdateCheckKeys.FindAppKey(servingApp, UpdateCheckPlatform.MacOs));
            Assert.Null(UpdateCheckKeys.FindAppKey(servingApp, UpdateCheckPlatform.Linux));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateCheckPlatform.Windows, "app_ver")]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateCheckPlatform.MacOs, "mac_app_ver")]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateCheckPlatform.Linux, "mac_app_ver")]
        [InlineData(DeviceId.Tko, UpdateCheckPlatform.Windows, "app_ver")]
        [InlineData(DeviceId.Tko, UpdateCheckPlatform.MacOs, "mac_app_ver")]
        [InlineData(DeviceId.Tko, UpdateCheckPlatform.Linux, "mac_app_ver")]
        [InlineData(DeviceId.Advantage360, UpdateCheckPlatform.Windows, "pc_app_version")]
        [InlineData(DeviceId.Advantage360, UpdateCheckPlatform.MacOs, "mac_app_version")]
        [InlineData(DeviceId.Advantage360, UpdateCheckPlatform.Linux, "mac_app_version")]
        public void FindAppKey_ResolvedFromDevice_UsesItsServingApp(DeviceId deviceId, UpdateCheckPlatform platform, string expectedKey)
        {
            Assert.Equal(expectedKey, UpdateCheckKeys.FindAppKey(deviceId, platform));
        }

        [Fact]
        public void FindAppKey_ForUnknownDevice_ReturnsNull()
        {
            Assert.Null(UpdateCheckKeys.FindAppKey(DeviceId.None, UpdateCheckPlatform.Windows));
        }
    }
}
