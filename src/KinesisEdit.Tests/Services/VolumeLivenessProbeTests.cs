using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The real probe against real directories. It answers one question — is this mount root still
    /// there — and the point of these tests is as much what it does <em>not</em> do: no enumeration,
    /// no file read, no throw.
    /// </summary>
    public class VolumeLivenessProbeTests
    {
        [Fact]
        public void IsPresent_WhileTheDirectoryExists_IsTrue()
        {
            var probe = new VolumeLivenessProbe();
            var root = Directory.CreateTempSubdirectory("kinesis-liveness-");

            try
            {
                Assert.True(probe.IsPresent(root.FullName));
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }

        /// <summary>
        /// The whole reason this seam exists: an unmounted v-Drive loses its mount point, which is
        /// what an eject or an unplug looks like from here.
        /// </summary>
        [Fact]
        public void IsPresent_OnceTheDirectoryIsGone_IsFalse()
        {
            var probe = new VolumeLivenessProbe();
            var root = Directory.CreateTempSubdirectory("kinesis-liveness-");

            Assert.True(probe.IsPresent(root.FullName));

            root.Delete(recursive: true);

            Assert.False(probe.IsPresent(root.FullName));
        }

        /// <summary>
        /// A mount root is a directory. A file at the same path — and the synthetic
        /// <c>kinesis-edit://demo/</c> root, which is not a path at all — are both "not present"
        /// rather than exceptions.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("kinesis-edit://demo/FreestyleEdgeRgb")]
        public void IsPresent_ForSomethingThatIsNotAMountedDirectory_IsFalseAndNeverThrows(string rootPath)
        {
            var probe = new VolumeLivenessProbe();

            Assert.False(probe.IsPresent(rootPath));
        }

        [Fact]
        public void IsPresent_ForAFileRatherThanADirectory_IsFalse()
        {
            var probe = new VolumeLivenessProbe();
            var root = Directory.CreateTempSubdirectory("kinesis-liveness-");
            var filePath = Path.Combine(root.FullName, "version.txt");

            try
            {
                File.WriteAllText(filePath, "Model Name: TKO");

                Assert.False(probe.IsPresent(filePath));
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }

        [Fact]
        public void IsPresent_ForAPathThatNeverExisted_IsFalse()
        {
            var probe = new VolumeLivenessProbe();

            Assert.False(probe.IsPresent(Path.Combine(Path.GetTempPath(), "kinesis-liveness-never-" + Guid.NewGuid())));
        }
    }
}
