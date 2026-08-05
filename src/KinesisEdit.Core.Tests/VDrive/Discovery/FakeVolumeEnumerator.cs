using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    /// <summary>Hand-rolled fake feeding a fixed candidate list to the scanner under test.</summary>
    internal sealed class FakeVolumeEnumerator : IVolumeEnumerator
    {
        private readonly IReadOnlyList<VolumeCandidate> _candidates;

        public FakeVolumeEnumerator(params VolumeCandidate[] candidates)
        {
            _candidates = candidates;
        }

        public IEnumerable<VolumeCandidate> EnumerateVolumes()
        {
            return _candidates;
        }
    }
}
