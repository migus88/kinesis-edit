using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Load→save fidelity of the two real device files of specs/12-savant-elite.md §4.1 (factory)
    /// and §4.5 (hand-edited): every comment line survives byte-for-byte, every assignment
    /// survives semantically, and legacy spellings normalize to the §4.3 canonical dialect instead
    /// of failing.
    /// </summary>
    public class PedalRoundTripTests
    {
        [Fact]
        public void RoundTrip_WithFactoryFile_KeepsEveryCommentLineByteForByte()
        {
            var lines = PedalFixtures.RoundTrip(PedalFixtures.FactoryFile);

            Assert.Equal(PedalFixtures.FactoryFile.Length, lines.Count);

            for (var i = 7; i < PedalFixtures.FactoryFile.Length; i++)
            {
                Assert.Equal(PedalFixtures.FactoryFile[i], lines[i]);
            }
        }

        [Fact]
        public void RoundTrip_WithFactoryFile_NormalizesThePairSpellingsToTheCanonicalDialect()
        {
            var lines = PedalFixtures.RoundTrip(PedalFixtures.FactoryFile);

            Assert.Equal(
                new[]
                {
                    "[lpedal]>[lmouse]",
                    "{mpedal}>{lmouse}{d125}{lmouse}",
                    "[rpedal]>[rmouse]",
                    "[jack1]>[lmouse]",
                    "[jack2]>[rmouse]",
                    "[jack3]>[bspace]",
                    "{jack4}>{-shift}{t}{+shift}{h}{a}{n}{k}{ }{y}{o}{u}{.}"
                },
                lines.Take(7));
        }

        [Fact]
        public void RoundTrip_WithFactoryFileTwice_IsStableAfterTheFirstSave()
        {
            var once = PedalFixtures.RoundTrip(PedalFixtures.FactoryFile);
            var twice = PedalFixtures.RoundTrip([.. once]);

            Assert.Equal(once, twice);
        }

        [Fact]
        public void RoundTrip_WithHandEditedFile_RewritesItInTheCanonicalDialect()
        {
            var lines = PedalFixtures.RoundTrip(PedalFixtures.HandEditedFile);

            Assert.Equal(
                new[]
                {
                    "[mpedal]>[space]",
                    "{rpedal}>{-ctrl}{-alt}{delete}{+ctrl}{+alt}",
                    "[jack1]>[F1]",
                    "{jack4}>{F1}{F2}{F3}{F4}"
                },
                lines);
        }

        [Fact]
        public void RoundTrip_WithAFileListingOnlySomeInputs_AddsNoLines()
        {
            // 12 §1: "your device will only have some of these inputs" — a four-input file stays a
            // four-line file, because §4.6 rewrites lines and never appends any.
            var lines = PedalFixtures.RoundTrip(PedalFixtures.HandEditedFile);

            Assert.Equal(PedalFixtures.HandEditedFile.Length, lines.Count);
        }

        [Fact]
        public void RoundTrip_WithDifferentPressReleaseKey_KeepsTheSplitMarkerOnTheSameKey()
        {
            var lines = PedalFixtures.RoundTrip("{lpedal}>{-a}{ }{+a}");

            Assert.Equal("{lpedal}>{-a}{ }{+a}", lines[0]);
        }

        [Fact]
        public void RoundTrip_WithCanonicalFile_IsByteEqual()
        {
            // The jack2 line carries a "{ }" in both positions: inside the Shift wrap and after it.
            // Under the §4.4 rule both are spaces, so the line survives verbatim either way.
            string[] file =
            [
                "[lpedal]>[lmouse]",
                "{mpedal}>{lmouse}{d125}{lmouse}",
                "[rpedal]>",
                "[jack1]>[F1]",
                "{jack2}>{-shift}{a}{b}{ }{+shift}{ }{speed5}",
                "[jack3]>[vol+]",
                "{jack4}>{-ctrl}{-alt}{delete}{+ctrl}{+alt}"
            ];

            Assert.Equal(file, PedalFixtures.RoundTrip(file));
        }

        [Fact]
        public void RoundTrip_WithAModifierShieldedSpace_KeepsTheSpaceKeystroke()
        {
            // The regression the old "modifiers held → flag the previous key" rule caused: Ctrl+a
            // then Ctrl+space wrote "{-ctrl}{a}{ }{+ctrl}" and reloaded as a single flagged "a".
            var input = new PedalInput(PedalInputId.Jack1);
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(PedalFixtures.Key("a"), MacroModifiers.Control));
            macro.AddKeystroke(new Keystroke(PedalTokenMap.SpaceKey, MacroModifiers.Control));
            input.SetMacro(macro);

            var line = PedalFileSerializer.SerializeInput(input);

            Assert.Equal("{jack1}>{-ctrl}{a}{ }{+ctrl}", line);

            var reloaded = PedalFixtures.Parse(line).Configuration[PedalInputId.Jack1];

            Assert.Equal(2, reloaded.Macro!.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("a"), reloaded.Macro.Keystrokes[0].Key);
            Assert.Equal(PedalTokenMap.SpaceKey, reloaded.Macro.Keystrokes[1].Key);
            Assert.All(reloaded.Macro.Keystrokes, keystroke => Assert.Equal(MacroModifiers.Control, keystroke.Modifiers));
            Assert.All(reloaded.Macro.Keystrokes, keystroke => Assert.False(keystroke.DiffPressRelease));
            Assert.Equal(line, PedalFileSerializer.SerializeInput(reloaded));
        }
    }
}
