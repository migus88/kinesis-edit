using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    /// <summary>
    /// The startup-profile ↔ <c>led_mode</c> pairing of specs/08-settings.md §5.1 and
    /// specs/07-lighting.md §1.2: the active-profile control writes both keys on the RGB/TKO,
    /// only <c>profile</c> on the Advantage 360, and nothing on devices with no startup key.
    /// </summary>
    public class StartupProfileSettingsTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        public void ApplyStartupProfile_OnALedFileNameDevice_SetsTheProfileAndThePairedLedFile(DeviceId deviceId)
        {
            var capability = DeviceCatalog.GetById(deviceId).Settings;

            var updated = StartupProfileSettings.ApplyStartupProfile(capability, KeyboardSettings.Empty, 5);

            Assert.Equal(5, updated.StartupProfileNumber);
            Assert.Equal("led5.txt", updated.LedMode);
        }

        [Fact]
        public void ApplyStartupProfile_OnTheAdvantage360_SetsTheProfileWithoutTouchingLedMode()
        {
            var capability = DeviceCatalog.GetById(DeviceId.Advantage360).Settings;

            var updated = StartupProfileSettings.ApplyStartupProfile(capability, KeyboardSettings.Empty, 3);

            Assert.Equal(3, updated.StartupProfileNumber);
            Assert.Null(updated.LedMode);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Advantage2)]
        public void ApplyStartupProfile_OnADeviceWithNoStartupKey_ReturnsTheSettingsUnchanged(DeviceId deviceId)
        {
            var capability = DeviceCatalog.GetById(deviceId).Settings;
            var settings = new KeyboardSettings { MacroSpeed = 4, LedMode = "B" };

            var updated = StartupProfileSettings.ApplyStartupProfile(capability, settings, 7);

            Assert.Same(settings, updated);
            Assert.Null(updated.StartupProfileNumber);
            Assert.Equal("B", updated.LedMode);
        }

        [Fact]
        public void ApplyStartupProfile_Always_LeavesTheOtherSettingsAlone()
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;
            var settings = new KeyboardSettings
            {
                MacroSpeed = 7,
                StatusPlaySpeed = 2,
                IsGameModeEnabled = true,
                Country = "us"
            };

            var updated = StartupProfileSettings.ApplyStartupProfile(capability, settings, 9);

            Assert.Equal(7, updated.MacroSpeed);
            Assert.Equal(2, updated.StatusPlaySpeed);
            Assert.True(updated.IsGameModeEnabled);
            Assert.Equal("us", updated.Country);
        }

        [Fact]
        public void ApplyStartupProfile_WithNullArguments_ThrowsArgumentNullException()
        {
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Settings;

            Assert.Throws<ArgumentNullException>(
                () => StartupProfileSettings.ApplyStartupProfile(null!, KeyboardSettings.Empty, 1));
            Assert.Throws<ArgumentNullException>(
                () => StartupProfileSettings.ApplyStartupProfile(capability, null!, 1));
        }

        [Theory]
        [InlineData(1, "led1.txt")]
        [InlineData(9, "led9.txt")]
        public void GetLedFileName_PerProfileNumber_MatchesTheSpec12FileNaming(int profileNumber, string expected)
        {
            Assert.Equal(expected, StartupProfileSettings.GetLedFileName(profileNumber));
        }
    }
}
