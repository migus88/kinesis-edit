using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The entry box's one display rule (specs/12-savant-elite.md §5): a space is shown as the
    /// open-box symbol so a recorded space is visible. It applies to the box only — the row memos
    /// keep the raw file tokens.
    /// </summary>
    public sealed class PedalEntryTextTests
    {
        [Fact]
        public void SpaceSymbol_IsTheOpenBoxCharacter()
        {
            Assert.Equal('␣', PedalEntryText.SpaceSymbol);
            Assert.Equal(' ', PedalEntryText.Space);
        }

        [Fact]
        public void Format_ForABareSpace_ShowsTheOpenBox()
        {
            Assert.Equal("␣", PedalEntryText.Format(" "));
        }

        [Fact]
        public void Format_ForAModifiedSpace_ShowsTheOpenBoxInsideTheGroup()
        {
            // A macro space under a held modifier is described as {lshift+ } (12 §5).
            Assert.Equal("{lshift+␣}", PedalEntryText.Format("{lshift+ }"));
        }

        [Fact]
        public void Format_ForAnItemWithoutSpaces_ReturnsItUnchanged()
        {
            Assert.Equal("[lmouse]", PedalEntryText.Format("[lmouse]"));
            Assert.Equal("{lmouse-dblclick}", PedalEntryText.Format("{lmouse-dblclick}"));
        }

        [Fact]
        public void Format_ForEveryItemOfAnEntry_KeepsTheirOrder()
        {
            var formatted = PedalEntryText.Format(new[] { "h", "i", " ", "{lshift+a}" });

            Assert.Equal(new[] { "h", "i", "␣", "{lshift+a}" }, formatted);
        }

        [Fact]
        public void Format_ForNothing_IsEmpty()
        {
            Assert.Equal(string.Empty, PedalEntryText.Format((string?)null));
            Assert.Empty(PedalEntryText.Format((IReadOnlyList<string>?)null));
            Assert.Empty(PedalEntryText.Format([]));
        }
    }
}
