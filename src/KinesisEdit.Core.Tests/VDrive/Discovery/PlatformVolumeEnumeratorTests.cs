using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    public class PlatformVolumeEnumeratorTests
    {
        [Fact]
        public void Create_OnCurrentPlatform_ReturnsMatchingEnumeratorType()
        {
            var enumerator = PlatformVolumeEnumerator.Create();

            if (OperatingSystem.IsMacOS())
            {
                Assert.IsType<MacVolumeEnumerator>(enumerator);
            }
            else if (OperatingSystem.IsWindows())
            {
                Assert.IsType<WindowsVolumeEnumerator>(enumerator);
            }
            else if (OperatingSystem.IsLinux())
            {
                Assert.IsType<LinuxVolumeEnumerator>(enumerator);
            }
            else
            {
                Assert.Empty(enumerator.EnumerateVolumes());
            }
        }
    }
}
