using Avalonia.Headless.XUnit;
using KinesisEdit.Core.Keys;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The real glyph probe, against the fonts the app ships. <c>[AvaloniaFact]</c> throughout:
    /// the probe resolves its families through the running application and reads
    /// <c>FontManager.Current</c>, neither of which exists without one.
    /// </summary>
    public class EmbeddedFontGlyphCoverageTests
    {
        /// <summary>The moon of mockup 1e — the reference miss, absent from both families.</summary>
        private const string Uncovered = "☾";

        [AvaloniaFact]
        public void CanPrint_WithACharacterBothFamiliesCarry_IsTrue()
        {
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint("A"));
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint("1"));
        }

        [AvaloniaFact]
        public void CanPrint_WithACharacterNeitherFamilyCarries_IsFalse()
        {
            // If this ever turns true the probe has stopped reading the embedded families — it is
            // the same anti-vacuity guard the keycap-legend gate keeps in Design/.
            Assert.False(EmbeddedFontGlyphCoverage.Instance.CanPrint(Uncovered));
        }

        [AvaloniaFact]
        public void CanPrint_WithAnAstralCharacter_ReadsItAsOneRuneAndReportsItMissing()
        {
            // U+1F568 is above U+FFFF, so it is two chars in a C# string. A char walk would hand
            // each half of the surrogate pair to the font separately; the answer has to be about
            // the codepoint. It is the mute key's glyph (05 §3.7) and no embedded face carries it.
            const string mute = "\U0001F568";

            Assert.Equal(2, mute.Length);
            Assert.Single(mute.EnumerateRunes().ToList());
            Assert.False(EmbeddedFontGlyphCoverage.Instance.CanPrint(mute));
        }

        [AvaloniaFact]
        public void CanPrint_WithSeveralCharacters_RequiresEveryOneOfThem()
        {
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint("Play"));
            Assert.False(EmbeddedFontGlyphCoverage.Instance.CanPrint("Play" + Uncovered));
            Assert.False(EmbeddedFontGlyphCoverage.Instance.CanPrint(Uncovered + "Play"));
        }

        [AvaloniaFact]
        public void CanPrint_WithWhitespace_IgnoresIt()
        {
            // A caption's `\n` is a two-line key cap (05 §1.1), not a character a font draws.
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint("Play\nPause"));
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint(" "));
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint(string.Empty));
            Assert.True(EmbeddedFontGlyphCoverage.Instance.CanPrint(null));
        }

        [AvaloniaFact]
        public void CanPrint_AskedTwice_GivesTheSameAnswer()
        {
            // The answer is cached per codepoint because the fonts are inside the assembly and the
            // caption rule runs across ~100 positions x 5 layers on every refresh. A cache that
            // returned a different answer on the second read would be worse than none.
            var coverage = EmbeddedFontGlyphCoverage.Instance;

            Assert.Equal(coverage.CanPrint("A"), coverage.CanPrint("A"));
            Assert.True(coverage.CanPrint("A"));

            Assert.Equal(coverage.CanPrint(Uncovered), coverage.CanPrint(Uncovered));
            Assert.False(coverage.CanPrint(Uncovered));
        }

        [AvaloniaFact]
        public async Task CanPrint_FromOffTheUiThread_AnswersInsteadOfThrowing()
        {
            // The trap this probe was built around. `Application.Current` is a process-wide static
            // that outlives any one test, so a caption resolved on a worker thread finds an
            // application whose resources it may not read — and reading them anyway throws "Call
            // from invalid thread" from deep inside a view model's constructor. Roughly a third of
            // this project's own view-model suite resolves captions that way.
            var exception = await Record.ExceptionAsync(
                () => Task.Run(() => EmbeddedFontGlyphCoverage.Instance.CanPrint("A")));

            Assert.Null(exception);
        }

        [AvaloniaFact]
        public void CanPrint_ForEveryGlyphTheKeyTableCarries_IsFalseToday()
        {
            // The finding behind the whole rule: all 17 distinct GlyphText values of
            // specs/05-key-model.md §3.7/§3.9 are missing from every embedded IBM Plex face, so
            // the glyph column falls through in full. Should a future font ship one of them, this
            // fails and the caption for that key becomes the glyph — which is the intended
            // behaviour, not a defect, and this test is then the record of the change.
            var glyphs = KeyRegistry.Entries
                .Select(entry => entry.GlyphText)
                .Where(glyph => glyph.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            Assert.Equal(17, glyphs.Count);
            Assert.All(glyphs, glyph => Assert.False(EmbeddedFontGlyphCoverage.Instance.CanPrint(glyph), glyph));
        }
    }
}
