using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Tracking of pedal lines that name an input but cannot be applied
    /// (specs/12-savant-elite.md §4.2 grammar; the never-drop-silently rule of
    /// specs/04-layout-file-format.md §5 applied to the pedal file): the line is reported with
    /// per-segment validity spans, the input keeps its previous state, and the rest of the file
    /// still parses.
    /// </summary>
    public class PedalFileParserInvalidLineTests
    {
        [Fact]
        public void Parse_WithUnknownSingleActionToken_TracksTheLineAndLeavesTheInputUnassigned()
        {
            var result = PedalFixtures.Parse("[lpedal]>[garbage]", "[rpedal]>[a]");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal(1, invalid.LineNumber);
            Assert.Equal(PedalInputId.LeftPedal, invalid.Input);
            Assert.Equal("[lpedal]>[garbage]", invalid.Text);
            Assert.Equal(PedalInputMode.None, result.Configuration[PedalInputId.LeftPedal].Mode);
            Assert.Equal(PedalFixtures.Key("a"), result.Configuration[PedalInputId.RightPedal].Action);
        }

        [Fact]
        public void Parse_WithUnknownSingleActionToken_MarksOnlyThatGroupInvalid()
        {
            var result = PedalFixtures.Parse("[lpedal]>[garbage]");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal(
                new[] { "[lpedal]>", "[garbage]" },
                invalid.Segments.Select(segment => segment.Text));
            Assert.Equal(new[] { true, false }, invalid.Segments.Select(segment => segment.IsValid));
        }

        [Fact]
        public void Parse_WithUnknownSingleActionToken_KeepsSegmentsTilingTheVerbatimText()
        {
            var result = PedalFixtures.Parse("[LPedal]>[GarBage]  ");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal("[LPedal]>[GarBage]  ", string.Concat(invalid.Segments.Select(segment => segment.Text)));
        }

        [Fact]
        public void Parse_WithUnknownMacroToken_MarksOnlyThatGroupInvalid()
        {
            var result = PedalFixtures.Parse("{jack1}>{a}{nonsense}{b}");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal(
                new[] { "{jack1}>", "{a}", "{nonsense}", "{b}" },
                invalid.Segments.Select(segment => segment.Text));
            Assert.Equal(new[] { true, true, false, true }, invalid.Segments.Select(segment => segment.IsValid));
        }

        [Fact]
        public void Parse_WithMissingSeparator_MarksTheWholeLineInvalid()
        {
            var result = PedalFixtures.Parse("[lpedal][a]");

            var invalid = Assert.Single(result.InvalidLines);
            var segment = Assert.Single(invalid.Segments);
            Assert.Equal("[lpedal][a]", segment.Text);
            Assert.False(segment.IsValid);
            Assert.Equal(PedalInputId.LeftPedal, result.SourceLines[0].Input);
        }

        [Fact]
        public void Parse_WithMacroGroupInASingleActionLine_TracksTheLine()
        {
            var result = PedalFixtures.Parse("[lpedal]>{a}");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal(new[] { true, false }, invalid.Segments.Select(segment => segment.IsValid));
        }

        [Fact]
        public void Parse_WithTwoGroupsInASingleActionLine_TracksTheExtraGroup()
        {
            var result = PedalFixtures.Parse("[lpedal]>[a][b]");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal(
                new[] { "[lpedal]>", "[a]", "[b]" },
                invalid.Segments.Select(segment => segment.Text));
            Assert.Equal(new[] { true, true, false }, invalid.Segments.Select(segment => segment.IsValid));
        }

        [Fact]
        public void Parse_WithUnterminatedGroup_TracksTheLine()
        {
            var result = PedalFixtures.Parse("{jack1}>{a}{b");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Contains(invalid.Segments, segment => !segment.IsValid);
        }

        [Fact]
        public void Parse_WithJunkTextBetweenGroups_TracksTheLine()
        {
            var result = PedalFixtures.Parse("{jack1}>{a}oops{b}");

            var invalid = Assert.Single(result.InvalidLines);
            Assert.Equal(
                new[] { "{jack1}>", "{a}", "oops", "{b}" },
                invalid.Segments.Select(segment => segment.Text));
            Assert.Equal(new[] { true, true, false, true }, invalid.Segments.Select(segment => segment.IsValid));
        }

        [Fact]
        public void Parse_WithInvalidLineForAnAlreadyAssignedInput_KeepsTheEarlierAssignment()
        {
            var result = PedalFixtures.Parse("[jack1]>[a]", "[jack1]>[garbage]");

            Assert.Single(result.InvalidLines);
            Assert.Equal(PedalFixtures.Key("a"), result.Configuration[PedalInputId.Jack1].Action);
        }

        [Fact]
        public void Parse_WithUnknownInputName_PreservesTheLineInsteadOfTrackingIt()
        {
            var result = PedalFixtures.Parse("[jack9]>[a]");

            Assert.Empty(result.InvalidLines);
            Assert.Equal(PedalInputId.None, result.SourceLines[0].Input);
        }
    }
}
