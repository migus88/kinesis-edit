using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the "Update Now" targets of specs/09-firmware.md §3 step 7: keyboard/lighting rows
    /// open the device's firmware section (the §2 page — troubleshooting URL + <c>#firmware</c>,
    /// Advantage 360 <c>#firmware-updates</c>), the app row opens <c>#smartset-app</c>.
    /// </summary>
    public class UpdateCheckUrlsTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#firmware")]
        [InlineData(DeviceId.Tko, "https://gaming.kinesis-ergo.com/tko-support/#firmware")]
        [InlineData(DeviceId.Advantage360, "https://kinesis-ergo.com/support/kb360/#firmware-updates")]
        public void FindFirmwareUrl_ForSupportedDevice_MatchesSpec09(DeviceId deviceId, string expectedUrl)
        {
            Assert.Equal(expectedUrl, UpdateCheckUrls.FindFirmwareUrl(deviceId));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        public void FindFirmwareUrl_ForGamingDevices_IsTheTroubleshootingUrlWithTheFirmwareAnchor(DeviceId deviceId)
        {
            var troubleshootingUrl = DeviceCatalog.GetById(deviceId).TroubleshootingUrl;

            Assert.Equal(troubleshootingUrl + "#firmware", UpdateCheckUrls.FindFirmwareUrl(deviceId));
        }

        [Fact]
        public void FindFirmwareUrl_ForSupportedDevice_ReusesTheSection2SupportPage()
        {
            foreach (var deviceId in UpdateCheckEligibility.SupportedDevices)
            {
                Assert.Equal(FirmwareSupportUrls.FindUrl(deviceId), UpdateCheckUrls.FindFirmwareUrl(deviceId));
            }
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#smartset-app")]
        [InlineData(DeviceId.Tko, "https://gaming.kinesis-ergo.com/tko-support/#smartset-app")]
        [InlineData(DeviceId.Advantage360, "https://kinesis-ergo.com/support/kb360/#smartset-app")]
        public void FindAppUrl_ForSupportedDevice_AnchorsTheTroubleshootingUrl(DeviceId deviceId, string expectedUrl)
        {
            Assert.Equal(expectedUrl, UpdateCheckUrls.FindAppUrl(deviceId));
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void FindAppUrl_ForDeviceWithoutSupportPage_ReturnsNull(DeviceId deviceId)
        {
            Assert.Null(UpdateCheckUrls.FindAppUrl(deviceId));
        }

        [Fact]
        public void AppAnchor_Always_IsTheSmartSetAppSection()
        {
            Assert.Equal("#smartset-app", UpdateCheckUrls.AppAnchor);
        }
    }
}
