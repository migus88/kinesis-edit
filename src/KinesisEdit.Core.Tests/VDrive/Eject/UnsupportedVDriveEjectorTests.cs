using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.VDrive.Eject
{
    public class UnsupportedVDriveEjectorTests
    {
        [Fact]
        public void IsSupported_Always_ReturnsFalse()
        {
            var ejector = new UnsupportedVDriveEjector();

            Assert.False(ejector.IsSupported);
        }

        [Theory]
        [InlineData("/Volumes/ADV360")]
        [InlineData("")]
        public void Eject_WithAnyVolumeRootPath_ReturnsFailureWithoutThrowing(string volumeRootPath)
        {
            var ejector = new UnsupportedVDriveEjector();

            var result = ejector.Eject(volumeRootPath);

            Assert.False(result.Succeeded);
            Assert.False(string.IsNullOrWhiteSpace(result.Message));
        }
    }
}
