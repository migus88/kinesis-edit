using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class KeyboardSettingsGateTests
    {
        private static readonly VersionFileInfo _twoMegabyteInfo = new();
        private static readonly VersionFileInfo _fourMegabyteInfo = new()
        {
            HasFourMegabyteMarker = true,
        };

        [Fact]
        public void CanEditKeyboardSettings_WithAdvantage2WithoutMarker_ReturnsFalse()
        {
            Assert.False(KeyboardSettingsGate.CanEditKeyboardSettings(DeviceId.Advantage2, _twoMegabyteInfo));
        }

        [Fact]
        public void CanEditKeyboardSettings_WithAdvantage2WithMarker_ReturnsTrue()
        {
            Assert.True(KeyboardSettingsGate.CanEditKeyboardSettings(DeviceId.Advantage2, _fourMegabyteInfo));
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        public void CanEditKeyboardSettings_WithOtherDeviceWithoutMarker_ReturnsTrue(DeviceId deviceId)
        {
            Assert.True(KeyboardSettingsGate.CanEditKeyboardSettings(deviceId, _twoMegabyteInfo));
        }

        [Fact]
        public void EnsureCanEditKeyboardSettings_WithAdvantage2WithoutMarker_ThrowsInvalidOperationException()
        {
            var exception = Assert.Throws<InvalidOperationException>(
                () => KeyboardSettingsGate.EnsureCanEditKeyboardSettings(DeviceId.Advantage2, _twoMegabyteInfo));

            Assert.Contains("4MB", exception.Message);
        }

        [Fact]
        public void EnsureCanEditKeyboardSettings_WithAdvantage2WithMarker_DoesNotThrow()
        {
            KeyboardSettingsGate.EnsureCanEditKeyboardSettings(DeviceId.Advantage2, _fourMegabyteInfo);
        }

        [Fact]
        public void CanEditKeyboardSettings_WithNullVersionFileInfo_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(
                () => KeyboardSettingsGate.CanEditKeyboardSettings(DeviceId.Advantage2, null!));
        }
    }
}
