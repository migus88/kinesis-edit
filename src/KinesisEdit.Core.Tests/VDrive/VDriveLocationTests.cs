using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;

namespace KinesisEdit.Core.Tests.VDrive
{
    public class VDriveLocationTests
    {
        private const string RootPath = "root";

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "firmware", "version.txt")]
        [InlineData(DeviceId.FreestylePro, "firmware", "version.txt")]
        [InlineData(DeviceId.Advantage2, "active", "version.txt")]
        [InlineData(DeviceId.SavantElite2, "active", "version.txt")]
        [InlineData(DeviceId.Advantage360, "settings", "settings.txt")]
        public void VersionFilePath_PerDevice_CombinesRootVersionFolderAndVersionFile(DeviceId deviceId, string versionFolder, string versionFile)
        {
            var location = CreateLocation(deviceId);

            Assert.Equal(Path.Combine(RootPath, versionFolder, versionFile), location.VersionFilePath);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "settings")]
        [InlineData(DeviceId.FreestylePro, "settings")]
        [InlineData(DeviceId.Advantage2, "active")]
        [InlineData(DeviceId.SavantElite2, "settings")]
        [InlineData(DeviceId.Advantage360, "settings")]
        public void SettingsFolderPath_PerDevice_CombinesRootAndSettingsFolder(DeviceId deviceId, string settingsFolder)
        {
            var location = CreateLocation(deviceId);

            Assert.Equal(Path.Combine(RootPath, settingsFolder), location.SettingsFolderPath);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.FreestylePro, "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.Advantage2, "active", "state.txt")]
        [InlineData(DeviceId.SavantElite2, "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.Advantage360, "settings", "settings.txt")]
        public void SettingsFilePath_PerDevice_CombinesRootSettingsFolderAndSettingsFile(DeviceId deviceId, string settingsFolder, string settingsFile)
        {
            var location = CreateLocation(deviceId);

            Assert.Equal(Path.Combine(RootPath, settingsFolder, settingsFile), location.SettingsFilePath);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "layouts")]
        [InlineData(DeviceId.FreestylePro, "layouts")]
        [InlineData(DeviceId.Advantage360, "layouts")]
        [InlineData(DeviceId.Advantage2, "active")]
        [InlineData(DeviceId.SavantElite2, "active")]
        public void LayoutsFolderPath_PerDevice_CombinesRootAndLayoutFolder(DeviceId deviceId, string layoutFolder)
        {
            var location = CreateLocation(deviceId);

            Assert.Equal(Path.Combine(RootPath, layoutFolder), location.LayoutsFolderPath);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        public void LightingFolderPath_OnDeviceWithLightingFiles_CombinesRootAndLightingFolder(DeviceId deviceId)
        {
            var location = CreateLocation(deviceId);

            Assert.Equal(Path.Combine(RootPath, "lighting"), location.LightingFolderPath);
        }

        [Theory]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.SavantElite2)]
        public void LightingFolderPath_OnDeviceWithoutLightingFiles_IsNull(DeviceId deviceId)
        {
            var location = CreateLocation(deviceId);

            Assert.Null(location.LightingFolderPath);
        }

        [Fact]
        public void VersionFilePath_OnAdvantage360_EqualsSettingsFilePath()
        {
            var location = CreateLocation(DeviceId.Advantage360);

            Assert.Equal(location.SettingsFilePath, location.VersionFilePath);
        }

        private static VDriveLocation CreateLocation(DeviceId deviceId)
        {
            return new VDriveLocation
            {
                Device = DeviceCatalog.GetById(deviceId),
                RootPath = RootPath
            };
        }
    }
}
