using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.Core.Tests.Lighting.Preview
{
    /// <summary>
    /// Pins the speed curve: the documented anchors (3.6 s at speed 1, 1.2 s at the default 5,
    /// 0.4 s at speed 9), that it is strictly monotonic across the 1-9 range
    /// <see cref="LayerLightingState"/> declares, and that out-of-range input clamps rather than
    /// running off the end.
    /// </summary>
    public class LightingSpeedScaleTests
    {
        private const int Precision = 9;

        [Theory]
        [InlineData(LayerLightingState.MinimumSpeed, LightingSpeedScale.SlowestPeriodSeconds)]
        [InlineData(LayerLightingState.DefaultSpeed, 1.2)]
        [InlineData(LayerLightingState.MaximumSpeed, LightingSpeedScale.FastestPeriodSeconds)]
        public void PeriodSecondsFor_AtTheDocumentedAnchors_MatchesThem(int speed, double expectedSeconds)
        {
            Assert.Equal(expectedSeconds, LightingSpeedScale.PeriodSecondsFor(speed), Precision);
        }

        [Fact]
        public void PeriodSecondsFor_AcrossTheRange_FallsStrictlyAsSpeedRises()
        {
            for (var speed = LayerLightingState.MinimumSpeed; speed < LayerLightingState.MaximumSpeed; speed++)
            {
                Assert.True(
                    LightingSpeedScale.PeriodSecondsFor(speed) > LightingSpeedScale.PeriodSecondsFor(speed + 1),
                    $"Speed {speed + 1} should complete a cycle faster than speed {speed}.");
            }
        }

        [Fact]
        public void PeriodSecondsFor_EveryStep_IsTheSameMultiplier()
        {
            var firstRatio = LightingSpeedScale.PeriodSecondsFor(2) / LightingSpeedScale.PeriodSecondsFor(1);

            for (var speed = LayerLightingState.MinimumSpeed + 1; speed < LayerLightingState.MaximumSpeed; speed++)
            {
                var ratio = LightingSpeedScale.PeriodSecondsFor(speed + 1) / LightingSpeedScale.PeriodSecondsFor(speed);

                Assert.Equal(firstRatio, ratio, Precision);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        public void PeriodSecondsFor_BelowTheMinimum_ClampsToTheSlowestPeriod(int speed)
        {
            Assert.Equal(LightingSpeedScale.SlowestPeriodSeconds, LightingSpeedScale.PeriodSecondsFor(speed), Precision);
        }

        [Theory]
        [InlineData(10)]
        [InlineData(99)]
        [InlineData(int.MaxValue)]
        public void PeriodSecondsFor_AboveTheMaximum_ClampsToTheFastestPeriod(int speed)
        {
            Assert.Equal(LightingSpeedScale.FastestPeriodSeconds, LightingSpeedScale.PeriodSecondsFor(speed), Precision);
        }

        [Fact]
        public void PeriodFor_Always_IsPeriodSecondsForAsATimeSpan()
        {
            // TimeSpan carries 100 ns ticks, so the comparison is to the tick, not to the double.
            for (var speed = LayerLightingState.MinimumSpeed; speed <= LayerLightingState.MaximumSpeed; speed++)
            {
                Assert.Equal(
                    LightingSpeedScale.PeriodSecondsFor(speed),
                    LightingSpeedScale.PeriodFor(speed).TotalSeconds,
                    6);
            }
        }

        [Fact]
        public void CyclesPerSecond_Always_IsTheReciprocalOfThePeriod()
        {
            Assert.Equal(1.0 / LightingSpeedScale.SlowestPeriodSeconds, LightingSpeedScale.CyclesPerSecond(1), Precision);
            Assert.Equal(2.5, LightingSpeedScale.CyclesPerSecond(LayerLightingState.MaximumSpeed), Precision);
        }
    }
}
