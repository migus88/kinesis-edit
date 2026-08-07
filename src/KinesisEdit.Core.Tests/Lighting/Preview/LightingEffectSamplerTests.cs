using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.Core.Tests.Lighting.Preview
{
    /// <summary>
    /// Pins what the preview engine promises: determinism (the same instant always samples to the
    /// same cells, pseudo-random effects included), that direction and speed are honoured, that
    /// the paint layer's opacity follows the mode, and that every <see cref="LightingMode"/> —
    /// including Disable and the reserved Pitch Black — produces a meaningful frame.
    /// </summary>
    public class LightingEffectSamplerTests
    {
        private const double GridColumns = 4.0;
        private const double GridRows = 3.0;

        [Theory]
        [InlineData(LightingMode.Starlight)]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Reactive)]
        [InlineData(LightingMode.Rain)]
        [InlineData(LightingMode.Fireball)]
        public void Sample_CalledTwiceAtTheSameInstant_ProducesIdenticalCells(LightingMode mode)
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(mode, keys);

            AssertSameCells(sampler.Sample(state, keys, 2.5), sampler.Sample(state, keys, 2.5));
        }

        [Fact]
        public void Sample_WithAPseudoRandomEffect_LightsTheSameKeyIdenticallyOnEveryReplay()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(LightingMode.Starlight, keys);

            var first = sampler.Sample(state, keys, 2.5);
            var replay = new LightingEffectSampler().Sample(state, keys, 2.5);

            Assert.NotEmpty(first.Cells);
            AssertSameCells(first, replay);
        }

        [Fact]
        public void Sample_WithTheDirectionReversed_ReversesTheGradient()
        {
            var sampler = new LightingEffectSampler();
            var keys = new[]
            {
                new LightingPreviewKey(1, 0.25, 0.5),
                new LightingPreviewKey(2, 0.75, 0.5)
            };
            var travellingRight = CreateState(LightingMode.Wave);
            var travellingLeft = CreateState(LightingMode.Wave);

            travellingRight.Direction = LightingDirection.Right;
            travellingLeft.Direction = LightingDirection.Left;

            var right = sampler.Sample(travellingRight, keys, 0.3);
            var left = sampler.Sample(travellingLeft, keys, 0.3);

            Assert.NotEqual(right.Cells[1].Color, right.Cells[2].Color);
            Assert.Equal(right.Cells[1].Color, left.Cells[2].Color);
            Assert.Equal(right.Cells[2].Color, left.Cells[1].Color);
        }

        [Fact]
        public void Sample_WithTheVerticalDirectionReversed_ReversesTheGradientToo()
        {
            var sampler = new LightingEffectSampler();
            var keys = new[]
            {
                new LightingPreviewKey(1, 0.5, 0.25),
                new LightingPreviewKey(2, 0.5, 0.75)
            };
            var travellingDown = CreateState(LightingMode.Wave);
            var travellingUp = CreateState(LightingMode.Wave);

            travellingDown.Direction = LightingDirection.Down;
            travellingUp.Direction = LightingDirection.Up;

            var down = sampler.Sample(travellingDown, keys, 0.3);
            var up = sampler.Sample(travellingUp, keys, 0.3);

            Assert.NotEqual(down.Cells[1].Color, down.Cells[2].Color);
            Assert.Equal(down.Cells[1].Color, up.Cells[2].Color);
            Assert.Equal(down.Cells[2].Color, up.Cells[1].Color);
        }

        [Fact]
        public void Sample_WithADirectionTheModeDoesNotAccept_FallsBackToLeft()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var invalid = CreateState(LightingMode.Rebound);
            var left = CreateState(LightingMode.Rebound);

            // Rebound accepts left (horizontal) and up (vertical) only (specs/07-lighting.md §3).
            invalid.Direction = LightingDirection.Right;
            left.Direction = LightingDirection.Left;

            AssertSameCells(sampler.Sample(left, keys, 0.7), sampler.Sample(invalid, keys, 0.7));
        }

        [Fact]
        public void Sample_AtAFasterSpeed_HasTravelledFurtherThroughTheCycle()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var slow = CreateState(LightingMode.Pulse);
            var fast = CreateState(LightingMode.Pulse);

            slow.Speed = LayerLightingState.MinimumSpeed;
            fast.Speed = LayerLightingState.MaximumSpeed;

            // Half of the fastest period: the fast layer is at the peak of its fade, the slow one
            // has barely begun.
            var elapsedSeconds = LightingSpeedScale.PeriodSecondsFor(LayerLightingState.MaximumSpeed) / 2.0;
            var fastCell = sampler.Sample(fast, keys, elapsedSeconds).Cells[1];
            var slowCell = sampler.Sample(slow, keys, elapsedSeconds).Cells[1];

            Assert.Equal(1.0, fastCell.Intensity, 9);
            Assert.True(
                slowCell.Intensity < 0.2,
                $"The slow layer should barely have started, but its intensity is {slowCell.Intensity}.");
        }

        [Fact]
        public void Sample_OverAFullCycle_ReturnsToWhereItStarted()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreateState(LightingMode.Spectrum);

            var start = sampler.Sample(state, keys, 0.0);
            var oneCycleLater = sampler.Sample(state, keys, LightingSpeedScale.PeriodSecondsFor(state.Speed) * 4.0);

            AssertSameCells(start, oneCycleLater);
        }

        [Theory]
        [InlineData(LightingMode.Freestyle, LightingEffectFrame.PaintOpacityDirect)]
        [InlineData(LightingMode.Breathe, LightingEffectFrame.PaintOpacityDirect)]
        [InlineData(LightingMode.FrozenWave, LightingEffectFrame.PaintOpacityDirect)]
        [InlineData(LightingMode.Monochrome, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Reactive, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Ripple, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Starlight, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Wave, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Spectrum, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Fireball, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Rebound, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Loop, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Pulse, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Rain, LightingEffectFrame.PaintOpacityDimmed)]
        [InlineData(LightingMode.Disabled, LightingEffectFrame.PaintOpacityHidden)]
        [InlineData(LightingMode.PitchBlack, LightingEffectFrame.PaintOpacityHidden)]
        public void Sample_PerMode_ReportsThePaintLayersOpacity(LightingMode mode, double expectedOpacity)
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();

            Assert.Equal(expectedOpacity, sampler.Sample(CreatePaintedState(mode, keys), keys, 1.1).PaintOpacity);
        }

        [Fact]
        public void Sample_WithDisabled_LightsNothingEvenWhenTheLayerIsFullyPainted()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(LightingMode.Disabled, keys);

            var frame = sampler.Sample(state, keys, 1.7);

            Assert.Empty(frame.Cells);
            Assert.Equal(LightingEffectFrame.PaintOpacityHidden, frame.PaintOpacity);
        }

        [Fact]
        public void Sample_WithPitchBlack_LightsEveryKeyBlackAtFullIntensity()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreateState(LightingMode.PitchBlack);

            var frame = sampler.Sample(state, keys, 1.7);

            Assert.Equal(keys.Count, frame.Cells.Count);

            foreach (var key in keys)
            {
                Assert.Equal(LedColor.Black, frame.Cells[key.KeyCode].Color);
                Assert.Equal(1.0, frame.Cells[key.KeyCode].Intensity);
            }
        }

        [Theory]
        [InlineData(LightingMode.Freestyle)]
        [InlineData(LightingMode.FrozenWave)]
        public void Sample_WithASteadyPaintMode_UsesThePaintedColorAndLeavesAnUnpaintedKeyUnlit(LightingMode mode)
        {
            var sampler = new LightingEffectSampler();
            var keys = new[]
            {
                new LightingPreviewKey(1, 0.25, 0.5),
                new LightingPreviewKey(2, 0.75, 0.5)
            };
            var painted = new LedColor(12, 200, 90);
            var state = CreateState(mode);

            state.SetKeyColor(1, painted);

            var frame = sampler.Sample(state, keys, 0.9);

            Assert.Equal(painted, frame.Cells[1].Color);
            Assert.Equal(1.0, frame.Cells[1].Intensity);
            Assert.False(frame.Cells.ContainsKey(2));
        }

        [Theory]
        [InlineData(LightingMode.Freestyle)]
        [InlineData(LightingMode.Breathe)]
        [InlineData(LightingMode.FrozenWave)]
        public void Sample_WithAPaintDirectMode_NeverLightsAnUnpaintedKey(LightingMode mode)
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreateState(mode);
            var painted = new LedColor(255, 0, 128);

            state.SetKeyColor(keys[0].KeyCode, painted);

            for (var step = 0; step < 40; step++)
            {
                var frame = sampler.Sample(state, keys, step * 0.13);

                foreach (var key in keys.Skip(1))
                {
                    Assert.False(
                        frame.Cells.ContainsKey(key.KeyCode),
                        $"{mode} lit unpainted key {key.KeyCode} at step {step}.");
                }

                if (frame.Cells.TryGetValue(keys[0].KeyCode, out var cell))
                {
                    Assert.Equal(painted, cell.Color);
                }
            }
        }

        [Fact]
        public void Sample_WithBreathe_FadesThePaintedColorsRatherThanHoldingThem()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(LightingMode.Breathe, keys);

            var intensities = new HashSet<double>();

            for (var step = 0; step < 20; step++)
            {
                var frame = sampler.Sample(state, keys, step * 0.11);

                if (frame.Cells.TryGetValue(keys[0].KeyCode, out var cell))
                {
                    intensities.Add(cell.Intensity);
                }
            }

            Assert.True(intensities.Count > 1, "Breathe should fade rather than hold one intensity.");
        }

        [Fact]
        public void Sample_WithMonochrome_FillsEveryKeyWithTheEffectColorAndIgnoresThePaint()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(LightingMode.Monochrome, keys);
            var effectColor = new LedColor(0, 128, 255);

            state.EffectColor = effectColor;

            var frame = sampler.Sample(state, keys, 1.3);

            Assert.Equal(keys.Count, frame.Cells.Count);

            foreach (var key in keys)
            {
                Assert.Equal(effectColor, frame.Cells[key.KeyCode].Color);
                Assert.Equal(1.0, frame.Cells[key.KeyCode].Intensity);
            }
        }

        [Theory]
        [InlineData(LightingMode.Reactive)]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Starlight)]
        public void Sample_WithTheTwoLinePaintIgnoringModes_LightsTheEffectColorAndNeverThePaint(LightingMode mode)
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(mode, keys);
            var effectColor = new LedColor(240, 16, 32);
            var lit = false;

            state.EffectColor = effectColor;

            for (var step = 0; step < 60; step++)
            {
                var frame = sampler.Sample(state, keys, step * 0.09);

                foreach (var (_, cell) in frame.Cells)
                {
                    // The base color is black, so every lit key is the effect color at some alpha.
                    Assert.Equal(effectColor, cell.Color);
                    lit = true;
                }
            }

            Assert.True(lit, $"{mode} never lit a key across several cycles.");
        }

        [Fact]
        public void Sample_WithAPaintIgnoringModeOverANonBlackBase_LightsEveryKey()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreateState(LightingMode.Loop);

            state.BaseColor = new LedColor(20, 20, 60);

            var frame = sampler.Sample(state, keys, 0.44);

            Assert.Equal(keys.Count, frame.Cells.Count);
        }

        [Fact]
        public void Sample_WithAPaintIgnoringModeOverABlackBase_LeavesTheUnreachedKeysHatched()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(LightingMode.Loop, keys);

            // The default base color is black, which is "no color" (§2.1): only the sweep is lit.
            Assert.Equal(LedColor.Black, state.BaseColor);

            var frame = sampler.Sample(state, keys, 0.44);

            Assert.InRange(frame.Cells.Count, 1, keys.Count - 1);
        }

        [Fact]
        public void Sample_WithANonFiniteElapsedTime_IsTreatedAsZero()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var state = CreatePaintedState(LightingMode.Wave, keys);

            AssertSameCells(sampler.Sample(state, keys, 0.0), sampler.Sample(state, keys, double.NaN));
            AssertSameCells(sampler.Sample(state, keys, 0.0), sampler.Sample(state, keys, double.PositiveInfinity));
        }

        [Fact]
        public void Sample_ForEveryLightingMode_ProducesAWellFormedFrame()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();
            var elapsedTimes = new[] { 0.0, 0.37, 1.0, 2.5, 9.75, -1.4 };

            foreach (var mode in Enum.GetValues<LightingMode>())
            {
                var state = CreatePaintedState(mode, keys);

                foreach (var elapsedSeconds in elapsedTimes)
                {
                    var frame = sampler.Sample(state, keys, elapsedSeconds);

                    Assert.Contains(
                        frame.PaintOpacity,
                        new[]
                        {
                            LightingEffectFrame.PaintOpacityDirect,
                            LightingEffectFrame.PaintOpacityDimmed,
                            LightingEffectFrame.PaintOpacityHidden
                        });

                    foreach (var (keyCode, cell) in frame.Cells)
                    {
                        Assert.Contains(keys, key => key.KeyCode == keyCode);
                        Assert.InRange(cell.Intensity, LightingPreviewCell.MinimumVisibleIntensity, 1.0);
                    }
                }
            }
        }

        [Fact]
        public void Sample_ForEveryModeExceptDisable_LightsSomethingAtSomePointInTheCycle()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();

            foreach (var mode in Enum.GetValues<LightingMode>())
            {
                if (mode == LightingMode.Disabled)
                {
                    continue;
                }

                var state = CreatePaintedState(mode, keys);
                var lit = false;

                // Several cycles' worth: the pseudo-random effects reseed once per cycle.
                for (var step = 0; step < 120 && !lit; step++)
                {
                    lit = sampler.Sample(state, keys, step * 0.05).Cells.Count > 0;
                }

                Assert.True(lit, $"{mode} never lit a key across a whole cycle.");
            }
        }

        [Fact]
        public void Sample_WithNullArguments_Throws()
        {
            var sampler = new LightingEffectSampler();
            var keys = CreateGrid();

            Assert.Throws<ArgumentNullException>(() => sampler.Sample(null!, keys, 0.0));
            Assert.Throws<ArgumentNullException>(() => sampler.Sample(CreateState(LightingMode.Wave), null!, 0.0));
        }

        [Fact]
        public void Sample_WithNoKeys_ProducesAnEmptyFrameWithTheModesPaintOpacity()
        {
            var sampler = new LightingEffectSampler();

            var frame = sampler.Sample(CreateState(LightingMode.Wave), [], 1.0);

            Assert.Empty(frame.Cells);
            Assert.Equal(LightingEffectFrame.PaintOpacityDimmed, frame.PaintOpacity);
        }

        private static LayerLightingState CreateState(LightingMode mode)
        {
            return new LayerLightingState { Mode = mode };
        }

        private static LayerLightingState CreatePaintedState(LightingMode mode, IReadOnlyList<LightingPreviewKey> keys)
        {
            var state = CreateState(mode);

            foreach (var key in keys)
            {
                state.SetKeyColor(key.KeyCode, new LedColor((byte)(10 + key.KeyCode), 180, 64));
            }

            return state;
        }

        private static IReadOnlyList<LightingPreviewKey> CreateGrid()
        {
            var keys = new List<LightingPreviewKey>();
            var keyCode = 1;

            for (var row = 0; row < GridRows; row++)
            {
                for (var column = 0; column < GridColumns; column++)
                {
                    keys.Add(new LightingPreviewKey(
                        keyCode++,
                        (column + 0.5) / GridColumns,
                        (row + 0.5) / GridRows));
                }
            }

            return keys;
        }

        private static void AssertSameCells(LightingEffectFrame expected, LightingEffectFrame actual)
        {
            Assert.Equal(expected.PaintOpacity, actual.PaintOpacity);
            Assert.Equal(expected.Cells.Count, actual.Cells.Count);

            foreach (var (keyCode, cell) in expected.Cells)
            {
                Assert.True(actual.Cells.TryGetValue(keyCode, out var other), $"Key {keyCode} is missing.");
                Assert.Equal(cell.Color, other.Color);
                Assert.Equal(cell.Intensity, other.Intensity);
            }
        }
    }
}
