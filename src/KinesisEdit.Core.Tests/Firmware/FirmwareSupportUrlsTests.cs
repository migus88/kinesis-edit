using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    public class FirmwareSupportUrlsTests
    {
        [Theory]
        [InlineData(DeviceId.FreestylePro, "https://kinesis-ergo.com/support/freestyle-pro/#firmware-updates")]
        [InlineData(DeviceId.FreestyleEdge, "https://gaming.kinesis-ergo.com/fs-edge-support/#firmware")]
        [InlineData(DeviceId.Advantage2, "https://kinesis-ergo.com/support/advantage2/#firmware-updates")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#firmware")]
        [InlineData(DeviceId.Tko, "https://gaming.kinesis-ergo.com/tko-support/#firmware")]
        [InlineData(DeviceId.Advantage360, "https://kinesis-ergo.com/support/kb360/#firmware-updates")]
        public void FindUrl_ForDeviceWithSupportPage_MatchesSpec09Section2(DeviceId deviceId, string expectedUrl)
        {
            Assert.Equal(expectedUrl, FirmwareSupportUrls.FindUrl(deviceId));
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void FindUrl_ForDeviceWithoutSupportPage_ReturnsNull(DeviceId deviceId)
        {
            Assert.Null(FirmwareSupportUrls.FindUrl(deviceId));
        }
    }
}
