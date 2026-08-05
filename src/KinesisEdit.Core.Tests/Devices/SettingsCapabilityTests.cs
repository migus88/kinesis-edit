using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    public class SettingsCapabilityTests
    {
        [Fact]
        public void None_Always_WritesNothing()
        {
            var capability = SettingsCapability.None;

            Assert.Equal(StartupSettingKind.None, capability.StartupSetting);
            Assert.Equal(LedModeKind.None, capability.LedMode);
            Assert.False(capability.WritesMacroSpeed);
            Assert.Null(capability.MacroSpeedMinimum);
            Assert.Equal(StatusSettingKind.None, capability.StatusSetting);
            Assert.Equal(VDriveSettingKind.None, capability.VDriveSetting);
            Assert.False(capability.WritesGameMode);
            Assert.False(capability.WritesProgramLock);
            Assert.False(capability.WritesKeyClickTone);
            Assert.False(capability.WritesToggleTone);
        }

        [Theory]
        [InlineData(
            DeviceId.SavantElite2,
            StartupSettingKind.None, LedModeKind.None, false, null, StatusSettingKind.None,
            VDriveSettingKind.None, false, false, false, false)]
        [InlineData(
            DeviceId.Advantage2,
            StartupSettingKind.None, LedModeKind.None, true, 0, StatusSettingKind.StatusPlaySpeedKey,
            VDriveSettingKind.OpenOnStartup, false, false, true, true)]
        [InlineData(
            DeviceId.FreestyleEdge,
            StartupSettingKind.None, LedModeKind.ModeString, true, 0, StatusSettingKind.StatusPlaySpeedKey,
            VDriveSettingKind.AutoManual, true, false, false, false)]
        [InlineData(
            DeviceId.FreestylePro,
            StartupSettingKind.None, LedModeKind.None, true, 0, StatusSettingKind.StatusPlaySpeedKey,
            VDriveSettingKind.AutoManual, false, false, false, false)]
        [InlineData(
            DeviceId.FreestyleEdgeRgb,
            StartupSettingKind.LayoutFileName, LedModeKind.LedFileName, true, 1, StatusSettingKind.StatusPlaySpeedKey,
            VDriveSettingKind.AutoManual, true, false, false, false)]
        [InlineData(
            DeviceId.CrossfireKeypad,
            StartupSettingKind.None, LedModeKind.None, false, null, StatusSettingKind.None,
            VDriveSettingKind.None, false, false, false, false)]
        [InlineData(
            DeviceId.Tko,
            StartupSettingKind.LayoutFileName, LedModeKind.LedFileName, true, 1, StatusSettingKind.StatusPlaySpeedKey,
            VDriveSettingKind.AutoManual, true, false, false, false)]
        [InlineData(
            DeviceId.Advantage360,
            StartupSettingKind.ProfileNumber, LedModeKind.None, false, null, StatusSettingKind.StatusKey,
            VDriveSettingKind.None, false, true, false, false)]
        [InlineData(
            DeviceId.Advantage360Professional,
            StartupSettingKind.None, LedModeKind.None, false, null, StatusSettingKind.None,
            VDriveSettingKind.None, false, false, false, false)]
        public void Settings_PerCatalogDevice_MatchesSpecEightSectionTwoTable(
            DeviceId deviceId,
            StartupSettingKind expectedStartupSetting,
            LedModeKind expectedLedMode,
            bool expectedWritesMacroSpeed,
            int? expectedMacroSpeedMinimum,
            StatusSettingKind expectedStatusSetting,
            VDriveSettingKind expectedVDriveSetting,
            bool expectedWritesGameMode,
            bool expectedWritesProgramLock,
            bool expectedWritesKeyClickTone,
            bool expectedWritesToggleTone)
        {
            var capability = DeviceCatalog.GetById(deviceId).Settings;

            Assert.Equal(expectedStartupSetting, capability.StartupSetting);
            Assert.Equal(expectedLedMode, capability.LedMode);
            Assert.Equal(expectedWritesMacroSpeed, capability.WritesMacroSpeed);
            Assert.Equal(expectedMacroSpeedMinimum, capability.MacroSpeedMinimum);
            Assert.Equal(expectedStatusSetting, capability.StatusSetting);
            Assert.Equal(expectedVDriveSetting, capability.VDriveSetting);
            Assert.Equal(expectedWritesGameMode, capability.WritesGameMode);
            Assert.Equal(expectedWritesProgramLock, capability.WritesProgramLock);
            Assert.Equal(expectedWritesKeyClickTone, capability.WritesKeyClickTone);
            Assert.Equal(expectedWritesToggleTone, capability.WritesToggleTone);
        }

        [Fact]
        public void Settings_ForSavantElite2_IsTheNoneCapability()
        {
            Assert.Same(SettingsCapability.None, DeviceCatalog.GetById(DeviceId.SavantElite2).Settings);
        }
    }
}
