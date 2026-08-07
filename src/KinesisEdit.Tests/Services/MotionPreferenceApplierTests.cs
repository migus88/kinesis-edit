using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Resolving a <see cref="MotionPreference"/> onto the live flag, and the half everybody
    /// forgets: re-running <see cref="MotionResourceBinder"/>, without which nothing on screen
    /// changes at all.
    /// </summary>
    public class MotionPreferenceApplierTests
    {
        /// <summary>One of the six aliases <see cref="MotionResourceBinder"/> writes.</summary>
        private const string AliasKey = "ToastTransitions";

        [Theory]
        [InlineData(MotionPreference.AlwaysReduce, false, true)]
        [InlineData(MotionPreference.AlwaysReduce, true, true)]
        [InlineData(MotionPreference.NeverReduce, false, false)]
        [InlineData(MotionPreference.NeverReduce, true, false)]
        [InlineData(MotionPreference.FollowSystem, false, false)]
        [InlineData(MotionPreference.FollowSystem, true, true)]
        public void Resolve_TurnsThePreference_IntoTheFlag(
            MotionPreference preference,
            bool systemReduceMotion,
            bool expected)
        {
            Assert.Equal(expected, MotionPreferenceApplier.Resolve(preference, systemReduceMotion));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Resolve_FollowsTheSystem_ForAnUnknownPreference(bool systemReduceMotion)
        {
            Assert.Equal(
                systemReduceMotion,
                MotionPreferenceApplier.Resolve((MotionPreference)99, systemReduceMotion));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FollowSystem_RestoresTheOsAnswer_AfterAnOverride(bool systemReduceMotion)
        {
            // The whole reason IMotionSettings.SystemReduceMotion exists: an override must be
            // reversible, and re-reading the OS is exactly what MotionSettings promises not to do.
            var detector = new FakeReduceMotionDetector(systemReduceMotion);
            var settings = new MotionSettings(detector);
            var overridingPreference = systemReduceMotion ? MotionPreference.NeverReduce : MotionPreference.AlwaysReduce;

            settings.ReduceMotion = MotionPreferenceApplier.Resolve(overridingPreference, settings.SystemReduceMotion);

            Assert.NotEqual(systemReduceMotion, settings.ReduceMotion);

            settings.ReduceMotion = MotionPreferenceApplier.Resolve(
                MotionPreference.FollowSystem,
                settings.SystemReduceMotion);

            Assert.Equal(systemReduceMotion, settings.ReduceMotion);

            // And the OS was asked exactly once, at construction — not again to answer this.
            Assert.Equal(1, detector.DetectCallCount);
        }

        [AvaloniaFact]
        public void Apply_SetsTheFlag_AndRePointsTheAliases()
        {
            var application = Application.Current!;
            var restore = application.Resources.ToArray();

            try
            {
                var settings = new MotionSettings(new FakeReduceMotionDetector(false));

                MotionPreferenceApplier.Apply(application, settings, MotionPreference.AlwaysReduce);

                Assert.True(settings.ReduceMotion);
                Assert.True(application.TryFindResource(AliasKey + "Reduced", out var reduced));
                Assert.Same(reduced, application.Resources[AliasKey]);

                MotionPreferenceApplier.Apply(application, settings, MotionPreference.NeverReduce);

                Assert.False(settings.ReduceMotion);
                Assert.True(application.TryFindResource(AliasKey + "Full", out var full));
                Assert.Same(full, application.Resources[AliasKey]);
            }
            finally
            {
                foreach (var entry in restore)
                {
                    application.Resources[entry.Key] = entry.Value;
                }
            }
        }

        [AvaloniaFact]
        public void Apply_FollowingTheSystem_ReturnsToTheOsAnswer_Live()
        {
            var application = Application.Current!;
            var restore = application.Resources.ToArray();

            try
            {
                var settings = new MotionSettings(new FakeReduceMotionDetector(true));

                MotionPreferenceApplier.Apply(application, settings, MotionPreference.NeverReduce);

                Assert.False(settings.ReduceMotion);

                MotionPreferenceApplier.Apply(application, settings, MotionPreference.FollowSystem);

                Assert.True(settings.ReduceMotion);
                Assert.True(application.TryFindResource(AliasKey + "Reduced", out var reduced));
                Assert.Same(reduced, application.Resources[AliasKey]);
            }
            finally
            {
                foreach (var entry in restore)
                {
                    application.Resources[entry.Key] = entry.Value;
                }
            }
        }

        [AvaloniaFact]
        public void Apply_Rejects_NullArguments()
        {
            var settings = new MotionSettings(new FakeReduceMotionDetector(false));

            Assert.Throws<ArgumentNullException>(
                () => MotionPreferenceApplier.Apply(null!, settings, MotionPreference.FollowSystem));
            Assert.Throws<ArgumentNullException>(
                () => MotionPreferenceApplier.Apply(Application.Current!, null!, MotionPreference.FollowSystem));
        }
    }
}
