using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.Core.Tests.Lighting.Preview
{
    /// <summary>
    /// Pins the speed curve: the documented anchors (24 s at speed 1, the measured 8 s at the
    /// default 5, 24/9 s at speed 9), that it is strictly monotonic across the 1-9 range
    /// <see cref="LayerLightingState"/> declares, and that out-of-range input clamps rather than
    /// running off the end.
    /// </summary>
    public class LightingSpeedScaleTests
    {
        private const int Precision = 9;

        /// <summary>
        /// The hardware measurement the whole scale is calibrated on: a Rebound bar crosses the
        /// Freestyle Edge RGB's board edge to edge in 4.0 s at the default speed 5.
        /// </summary>
        private const double MeasuredReboundEdgeToEdgeSeconds = 4.0;

        [Theory]
        [InlineData(LayerLightingState.MinimumSpeed, LightingSpeedScale.SlowestPeriodSeconds)]
        [InlineData(LayerLightingState.DefaultSpeed, 8.0)]
        [InlineData(LayerLightingState.MaximumSpeed, LightingSpeedScale.FastestPeriodSeconds)]
        public void PeriodSecondsFor_AtTheDocumentedAnchors_MatchesThem(int speed, double expectedSeconds)
        {
            Assert.Equal(expectedSeconds, LightingSpeedScale.PeriodSecondsFor(speed), Precision);
        }

        /// <summary>
        /// The anchor itself, stated on its own so a future recalibration has to face it: speed 5
        /// is 8.0 s, not "whatever the geometric middle happens to be".
        /// </summary>
        [Fact]
        public void PeriodSecondsFor_AtTheDefaultSpeed_IsTheMeasuredEightSecondPeriod()
        {
            Assert.Equal(8.0, LightingSpeedScale.PeriodSecondsFor(LayerLightingState.DefaultSpeed), Precision);
        }

        /// <summary>
        /// Rebound travels as a triangle wave, so edge-to-edge is half a cycle. Half of the
        /// default period must therefore be the 4.0 s that was measured on the hardware; the
        /// behavioural half of this pin lives in <c>LightingEffectSamplerTests</c>.
        /// </summary>
        [Fact]
        public void PeriodSecondsFor_HalvedAtTheDefaultSpeed_IsTheMeasuredReboundEdgeToEdgeTime()
        {
            var halfPeriod = LightingSpeedScale.PeriodSecondsFor(LayerLightingState.DefaultSpeed) / 2.0;

            Assert.Equal(MeasuredReboundEdgeToEdgeSeconds, halfPeriod, Precision);
        }

        [Fact]
        public void PeriodSecondsFor_AcrossTheWholeKnob_SpansTheDocumentedNineToOneRatio()
        {
            Assert.Equal(
                LightingSpeedScale.SpeedSpanRatio,
                LightingSpeedScale.SlowestPeriodSeconds / LightingSpeedScale.FastestPeriodSeconds,
                Precision);
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
            Assert.Equal(
                1.0 / LightingSpeedScale.FastestPeriodSeconds,
                LightingSpeedScale.CyclesPerSecond(LayerLightingState.MaximumSpeed),
                Precision);
            Assert.Equal(0.125, LightingSpeedScale.CyclesPerSecond(LayerLightingState.DefaultSpeed), Precision);
        }
    }
}
