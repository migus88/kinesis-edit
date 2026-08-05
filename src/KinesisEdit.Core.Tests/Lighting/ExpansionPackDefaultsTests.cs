using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Pins the factory Expansion Pack 2 data against the files specs/07-lighting.md §2.6
    /// quotes verbatim (profiles 1, 2, and 6, in the legacy effect-first line order) and
    /// asserts they parse to the spec'd models.
    /// </summary>
    public class ExpansionPackDefaultsTests
    {
        [Fact]
        public void ProfileLines_Always_ContainsExactlyTheThreeSpecQuotedProfiles()
        {
            Assert.Equal(new[] { 1, 2, 6 }, ExpansionPackDefaults.ProfileLines.Keys.Order());
        }

        [Fact]
        public void FindProfileLines_WithProfile1_CarriesTheSpecLinesVerbatim()
        {
            Assert.Equal(
                new[]
                {
                    "[wave]>[spd5][dirright]",
                    "fn [wave]>[spd5][dirdown]"
                },
                ExpansionPackDefaults.FindProfileLines(1));
        }

        [Fact]
        public void FindProfileLines_WithProfile2_CarriesTheSpecLinesVerbatim()
        {
            Assert.Equal(
                new[]
                {
                    "[rain]>[0][255][0][spd5]",
                    "[mono]>[0][0][0]",
                    "fn [rain]>[0][255][0][spd5]",
                    "fn [mono]>[255][255][255]"
                },
                ExpansionPackDefaults.FindProfileLines(2));
        }

        [Fact]
        public void FindProfileLines_WithProfile6_CarriesTheSpecLinesVerbatim()
        {
            Assert.Equal(
                new[]
                {
                    "[mono]>[0][0][255]",
                    "fn [breathe]>[spd5]",
                    "fn [mono]>[0][0][255]"
                },
                ExpansionPackDefaults.FindProfileLines(6));
        }

        [Fact]
        public void FindProfileLines_WithUnpinnedProfile_ReturnsNull()
        {
            Assert.Null(ExpansionPackDefaults.FindProfileLines(3));
            Assert.Null(ExpansionPackDefaults.FindProfileLines(9));
        }

        [Fact]
        public void ParseRgb_WithProfile1_ReadsWaveOnBothLayersWithTheirDirections()
        {
            var model = LedFileParser.ParseRgb(ExpansionPackDefaults.ProfileLines[1].ToArray());

            Assert.Equal(LightingMode.Wave, model.TopLayer.Mode);
            Assert.Equal(LightingDirection.Right, model.TopLayer.Direction);
            Assert.Equal(LightingMode.Wave, model.FnLayer.Mode);
            Assert.Equal(LightingDirection.Down, model.FnLayer.Direction);
            Assert.Equal(5, model.TopLayer.Speed);
        }

        [Fact]
        public void ParseRgb_WithProfile2_ReadsTheEffectFirstRainOnBothLayers()
        {
            var model = LedFileParser.ParseRgb(ExpansionPackDefaults.ProfileLines[2].ToArray());

            Assert.Equal(LightingMode.Rain, model.TopLayer.Mode);
            Assert.Equal(new LedColor(0, 255, 0), model.TopLayer.EffectColor);
            Assert.Equal(LedColor.Black, model.TopLayer.BaseColor);
            Assert.Equal(LightingMode.Rain, model.FnLayer.Mode);
            Assert.Equal(new LedColor(255, 255, 255), model.FnLayer.BaseColor);
        }

        [Fact]
        public void ParseRgb_WithProfile6_ReadsMonochromeTopAndBreatheFnWithBlueFill()
        {
            var model = LedFileParser.ParseRgb(ExpansionPackDefaults.ProfileLines[6].ToArray());

            Assert.Equal(LightingMode.Monochrome, model.TopLayer.Mode);
            Assert.Equal(new LedColor(0, 0, 255), model.TopLayer.EffectColor);
            Assert.Equal(LightingMode.Breathe, model.FnLayer.Mode);
            Assert.Equal(5, model.FnLayer.Speed);
            Assert.Equal(95, model.FnLayer.KeyColors.Count);
            Assert.All(model.FnLayer.KeyColors.Values, color => Assert.Equal(new LedColor(0, 0, 255), color));
        }
    }
}
