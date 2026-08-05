using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.VDrive.Eject
{
    public class VDriveEjectTests
    {
        [Fact]
        public void CreateForCurrentPlatform_OnCurrentOperatingSystem_ReturnsMatchingEjector()
        {
            var ejector = VDriveEject.CreateForCurrentPlatform();

            if (OperatingSystem.IsMacOS())
            {
                Assert.IsType<MacVDriveEjector>(ejector);
                Assert.True(ejector.IsSupported);
            }
            else
            {
                Assert.IsType<UnsupportedVDriveEjector>(ejector);
                Assert.False(ejector.IsSupported);
            }
        }
    }
}
