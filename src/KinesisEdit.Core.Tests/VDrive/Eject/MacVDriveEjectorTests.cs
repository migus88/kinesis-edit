using KinesisEdit.Core.VDrive.Eject;

namespace KinesisEdit.Core.Tests.VDrive.Eject
{
    public class MacVDriveEjectorTests
    {
        [Fact]
        public void Constructor_WithNullProcessRunner_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new MacVDriveEjector(null!));
        }

        [Fact]
        public void IsSupported_Always_ReturnsTrue()
        {
            var ejector = new MacVDriveEjector(new FakeProcessRunner());

            Assert.True(ejector.IsSupported);
        }

        [Fact]
        public void Eject_WithVolumeRootPath_RunsDiskutilUnmountWithThatPath()
        {
            var runner = new FakeProcessRunner();
            var ejector = new MacVDriveEjector(runner);

            ejector.Eject("/Volumes/ADV360");

            Assert.Equal("diskutil", runner.LastFileName);
            Assert.Equal(new[] { "unmount", "/Volumes/ADV360" }, runner.LastArguments);
        }

        [Fact]
        public void Eject_WithZeroExitCode_ReturnsSuccessWithStdoutMessage()
        {
            var runner = new FakeProcessRunner
            {
                ResultToReturn = new ProcessRunResult
                {
                    ExitCode = 0,
                    StandardOutput = "Volume ADV360 on disk4s1 unmounted\n",
                    StandardError = string.Empty,
                },
            };
            var ejector = new MacVDriveEjector(runner);

            var result = ejector.Eject("/Volumes/ADV360");

            Assert.True(result.Succeeded);
            Assert.Equal("Volume ADV360 on disk4s1 unmounted", result.Message);
        }

        [Fact]
        public void Eject_WithNonZeroExitCode_ReturnsFailureWithStderrMessage()
        {
            var runner = new FakeProcessRunner
            {
                ResultToReturn = new ProcessRunResult
                {
                    ExitCode = 1,
                    StandardOutput = "ignored stdout\n",
                    StandardError = "Unmount failed for /Volumes/ADV360\n",
                },
            };
            var ejector = new MacVDriveEjector(runner);

            var result = ejector.Eject("/Volumes/ADV360");

            Assert.False(result.Succeeded);
            Assert.Equal("Unmount failed for /Volumes/ADV360", result.Message);
        }

        [Fact]
        public void Eject_WithNonZeroExitCodeAndEmptyStderr_FallsBackToStdoutMessage()
        {
            var runner = new FakeProcessRunner
            {
                ResultToReturn = new ProcessRunResult
                {
                    ExitCode = 1,
                    StandardOutput = "diskutil wrote the error here\n",
                    StandardError = "   ",
                },
            };
            var ejector = new MacVDriveEjector(runner);

            var result = ejector.Eject("/Volumes/ADV360");

            Assert.False(result.Succeeded);
            Assert.Equal("diskutil wrote the error here", result.Message);
        }

        [Fact]
        public void Eject_WithEmptyVolumeRootPath_ThrowsArgumentException()
        {
            var ejector = new MacVDriveEjector(new FakeProcessRunner());

            Assert.Throws<ArgumentException>(() => ejector.Eject(string.Empty));
        }
    }
}
