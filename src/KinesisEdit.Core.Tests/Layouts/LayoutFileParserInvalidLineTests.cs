using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Invalid-line collection per specs/04-layout-file-format.md §5: what makes a line
    /// invalid (§5.1), the tracked verbatim text with per-segment validity and the default-off
    /// keep flag (§5.2), and the per-dialect structural rules of §1.3 (missing <c>&gt;</c>,
    /// bracket-start requirement, unknown Gen2 headers of §3.3).
    /// </summary>
    public class LayoutFileParserInvalidLineTests
    {
        [Fact]
        public void Parse_ALineWithoutASeparator_IsOneInvalidSegment()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[F1][a]");

            var line = Assert.Single(result.InvalidLines);
            var segment = Assert.Single(line.Segments);

            Assert.Equal(1, line.LineNumber);
            Assert.Equal("[F1][a]", line.Text);
            Assert.False(segment.IsValid);
            Assert.False(line.Keep);
        }

        [Fact]
        public void Parse_ALineWithNoBrackets_IsWhollyInvalid()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "caps>lwin");

            var line = Assert.Single(result.InvalidLines);

            Assert.Equal("caps>lwin", line.Text);
            Assert.False(line.Segments[0].IsValid);
        }

        [Fact]
        public void Parse_AnUnknownOutputToken_FlagsOnlyThatSegment()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[F1]>[Bogus]");

            var line = Assert.Single(result.InvalidLines);

            // Verbatim original casing survives in the tracked text and segments (04 §5.2).
            Assert.Equal("[F1]>[Bogus]", line.Text);
            Assert.Contains(line.Segments, segment => segment is { Text: "[F1]", IsValid: true });
            Assert.Contains(line.Segments, segment => segment is { Text: "[Bogus]", IsValid: false });
            Assert.Equal(line.Text, string.Concat(line.Segments.Select(segment => segment.Text)));
        }

        [Fact]
        public void Parse_AnUnknownPositionToken_FlagsThePositionSegment()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[zzz]>[a]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Contains(line.Segments, segment => segment is { Text: "[zzz]", IsValid: false });
            Assert.Contains(line.Segments, segment => segment is { Text: "[a]", IsValid: true });
        }

        [Fact]
        public void Parse_APositionMissingFromTheAddressedLayer_IsInvalid()
        {
            // "insert" alone is not a keypad exception, so it addresses the top layer, which
            // has no insert position on the Advantage2 (05 §5.4, §4.5).
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "[insert]>[a]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Equal(0, line.LayerIndex);
            Assert.Contains(line.Segments, segment => segment is { Text: "[insert]", IsValid: false });
        }

        [Fact]
        public void Parse_AnUnterminatedPositionBracket_IsInvalid()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[F1>[a]");

            Assert.Single(result.InvalidLines);
            Assert.False(LayoutFixtures.Position(result.Layout, 0, "F1", TokenDialect.Gen1).IsModified);
        }

        [Fact]
        public void Parse_AnUnknownGen2Header_IsInvalidAndKeepsTheCurrentLayer()
        {
            var result = LayoutFixtures.Parse(
                DeviceId.Advantage360,
                "<keypad>",
                "<funky>",
                "[u]>[F14]");

            var line = Assert.Single(result.InvalidLines);
            var keypadKey = LayoutFixtures.Position(result.Layout, 1, "u", TokenDialect.Gen2);

            Assert.Equal("<funky>", line.Text);
            Assert.Equal(2, line.LineNumber);
            Assert.Equal(1, line.LayerIndex);
            Assert.True(keypadKey.IsModified);
        }

        [Fact]
        public void Parse_AFreestyleLineNotStartingWithABracket_IsWhollyInvalid()
        {
            // 04 §1.3: the older FS and Adv2 parsers require the line to start with '[' or '{'.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdge, " [F1]>[a]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Equal(" [F1]>[a]", line.Text);
            Assert.False(LayoutFixtures.Position(result.Layout, 0, "F1", TokenDialect.Gen1).IsModified);
        }

        [Fact]
        public void Parse_AnFnPrefixOnAdvantage2_IsNotALayerPrefix()
        {
            // "fn " is Gen1-family syntax only; on the Adv2 the line fails the bracket-start rule.
            var result = LayoutFixtures.Parse(DeviceId.Advantage2, "fn [4]>[a]");

            Assert.Single(result.InvalidLines);
        }

        [Fact]
        public void Parse_AnInvalidBottomLayerLine_RecordsLayerAndVerbatimPrefix()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[F1]>[a]", "FN [zzz]>[a]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Equal(2, line.LineNumber);
            Assert.Equal(1, line.LayerIndex);
            Assert.Equal("FN [zzz]>[a]", line.Text);
            Assert.Equal(line.Text, string.Concat(line.Segments.Select(segment => segment.Text)));
        }

        [Fact]
        public void Parse_JunkBetweenGroups_IsAnInvalidSegment()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "[F1]x>[a]");

            var line = Assert.Single(result.InvalidLines);

            Assert.Contains(line.Segments, segment => segment is { Text: "x", IsValid: false });
        }

        [Fact]
        public void Parse_AMacroLineWithLeadingWhitespace_IsInvalidAndNotApplied()
        {
            // 04 §5.1: the config side must start with { for macros — same rule the
            // single-key path enforces for [.
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, " {q}>{a}");

            var line = Assert.Single(result.InvalidLines);

            Assert.Equal(" {q}>{a}", line.Text);
            Assert.Contains(line.Segments, segment => segment is { Text: " ", IsValid: false });
            Assert.Equal(0, result.Layout.MacroCount);
        }

        [Fact]
        public void Parse_BlankLines_AreIgnoredNotTracked()
        {
            var result = LayoutFixtures.Parse(DeviceId.FreestyleEdgeRgb, "", "   ", "[F1]>[a]");

            Assert.Empty(result.InvalidLines);
        }
    }
}
