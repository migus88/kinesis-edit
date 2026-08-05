using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Tap-and-hold parsing per specs/04-layout-file-format.md §2.2 and §6.6: the three-group
    /// value side, and the per-dialect delay policy — the Gen1 RGB-family/Gen2 parser marks a
    /// missing or negative delay invalid, the older FS and Adv2 parsers substitute the 250 ms
    /// default (specs/11-feature-dialogs.md §11.1).
    /// </summary>
    public class LayoutFileParserTapAndHoldTests
    {
        [Fact]
        public void Parse_TheSection66Example_StoresTapHoldAndDelay()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "[lspc]>[spc][t&h250][lctrl]");

            var key = LayoutFixtures.Position(result.Layout, 0, "lspc", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsTapAndHold);
            Assert.Equal(LayoutFixtures.Key("spc").Code, key.TapAction!.Code);
            Assert.Equal(LayoutFixtures.Key("lctrl").Code, key.HoldAction!.Code);
            Assert.Equal(250, key.TimingDelay);
        }

        [Theory]
        [InlineData("[caps]>[a][t&h][lctrl]")]
        [InlineData("[caps]>[a][t&h-5][lctrl]")]
        [InlineData("[caps]>[a][t&habc][lctrl]")]
        public void Parse_ABadDelayOnTheRgbFamily_MarksTheLineInvalid(string line)
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, line);

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen1);

            Assert.Single(result.InvalidLines);
            Assert.False(key.IsTapAndHold);
        }

        [Theory]
        [InlineData("[caps]>[a][t&h][lctrl]")]
        [InlineData("[caps]>[a][t&h-5][lctrl]")]
        [InlineData("[caps]>[a][t&habc][lctrl]")]
        public void Parse_ABadDelayOnFreestyle_SubstitutesTheDefault(string line)
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, line);

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsTapAndHold);
            Assert.Equal(250, key.TimingDelay);
        }

        [Fact]
        public void Parse_ABadDelayOnAdvantage2_SubstitutesTheDefault()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "[caps]>[a][t&h][lctrl]");

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Legacy);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(250, key.TimingDelay);
        }

        [Fact]
        public void Parse_AMissingHoldGroup_MarksTheLineInvalid()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, "[caps]>[a][t&h250]");

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen1);

            Assert.Single(result.InvalidLines);
            Assert.False(key.IsTapAndHold);
        }

        [Fact]
        public void Parse_AnUnknownTapToken_MarksThatSegmentInvalid()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[caps]>[bogus][t&h250][lctrl]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Contains(line.Segments, segment => segment is { Text: "[bogus]", IsValid: false });
            Assert.Contains(line.Segments, segment => segment is { Text: "[t&h250]", IsValid: true });
        }

        [Fact]
        public void Parse_ATapAndHoldUnderAGen2Header_TargetsTheHeadersLayer()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage360, "<keypad>", "[u]>[u][t&h150][lctr]");

            var key = LayoutFixtures.Position(result.Layout, 1, "u", TokenDialect.Gen2);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.IsTapAndHold);
            Assert.Equal(150, key.TimingDelay);
        }
    }
}
