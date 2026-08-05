using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    /// <summary>Hand-rolled scripted scanner: tests set the result, then drive the monitor via Poll.</summary>
    internal sealed class FakeVDriveScanner : IVDriveScanner
    {
        private IReadOnlyList<VDriveLocation> _result = [];

        public void SetResult(params VDriveLocation[] locations)
        {
            _result = locations;
        }

        public IReadOnlyList<VDriveLocation> Scan()
        {
            return _result;
        }
    }
}
