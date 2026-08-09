using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.Core.Tests.Lighting.Preview
{
    /// <summary>
    /// Pins the speed curve: the three measured anchors (10 s at speed 1, 6 s at the default 5, 2 s
    /// at speed 9), that the ramp between them is linear rather than geometric, that it is strictly
    /// monotonic across the 1-9 range <see cref="LayerLightingState"/> declares, and that
    /// out-of-range input clamps rather than running off the end.
    /// </summary>
    public class LightingSpeedScaleTests
    {
        private const int Precision = 9;

        /// <summary>
        /// The three hardware measurements the whole scale is calibrated on: a Rebound bar crosses
        /// the Freestyle Edge RGB's board edge to edge in these many seconds at speeds 1, 5 and 9.
        /// Rebound is a triangle wave, so each is <i>half</i> a cycle.
        /// </summary>
        public static TheoryData<int, double> MeasuredReboundEdgeToEdgeSeconds => new()
        {
            { LayerLightingState.MinimumSpeed, 5.0 },
            { LayerLightingState.DefaultSpeed, 3.0 },
            { LayerLightingState.MaximumSpeed, 1.0 },
        };

        [Theory]
        [InlineData(LayerLightingState.MinimumSpeed, 10.0)]
        [InlineData(LayerLightingState.DefaultSpeed, 6.0)]
        [InlineData(LayerLightingState.MaximumSpeed, 2.0)]
        public void PeriodSecondsFor_AtTheMeasuredAnchors_MatchesThem(int speed, double expectedSeconds)
        {
            Assert.Equal(expectedSeconds, LightingSpeedScale.PeriodSecondsFor(speed), Precision);
        }

        /// <summary>
        /// Rebound travels as a triangle wave, so edge-to-edge is half a cycle. Half the period at
        /// each measured speed must therefore be what was timed on the hardware; the behavioural
        /// half of this pin lives in <c>LightingEffectSamplerTests</c>.
        /// </summary>
        [Theory]
        [MemberData(nameof(MeasuredReboundEdgeToEdgeSeconds))]
        public void PeriodSecondsFor_Halved_IsTheMeasuredReboundEdgeToEdgeTime(int speed, double measuredSeconds)
        {
            var halfPeriod = LightingSpeedScale.PeriodSecondsFor(speed) / 2.0;

            Assert.Equal(measuredSeconds, halfPeriod, Precision);
        }

        /// <summary>
        /// The end points, stated on their own so a future recalibration has to face them rather
        /// than inherit them from whichever curve happens to be in the file.
        /// </summary>
        [Fact]
        public void TheAnchors_AreTheMeasuredPeriods()
        {
            Assert.Equal(10.0, LightingSpeedScale.SlowestPeriodSeconds, Precision);
            Assert.Equal(2.0, LightingSpeedScale.FastestPeriodSeconds, Precision);
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

        /// <summary>
        /// The curve is <b>linear</b>: every step of the knob takes the same number of seconds off
        /// the period. It used to be geometric — the same multiplier per step — and the third
        /// measurement is what replaced it: a geometric ramp anchored at 10 s and 2 s puts speed 5
        /// at 4.47 s, and the board says 6 s.
        /// </summary>
        [Fact]
        public void PeriodSecondsFor_EveryStep_TakesOffTheSameNumberOfSeconds()
        {
            var firstStep = LightingSpeedScale.PeriodSecondsFor(1) - LightingSpeedScale.PeriodSecondsFor(2);

            Assert.Equal(1.0, firstStep, Precision);

            for (var speed = LayerLightingState.MinimumSpeed + 1; speed < LayerLightingState.MaximumSpeed; speed++)
            {
                var step = LightingSpeedScale.PeriodSecondsFor(speed) - LightingSpeedScale.PeriodSecondsFor(speed + 1);

                Assert.Equal(firstStep, step, Precision);
            }
        }

        /// <summary>
        /// The whole curve in one line — <c>period = 11 − speed</c> — which is the fit through the
        /// three measurements and the cheapest way to state what this class does.
        /// </summary>
        [Fact]
        public void PeriodSecondsFor_AcrossTheRange_IsElevenMinusTheSpeed()
        {
            for (var speed = LayerLightingState.MinimumSpeed; speed <= LayerLightingState.MaximumSpeed; speed++)
            {
                Assert.Equal(11.0 - speed, LightingSpeedScale.PeriodSecondsFor(speed), Precision);
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
            Assert.Equal(1.0 / 6.0, LightingSpeedScale.CyclesPerSecond(LayerLightingState.DefaultSpeed), Precision);
        }
    }
}
