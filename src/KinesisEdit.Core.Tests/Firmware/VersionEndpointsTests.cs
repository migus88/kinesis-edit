using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the published-versions endpoints of specs/09-firmware.md §3 step 3 — one per master
    /// app — and their resolution from a device through its serving app.
    /// </summary>
    public class VersionEndpointsTests
    {
        private const string GamingUrl = "https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions";
        private const string OfficeUrl = "https://kinesis-ergo.com/wp-json/ksv/v1/get_versions";

        [Fact]
        public void Urls_Always_MatchSpec09Section3()
        {
            Assert.Equal(GamingUrl, VersionEndpoints.GamingUrl);
            Assert.Equal(OfficeUrl, VersionEndpoints.OfficeUrl);
        }

        [Theory]
        [InlineData(ServingApp.SmartSetMasterGaming, GamingUrl)]
        [InlineData(ServingApp.SmartSetMasterOffice, OfficeUrl)]
        public void FindUrl_ForMasterApp_ReturnsItsEndpoint(ServingApp servingApp, string expectedUrl)
        {
            Assert.Equal(expectedUrl, VersionEndpoints.FindUrl(servingApp));
        }

        [Theory]
        [InlineData(ServingApp.None)]
        [InlineData(ServingApp.StandaloneOnly)]
        public void FindUrl_ForAppWithoutEndpoint_ReturnsNull(ServingApp servingApp)
        {
            Assert.Null(VersionEndpoints.FindUrl(servingApp));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, GamingUrl)]
        [InlineData(DeviceId.Tko, GamingUrl)]
        [InlineData(DeviceId.Advantage360, OfficeUrl)]
        [InlineData(DeviceId.Advantage360Professional, OfficeUrl)]
        [InlineData(DeviceId.Advantage2, OfficeUrl)]
        [InlineData(DeviceId.SavantElite2, OfficeUrl)]
        public void FindUrl_ForDeviceServedByAMasterApp_ReturnsThatEndpoint(DeviceId deviceId, string expectedUrl)
        {
            Assert.Equal(expectedUrl, VersionEndpoints.FindUrl(deviceId));
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.CrossfireKeypad)]
        public void FindUrl_ForStandaloneOrUnknownDevice_ReturnsNull(DeviceId deviceId)
        {
            Assert.Null(VersionEndpoints.FindUrl(deviceId));
        }
    }
}
