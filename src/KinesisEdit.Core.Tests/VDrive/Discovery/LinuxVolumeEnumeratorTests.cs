using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    public sealed class LinuxVolumeEnumeratorTests : IDisposable
    {
        private readonly string _tempRoot;

        public LinuxVolumeEnumeratorTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "kinesis-edit-linux-enum-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_tempRoot);
        }

        [Fact]
        public void EnumerateVolumes_WithMultipleScanRoots_YieldsSubdirectoriesFromAllRoots()
        {
            var mediaRoot = Path.Combine(_tempRoot, "media");
            var runMediaRoot = Path.Combine(_tempRoot, "run-media");

            Directory.CreateDirectory(Path.Combine(mediaRoot, "TKO"));
            Directory.CreateDirectory(Path.Combine(runMediaRoot, "ADV360"));

            var enumerator = new LinuxVolumeEnumerator([mediaRoot, runMediaRoot]);

            var candidates = enumerator.EnumerateVolumes().OrderBy(candidate => candidate.Label).ToList();

            Assert.Equal(2, candidates.Count);
            Assert.Equal(new VolumeCandidate(Path.Combine(runMediaRoot, "ADV360"), "ADV360"), candidates[0]);
            Assert.Equal(new VolumeCandidate(Path.Combine(mediaRoot, "TKO"), "TKO"), candidates[1]);
        }

        [Fact]
        public void EnumerateVolumes_WithMissingScanRoots_YieldsNothing()
        {
            var enumerator = new LinuxVolumeEnumerator([
                Path.Combine(_tempRoot, "missing-a"),
                Path.Combine(_tempRoot, "missing-b")
            ]);

            Assert.Empty(enumerator.EnumerateVolumes());
        }

        [Fact]
        public void EnumerateVolumes_WithDefaultScanRoots_DoesNotThrow()
        {
            var enumerator = new LinuxVolumeEnumerator();

            var candidates = enumerator.EnumerateVolumes().ToList();

            Assert.NotNull(candidates);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
    }
}
