using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Tests.Services
{
    /// <summary>Hand-rolled scripted scanner: tests set the result, then drive a refresh.</summary>
    internal sealed class FakeVDriveScanner : IVDriveScanner
    {
        private static readonly TimeSpan _gateTimeout = TimeSpan.FromSeconds(10);

        /// <summary>When set, a scan stalls here until the test releases it — a slow mount.</summary>
        public ManualResetEventSlim? Gate { get; set; }

        private IReadOnlyList<VDriveLocation> _result = [];

        public void SetResult(params VDriveLocation[] locations)
        {
            _result = locations;
        }

        public IReadOnlyList<VDriveLocation> Scan()
        {
            // Bounded so a regression fails the test instead of hanging the suite.
            Gate?.Wait(_gateTimeout);

            return _result;
        }
    }
}
