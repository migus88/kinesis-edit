using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    public sealed class MacVolumeEnumeratorTests : IDisposable
    {
        private readonly string _tempRoot;

        public MacVolumeEnumeratorTests()
        {
            _tempRoot = Path.Combine(Path.GetTempPath(), "kinesis-edit-mac-enum-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(_tempRoot);
        }

        [Fact]
        public void EnumerateVolumes_WithMountedVolumeDirectories_YieldsDirectoryNamesAsLabels()
        {
            Directory.CreateDirectory(Path.Combine(_tempRoot, "ADV360"));
            Directory.CreateDirectory(Path.Combine(_tempRoot, "Macintosh HD"));
            File.WriteAllText(Path.Combine(_tempRoot, "stray-file.txt"), string.Empty);

            var enumerator = new MacVolumeEnumerator(_tempRoot);

            var candidates = enumerator.EnumerateVolumes().OrderBy(candidate => candidate.Label).ToList();

            Assert.Equal(2, candidates.Count);
            Assert.Equal(new VolumeCandidate(Path.Combine(_tempRoot, "ADV360"), "ADV360"), candidates[0]);
            Assert.Equal(new VolumeCandidate(Path.Combine(_tempRoot, "Macintosh HD"), "Macintosh HD"), candidates[1]);
        }

        [Fact]
        public void EnumerateVolumes_WithMissingScanRoot_YieldsNothing()
        {
            var enumerator = new MacVolumeEnumerator(Path.Combine(_tempRoot, "does-not-exist"));

            Assert.Empty(enumerator.EnumerateVolumes());
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
