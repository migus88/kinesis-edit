using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Assignment display text and double-click recognition (specs/12-savant-elite.md §5 display
    /// conventions: "single keys as <c>[x]</c>, macro items as <c>x</c> or <c>{mod+key}</c>";
    /// §4.5/§6: the <c>lmouse</c> / 125 ms / <c>lmouse</c> triple shows as
    /// <c>{lmouse-dblclick}</c> in both field spellings).
    /// </summary>
    public class PedalActionDescriberTests
    {
        [Fact]
        public void Describe_WithUnassignedInput_ReturnsEmpty()
        {
            var configuration = new PedalConfiguration();

            Assert.Equal(string.Empty, PedalActionDescriber.Describe(configuration[PedalInputId.LeftPedal]));
        }

        [Fact]
        public void Describe_WithSingleAction_UsesTheBracketForm()
        {
            Assert.Equal("[lmouse]", Describe(PedalInputId.LeftPedal, "[lpedal]>[lmouse]"));
        }

        [Fact]
        public void Describe_WithPlainMacro_ConcatenatesTheItemTokens()
        {
            Assert.Equal("abc", Describe(PedalInputId.Jack1, "{jack1}>{a}{b}{c}"));
        }

        [Fact]
        public void Describe_WithModifiedKey_UsesTheBracedModifierForm()
        {
            Assert.Equal("{ctrl+alt+delete}", Describe(PedalInputId.RightPedal, PedalFixtures.HandEditedFile[1]));
        }

        [Fact]
        public void Describe_WithFactoryThankYouMacro_ReadsAsTheTypedText()
        {
            Assert.Equal("{shift+t}hank you.", Describe(PedalInputId.Jack4, PedalFixtures.FactoryFile[6]));
        }

        [Fact]
        public void Describe_WithDifferentPressReleaseKey_AppendsTheSplitMarker()
        {
            Assert.Equal("{shift+k}{ }", Describe(PedalInputId.LeftPedal, "{lpedal}>{-shift}{-k}{ }{+k}{+shift}"));
        }

        [Theory]
        [InlineData("{mpedal}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}")]
        [InlineData("{mpedal}>{lmouse}{d125}{lmouse}")]
        public void Describe_WithEitherDoubleClickSpelling_ShowsTheDoubleClickCaption(string line)
        {
            Assert.Equal(PedalActionDescriber.LeftMouseDoubleClickText, Describe(PedalInputId.MiddlePedal, line));
        }

        [Theory]
        [InlineData("{mpedal}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}")]
        [InlineData("{mpedal}>{lmouse}{d125}{lmouse}")]
        public void IsLeftMouseDoubleClick_WithEitherFieldSpelling_ReturnsTrue(string line)
        {
            var macro = PedalFixtures.Parse(line).Configuration[PedalInputId.MiddlePedal].Macro!;

            Assert.True(PedalActionDescriber.IsLeftMouseDoubleClick(macro));
        }

        [Theory]
        [InlineData("{mpedal}>{lmouse}{d500}{lmouse}")]
        [InlineData("{mpedal}>{lmouse}{d125}{rmouse}")]
        [InlineData("{mpedal}>{lmouse}{d125}{lmouse}{a}")]
        public void IsLeftMouseDoubleClick_WithAnythingElse_ReturnsFalse(string line)
        {
            var macro = PedalFixtures.Parse(line).Configuration[PedalInputId.MiddlePedal].Macro!;

            Assert.False(PedalActionDescriber.IsLeftMouseDoubleClick(macro));
        }

        [Fact]
        public void DescribeMacroItems_WithADoubleClickInsideALongerMacro_CollapsesOnlyThatTriple()
        {
            var macro = PedalFixtures.Parse("{jack1}>{a}{lmouse}{d125}{lmouse}{b}")
                .Configuration[PedalInputId.Jack1]
                .Macro!;

            Assert.Equal(
                new[] { "a", PedalActionDescriber.LeftMouseDoubleClickText, "b" },
                PedalActionDescriber.DescribeMacroItems(macro));
        }

        private static string Describe(PedalInputId input, params string[] lines)
        {
            return PedalActionDescriber.Describe(PedalFixtures.Parse(lines).Configuration[input]);
        }
    }
}
