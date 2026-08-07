using KinesisEdit.Core.Keys;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The cap-only abbreviation of the four captions that overflow a 1U key cap. Toolkit-free:
    /// the rule is a dictionary lookup over a string, and the coverage answer it is composed with
    /// is injected.
    /// </summary>
    public class KeyCapCaptionTests
    {
        [Theory]
        [InlineData("Forward", "Fwd")]
        [InlineData("Rewind", "Rew")]
        [InlineData("Record", "Rec")]
        [InlineData("Play\nPause", "Play")]
        public void For_WithACaptionTooWideForACap_Abbreviates(string caption, string expected)
        {
            Assert.Equal(expected, KeyCapCaption.For(caption));
        }

        [Theory]
        [InlineData("Next")]
        [InlineData("Prev")]
        [InlineData("Mute")]
        [InlineData("Vol-")]
        [InlineData("Vol+")]
        [InlineData("Eject")]
        [InlineData("NUL")]
        [InlineData("Led+")]
        [InlineData("Led-")]
        [InlineData("Stop")]
        [InlineData("LED")]
        [InlineData("Play")]
        [InlineData("Pause")]
        [InlineData("Caps\nLock")]
        [InlineData("A")]
        public void For_WithACaptionThatFits_LeavesItAlone(string caption)
        {
            Assert.Equal(caption, KeyCapCaption.For(caption));
        }

        [Fact]
        public void EveryShortenedCaption_IsStillOneTheKeyTableProduces()
        {
            // The same shape as the chrome gate's `EveryDeferredChromeGlyph_IsStillNeededByItsView`:
            // a table of exceptions rots silently unless something fails when an entry stops being
            // needed. If the key table is ever reworded, an abbreviation for a caption nothing
            // resolves to any more is dead code that still claims to be load-bearing.
            var produced = KeyRegistry.Entries
                .SelectMany(entry => new[] { TokenDialect.Legacy, TokenDialect.Gen1, TokenDialect.Gen2 }
                    .SelectMany(dialect => new[] { true, false }
                        .Select(isMacOs => KeyCaption.For(
                            entry,
                            dialect,
                            isMacOs,
                            FakeGlyphCoverage.CoveringNothing))))
                .ToHashSet(StringComparer.Ordinal);

            Assert.All(
                KeyCapCaption.Shortened,
                caption => Assert.True(
                    produced.Contains(caption),
                    $"KeyCapCaption shortens \"{caption.Replace("\n", "\\n")}\", which no key resolves to any more."));
        }

        [Fact]
        public void EveryCaptionTheKeyTableProduces_FitsACapOnceShortened()
        {
            // Roughly five characters fit a 1U cap at the 9/400 keycap step, and every caption the
            // key table can put on one is at or under that once the table above has run. Captions
            // longer than five survive only where they carry their own line break (`Caps\nLock`),
            // which the cap stacks, or where the position is wider than 1U -- but no *media or LED*
            // action, which is what this fix is about, lands on a wide position on any board.
            var overflowing = KeyRegistry.Entries
                .Where(entry => entry.GlyphText.Length > 0)
                .Select(entry => KeyCapCaption.For(
                    KeyCaption.For(entry, TokenDialect.Gen1, isMacOs: false, FakeGlyphCoverage.CoveringNothing)))
                .SelectMany(caption => caption.Split('\n'))
                .Where(line => line.Length > 5)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.Empty(overflowing);
        }
    }
}
