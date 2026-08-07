using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The reduce-motion flag: what the detector's three possible answers resolve to, that a
    /// failing detector cannot take a launch down with it, and that the flag stays overridable.
    /// </summary>
    public class MotionSettingsTests
    {
        [Fact]
        public void ReduceMotion_IsTrue_WhenDetectorReportsReduced()
        {
            var settings = new MotionSettings(new FakeReduceMotionDetector(true));

            Assert.True(settings.ReduceMotion);
        }

        [Fact]
        public void ReduceMotion_IsFalse_WhenDetectorReportsNotReduced()
        {
            var settings = new MotionSettings(new FakeReduceMotionDetector(false));

            Assert.False(settings.ReduceMotion);
        }

        [Fact]
        public void ReduceMotion_IsFalse_WhenDetectorCannotTell()
        {
            // An unknown answer means motion-on: the app was designed with the full budget, and
            // an unreadable accessibility setting must not silently strip it.
            var settings = new MotionSettings(new FakeReduceMotionDetector(null));

            Assert.False(settings.ReduceMotion);
        }

        [Fact]
        public void ReduceMotion_IsFalse_WhenDetectorThrows()
        {
            var detector = FakeReduceMotionDetector.Failing(new InvalidOperationException("no such tool"));

            var settings = new MotionSettings(detector);

            Assert.False(settings.ReduceMotion);
        }

        [Fact]
        public void Constructor_DoesNotPropagate_DetectorFailure()
        {
            // Detection runs on the startup path, so no failure of it may reach the caller.
            var detector = FakeReduceMotionDetector.Failing(new UnauthorizedAccessException());

            var exception = Record.Exception(() => new MotionSettings(detector));

            Assert.Null(exception);
        }

        [Fact]
        public void Constructor_AsksTheDetector_ExactlyOnce()
        {
            var detector = new FakeReduceMotionDetector(true);

            var settings = new MotionSettings(detector);

            Assert.Equal(1, detector.DetectCallCount);
            Assert.True(settings.ReduceMotion);
        }

        [Fact]
        public void ReadingReduceMotion_DoesNotReAskTheDetector()
        {
            var detector = new FakeReduceMotionDetector(true);
            var settings = new MotionSettings(detector);

            _ = settings.ReduceMotion;
            _ = settings.ReduceMotion;

            Assert.Equal(1, detector.DetectCallCount);
        }

        [Fact]
        public void ReduceMotion_CanBeOverridden_AfterDetection()
        {
            var settings = new MotionSettings(new FakeReduceMotionDetector(false));

            settings.ReduceMotion = true;

            Assert.True(settings.ReduceMotion);
        }

        [Fact]
        public void ReduceMotion_OverrideSurvives_WhenDetectorSaidReduced()
        {
            var settings = new MotionSettings(new FakeReduceMotionDetector(true));

            settings.ReduceMotion = false;

            Assert.False(settings.ReduceMotion);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SystemReduceMotion_IsTheDetectorsAnswer(bool detected)
        {
            var settings = new MotionSettings(new FakeReduceMotionDetector(detected));

            Assert.Equal(detected, settings.SystemReduceMotion);
            Assert.Equal(detected, settings.ReduceMotion);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SystemReduceMotion_SurvivesAnOverride(bool detected)
        {
            // This is what makes MotionPreference.FollowSystem reversible: an override must not
            // destroy the only copy of the OS answer, because nothing re-reads the OS.
            var settings = new MotionSettings(new FakeReduceMotionDetector(detected));

            settings.ReduceMotion = !detected;

            Assert.Equal(detected, settings.SystemReduceMotion);
        }

        [Fact]
        public void SystemReduceMotion_IsFalse_WhenDetectionFailed()
        {
            var settings = new MotionSettings(FakeReduceMotionDetector.Failing(new InvalidOperationException()));

            Assert.False(settings.SystemReduceMotion);
        }

        [Fact]
        public void Constructor_Rejects_NullDetector()
        {
            Assert.Throws<ArgumentNullException>(() => new MotionSettings(null!));
        }

        [Fact]
        public void CreateForCurrentPlatform_ReturnsUsableSettings()
        {
            // Whatever platform the suite runs on, the factory must produce a working instance
            // and must not throw: it is called before the shell window exists.
            var settings = MotionSettings.CreateForCurrentPlatform();

            Assert.NotNull(settings);
        }
    }
}
