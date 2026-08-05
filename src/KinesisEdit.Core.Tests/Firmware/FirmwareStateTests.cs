using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    public class FirmwareStateTests
    {
        [Fact]
        public void FromVersionFile_WithParsedInfo_CopiesKeyboardAndLedVersions()
        {
            var info = new VersionFileInfo
            {
                KeyboardFirmware = new FirmwareVersion(1, 0, 121),
                LedFirmware = new FirmwareVersion(1, 0, 58)
            };

            var state = FirmwareState.FromVersionFile(info);

            Assert.Equal(new FirmwareVersion(1, 0, 121), state.KeyboardFirmware);
            Assert.Equal(new FirmwareVersion(1, 0, 58), state.LedFirmware);
            Assert.False(state.IsDemoMode);
        }

        [Fact]
        public void FromVersionFile_WithDemoModeFlag_SetsDemoMode()
        {
            var state = FirmwareState.FromVersionFile(VersionFileInfo.Empty, isDemoMode: true);

            Assert.True(state.IsDemoMode);
            Assert.Null(state.KeyboardFirmware);
            Assert.Null(state.LedFirmware);
        }

        [Fact]
        public void FromVersionFile_WithNullInfo_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => FirmwareState.FromVersionFile(null!));
        }
    }
}
