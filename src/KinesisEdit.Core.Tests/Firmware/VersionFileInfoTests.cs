using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    public class VersionFileInfoTests
    {
        [Theory]
        [InlineData("FS PRO", DeviceId.FreestylePro)]
        [InlineData("fs pro", DeviceId.FreestylePro)]
        [InlineData("  FS PRO  ", DeviceId.FreestylePro)]
        [InlineData("FS Edge", DeviceId.FreestyleEdge)]
        [InlineData("FS Edge RGB", DeviceId.FreestyleEdge)]
        [InlineData("", DeviceId.FreestyleEdge)]
        [InlineData("anything else", DeviceId.FreestyleEdge)]
        public void ResolveFreestyleModel_WithModelName_SelectsProOnlyForFsPro(string modelName, DeviceId expectedDeviceId)
        {
            var info = new VersionFileInfo
            {
                ModelName = modelName
            };

            Assert.Equal(expectedDeviceId, info.ResolveFreestyleModel());
        }

        [Fact]
        public void Empty_Always_HasNoModelNoVersionsAndNoMarker()
        {
            var empty = VersionFileInfo.Empty;

            Assert.Equal(string.Empty, empty.ModelName);
            Assert.Null(empty.KeyboardFirmware);
            Assert.Equal(string.Empty, empty.KeyboardFirmwareText);
            Assert.Null(empty.LedFirmware);
            Assert.Equal(string.Empty, empty.LedFirmwareText);
            Assert.False(empty.HasFourMegabyteMarker);
        }
    }
}
