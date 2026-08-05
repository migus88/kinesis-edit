using KinesisEdit.Core.VDrive.Eject;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class DeviceEjectServiceTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void IsSupported_Always_MirrorsTheEjector(bool isSupported)
        {
            var ejector = new FakeVDriveEjector
            {
                IsSupported = isSupported
            };

            var service = new DeviceEjectService(ejector);

            Assert.Equal(isSupported, service.IsSupported);
        }

        [Fact]
        public async Task EjectAsync_WithVolumePath_DelegatesToTheEjector()
        {
            var ejector = new FakeVDriveEjector();
            var service = new DeviceEjectService(ejector);

            var result = await service.EjectAsync("/Volumes/ADV360");

            Assert.True(result.Succeeded);
            Assert.Equal("/Volumes/ADV360", ejector.LastVolumeRootPath);
        }

        private sealed class FakeVDriveEjector : IVDriveEjector
        {
            public bool IsSupported { get; set; } = true;

            public VDriveEjectResult ResultToReturn { get; set; } = new VDriveEjectResult
            {
                Succeeded = true
            };

            public string? LastVolumeRootPath { get; private set; }

            public VDriveEjectResult Eject(string volumeRootPath)
            {
                LastVolumeRootPath = volumeRootPath;

                return ResultToReturn;
            }
        }
    }
}
