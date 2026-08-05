using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// One keystroke inside a macro per specs/05-key-model.md §1.1, §5.1 (modifiers are never
    /// attached to a modifier key), §5.2 (shifted/AltGr), §5.8 (when a direction applies) and the
    /// weighted keystroke count of specs/04-layout-file-format.md §5.3.
    /// </summary>
    public class KeystrokeTests
    {
        [Fact]
        public void Constructor_ForAnOrdinaryKey_DefaultsToPressReleaseWithoutModifiers()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"));

            Assert.Equal(MacroModifiers.None, keystroke.Modifiers);
            Assert.Equal(KeyDirection.None, keystroke.UpDown);
            Assert.True(keystroke.WriteDownUp);
            Assert.False(keystroke.DiffPressRelease);
        }

        [Fact]
        public void Constructor_ForADelayPseudoKey_ClearsWriteDownUp()
        {
            var keystroke = new Keystroke(ModelTokens.Key("d050"));

            Assert.False(keystroke.WriteDownUp);
        }

        [Fact]
        public void Modifiers_OnAModifierKey_AreDropped()
        {
            var keystroke = new Keystroke(ModelTokens.Key("lshft"), MacroModifiers.LeftControl)
            {
                Modifiers = MacroModifiers.LeftAlt
            };

            Assert.True(keystroke.IsModifierKey);
            Assert.Equal(MacroModifiers.None, keystroke.Modifiers);
        }

        [Fact]
        public void Key_WhenChangedToAModifierKey_ClearsTheModifiers()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift)
            {
                Key = ModelTokens.Key("lctrl")
            };

            Assert.Equal(MacroModifiers.None, keystroke.Modifiers);
        }

        [Theory]
        [InlineData(MacroModifiers.Shift, true)]
        [InlineData(MacroModifiers.LeftShift, true)]
        [InlineData(MacroModifiers.RightShift, true)]
        [InlineData(MacroModifiers.LeftShift | MacroModifiers.LeftControl, false)]
        [InlineData(MacroModifiers.None, false)]
        public void IsShifted_ForAModifierSet_MatchesTheShiftOnlyRule(MacroModifiers modifiers, bool expected)
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), modifiers);

            Assert.Equal(expected, keystroke.IsShifted);
        }

        [Theory]
        [InlineData(MacroModifiers.LeftControl | MacroModifiers.RightAlt, true)]
        [InlineData(MacroModifiers.Control | MacroModifiers.Alt, true)]
        [InlineData(MacroModifiers.LeftControl | MacroModifiers.RightAlt | MacroModifiers.LeftShift, false)]
        [InlineData(MacroModifiers.LeftControl | MacroModifiers.RightControl, false)]
        [InlineData(MacroModifiers.LeftAlt, false)]
        public void IsAltGr_ForAModifierSet_RequiresExactlyOneCtrlAltPair(MacroModifiers modifiers, bool expected)
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), modifiers);

            Assert.Equal(expected, keystroke.IsAltGr);
        }

        [Fact]
        public void EffectiveDirection_WithoutModifiers_IsTheRequestedDirection()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"))
            {
                UpDown = KeyDirection.Down
            };

            Assert.Equal(KeyDirection.Down, keystroke.EffectiveDirection);
        }

        [Fact]
        public void EffectiveDirection_WithModifiers_IsSuppressed()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift)
            {
                UpDown = KeyDirection.Up
            };

            Assert.Equal(KeyDirection.None, keystroke.EffectiveDirection);
        }

        [Fact]
        public void EffectiveDirection_OnAModifierKey_IsTheRequestedDirection()
        {
            var keystroke = new Keystroke(ModelTokens.Key("lshft"))
            {
                UpDown = KeyDirection.Down
            };

            Assert.Equal(KeyDirection.Down, keystroke.EffectiveDirection);
        }

        [Fact]
        public void EffectiveDirection_WhenWriteDownUpIsFalse_IsNone()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"))
            {
                UpDown = KeyDirection.Down,
                WriteDownUp = false
            };

            Assert.Equal(KeyDirection.None, keystroke.EffectiveDirection);
        }

        [Theory]
        [InlineData(MacroModifiers.None, 1)]
        [InlineData(MacroModifiers.LeftShift, 3)]
        [InlineData(MacroModifiers.LeftShift | MacroModifiers.LeftControl, 5)]
        [InlineData(MacroModifiers.LeftShift | MacroModifiers.LeftControl | MacroModifiers.LeftAlt, 7)]
        public void WeightedKeystrokeCount_ForAModifierSet_IsOnePlusTwoPerModifier(MacroModifiers modifiers, int expected)
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), modifiers);

            Assert.Equal(expected, keystroke.WeightedKeystrokeCount);
        }

        [Fact]
        public void FormatModifiers_ForASet_ReturnsTheSpecCodes()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftControl | MacroModifiers.LeftAlt);

            Assert.Equal("LC,LA", keystroke.FormatModifiers());
        }

        [Fact]
        public void Clone_ForAKeystroke_CopiesEveryFieldAndDetachesState()
        {
            var original = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift)
            {
                UpDown = KeyDirection.Up,
                WriteDownUp = false,
                DiffPressRelease = true
            };

            var clone = original.Clone();

            Assert.True(clone.IsEquivalentTo(original));
            Assert.NotSame(original, clone);

            clone.Modifiers = MacroModifiers.None;

            Assert.Equal(MacroModifiers.LeftShift, original.Modifiers);
        }

        [Fact]
        public void Modifiers_ForBitsOutsideTheSpecTable_DropsThem()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), (MacroModifiers)0x8000)
            {
                Modifiers = MacroModifiers.LeftShift | (MacroModifiers)0x8000
            };

            // §5.1 defines twelve codes; a bit outside them names no modifier, so it is not stored —
            // otherwise two keystrokes that render alike would compare unequal.
            Assert.Equal(MacroModifiers.LeftShift, keystroke.Modifiers);
            Assert.Equal(1, keystroke.ModifierCount);
        }

        [Fact]
        public void IsEquivalentTo_ForKeystrokesDifferingOnlyInUndefinedBits_IsTrue()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift);
            var other = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift | (MacroModifiers)0x8000);

            Assert.Equal(keystroke.FormatModifiers(), other.FormatModifiers());
            Assert.True(keystroke.IsEquivalentTo(other));
        }

        [Fact]
        public void ModifierCount_ForGenericAndLeftWinTogether_CountsOnePhysicalKey()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), MacroModifiers.Win | MacroModifiers.LeftWin);

            // §5.1: "W " and "LW" both map to the Left Win key, and the active-modifier set is
            // deduplicated by key code — so the keystroke accounting of 04 §5.3 sees one modifier.
            Assert.Equal(1, keystroke.ModifierCount);
            Assert.Equal(3, keystroke.WeightedKeystrokeCount);
            Assert.Equal("W ,LW", keystroke.FormatModifiers());
        }

        [Fact]
        public void IsEquivalentTo_ForKeystrokesDifferingInOneField_IsFalse()
        {
            var keystroke = new Keystroke(ModelTokens.Key("a"), MacroModifiers.LeftShift);
            var other = new Keystroke(ModelTokens.Key("a"), MacroModifiers.RightShift);

            Assert.False(keystroke.IsEquivalentTo(other));
            Assert.False(keystroke.IsEquivalentTo(null));
        }

        [Fact]
        public void IsEquivalentTo_ForDifferentKeyTableEntriesWithTheSameToken_IsFalse()
        {
            var keypadSeven = new Keystroke(ModelTokens.Key("kp7", TokenDialect.Legacy));
            var keypadLayerSeven = new Keystroke(ModelTokens.KeyByCode(10063));

            Assert.Equal(keypadSeven.Key.LegacyToken, keypadLayerSeven.Key.LegacyToken);
            Assert.False(keypadSeven.IsEquivalentTo(keypadLayerSeven));
        }
    }
}
