using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.Core.Tests.Lighting.Preview
{
    /// <summary>
    /// Pins the mode-parameter descriptor the lighting rail draws from: that every fact is the
    /// one the catalog and <see cref="LightingAvailability"/> already hold (specs/07-lighting.md
    /// §3), that the base colour follows the LED ≥ 1.0.44 layer-customization gate, that the
    /// paint-direct set is exactly the five modes of <see cref="LightingPaintModes"/>, that a mode
    /// accepts paint exactly when its layer writes a file body at all, and the grammar of the
    /// one-line summary.
    /// </summary>
    public class LightingModeParametersTests
    {
        [Theory]
        [InlineData(LightingMode.Disabled, false, false, false, false, false, false, "—")]
        [InlineData(LightingMode.Freestyle, true, false, false, true, true, true, "color")]
        [InlineData(LightingMode.Monochrome, true, false, false, false, false, true, "color")]
        [InlineData(LightingMode.Breathe, true, false, true, true, true, true, "color · spd")]
        [InlineData(LightingMode.Spectrum, false, false, true, false, false, true, "spd")]
        [InlineData(LightingMode.Wave, false, false, true, false, false, true, "spd · D/L/U/R")]
        [InlineData(LightingMode.FrozenWave, true, false, false, true, true, true, "color")]
        [InlineData(LightingMode.Reactive, true, true, true, false, false, true, "2 colors · spd")]
        [InlineData(LightingMode.Ripple, true, true, true, false, false, true, "2 colors · spd")]
        [InlineData(LightingMode.Fireball, true, true, true, false, false, true, "2 colors · spd")]
        [InlineData(LightingMode.Starlight, true, true, true, false, false, true, "2 colors · spd")]
        [InlineData(LightingMode.Rebound, true, true, true, false, false, true, "2 colors · spd · L/U")]
        [InlineData(LightingMode.Loop, true, true, true, false, false, true, "2 colors · spd · D/L/U/R")]
        [InlineData(LightingMode.Pulse, false, false, true, false, false, true, "spd")]
        [InlineData(LightingMode.Rain, true, true, true, false, false, true, "2 colors · spd")]
        [InlineData(LightingMode.PitchBlack, false, false, false, false, false, false, "—")]
        public void For_OnTheRgbWithTheGateOpen_DerivesEveryModesParameterSet(
            LightingMode mode,
            bool acceptsEffectColor,
            bool acceptsBaseColor,
            bool acceptsSpeed,
            bool hasPerKeyColors,
            bool rendersPaintDirectly,
            bool acceptsPaint,
            string summary)
        {
            var parameters = LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, mode, true);

            Assert.Equal(mode, parameters.Mode);
            Assert.Equal(acceptsEffectColor, parameters.AcceptsEffectColor);
            Assert.Equal(acceptsBaseColor, parameters.AcceptsBaseColor);
            Assert.Equal(acceptsSpeed, parameters.AcceptsSpeed);
            Assert.Equal(hasPerKeyColors, parameters.HasPerKeyColors);
            Assert.Equal(rendersPaintDirectly, parameters.RendersPaintDirectly);

            // The whole column, not only the two false rows: the claim is that Off and Pitch Black
            // are the *only* modes a paint gesture cannot reach the file in (specs/07-lighting.md
            // §2.2), and a matrix that named just those two would pass with the answer inverted
            // everywhere else.
            Assert.Equal(acceptsPaint, parameters.AcceptsPaint);
            Assert.Equal(summary, parameters.Summary);
        }

        [Theory]
        [InlineData(LightingMode.Disabled, false, "—")]
        [InlineData(LightingMode.Freestyle, true, "color")]
        [InlineData(LightingMode.Monochrome, true, "color")]
        [InlineData(LightingMode.Breathe, true, "color · spd")]
        [InlineData(LightingMode.Spectrum, false, "spd")]
        [InlineData(LightingMode.Wave, false, "spd · D/L/U/R")]
        [InlineData(LightingMode.Reactive, true, "color · spd")]
        [InlineData(LightingMode.Ripple, true, "color · spd")]
        [InlineData(LightingMode.Fireball, true, "color · spd")]
        [InlineData(LightingMode.Starlight, true, "color · spd")]
        [InlineData(LightingMode.Rebound, true, "color · spd · L/U")]
        [InlineData(LightingMode.Loop, true, "color · spd · D/L/U/R")]
        [InlineData(LightingMode.Pulse, false, "spd")]
        [InlineData(LightingMode.Rain, true, "color · spd")]
        [InlineData(LightingMode.PitchBlack, false, "—")]
        public void For_OnTheRgbWithTheGateClosed_DropsTheBaseColorFromTheSetAndTheSummary(
            LightingMode mode,
            bool acceptsEffectColor,
            string summary)
        {
            var parameters = LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, mode, false);

            Assert.False(parameters.AcceptsBaseColor);
            Assert.Equal(acceptsEffectColor, parameters.AcceptsEffectColor);
            Assert.Equal(summary, parameters.Summary);
        }

        [Theory]
        [InlineData(LightingMode.Reactive)]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        [InlineData(LightingMode.Starlight)]
        [InlineData(LightingMode.Rebound)]
        [InlineData(LightingMode.Loop)]
        [InlineData(LightingMode.Rain)]
        public void For_WithTheTwoLineEffects_TiesTheBaseColorToTheLayerCustomizationGate(LightingMode mode)
        {
            Assert.True(LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, mode, true).AcceptsBaseColor);
            Assert.False(LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, mode, false).AcceptsBaseColor);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage2)]
        public void For_ForEveryMode_ReportsTheDirectionsAvailabilityAlreadyAnswers(DeviceId deviceId)
        {
            foreach (var mode in Enum.GetValues<LightingMode>())
            {
                var parameters = LightingModeParameters.For(deviceId, mode, true);

                Assert.Equal(
                    LightingAvailability.GetKeyBacklightDirections(deviceId, mode),
                    parameters.Directions);
                Assert.Equal(parameters.Directions.Count > 0, parameters.AcceptsDirection);
            }
        }

        [Fact]
        public void For_OnTheTko_KeepsFireballsDirectionsWhereTheRgbHasNone()
        {
            // §3: "TKO also shows it for Fireball (left/right only)".
            var onRgb = LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, LightingMode.Fireball, true);
            var onTko = LightingModeParameters.For(DeviceId.Tko, LightingMode.Fireball, true);

            Assert.Empty(onRgb.Directions);
            Assert.Equal(new[] { LightingDirection.Left, LightingDirection.Right }, onTko.Directions);
            Assert.Equal("2 colors · spd", onRgb.Summary);
            Assert.Equal("2 colors · spd · L/R", onTko.Summary);
        }

        [Fact]
        public void RenderedDirectly_Always_IsExactlyTheCatalogRowsWithPerKeyColors()
        {
            // The invariant, not a list of names: a mode renders the paint exactly when its file
            // body is per-key colour lines. Mockup 2f's "Solid, Reactive, Ripple and Starlight"
            // is wrong about the hardware — those four write no per-key line at all.
            var expected = LightingModeCatalog.All
                .Where(definition => definition.HasPerKeyColors)
                .Select(definition => definition.Mode)
                .OrderBy(mode => mode);

            Assert.Equal(expected, LightingPaintModes.RenderedDirectly.OrderBy(mode => mode));
        }

        [Fact]
        public void RendersPaintDirectly_AcrossEveryMode_TracksHasPerKeyColors()
        {
            foreach (var mode in Enum.GetValues<LightingMode>())
            {
                var parameters = LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, mode, true);

                Assert.Equal(parameters.HasPerKeyColors, parameters.RendersPaintDirectly);
            }
        }

        [Fact]
        public void RenderedDirectly_Always_IsFreestyleBreatheAndFrozenWave()
        {
            Assert.Equal(
                new[] { LightingMode.Freestyle, LightingMode.Breathe, LightingMode.FrozenWave }.OrderBy(mode => mode),
                LightingPaintModes.RenderedDirectly.OrderBy(mode => mode));
        }

        [Theory]
        [InlineData(LightingMode.Monochrome)]
        [InlineData(LightingMode.Reactive)]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Starlight)]
        public void RendersPaintDirectly_ForTheModesMockup2fNames_IsFalseBecauseTheyWriteNoPerKeyLines(LightingMode mode)
        {
            Assert.False(LightingModeCatalog.Find(mode).HasPerKeyColors);
            Assert.False(LightingPaintModes.RendersPaintDirectly(mode));
            Assert.Equal(
                LightingEffectFrame.PaintOpacityDimmed,
                LightingPaintModes.PaintOpacityFor(mode));
        }

        [Fact]
        public void AcceptsPaint_ForEveryModeTheRgbOffers_IsExactlyWhenTheLayerWritesAFileBody()
        {
            // The invariant behind the flag, asserted against the serializer rather than against a
            // second list of names: a layer carrying a painted key writes nothing at all exactly
            // when the mode accepts no paint. §2.2 gives Disable an empty section and never writes
            // the reserved [black] token, so nothing about such a layer — its per-key colours
            // included — can be expressed, which is why the rail draws no colour picker in them.
            var painted = KeyRegistry.FindByToken("F4", TokenDialect.Gen1)!.Code;

            foreach (var definition in LightingModeCatalog.All)
            {
                // The RGB's own menu (§3), which is the set the rail can select. Frozen Wave is
                // left out on purpose: it is edge-only, and a mode the key-backlight context has
                // no token for writes nothing there for a reason that is about the context rather
                // than about the mode.
                if (!definition.OffersInRgbKeyBacklightMenu && definition.Mode != LightingMode.PitchBlack)
                {
                    continue;
                }

                var model = new LightingModel();

                model.TopLayer.Mode = definition.Mode;
                model.TopLayer.SetKeyColor(painted, new LedColor(87, 196, 216));

                var parameters = LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, definition.Mode, true);
                var lines = LedFileSerializer.SerializeRgb(model);

                Assert.Equal(parameters.AcceptsPaint, lines.Count > 0);
            }
        }

        [Fact]
        public void AcceptsPaint_OnEveryDeviceAndEitherGate_IsTheSameTwoModesAndImpliesTheSwatches()
        {
            // Two claims the rail leans on. First, the answer is the file format's, so no device
            // and no firmware gate moves it. Second — and this is what lets the swatch handler
            // scroll to a section that can now be collapsed — AcceptsAnyColor IMPLIES AcceptsPaint:
            // the two modes that write no body write no colour line either, so a swatch is never on
            // screen while the picker is not.
            foreach (var deviceId in Enum.GetValues<DeviceId>())
            {
                foreach (var mode in Enum.GetValues<LightingMode>())
                {
                    foreach (var gateOpen in new[] { true, false })
                    {
                        var parameters = LightingModeParameters.For(deviceId, mode, gateOpen);

                        Assert.Equal(
                            mode is not (LightingMode.Disabled or LightingMode.PitchBlack),
                            parameters.AcceptsPaint);

                        if (parameters.AcceptsAnyColor)
                        {
                            Assert.True(parameters.AcceptsPaint);
                        }
                    }
                }
            }
        }

        [Fact]
        public void AcceptsAnyColor_PerMode_IsTrueWheneverEitherSwatchApplies()
        {
            Assert.True(LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, LightingMode.Monochrome, true).AcceptsAnyColor);
            Assert.True(LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, LightingMode.Rain, false).AcceptsAnyColor);
            Assert.False(LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, LightingMode.Spectrum, true).AcceptsAnyColor);
            Assert.False(LightingModeParameters.For(DeviceId.FreestyleEdgeRgb, LightingMode.Disabled, true).AcceptsAnyColor);
        }

        [Fact]
        public void None_Always_AcceptsNothing()
        {
            var parameters = LightingModeParameters.None;

            Assert.Equal(LightingMode.Disabled, parameters.Mode);
            Assert.False(parameters.AcceptsEffectColor);
            Assert.False(parameters.AcceptsBaseColor);
            Assert.False(parameters.AcceptsSpeed);
            Assert.False(parameters.HasPerKeyColors);
            Assert.False(parameters.RendersPaintDirectly);

            // Including the picker: "no layer selected yet" is not a surface to paint on either.
            Assert.False(parameters.AcceptsPaint);
            Assert.Empty(parameters.Directions);
            Assert.Equal(LightingModeParameters.NoParametersSummary, parameters.Summary);
        }

        [Fact]
        public void For_ForEveryModeOnEveryDevice_ProducesANonEmptySummary()
        {
            foreach (var deviceId in Enum.GetValues<DeviceId>())
            {
                foreach (var mode in Enum.GetValues<LightingMode>())
                {
                    foreach (var gateOpen in new[] { true, false })
                    {
                        var summary = LightingModeParameters.For(deviceId, mode, gateOpen).Summary;

                        Assert.False(string.IsNullOrWhiteSpace(summary));
                    }
                }
            }
        }
    }
}
