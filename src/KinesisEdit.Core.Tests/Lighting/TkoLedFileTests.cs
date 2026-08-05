using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the TKO led-file handling of specs/07-lighting.md §1.4 and §2.3: edge lines are
    /// split out before key-backlight parsing, the edge section is appended after the
    /// key-backlight section on save, and each edge mode follows its own grammar (left/right
    /// only for wave/loop, no direction for rebound, bare frozen wave).
    /// </summary>
    public class TkoLedFileTests
    {
        [Fact]
        public void ParseTko_WithInterleavedKeyAndEdgeLines_ParsesTheSectionsIndependently()
        {
            var lines = new[]
            {
                "[wave]>[spd4][dirright]",
                "[mono_edge]>[255][0][0]",
                "fn [pulse]>[spd2]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Wave, model.KeyBacklight.TopLayer.Mode);
            Assert.Equal(LightingDirection.Right, model.KeyBacklight.TopLayer.Direction);
            Assert.Equal(LightingMode.Pulse, model.KeyBacklight.FnLayer.Mode);
            Assert.Equal(LightingMode.Monochrome, model.Edge.TopLayer.Mode);
            Assert.Equal(new LedColor(255, 0, 0), model.Edge.TopLayer.EffectColor);
        }

        [Fact]
        public void ParseTko_WithPerLedFreestyleLines_ResolvesTheEdgeAddressTokens()
        {
            var lines = new[]
            {
                "[l1]>[255][0][0]",
                "[b15]>[0][255][0]",
                "[r9]>[0][0][255]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Disabled, model.KeyBacklight.TopLayer.Mode);
            Assert.Equal(LightingMode.Freestyle, model.Edge.TopLayer.Mode);
            Assert.Equal(3, model.Edge.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(255, 0, 0), model.Edge.TopLayer.KeyColors[CodeOf("L1")]);
            Assert.Equal(new LedColor(0, 255, 0), model.Edge.TopLayer.KeyColors[CodeOf("B15")]);
            Assert.Equal(new LedColor(0, 0, 255), model.Edge.TopLayer.KeyColors[CodeOf("R9")]);
        }

        [Fact]
        public void ParseTko_WithNonExistentEdgeAddress_ClassifiesButIgnoresIt()
        {
            // The classifier tolerates N = 1..30, but only 33 LEDs exist (§2.3): the line is
            // routed to the edge section and dropped there because the token resolves no LED.
            var model = LedFileParser.ParseTko(["[l15]>[255][0][0]", "[l1]>[0][255][0]"]);

            Assert.Equal(LightingMode.Freestyle, model.Edge.TopLayer.Mode);
            var ledColor = Assert.Single(model.Edge.TopLayer.KeyColors);
            Assert.Equal(CodeOf("L1"), ledColor.Key);
        }

        [Fact]
        public void ParseTko_WithFnPrefixedEdgeLines_FillsTheEdgeFnLayer()
        {
            var lines = new[]
            {
                "fn [breathe_edge]>[spd6]",
                "fn [r3]>[1][2][3]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Disabled, model.Edge.TopLayer.Mode);
            Assert.Equal(LightingMode.Breathe, model.Edge.FnLayer.Mode);
            Assert.Equal(6, model.Edge.FnLayer.Speed);
            Assert.Equal(new LedColor(1, 2, 3), model.Edge.FnLayer.KeyColors[CodeOf("R3")]);
        }

        [Fact]
        public void ParseTko_WithWaveEdgeDirectionOutsideLeftRight_FallsBackToLeft()
        {
            var model = LedFileParser.ParseTko(["[wave_edge]>[spd5][dirup]"]);

            Assert.Equal(LightingMode.Wave, model.Edge.TopLayer.Mode);
            Assert.Equal(LightingDirection.Left, model.Edge.TopLayer.Direction);
        }

        [Fact]
        public void ParseTko_WithFrozenWaveEdge_ReadsBareTokenAndPerLedLines()
        {
            var lines = new[]
            {
                "[frozenwave_edge]",
                "[l2]>[255][0][0]",
                "[b1]>[0][255][0]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.FrozenWave, model.Edge.TopLayer.Mode);
            Assert.Equal(2, model.Edge.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(255, 0, 0), model.Edge.TopLayer.KeyColors[CodeOf("L2")]);
        }

        [Fact]
        public void ParseTko_WithReboundEdge_ReadsBaseAndEffectButNoDirection()
        {
            var lines = new[]
            {
                "[mono_edge]>[9][8][7]",
                "[rebound_edge]>[0][255][0][spd3][dirright]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Rebound, model.Edge.TopLayer.Mode);
            Assert.Equal(new LedColor(9, 8, 7), model.Edge.TopLayer.BaseColor);
            Assert.Equal(new LedColor(0, 255, 0), model.Edge.TopLayer.EffectColor);
            Assert.Equal(3, model.Edge.TopLayer.Speed);

            // Rebound has no direction on the edge (§2.3): the token is ignored, the default stays.
            Assert.Equal(LightingDirection.Left, model.Edge.TopLayer.Direction);
        }

        [Fact]
        public void ParseTko_WithLoopEdge_AcceptsLeftAndRightOnly()
        {
            var valid = LedFileParser.ParseTko(["[mono_edge]>[0][0][0]", "[loop_edge]>[1][2][3][spd4][dirright]"]);
            var invalid = LedFileParser.ParseTko(["[mono_edge]>[0][0][0]", "[loop_edge]>[1][2][3][spd4][dirdown]"]);

            Assert.Equal(LightingDirection.Right, valid.Edge.TopLayer.Direction);
            Assert.Equal(LightingDirection.Left, invalid.Edge.TopLayer.Direction);
        }

        [Theory]
        [InlineData("[spectrum_edge]>[spd8]", LightingMode.Spectrum, 8)]
        [InlineData("[pulse_edge]>[spd2]", LightingMode.Pulse, 2)]
        [InlineData("[breathe_edge]>[spd6]", LightingMode.Breathe, 6)]
        public void ParseTko_WithSingleLineEdgeModes_ReadsModeAndSpeed(
            string line,
            LightingMode expectedMode,
            int expectedSpeed)
        {
            var model = LedFileParser.ParseTko([line]);

            Assert.Equal(expectedMode, model.Edge.TopLayer.Mode);
            Assert.Equal(expectedSpeed, model.Edge.TopLayer.Speed);
        }

        [Fact]
        public void ParseTko_WithMonoEdgeFillAmongPerLedLines_SetsAllThirtyThreeLeds()
        {
            var lines = new[]
            {
                "[mono_edge]>[255][0][0]",
                "[r1]>[0][0][255]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Freestyle, model.Edge.TopLayer.Mode);
            Assert.Equal(33, model.Edge.TopLayer.KeyColors.Count);
            Assert.Equal(new LedColor(0, 0, 255), model.Edge.TopLayer.KeyColors[CodeOf("R1")]);
            Assert.Equal(new LedColor(255, 0, 0), model.Edge.TopLayer.KeyColors[CodeOf("L1")]);
        }

        [Fact]
        public void SerializeTko_WithBothSections_AppendsTheEdgeSectionAfterTheKeySection()
        {
            var model = new TkoLightingModel();

            model.KeyBacklight.TopLayer.Mode = LightingMode.Wave;
            model.KeyBacklight.FnLayer.Mode = LightingMode.Pulse;
            model.Edge.TopLayer.Mode = LightingMode.Monochrome;
            model.Edge.TopLayer.EffectColor = new LedColor(255, 0, 0);
            model.Edge.FnLayer.Mode = LightingMode.Spectrum;

            Assert.Equal(
                new[]
                {
                    "[wave]>[spd5][dirleft]",
                    "fn [pulse]>[spd5]",
                    "[mono_edge]>[255][0][0]",
                    "fn [spectrum_edge]>[spd5]"
                },
                LedFileSerializer.SerializeTko(model));
        }

        [Fact]
        public void SerializeTko_WithFrozenWaveEdge_WritesTheBareTokenAndPerLedLines()
        {
            var model = new TkoLightingModel();

            model.Edge.TopLayer.Mode = LightingMode.FrozenWave;
            model.Edge.TopLayer.SetKeyColor(CodeOf("B2"), new LedColor(4, 5, 6));
            model.Edge.TopLayer.SetKeyColor(CodeOf("L1"), new LedColor(1, 2, 3));

            Assert.Equal(
                new[]
                {
                    "[frozenwave_edge]",
                    "[L1]>[1][2][3]",
                    "[B2]>[4][5][6]"
                },
                LedFileSerializer.SerializeTko(model));
        }

        [Fact]
        public void SerializeTko_WithReboundEdge_WritesNoDirectionToken()
        {
            var model = new TkoLightingModel();

            model.Edge.TopLayer.Mode = LightingMode.Rebound;
            model.Edge.TopLayer.EffectColor = new LedColor(0, 255, 0);
            model.Edge.TopLayer.BaseColor = new LedColor(9, 8, 7);
            model.Edge.TopLayer.Speed = 3;
            model.Edge.TopLayer.Direction = LightingDirection.Up;

            Assert.Equal(
                new[]
                {
                    "[mono_edge]>[9][8][7]",
                    "[rebound_edge]>[0][255][0][spd3]"
                },
                LedFileSerializer.SerializeTko(model));
        }

        [Fact]
        public void SerializeTko_WithEdgeModeTheEdgeCannotExpress_WritesNothingForIt()
        {
            var model = new TkoLightingModel();

            // Reactive has no _edge token (§2.3) — an inexpressible edge mode serializes empty.
            model.Edge.TopLayer.Mode = LightingMode.Reactive;

            Assert.Empty(LedFileSerializer.SerializeTko(model));
        }

        [Fact]
        public void ParseTko_ThenSerializeAndReparse_RoundTripsAMixedFile()
        {
            var lines = new[]
            {
                "[breathe]>[spd7]",
                "[s]>[0][255][0]",
                "fn [mono]>[10][20][30]",
                "[wave_edge]>[spd2][dirright]",
                "fn [l5]>[1][2][3]",
                "fn [b7]>[4][5][6]"
            };

            var firstParse = LedFileParser.ParseTko(lines);
            var serialized = LedFileSerializer.SerializeTko(firstParse);
            var secondParse = LedFileParser.ParseTko(serialized);

            Assert.True(firstParse.IsEquivalentTo(secondParse));
        }

        [Fact]
        public void ParseTko_WithFnLayerMediaAndFunctionKeys_NeverAppliesTheFnTokenTranslation()
        {
            // Spec 05 §5.5 scopes the Fn save-token exceptions to the FS-family bottom layer;
            // the TKO top layer has no F-row/pause/del positions, so nothing is translated.
            var lines = new[]
            {
                "fn [mute]>[7][8][9]",
                "fn [F4]>[1][2][3]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Freestyle, model.KeyBacklight.FnLayer.Mode);
            Assert.Equal(2, model.KeyBacklight.FnLayer.KeyColors.Count);
            Assert.True(model.KeyBacklight.FnLayer.KeyColors.ContainsKey(CodeOf("mute")));
            Assert.True(model.KeyBacklight.FnLayer.KeyColors.ContainsKey(CodeOf("F4")));
            Assert.False(model.KeyBacklight.FnLayer.KeyColors.ContainsKey(CodeOf("play")));
        }

        [Fact]
        public void SerializeTko_WithFnLayerMediaKey_WritesItsOwnTokenNotTheTranslatedOne()
        {
            var model = new TkoLightingModel();

            model.KeyBacklight.FnLayer.Mode = LightingMode.Freestyle;
            model.KeyBacklight.FnLayer.SetKeyColor(CodeOf("mute"), new LedColor(7, 8, 9));

            Assert.Equal(new[] { "fn [mute]>[7][8][9]" }, LedFileSerializer.SerializeTko(model));
        }

        [Fact]
        public void ParseTko_WithKeyBacklightTwoLineEffect_UsesThePlainTokens()
        {
            var lines = new[]
            {
                "[mono]>[1][1][1]",
                "[rain]>[2][2][2][spd6]"
            };

            var model = LedFileParser.ParseTko(lines);

            Assert.Equal(LightingMode.Rain, model.KeyBacklight.TopLayer.Mode);
            Assert.Equal(new LedColor(1, 1, 1), model.KeyBacklight.TopLayer.BaseColor);
            Assert.Equal(LightingMode.Disabled, model.Edge.TopLayer.Mode);
        }

        private static int CodeOf(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)!.Code;
        }
    }
}
