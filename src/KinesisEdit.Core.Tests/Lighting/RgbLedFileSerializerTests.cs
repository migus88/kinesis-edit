using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Pins the canonical key-backlight serialization of specs/07-lighting.md §2.1-§2.2: value
    /// tokens in color → <c>[spdN]</c> → <c>[dirX]</c> order, base <c>[mono]</c> line first
    /// for two-line effects, registry-canonical per-key token casing, Fn-layer save-token
    /// exceptions, and the write-side default fallbacks for invalid speeds and directions.
    /// </summary>
    public class RgbLedFileSerializerTests
    {
        [Fact]
        public void SerializeRgb_WithDisabledLayers_WritesNothing()
        {
            Assert.Empty(LedFileSerializer.SerializeRgb(new LightingModel()));
        }

        [Fact]
        public void SerializeRgb_WithPitchBlack_WritesNothingBecauseTheTokenIsReserved()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.PitchBlack;

            Assert.Empty(LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithMonochrome_WritesTheColorLine()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Monochrome;
            model.TopLayer.EffectColor = new LedColor(255, 128, 0);

            Assert.Equal(new[] { "[mono]>[255][128][0]" }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithWave_WritesSpeedThenDirection()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Wave;
            model.TopLayer.Speed = 5;
            model.TopLayer.Direction = LightingDirection.Right;

            Assert.Equal(new[] { "[wave]>[spd5][dirright]" }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithRainModel_WritesBaseMonoLineFirst()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Rain;
            model.TopLayer.EffectColor = new LedColor(255, 128, 0);
            model.TopLayer.BaseColor = new LedColor(195, 255, 142);
            model.TopLayer.Speed = 1;

            var lines = LedFileSerializer.SerializeRgb(model);

            Assert.Equal(
                new[]
                {
                    "[mono]>[195][255][142]",
                    "[rain]>[255][128][0][spd1]"
                },
                lines);
        }

        [Fact]
        public void SerializeRgb_WithTwoLineEffectAndDefaultBase_StillWritesTheBaseLine()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Starlight;
            model.TopLayer.EffectColor = new LedColor(0, 0, 255);
            model.TopLayer.Speed = 7;

            Assert.Equal(
                new[]
                {
                    "[mono]>[0][0][0]",
                    "[star]>[0][0][255][spd7]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithFireballDirectionOutsideLeftRight_WritesLeft()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Fireball;
            model.TopLayer.EffectColor = new LedColor(1, 2, 3);
            model.TopLayer.Speed = 4;
            model.TopLayer.Direction = LightingDirection.Up;

            Assert.Equal(
                new[]
                {
                    "[mono]>[0][0][0]",
                    "[fireball]>[1][2][3][spd4][dirleft]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithOutOfRangeSpeed_WritesTheDefaultFive()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Spectrum;
            model.TopLayer.Speed = 42;

            Assert.Equal(new[] { "[spectrum]>[spd5]" }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithBreatheAndNoKeyColors_AppendsBlackMonoLine()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Breathe;
            model.TopLayer.Speed = 7;

            Assert.Equal(
                new[]
                {
                    "[breathe]>[spd7]",
                    "[mono]>[0][0][0]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithBreatheKeyColors_MatchesTheLed8SampleExactly()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Breathe;
            model.TopLayer.Speed = 7;
            model.TopLayer.SetKeyColor(CodeOf("s"), new LedColor(0, 255, 0));

            Assert.Equal(
                new[]
                {
                    "[breathe]>[spd7]",
                    "[s]>[0][255][0]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithFreestyleKeys_WritesThemInTopLayerGeometryOrder()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Freestyle;
            model.TopLayer.SetKeyColor(CodeOf("a"), new LedColor(189, 255, 57));
            model.TopLayer.SetKeyColor(CodeOf("e"), new LedColor(255, 0, 0));
            model.TopLayer.SetKeyColor(CodeOf("t"), new LedColor(255, 0, 0));

            Assert.Equal(
                new[]
                {
                    "[e]>[255][0][0]",
                    "[t]>[255][0][0]",
                    "[a]>[189][255][57]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithPerKeyTokens_UsesRegistryCanonicalCasing()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Freestyle;
            model.TopLayer.SetKeyColor(CodeOf("F4"), new LedColor(1, 2, 3));

            Assert.Equal(new[] { "[F4]>[1][2][3]" }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithFnLayerLines_PrefixesThemAfterTheTopSection()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Wave;
            model.TopLayer.Direction = LightingDirection.Right;
            model.FnLayer.Mode = LightingMode.Pulse;
            model.FnLayer.Speed = 3;

            Assert.Equal(
                new[]
                {
                    "[wave]>[spd5][dirright]",
                    "fn [pulse]>[spd3]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Theory]
        [InlineData("mute", "fn [F1]>[7][8][9]")]
        [InlineData("vol-", "fn [F2]>[7][8][9]")]
        [InlineData("vol+", "fn [F3]>[7][8][9]")]
        [InlineData("play", "fn [F4]>[7][8][9]")]
        [InlineData("prev", "fn [F5]>[7][8][9]")]
        [InlineData("next", "fn [F6]>[7][8][9]")]
        [InlineData("ins", "fn [pause]>[7][8][9]")]
        [InlineData("scrlk", "fn [del]>[7][8][9]")]
        public void SerializeRgb_WithFnLayerExceptionKeys_WritesTheTopLayerPositionToken(
            string memoryToken,
            string expectedLine)
        {
            var model = new LightingModel();

            model.FnLayer.Mode = LightingMode.Freestyle;
            model.FnLayer.SetKeyColor(CodeOf(memoryToken), new LedColor(7, 8, 9));

            Assert.Equal(new[] { expectedLine }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithTopLayerFunctionKeys_NeverTranslates()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Freestyle;
            model.TopLayer.SetKeyColor(CodeOf("F1"), new LedColor(7, 8, 9));

            Assert.Equal(new[] { "[F1]>[7][8][9]" }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithFreestyleAndNoColors_WritesNothing()
        {
            var model = new LightingModel();

            model.TopLayer.Mode = LightingMode.Freestyle;

            // Legacy-faithful corner: an all-cleared Freestyle layer serializes empty and
            // loads back as Disabled (§2.2).
            Assert.Empty(LedFileSerializer.SerializeRgb(model));
        }

        private static int CodeOf(string token)
        {
            return KeyRegistry.FindByToken(token, TokenDialect.Gen1)!.Code;
        }
    }
}
