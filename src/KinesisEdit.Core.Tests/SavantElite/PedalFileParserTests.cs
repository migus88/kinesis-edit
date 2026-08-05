using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Line recognition and single-action parsing of <c>pedals.txt</c>
    /// (specs/12-savant-elite.md §4.1 general structure, §4.2 line grammar): which lines are pedal
    /// lines at all, how the bracket style selects the mode, and the last-one-wins rule for
    /// duplicates.
    /// </summary>
    public class PedalFileParserTests
    {
        [Fact]
        public void Parse_WithFactoryFile_ReadsAllSevenInputsAndReportsNoInvalidLines()
        {
            var result = PedalFixtures.Parse(PedalFixtures.FactoryFile);

            Assert.Empty(result.InvalidLines);
            Assert.Equal(PedalInputMode.Single, result.Configuration[PedalInputId.LeftPedal].Mode);
            Assert.Equal(PedalFixtures.Key("lmouse"), result.Configuration[PedalInputId.LeftPedal].Action);
            Assert.Equal(PedalInputMode.Macro, result.Configuration[PedalInputId.MiddlePedal].Mode);
            Assert.Equal(PedalFixtures.Key("rmouse"), result.Configuration[PedalInputId.RightPedal].Action);
            Assert.Equal(PedalFixtures.Key("lmouse"), result.Configuration[PedalInputId.Jack1].Action);
            Assert.Equal(PedalFixtures.Key("rmouse"), result.Configuration[PedalInputId.Jack2].Action);
            Assert.Equal(PedalFixtures.Key("bspace"), result.Configuration[PedalInputId.Jack3].Action);
            Assert.Equal(PedalInputMode.Macro, result.Configuration[PedalInputId.Jack4].Mode);
        }

        [Fact]
        public void Parse_WithFactoryFile_TagsOnlyTheSevenAssignmentLinesWithAnInput()
        {
            var result = PedalFixtures.Parse(PedalFixtures.FactoryFile);

            Assert.Equal(PedalFixtures.FactoryFile.Length, result.SourceLines.Count);
            Assert.Equal(7, result.SourceLines.Count(line => line.Input != PedalInputId.None));

            for (var i = 7; i < PedalFixtures.FactoryFile.Length; i++)
            {
                Assert.Equal(PedalInputId.None, result.SourceLines[i].Input);
                Assert.Equal(PedalFixtures.FactoryFile[i], result.SourceLines[i].Text);
            }
        }

        [Fact]
        public void Parse_WithLineContainingButNotStartingWithAPedalToken_PreservesItInsteadOfParsing()
        {
            var result = PedalFixtures.Parse("* the [lpedal]>[a] example above", "  [lpedal]>[b]");

            Assert.Equal(PedalInputId.None, result.SourceLines[0].Input);
            Assert.Equal(PedalInputId.None, result.SourceLines[1].Input);
            Assert.Equal(PedalInputMode.None, result.Configuration[PedalInputId.LeftPedal].Mode);
            Assert.Empty(result.InvalidLines);
        }

        [Fact]
        public void Parse_WithMixedCaseInputAndToken_ResolvesCaseInsensitively()
        {
            var result = PedalFixtures.Parse("[LPedal]>[BSpace]");

            Assert.Equal(PedalFixtures.Key("bspace"), result.Configuration[PedalInputId.LeftPedal].Action);
            Assert.Empty(result.InvalidLines);
        }

        [Fact]
        public void Parse_WithEmptySingleActionLine_LeavesTheInputUnassigned()
        {
            var result = PedalFixtures.Parse("[lpedal]>");

            Assert.Equal(PedalInputMode.None, result.Configuration[PedalInputId.LeftPedal].Mode);
            Assert.Null(result.Configuration[PedalInputId.LeftPedal].Action);
            Assert.False(result.Configuration[PedalInputId.LeftPedal].IsAssigned);
            Assert.Empty(result.InvalidLines);
        }

        [Fact]
        public void Parse_WithEmptyMacroLine_KeepsMacroModeWithAnEmptyMacro()
        {
            var result = PedalFixtures.Parse("{jack2}>");
            var input = result.Configuration[PedalInputId.Jack2];

            Assert.Equal(PedalInputMode.Macro, input.Mode);
            Assert.NotNull(input.Macro);
            Assert.True(input.Macro.IsEmpty);
            Assert.False(input.IsAssigned);
        }

        [Fact]
        public void Parse_WithBracketStyleSelectingTheMode_AppliesItToTheWholeLine()
        {
            var result = PedalFixtures.Parse("[lpedal]>[a]", "{rpedal}>{a}");

            Assert.Equal(PedalInputMode.Single, result.Configuration[PedalInputId.LeftPedal].Mode);
            Assert.Equal(PedalInputMode.Macro, result.Configuration[PedalInputId.RightPedal].Mode);
        }

        [Fact]
        public void Parse_WithDuplicateLinesForOneInput_LetsTheLastOneWin()
        {
            var result = PedalFixtures.Parse("[jack1]>[a]", "[jack1]>[b]");

            Assert.Equal(PedalFixtures.Key("b"), result.Configuration[PedalInputId.Jack1].Action);
            Assert.Equal(PedalInputId.Jack1, result.SourceLines[0].Input);
            Assert.Equal(PedalInputId.Jack1, result.SourceLines[1].Input);
        }

        [Fact]
        public void Parse_WithOneInputInBothBracketForms_LetsTheLastOneWin()
        {
            var result = PedalFixtures.Parse("{jack1}>{a}", "[jack1]>[b]");

            Assert.Equal(PedalInputMode.Single, result.Configuration[PedalInputId.Jack1].Mode);
            Assert.Equal(PedalFixtures.Key("b"), result.Configuration[PedalInputId.Jack1].Action);
            Assert.Null(result.Configuration[PedalInputId.Jack1].Macro);
        }

        [Fact]
        public void Parse_WithWhitespaceAroundTheAction_IgnoresItAndStaysValid()
        {
            var result = PedalFixtures.Parse("[lpedal]> [a]   ");

            Assert.Equal(PedalFixtures.Key("a"), result.Configuration[PedalInputId.LeftPedal].Action);
            Assert.Empty(result.InvalidLines);
        }

        [Fact]
        public void Parse_WithBlankAndUnrelatedLines_PreservesThemAndAssignsNothing()
        {
            var result = PedalFixtures.Parse("", "   ", "not a pedal line at all");

            Assert.All(result.SourceLines, line => Assert.Equal(PedalInputId.None, line.Input));
            Assert.Empty(result.InvalidLines);
        }

        [Fact]
        public void Parse_WithEveryPedalPositionToken_RecognisesAllSevenInputs()
        {
            var lines = PedalInputs.All
                .Select(input => "[" + PedalInputs.GetToken(input) + "]>[a]")
                .ToArray();

            var result = PedalFixtures.Parse(lines);

            Assert.Empty(result.InvalidLines);
            Assert.All(result.Configuration.Inputs, input => Assert.True(input.IsAssigned));
        }

        [Fact]
        public void Parse_WithMediaAndPseudoKeyTokens_ResolvesThemAsSingleActions()
        {
            var result = PedalFixtures.Parse(
                "[lpedal]>[vol+]",
                "[mpedal]>[calc]",
                "[rpedal]>[shutdn]",
                "[jack1]>[intl-\\]");

            Assert.Empty(result.InvalidLines);
            Assert.Equal(PedalFixtures.Key("vol+"), result.Configuration[PedalInputId.LeftPedal].Action);
            Assert.Equal(PedalFixtures.Key("calc"), result.Configuration[PedalInputId.MiddlePedal].Action);
            Assert.Equal(PedalFixtures.Key("shutdn"), result.Configuration[PedalInputId.RightPedal].Action);
            Assert.Equal(PedalFixtures.Key("intl-\\"), result.Configuration[PedalInputId.Jack1].Action);
        }

        [Fact]
        public void Parse_WithNullLines_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => PedalFileParser.Parse(null!));
        }
    }
}
