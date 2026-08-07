using KinesisEdit.Core.Devices;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The gate, board by board.
    /// <para>
    /// It is asserted per device rather than once, because the failure it prevents is silent and
    /// wide: <see cref="DeviceMonitorService"/> builds a demo snapshot for <b>every</b> undetected
    /// catalog device, so a provider that handed out a location for a board with no fixtures would
    /// point that board's editor at files which are not there — turning six boards that today open
    /// an empty demo session into six that fail to open one.
    /// </para>
    /// </summary>
    public class DemoDeviceProviderTests
    {
        public static TheoryData<DeviceId> AllDevices =>
            new(DeviceCatalog.All.Select(device => device.Id));

        [Theory]
        [MemberData(nameof(AllDevices))]
        public void TryCreateLocation_AnswersOnlyForBoardsWithFixtures(DeviceId deviceId)
        {
            var provider = new DemoDeviceProvider();
            var device = DeviceCatalog.GetById(deviceId);

            var location = provider.TryCreateLocation(device);

            if (deviceId == DeviceId.FreestyleEdgeRgb)
            {
                Assert.NotNull(location);
                Assert.Same(device, location.Device);
                Assert.Equal(DemoVDrive.GetRootPath(deviceId), location.RootPath);
                Assert.False(location.IsWritable);
            }
            else
            {
                Assert.Null(location);
            }
        }

        [Theory]
        [MemberData(nameof(AllDevices))]
        public void HasDemoContent_AgreesWithTryCreateLocation(DeviceId deviceId)
        {
            var provider = new DemoDeviceProvider();
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(provider.HasDemoContent(deviceId), provider.TryCreateLocation(device) is not null);
        }

        [Fact]
        public void HasDemoContent_IsFalseForTheUnknownDevice()
        {
            Assert.False(new DemoDeviceProvider().HasDemoContent(DeviceId.None));
        }

        [Fact]
        public void TryCreateLocation_RefusesNull()
        {
            Assert.Throws<ArgumentNullException>(() => new DemoDeviceProvider().TryCreateLocation(null!));
        }
    }
}
