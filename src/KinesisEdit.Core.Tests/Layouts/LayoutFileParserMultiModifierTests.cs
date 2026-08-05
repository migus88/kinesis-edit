using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Multi-modifier lines per specs/04-layout-file-format.md §2.3 and §6.5, and
    /// specs/05-key-model.md §5.7: recognized by the Gen1 RGB/TKO and Gen2 parser (04 §1.3),
    /// stored verbatim in canonical lowercase; on the older FS and Adv2 dialects the code is an
    /// unknown output token and the line is tracked invalid.
    /// </summary>
    public class LayoutFileParserMultiModifierTests
    {
        [Fact]
        public void Parse_TheSection65Example_StoresTheCodeVerbatim()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[caps]>[cxxs]");

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.True(key.HasMultiModifiers);
            Assert.Equal("cxxs", key.MultiModifiers);
            Assert.False(key.IsModified);
        }

        [Fact]
        public void Parse_AMixedCaseCode_NormalizesToLowercase()
        {
            var result = LayoutFixtures.Parse(DeviceId.Advantage360, "[caps]>[CAWS]");

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen2);

            Assert.Empty(result.InvalidLines);
            Assert.Equal("caws", key.MultiModifiers);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.Advantage2)]
        public void Parse_ACodeOnAnOlderDialect_IsAnUnknownOutputToken(DeviceId deviceId)
        {
            var result = LayoutFixtures.Parse(deviceId, "[caps]>[cxxs]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Contains(line.Segments, segment => segment is { Text: "[cxxs]", IsValid: false });
        }

        [Fact]
        public void Parse_ASingleModifierOutput_IsAnOrdinaryRemap()
        {
            // 04 §2.3: a single modifier is not a multi-modifier code.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[caps]>[lwin]");

            var key = LayoutFixtures.Position(result.Layout, 0, "caps", TokenDialect.Gen1);

            Assert.Empty(result.InvalidLines);
            Assert.False(key.HasMultiModifiers);
            Assert.True(key.IsModified);
        }
    }
}
