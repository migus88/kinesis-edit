using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Golden parse → serialize round trips for the worked examples of
    /// specs/04-layout-file-format.md §6 (6.1-6.6) and specs/06-macros.md §7 (7.1-7.5; 7.6 is
    /// pedal-only and out of scope), plus whole-file multi-line round trips per dialect
    /// covering layer targeting, ordering, and kept invalid lines. Expected strings are the
    /// spec's canonical text (the §7 examples abridged with "..." are used with the ellipsis
    /// dropped).
    /// </summary>
    public class LayoutRoundTripTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdge, "[F1]>[a]")] // 04 §6.1
        [InlineData(DeviceId.FreestyleEdgeRgb, "[F1]>[a]")] // 04 §6.1
        [InlineData(DeviceId.Advantage2, "[F1]>[a]")] // 04 §6.1
        [InlineData(DeviceId.FreestyleEdge, "fn [4]>[LED]")] // 04 §6.2
        [InlineData(DeviceId.FreestyleEdgeRgb, "fn [4]>[LED]")] // 04 §6.2
        [InlineData(DeviceId.Advantage2, "[kp-w]>[b]")] // 04 §6.3
        [InlineData(DeviceId.FreestyleEdge, "[hyph]>[obrk]")] // 04 §6.4
        [InlineData(DeviceId.FreestyleEdgeRgb, "[caps]>[cxxs]")] // 04 §6.5
        [InlineData(DeviceId.FreestyleEdge, "[lspc]>[spc][t&h250][lctrl]")] // 04 §6.6
        [InlineData(DeviceId.FreestyleEdge, "{2}>{x1}{a}{s}{d}{f}{a}{s}{f}{d}{a}{s}{f}")] // 06 §2
        [InlineData(DeviceId.FreestyleEdge, "{q}>{x1}{-lshft}{n}{+lshft}{a}{d}{i}{a}")] // 06 §7.1
        [InlineData(DeviceId.FreestyleEdgeRgb, "{lshft}{esc}>{s2}{x2}{a}{s}{d}{f}{kpent}")] // 06 §7.2
        [InlineData(
            DeviceId.FreestyleEdge,
            "{lshft}{3}>{x1}{esc}{-shift}{s}{+shift}{a}{l}{u}{t}{spc}{-shift}{/}{+shift}{r}{i}{c}{d999}{d001}{dran}")] // 06 §7.3
        [InlineData(
            DeviceId.Advantage2,
            "{lshift}{lctrl}{lalt}{d}>{speed5}{a}{d}{s}{shift}{shift}{-ctrl}{d}{+ctrl}")] // 06 §7.4
        [InlineData(
            DeviceId.FreestyleEdge,
            "{o}>{x1}{a}{dran}{d050}{d999}{d125}{d125}{d500}{d974}{s}")] // 06 §7.5
        public void RoundTrip_ASpecWorkedExample_ReproducesTheCanonicalLine(DeviceId deviceId, string line)
        {
            Assert.Equal([line], LayoutFixtures.RoundTrip(deviceId, line));
        }

        [Fact]
        public void RoundTrip_TheGen2MultiModifierExample_ReproducesTheCanonicalFile()
        {
            // 04 §6.5 under the Gen2 dialect: the serializer always emits every layer header.
            var lines = new[] { "<base>", "[caps]>[cxxs]" };

            Assert.Equal(
                ["<base>", "[caps]>[cxxs]", "<keypad>", "<function1>", "<function2>", "<function3>"],
                LayoutFixtures.RoundTrip(DeviceId.Advantage360, lines));
        }

        [Fact]
        public void RoundTrip_TheSection75ExampleOnTheRgbFamily_AddsTheDefaultSpeedToken()
        {
            // 06 §7.5 carries no {sN}; on load the RGB family defaults the speed to 5 and its
            // serializer always writes the token (06 §3) — while d125/d500 resolve to the
            // generated delays and still serialize to the same text (05 §3.12).
            var line = "{o}>{x1}{a}{dran}{d050}{d999}{d125}{d125}{d500}{d974}{s}";

            Assert.Equal(
                ["{o}>{s5}{x1}{a}{dran}{d050}{d999}{d125}{d125}{d500}{d974}{s}"],
                LayoutFixtures.RoundTrip(DeviceId.FreestyleEdgeRgb, line));
        }

        [Fact]
        public void RoundTrip_AMixedCaseFile_NormalizesToCanonicalCasing()
        {
            Assert.Equal(
                ["[F1]>[a]", "fn [4]>[LED]"],
                LayoutFixtures.RoundTrip(DeviceId.FreestyleEdgeRgb, "[f1]>[A]", "FN [4]>[led]"));
        }

        [Fact]
        public void RoundTrip_AWholeFreestyleFile_PreservesEveryLineInOrder()
        {
            // Top-layer rules in key-index order (F1=1, hyph=30, q=37 with its macro, caps=53,
            // lspc=88), then the fn layer (04 §4.3).
            var lines = new[]
            {
                "[F1]>[a]",
                "[hyph]>[obrk]",
                "{q}>{x1}{-lshft}{n}{+lshft}{a}{d}{i}{a}",
                "[caps]>[lwin]",
                "[lspc]>[spc][t&h250][lctrl]",
                "fn [4]>[LED]"
            };

            Assert.Equal(lines, LayoutFixtures.RoundTrip(DeviceId.FreestyleEdge, lines));
        }

        [Fact]
        public void RoundTrip_AWholeRgbFileWithAKeptInvalidLine_PreservesItVerbatim()
        {
            var lines = new[]
            {
                "{esc}>{s2}{x2}{a}",
                "[caps]>[cxxs]",
                "fn [4]>[LED]",
                "fn [ZZZ]>[a]"
            };

            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, lines);

            Assert.Single(result.InvalidLines);
            result.InvalidLines[0].Keep = true;

            Assert.Equal(
                lines,
                Core.Layouts.LayoutFileSerializer.Serialize(result.Layout, result.InvalidLines));
        }

        [Fact]
        public void RoundTrip_AWholeAdvantage2File_PreservesLayerRoutingAndPrefixes()
        {
            // Top layer (F1=1, caps=42, d=45 with its macro), then the keypad layer in index
            // order: numlk(25), w(32), kp0(69) plus its macro, insert(77) — exception tokens
            // bare, everything else kp- prefixed (04 §3.2, 05 §5.4).
            var lines = new[]
            {
                "[F1]>[a]",
                "[caps]>[lwin]",
                "{d}>{speed5}{a}{d}{s}",
                "[numlk]>[z]",
                "[kp-w]>[b]",
                "[kp0]>[space]",
                "{kp-lshift}{kp0}>{speed5}{a}",
                "[kp-insert]>[z]"
            };

            Assert.Equal(lines, LayoutFixtures.RoundTrip(DeviceId.Advantage2, lines));
        }

        [Fact]
        public void RoundTrip_AWholeGen2File_PreservesHeadersRulesAndFlatMacros()
        {
            // Per layer: header, key rules in index order, then that layer's flat-list macros
            // (04 §4.3, §3.3).
            var lines = new[]
            {
                "<base>",
                "[caps]>[cxxs]",
                "{caps}>{s5}{x1}{spc}",
                "<keypad>",
                "[u]>[F14]",
                "<function1>",
                "[1]>[F13]",
                "{esc}>{s1}{x1}{-d}{spc}{+d}",
                "<function2>",
                "<function3>"
            };

            Assert.Equal(lines, LayoutFixtures.RoundTrip(DeviceId.Advantage360, lines));
        }

        [Fact]
        public void RoundTrip_TheAdvantage2ValueSpellings_WriteTheLegacySaveTokens()
        {
            // 05 §3.6: kp/ kp* kp+ are accepted on read and written back as the save spellings.
            Assert.Equal(
                ["[a]>[kpdiv]", "[s]>[kpmult]", "[d]>[kpplus]"],
                LayoutFixtures.RoundTrip(DeviceId.Advantage2, "[a]>[kp/]", "[s]>[kp*]", "[d]>[kp+]"));
        }
    }
}
