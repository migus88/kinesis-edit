using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    public class WindowsVolumeEnumeratorTests
    {
        [Fact]
        public void EnumerateVolumes_OnWindows_DoesNotThrow()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            var enumerator = new WindowsVolumeEnumerator();

            var candidates = enumerator.EnumerateVolumes().ToList();

            Assert.NotNull(candidates);
        }
    }
}
