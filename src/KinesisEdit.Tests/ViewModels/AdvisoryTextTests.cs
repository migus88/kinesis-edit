using KinesisEdit.ViewModels.Advisories;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The advisory copy, sentence by sentence. It is asserted here rather than through the view
    /// models that build it because the wording is the <b>design's</b>
    /// (<c>docs/design/mockups.md</c> 1e/1i): a sentence that drifts is a bug even when everything
    /// around it still works, and nothing else in the suite would notice.
    /// </summary>
    public sealed class AdvisoryTextTests
    {
        [Fact]
        public void LayerSummary_IsMockup1EsSentenceVerbatim()
        {
            Assert.Equal(
                "3 keys carry advisory notes on this layer — tap-and-hold count is 11 of 10. "
                + "Files from older firmware can exceed today's limits; nothing is blocked.",
                AdvisoryText.LayerSummary(3, AdvisoryText.TapAndHoldCountDetail(11, 10)));
        }

        [Fact]
        public void LayerSummary_ForOneKey_ReadsSingular()
        {
            Assert.StartsWith(
                "1 key carries advisory notes on this layer — ",
                AdvisoryText.LayerSummary(1, "something"),
                StringComparison.Ordinal);
        }

        [Fact]
        public void LayerSummary_AlwaysEndsInTheReassurance()
        {
            Assert.EndsWith(
                AdvisoryText.OlderFirmwareReassurance,
                AdvisoryText.LayerSummary(4, "something"),
                StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(1, "1 advisory note in this section — detail. Nothing is blocked.")]
        [InlineData(2, "2 advisory notes in this section — detail. Nothing is blocked.")]
        public void SectionSummary_ReadsCorrectlyInBothNumbers(int count, string expected)
        {
            Assert.Equal(expected, AdvisoryText.SectionSummary(count, "detail"));
        }

        [Fact]
        public void MacroCharacters_IsMockup1IsSentenceVerbatim()
        {
            Assert.Equal(
                "512 of 500 characters — over the device budget. Saved as-is.",
                AdvisoryText.MacroCharacters(512, 500));
        }

        [Fact]
        public void LayoutKeystrokeBudget_CarriesMockup1IsSpacedThousands()
        {
            // "layout keystroke budget 5 140 / 7 200" — a SPACE, not the invariant culture's comma.
            Assert.Equal(
                "layout keystroke budget 5 140 / 7 200 — over the device budget. Saved as-is.",
                AdvisoryText.LayoutKeystrokeBudget(5140, 7200));
        }

        [Fact]
        public void Number_GroupsThousandsWithASpace()
        {
            Assert.Equal("5 140", AdvisoryText.Number(5140));
            Assert.Equal("7 200", AdvisoryText.Number(7200));
            Assert.Equal("512", AdvisoryText.Number(512));
            Assert.Equal("1 234 567", AdvisoryText.Number(1234567));
        }

        [Fact]
        public void CoTriggers_IsMockup1IsRowVerbatim()
        {
            Assert.Equal("6 co-trigger modifiers — legacy budget is 4", AdvisoryText.CoTriggers(6, 4));
        }

        [Fact]
        public void DuplicateKey_NamesTheTokenAndEveryPositionAndEndsInTheAllowance()
        {
            var message = AdvisoryText.DuplicateKey("esc", [0, 20]);

            Assert.Equal("[esc] is on 2 positions of this layer — 0, 20. Duplicates are allowed.", message);
            Assert.EndsWith(AdvisoryText.DuplicatesAreAllowed, message, StringComparison.Ordinal);

            // The fragment stops short of "of this layer": a summary already said it.
            Assert.Equal("[esc] is on 2 positions", AdvisoryText.DuplicateKeyDetail("esc", [0, 20]));
            Assert.Equal(
                "2 keys carry advisory notes on this layer — [esc] is on 2 positions. "
                + AdvisoryText.OlderFirmwareReassurance,
                AdvisoryText.LayerSummary(2, AdvisoryText.DuplicateKeyDetail("esc", [0, 20])));
        }

        [Fact]
        public void Review_IsTheStripsAction()
        {
            Assert.Equal("Review 3", AdvisoryText.Review(3));
            Assert.Equal("Review 1", AdvisoryText.Review(1));
        }

        [Theory]
        [InlineData(1, "Saved with 1 advisory.")]
        [InlineData(3, "Saved with 3 advisories.")]
        [InlineData(1200, "Saved with 1 200 advisories.")]
        public void SavedWithAdvisories_ReadsCorrectlyInBothNumbers(int count, string expected)
        {
            Assert.Equal(expected, AdvisoryText.SavedWithAdvisories(count));
        }

        [Fact]
        public void EverySentence_IsFreeOfTheWordsOfARefusal()
        {
            // The law is "advisories never block". Copy that says something was refused, dropped or
            // clamped would be a lie about what the app just did, whatever the code does.
            var sentences = new[]
            {
                AdvisoryText.LayerSummary(3, AdvisoryText.TapAndHoldCountDetail(11, 10)),
                AdvisoryText.SectionSummary(2, AdvisoryText.MacroCharactersDetail(512, 500)),
                AdvisoryText.TapAndHoldCount(11, 10),
                AdvisoryText.MacroCharacters(512, 500),
                AdvisoryText.LayoutKeystrokeBudget(5140, 7200),
                AdvisoryText.CoTriggers(6, 4),
                AdvisoryText.DuplicateKey("esc", [0, 20]),
                AdvisoryText.SavedWithAdvisories(3)
            };

            foreach (var sentence in sentences)
            {
                foreach (var forbidden in new[] { "refus", "reject", "cannot", "was not saved", "dropped", "clamp" })
                {
                    Assert.DoesNotContain(forbidden, sentence, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }
}
