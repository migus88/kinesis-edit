using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// Macro-line parsing of <c>pedals.txt</c> (specs/12-savant-elite.md §4.2 press/release
    /// semantics and the active-modifier set, §4.3 the factory <c>{-x}{+x}</c> pair spelling,
    /// §4.4 the <c>{ }</c> ambiguity and the <c>{125}</c> delay spelling, §6 the different
    /// press &amp; release marker).
    /// </summary>
    public class PedalFileParserMacroTests
    {
        [Fact]
        public void Parse_WithFactoryPairSpelling_CollapsesEachPairIntoOneKeystroke()
        {
            var macro = ParseMacro("{jack4}>{-F1}{+F1}{-F2}{+F2}{-F3}{+F3}{-F4}{+F4}");

            Assert.Equal(4, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("F1"), macro.Keystrokes[0].Key);
            Assert.Equal(PedalFixtures.Key("F4"), macro.Keystrokes[3].Key);
            Assert.All(macro.Keystrokes, keystroke => Assert.Equal(KeyDirection.None, keystroke.EffectiveDirection));
        }

        [Fact]
        public void Parse_WithBareTokenSpelling_ReadsOnePressAndReleaseKeystroke()
        {
            var macro = ParseMacro("{jack4}>{a}{b}");

            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("a"), macro.Keystrokes[0].Key);
            Assert.Equal(MacroModifiers.None, macro.Keystrokes[0].Modifiers);
        }

        [Fact]
        public void Parse_WithHeldModifier_AttachesItToTheKeysItShields()
        {
            var macro = ParseMacro("{rpedal}>{-ctrl}{-alt}{-delete}{+delete}{+ctrl}{+alt}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalFixtures.Key("delete"), keystroke.Key);
            Assert.Equal(MacroModifiers.Control | MacroModifiers.Alt, keystroke.Modifiers);
        }

        [Fact]
        public void Parse_WithModifierPressedAndImmediatelyReleased_StoresAPlainKeystrokeOfThatModifier()
        {
            var macro = ParseMacro("{lpedal}>{-ctrl}{+ctrl}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalFixtures.Key("ctrl"), keystroke.Key);
            Assert.Equal(MacroModifiers.None, keystroke.Modifiers);
        }

        [Fact]
        public void Parse_WithModifierStillHeldAtLineEndAndNeverUsed_KeepsItAsATap()
        {
            var macro = ParseMacro("{lpedal}>{a}{-shift}");

            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("a"), macro.Keystrokes[0].Key);
            Assert.Equal(PedalFixtures.Key("shift"), macro.Keystrokes[1].Key);
        }

        [Fact]
        public void Parse_WithModifierReleaseWithoutPress_KeepsItAsATap()
        {
            var macro = ParseMacro("{lpedal}>{+shift}{a}");

            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("shift"), macro.Keystrokes[0].Key);
            Assert.Equal(MacroModifiers.None, macro.Keystrokes[1].Modifiers);
        }

        [Fact]
        public void Parse_WithSplitMarkerInsideAKeyPressPair_SetsDifferentPressReleaseOnThatKey()
        {
            var macro = ParseMacro("{lpedal}>{-k}{ }{+k}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalFixtures.Key("k"), keystroke.Key);
            Assert.True(keystroke.DiffPressRelease);
        }

        [Fact]
        public void Parse_WithSplitMarkerInsideAModifierWrap_SetsDifferentPressReleaseOnTheModifiedKey()
        {
            var macro = ParseMacro("{lpedal}>{-shift}{-k}{ }{+k}{+shift}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(MacroModifiers.Shift, keystroke.Modifiers);
            Assert.True(keystroke.DiffPressRelease);
        }

        [Fact]
        public void Parse_WithSplitMarkerAfterAModifiedKeyWhileModifiersAreHeld_ReadsAModifiedSpace()
        {
            // §6 appends a standalone marker only when the previous key has NO modifiers, so this
            // shape is a modifier-shielded space (§4.4: "{ }" resolves to the space key), not a
            // split of "x".
            var macro = ParseMacro("{lpedal}>{-ctrl}{x}{ }{+ctrl}");

            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("x"), macro.Keystrokes[0].Key);
            Assert.Equal(MacroModifiers.Control, macro.Keystrokes[0].Modifiers);
            Assert.Equal(PedalTokenMap.SpaceKey, macro.Keystrokes[1].Key);
            Assert.Equal(MacroModifiers.Control, macro.Keystrokes[1].Modifiers);
            Assert.All(macro.Keystrokes, keystroke => Assert.False(keystroke.DiffPressRelease));
        }

        [Fact]
        public void Parse_WithSplitMarkerInsideAModifierWrapAndNoKey_ReadsAModifiedSpace()
        {
            var macro = ParseMacro("{lpedal}>{-shift}{ }{+shift}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalTokenMap.SpaceKey, keystroke.Key);
            Assert.Equal(MacroModifiers.Shift, keystroke.Modifiers);
            Assert.False(keystroke.DiffPressRelease);
        }

        [Fact]
        public void Parse_WithASecondSplitMarkerOnTheSameKeyDown_ReadsTheSecondOneAsASpace()
        {
            var macro = ParseMacro("{jack1}>{-a}{ }{ }");

            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("a"), macro.Keystrokes[0].Key);
            Assert.True(macro.Keystrokes[0].DiffPressRelease);
            Assert.Equal(PedalTokenMap.SpaceKey, macro.Keystrokes[1].Key);
            Assert.False(macro.Keystrokes[1].DiffPressRelease);
        }

        [Fact]
        public void Parse_WithStandaloneSplitMarkerAndNoModifiersHeld_ReadsTheSpaceKey()
        {
            var macro = ParseMacro("{lpedal}>{a}{ }{b}");

            Assert.Equal(3, macro.Keystrokes.Count);
            Assert.Equal(PedalTokenMap.SpaceKey, macro.Keystrokes[1].Key);
            Assert.All(macro.Keystrokes, keystroke => Assert.False(keystroke.DiffPressRelease));
        }

        [Fact]
        public void Parse_WithFactorySpacePairSpelling_ReadsTheSpaceKey()
        {
            var macro = ParseMacro("{lpedal}>{-space}{+space}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalTokenMap.SpaceKey, keystroke.Key);
        }

        [Theory]
        [InlineData("{-.}{+.}", ".")]
        [InlineData("{-vol-}{+vol-}", "vol-")]
        [InlineData("{-vol+}{+vol+}", "vol+")]
        [InlineData("{-intl-\\}{+intl-\\}", "intl-\\")]
        [InlineData("{-hyphen}{+hyphen}", "hyphen")]
        public void Parse_WithDirectionPrefix_StripsOnlyTheFirstCharacter(string value, string token)
        {
            var macro = ParseMacro("{jack1}>" + value);

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalFixtures.Key(token), keystroke.Key);
        }

        [Theory]
        [InlineData("{125}", "d125")]
        [InlineData("{d125}", "d125")]
        [InlineData("{500}", "d500")]
        [InlineData("{d500}", "d500")]
        public void Parse_WithEitherDelaySpelling_ReadsTheSameDelayKey(string value, string token)
        {
            var macro = ParseMacro("{jack1}>" + value);

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalFixtures.Key(token), keystroke.Key);
            Assert.False(keystroke.WriteDownUp);
        }

        [Theory]
        [InlineData("speed1")]
        [InlineData("speed3")]
        [InlineData("speed5")]
        public void Parse_WithSpeedToken_ReadsItAsASingleEventKeystroke(string token)
        {
            var macro = ParseMacro("{jack1}>{" + token + "}{a}");

            Assert.Equal(2, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key(token), macro.Keystrokes[0].Key);
            Assert.False(macro.Keystrokes[0].WriteDownUp);
        }

        [Fact]
        public void Parse_WithWinToken_ResolvesTheModifierThatIsNotInTheKeyTable()
        {
            var macro = ParseMacro("{jack1}>{-win}{c}{+win}");

            var keystroke = Assert.Single(macro.Keystrokes);
            Assert.Equal(PedalFixtures.Key("c"), keystroke.Key);
            Assert.Equal(MacroModifiers.LeftWin, keystroke.Modifiers);
        }

        [Fact]
        public void Parse_WithFactoryDoubleClickMacro_ReadsThreeKeystrokes()
        {
            var macro = ParseMacro("{mpedal}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}");

            Assert.Equal(3, macro.Keystrokes.Count);
            Assert.Equal(PedalTokenMap.LeftMouseKey, macro.Keystrokes[0].Key);
            Assert.Equal(PedalTokenMap.Delay125Key, macro.Keystrokes[1].Key);
            Assert.Equal(PedalTokenMap.LeftMouseKey, macro.Keystrokes[2].Key);
        }

        [Fact]
        public void Parse_WithFactoryThankYouMacro_ReadsTheShiftedFirstLetterAndTheSpace()
        {
            var result = PedalFixtures.Parse(PedalFixtures.FactoryFile);
            var macro = result.Configuration[PedalInputId.Jack4].Macro!;

            Assert.Equal(10, macro.Keystrokes.Count);
            Assert.Equal(PedalFixtures.Key("t"), macro.Keystrokes[0].Key);
            Assert.Equal(MacroModifiers.Shift, macro.Keystrokes[0].Modifiers);
            Assert.Equal(PedalTokenMap.SpaceKey, macro.Keystrokes[5].Key);
            Assert.Equal(PedalFixtures.Key("."), macro.Keystrokes[9].Key);
        }

        private static Macro ParseMacro(string line)
        {
            var result = PedalFixtures.Parse(line);

            Assert.Empty(result.InvalidLines);

            foreach (var input in result.Configuration.Inputs)
            {
                if (input.Mode == PedalInputMode.Macro)
                {
                    return input.Macro!;
                }
            }

            throw new InvalidOperationException("The line did not produce a macro.");
        }
    }
}
