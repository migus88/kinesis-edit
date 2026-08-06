using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The platform detectors behind <see cref="IReduceMotionDetector"/>: how the macOS
    /// <c>defaults</c> output is read, and that the other platforms answer "unknown".
    /// </summary>
    public class ReduceMotionDetectorTests
    {
        [Theory]
        [InlineData("1")]
        [InlineData("1\n")]
        [InlineData(" 1 ")]
        [InlineData("true")]
        [InlineData("YES")]
        public void MacOsDetector_ReportsReduced_ForTruthyOutput(string standardOutput)
        {
            var detector = new MacOsReduceMotionDetector(new FakeProcessRunner(0, standardOutput));

            Assert.True(detector.Detect());
        }

        [Theory]
        [InlineData("0")]
        [InlineData("0\n")]
        [InlineData("false")]
        [InlineData("No")]
        public void MacOsDetector_ReportsNotReduced_ForFalsyOutput(string standardOutput)
        {
            var detector = new MacOsReduceMotionDetector(new FakeProcessRunner(0, standardOutput));

            Assert.False(detector.Detect());
        }

        [Fact]
        public void MacOsDetector_ReportsUnknown_WhenTheKeyIsNotSet()
        {
            // `defaults read` exits 1 with "The domain/default pair ... does not exist" when the
            // user has never touched the switch. That is not a failure, just no answer.
            var runner = new FakeProcessRunner(1, string.Empty, "does not exist");
            var detector = new MacOsReduceMotionDetector(runner);

            Assert.Null(detector.Detect());
        }

        [Fact]
        public void MacOsDetector_ReportsUnknown_ForUnrecognizedOutput()
        {
            var detector = new MacOsReduceMotionDetector(new FakeProcessRunner(0, "maybe"));

            Assert.Null(detector.Detect());
        }

        [Fact]
        public void MacOsDetector_ReadsTheUniversalAccessDomain()
        {
            var runner = new FakeProcessRunner(0, "1");
            var detector = new MacOsReduceMotionDetector(runner);

            detector.Detect();

            Assert.Equal("defaults", runner.LastFileName);
            Assert.Equal(["read", "com.apple.universalaccess", "reduceMotion"], runner.LastArguments);
        }

        [Fact]
        public void MacOsDetector_PropagatesLaunchFailure_ToBeSwallowedByMotionSettings()
        {
            // The detector itself does not hide a broken process launch; MotionSettings is the one
            // place that decides a failed detection means motion-on.
            var runner = FakeProcessRunner.Failing(new InvalidOperationException("no defaults"));
            var detector = new MacOsReduceMotionDetector(runner);

            Assert.Throws<InvalidOperationException>(() => detector.Detect());
            Assert.False(new MotionSettings(detector).ReduceMotion);
        }

        [Fact]
        public void MacOsDetector_Rejects_NullProcessRunner()
        {
            Assert.Throws<ArgumentNullException>(() => new MacOsReduceMotionDetector(null!));
        }

        [Fact]
        public void UnsupportedDetector_AlwaysReportsUnknown()
        {
            var detector = new UnsupportedReduceMotionDetector();

            Assert.Null(detector.Detect());
        }

        [Fact]
        public void CreateForCurrentPlatform_ReturnsTheMacDetector_OnMacOnly()
        {
            var detector = ReduceMotionDetector.CreateForCurrentPlatform();

            if (OperatingSystem.IsMacOS())
            {
                Assert.IsType<MacOsReduceMotionDetector>(detector);
            }
            else
            {
                Assert.IsType<UnsupportedReduceMotionDetector>(detector);
            }
        }
    }
}
