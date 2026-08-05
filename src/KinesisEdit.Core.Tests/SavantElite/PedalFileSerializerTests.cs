using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Canonical output of <c>pedals.txt</c> (specs/12-savant-elite.md §4.3 what the app writes,
    /// §4.4 token catalog and the space/delay spellings, §4.6 save mechanics, §6 the speed/delay
    /// groups and the different press &amp; release marker).
    /// </summary>
    public class PedalFileSerializerTests
    {
        [Fact]
        public void Serialize_WithEmptyConfiguration_WritesSevenEmptySingleLinesInFixedOrder()
        {
            var lines = PedalFileSerializer.Serialize(new PedalConfiguration());

            Assert.Equal(
                new[]
                {
                    "[lpedal]>",
                    "[mpedal]>",
                    "[rpedal]>",
                    "[jack1]>",
                    "[jack2]>",
                    "[jack3]>",
                    "[jack4]>"
                },
                lines);
        }

        [Fact]
        public void SerializeInput_WithSingleAction_WritesTheBracketForm()
        {
            Assert.Equal("[jack1]>[pdown]", PedalFixtures.SerializeInput(PedalInputId.Jack1, "[jack1]>[pdown]"));
        }

        [Fact]
        public void SerializeInput_WithSingleSpace_WritesTheNamedSpaceToken()
        {
            Assert.Equal("[mpedal]>[space]", PedalFixtures.SerializeInput(PedalInputId.MiddlePedal, "[mpedal]>[space]"));
        }

        [Fact]
        public void SerializeInput_WithMacroSpace_WritesTheBareSpaceGroup()
        {
            Assert.Equal("{jack1}>{a}{ }{b}", PedalFixtures.SerializeInput(PedalInputId.Jack1, "{jack1}>{a}{ }{b}"));
        }

        [Fact]
        public void SerializeInput_WithFactoryPairSpelling_WritesOneGroupPerKey()
        {
            Assert.Equal(
                "{jack4}>{F1}{F2}{F3}{F4}",
                PedalFixtures.SerializeInput(PedalInputId.Jack4, "{jack4}>{-F1}{+F1}{-F2}{+F2}{-F3}{+F3}{-F4}{+F4}"));
        }

        [Fact]
        public void SerializeInput_WithConsecutiveShiftOnlyKeys_SharesOneShiftWrapper()
        {
            Assert.Equal(
                "{jack1}>{-shift}{n}{o}{+shift}{w}",
                PedalFixtures.SerializeInput(PedalInputId.Jack1, "{jack1}>{-shift}{n}{o}{+shift}{w}"));
        }

        [Fact]
        public void SerializeInput_WithModifiersOnEveryKey_WritesModifiersFirstThenTheKey()
        {
            Assert.Equal(
                "{rpedal}>{-ctrl}{-alt}{delete}{+ctrl}{+alt}",
                PedalFixtures.SerializeInput(
                    PedalInputId.RightPedal,
                    "{rpedal}>{-ctrl}{-alt}{-delete}{+delete}{+ctrl}{+alt}"));
        }

        [Theory]
        [InlineData("{speed1}")]
        [InlineData("{speed3}")]
        [InlineData("{speed5}")]
        [InlineData("{d125}")]
        [InlineData("{d500}")]
        public void SerializeInput_WithSpeedOrDelayKey_WritesOneBareGroup(string group)
        {
            Assert.Equal(
                "{jack1}>" + group + "{a}",
                PedalFixtures.SerializeInput(PedalInputId.Jack1, "{jack1}>" + group + "{a}"));
        }

        [Theory]
        [InlineData("{125}", "{d125}")]
        [InlineData("{500}", "{d500}")]
        public void SerializeInput_WithFieldDelaySpelling_UpgradesItToTheAppSpelling(string field, string canonical)
        {
            Assert.Equal(
                "{jack1}>" + canonical,
                PedalFixtures.SerializeInput(PedalInputId.Jack1, "{jack1}>" + field));
        }

        [Fact]
        public void SerializeInput_WithDifferentPressRelease_SplitsTheKeyInsideItsModifierWrap()
        {
            Assert.Equal(
                "{lpedal}>{-shift}{-k}{ }{+k}{+shift}",
                PedalFixtures.SerializeInput(PedalInputId.LeftPedal, "{lpedal}>{-shift}{-k}{ }{+k}{+shift}"));
        }

        [Fact]
        public void SerializeInput_WithWinModifier_WritesThePedalWinToken()
        {
            Assert.Equal(
                "{jack1}>{-win}{c}{+win}",
                PedalFixtures.SerializeInput(PedalInputId.Jack1, "{jack1}>{-lwin}{c}{+lwin}"));
        }

        [Fact]
        public void SerializeInput_WithFunctionKey_KeepsTheCapitalisedRegistryCasing()
        {
            Assert.Equal("[jack1]>[F1]", PedalFixtures.SerializeInput(PedalInputId.Jack1, "[jack1]>[f1]"));
        }

        [Fact]
        public void SerializeInput_WithUnassignedInput_WritesTheEmptySingleForm()
        {
            var configuration = new PedalConfiguration();

            Assert.Equal("[lpedal]>", PedalFileSerializer.SerializeInput(configuration[PedalInputId.LeftPedal]));
        }

        [Fact]
        public void SerializeInput_WithEmptyMacro_KeepsTheMacroBracketForm()
        {
            var configuration = new PedalConfiguration();

            configuration[PedalInputId.Jack2].SetMacro(new Macro());

            Assert.Equal("{jack2}>", PedalFileSerializer.SerializeInput(configuration[PedalInputId.Jack2]));
        }

        [Fact]
        public void Merge_WithNonPedalLines_KeepsThemVerbatimAndRewritesOnlyPedalLines()
        {
            var lines = PedalFixtures.RoundTrip(
                "* a comment",
                "[lpedal]>[a]",
                "* another comment",
                "{rpedal}>{-b}{+b}");

            Assert.Equal(
                new[]
                {
                    "* a comment",
                    "[lpedal]>[a]",
                    "* another comment",
                    "{rpedal}>{b}"
                },
                lines.Take(4));
        }

        [Fact]
        public void Merge_WithUnassignedInputsMissingFromTheFile_AddsNoLines()
        {
            // 12 §4.6 rewrites lines, it never appends; 12 §1: a device has only some inputs.
            var lines = PedalFixtures.RoundTrip("[jack4]>[a]");

            Assert.Equal(new[] { "[jack4]>[a]" }, lines);
        }

        [Fact]
        public void Merge_WithAssignedInputsMissingFromTheFile_InsertsThemAfterTheLastPedalLineInFixedOrder()
        {
            var result = PedalFixtures.Parse("* header", "[jack4]>[a]", "* the factory diagram");

            result.Configuration[PedalInputId.MiddlePedal].SetSingleAction(PedalFixtures.Key("b"));
            result.Configuration[PedalInputId.Jack1].SetSingleAction(PedalFixtures.Key("c"));

            Assert.Equal(
                new[]
                {
                    "* header",
                    "[jack4]>[a]",
                    "[mpedal]>[b]",
                    "[jack1]>[c]",
                    "* the factory diagram"
                },
                PedalFileSerializer.Merge(result.Configuration, result.SourceLines));
        }

        [Fact]
        public void Merge_WithAnAssignedInputAndNoPedalLineAtAll_AddsItAtTheEnd()
        {
            var result = PedalFixtures.Parse("* only a comment");

            result.Configuration[PedalInputId.LeftPedal].SetSingleAction(PedalFixtures.Key("a"));

            Assert.Equal(
                new[] { "* only a comment", "[lpedal]>[a]" },
                PedalFileSerializer.Merge(result.Configuration, result.SourceLines));
        }

        [Fact]
        public void Merge_WithAnInputThatFlippedMode_RewritesTheLineInPlaceInTheOtherBracketForm()
        {
            var result = PedalFixtures.Parse("* header", "[lpedal]>[a]", "* footer");
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(PedalFixtures.Key("b")));
            result.Configuration[PedalInputId.LeftPedal].SetMacro(macro);

            var lines = PedalFileSerializer.Merge(result.Configuration, result.SourceLines);

            Assert.Equal("* header", lines[0]);
            Assert.Equal("{lpedal}>{b}", lines[1]);
            Assert.Equal("* footer", lines[2]);
        }

        [Fact]
        public void Merge_WithDuplicateLinesForOneInput_RewritesEveryOneOfThemFromTheModel()
        {
            var lines = PedalFixtures.RoundTrip("[jack1]>[a]", "* between", "{jack1}>{b}");

            Assert.Equal("{jack1}>{b}", lines[0]);
            Assert.Equal("* between", lines[1]);
            Assert.Equal("{jack1}>{b}", lines[2]);
        }

        [Fact]
        public void Merge_WithInvalidPedalLine_ReplacesItWithTheModelState()
        {
            var lines = PedalFixtures.RoundTrip("[lpedal]>[garbage]");

            Assert.Equal("[lpedal]>", lines[0]);
        }
    }
}
