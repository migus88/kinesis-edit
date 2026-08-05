using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts semantic round-tripping (parse → serialize → parse yields an equivalent model)
    /// for every sample file of specs/07-lighting.md §2.5 and every factory Expansion Pack 2
    /// file of §2.6, plus representative files for each mode shape. Byte-exact reproduction of
    /// legacy inputs is deliberately not a goal — canonical output may reorder lines (§2.4).
    /// </summary>
    public class LedFileRoundTripTests
    {
        public static IEnumerable<object[]> RgbSampleFiles()
        {
            // §2.5 sample profiles led1-led9.
            yield return new object[] { "led1", new[] { "[wave]" } };
            yield return new object[] { "led2", new[] { "[rain]>[255][128][0][spd1]", "[mono]>[195][255][142]" } };
            yield return new object[] { "led3-empty", Array.Empty<string>() };
            yield return new object[] { "led4", new[] { "[rebound]>[0][255][0][spd8][dirleft]" } };
            yield return new object[] { "led5", new[] { "[star]>[0][0][255][spd7]" } };
            yield return new object[] { "led8", new[] { "[breathe]>[spd7]", "[s]>[0][255][0]" } };
            yield return new object[]
            {
                "led9",
                new[] { "[e]>[255][0][0]", "[t]>[255][0][0]", "[a]>[189][255][57]" }
            };

            // §2.5 further examples.
            yield return new object[]
            {
                "breathe-layout-tokens",
                new[]
                {
                    "[breathe]>[spd7]",
                    "[esc]>[255][0][0]",
                    "[F4]>[0][255][0]",
                    "[cbrk]>[0][0][255]",
                    "[hk6]>[255][255][0]",
                    "[ent]>[0][255][255]",
                    "[/]>[255][0][255]"
                }
            };
            yield return new object[] { "reactive-no-base", new[] { "[reactive]>[0][0][255][spd5]" } };

            // §2.6 factory Expansion Pack 2 files.
            yield return new object[] { "pack2-profile1", ExpansionPackDefaults.ProfileLines[1].ToArray() };
            yield return new object[] { "pack2-profile2", ExpansionPackDefaults.ProfileLines[2].ToArray() };
            yield return new object[] { "pack2-profile6", ExpansionPackDefaults.ProfileLines[6].ToArray() };

            // One representative per remaining mode shape.
            yield return new object[] { "mono", new[] { "[mono]>[10][20][30]" } };
            yield return new object[] { "spectrum", new[] { "[spectrum]>[spd2]" } };
            yield return new object[] { "pulse", new[] { "[pulse]>[spd9]" } };
            yield return new object[]
            {
                "ripple",
                new[] { "[mono]>[5][6][7]", "[ripple]>[200][100][50][spd3]" }
            };
            yield return new object[]
            {
                "fireball",
                new[] { "[mono]>[5][6][7]", "[fireball]>[200][100][50][spd3][dirright]" }
            };
            yield return new object[]
            {
                "loop",
                new[] { "[mono]>[5][6][7]", "[loop]>[200][100][50][spd3][dirdown]" }
            };
            yield return new object[]
            {
                "freestyle-fill-then-override",
                new[] { "[mono]>[255][0][0]", "[a]>[0][0][255]" }
            };
            yield return new object[]
            {
                "both-layers",
                new[]
                {
                    "[mono]>[0][0][255]",
                    "fn [breathe]>[spd5]",
                    "fn [F1]>[9][9][9]",
                    "fn [del]>[8][8][8]"
                }
            };
        }

        [Theory]
        [MemberData(nameof(RgbSampleFiles))]
        public void ParseRgb_ThenSerializeAndReparse_YieldsAnEquivalentModel(string sampleName, string[] lines)
        {
            Assert.NotNull(sampleName);

            var firstParse = LedFileParser.ParseRgb(lines);
            var serialized = LedFileSerializer.SerializeRgb(firstParse);
            var secondParse = LedFileParser.ParseRgb(serialized);

            Assert.True(firstParse.IsEquivalentTo(secondParse));
        }

        [Fact]
        public void SerializeRgb_WithLed2Model_PinsTheCanonicalMonoFirstOrder()
        {
            var model = LedFileParser.ParseRgb(["[rain]>[255][128][0][spd1]", "[mono]>[195][255][142]"]);

            Assert.Equal(
                new[]
                {
                    "[mono]>[195][255][142]",
                    "[rain]>[255][128][0][spd1]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithLed1Model_PinsTheCanonicalExplicitDefaults()
        {
            var model = LedFileParser.ParseRgb(["[wave]"]);

            Assert.Equal(new[] { "[wave]>[spd5][dirleft]" }, LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithLed4Model_PinsTheCanonicalBaseLine()
        {
            var model = LedFileParser.ParseRgb(["[rebound]>[0][255][0][spd8][dirleft]"]);

            Assert.Equal(
                new[]
                {
                    "[mono]>[0][0][0]",
                    "[rebound]>[0][255][0][spd8][dirleft]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithPack2Profile1Model_ReproducesTheFactoryFile()
        {
            var model = LedFileParser.ParseRgb(ExpansionPackDefaults.ProfileLines[1].ToArray());

            Assert.Equal(
                new[]
                {
                    "[wave]>[spd5][dirright]",
                    "fn [wave]>[spd5][dirdown]"
                },
                LedFileSerializer.SerializeRgb(model));
        }

        [Fact]
        public void SerializeRgb_WithPack2Profile6Model_ExpandsTheFnMonoFillToPerKeyLines()
        {
            var model = LedFileParser.ParseRgb(ExpansionPackDefaults.ProfileLines[6].ToArray());
            var lines = LedFileSerializer.SerializeRgb(model);

            // Top layer: Monochrome blue. Fn layer: Breathe whose [mono] fill colored all 95
            // keys, so the canonical form writes 95 per-key lines after the header (§2.4 item 4).
            Assert.Equal("[mono]>[0][0][255]", lines[0]);
            Assert.Equal("fn [breathe]>[spd5]", lines[1]);
            Assert.Equal(97, lines.Count);
            Assert.Equal(95, lines.Skip(2).Count(line => line.StartsWith("fn ", StringComparison.Ordinal)));
        }
    }
}
