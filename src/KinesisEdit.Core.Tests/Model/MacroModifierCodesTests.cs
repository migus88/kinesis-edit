using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Model
{
    /// <summary>
    /// The two-character modifier codes of specs/05-key-model.md §5.1, including the trailing space
    /// of the generic codes that substring tests depend on, and the reverse mapping to modifier keys.
    /// </summary>
    public class MacroModifierCodesTests
    {
        [Theory]
        [InlineData(MacroModifiers.Shift, "S ")]
        [InlineData(MacroModifiers.LeftShift, "LS")]
        [InlineData(MacroModifiers.RightShift, "RS")]
        [InlineData(MacroModifiers.Control, "C ")]
        [InlineData(MacroModifiers.LeftControl, "LC")]
        [InlineData(MacroModifiers.RightControl, "RC")]
        [InlineData(MacroModifiers.Alt, "A ")]
        [InlineData(MacroModifiers.LeftAlt, "LA")]
        [InlineData(MacroModifiers.RightAlt, "RA")]
        [InlineData(MacroModifiers.Win, "W ")]
        [InlineData(MacroModifiers.LeftWin, "LW")]
        [InlineData(MacroModifiers.RightWin, "RW")]
        public void GetCode_ForEverySingleModifier_ReturnsTheTwoCharacterCode(MacroModifiers modifier, string expected)
        {
            Assert.Equal(expected, MacroModifierCodes.GetCode(modifier));
            Assert.Equal(MacroModifierCodes.CodeLength, expected.Length);
        }

        [Theory]
        [InlineData(MacroModifiers.Shift)]
        [InlineData(MacroModifiers.Control)]
        [InlineData(MacroModifiers.Alt)]
        [InlineData(MacroModifiers.Win)]
        public void GetCode_ForGenericModifiers_KeepsTheTrailingSpace(MacroModifiers modifier)
        {
            Assert.EndsWith(" ", MacroModifierCodes.GetCode(modifier), StringComparison.Ordinal);
        }

        [Fact]
        public void Format_ForASet_JoinsCodesWithCommasInSpecOrder()
        {
            var modifiers = MacroModifiers.LeftAlt | MacroModifiers.LeftControl | MacroModifiers.RightShift;

            Assert.Equal("RS,LC,LA", MacroModifierCodes.Format(modifiers));
        }

        [Fact]
        public void Format_ForNone_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, MacroModifierCodes.Format(MacroModifiers.None));
        }

        [Theory]
        [InlineData("S ", MacroModifiers.Shift)]
        [InlineData("s ", MacroModifiers.Shift)]
        [InlineData("S", MacroModifiers.Shift)]
        [InlineData("LS", MacroModifiers.LeftShift)]
        [InlineData("lc,la", MacroModifiers.LeftControl | MacroModifiers.LeftAlt)]
        [InlineData("", MacroModifiers.None)]
        [InlineData(null, MacroModifiers.None)]
        public void TryParse_ForKnownCodes_RoundTripsToFlags(string? text, MacroModifiers expected)
        {
            Assert.True(MacroModifierCodes.TryParse(text, out var modifiers));
            Assert.Equal(expected, modifiers);
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("LSS")]
        [InlineData("LS,ZZ")]
        public void TryParse_ForUnknownCodes_Fails(string text)
        {
            Assert.False(MacroModifierCodes.TryParse(text, out var modifiers));
            Assert.Equal(MacroModifiers.None, modifiers);
        }

        [Fact]
        public void FormatThenParse_ForEverySingleModifier_ReturnsTheSameFlag()
        {
            foreach (var modifier in MacroModifierCodes.All)
            {
                Assert.True(MacroModifierCodes.TryParse(MacroModifierCodes.Format(modifier), out var parsed));
                Assert.Equal(modifier, parsed);
            }
        }

        [Fact]
        public void GetKeyCode_ForGenericAndLeftWin_BothMapToTheLeftWinKey()
        {
            Assert.Equal(
                MacroModifierCodes.GetKeyCode(MacroModifiers.LeftWin),
                MacroModifierCodes.GetKeyCode(MacroModifiers.Win));
        }

        [Fact]
        public void TryFromKeyCode_ForTheLeftWinKey_ReturnsLeftWin()
        {
            var keyCode = MacroModifierCodes.GetKeyCode(MacroModifiers.LeftWin);

            Assert.True(MacroModifierCodes.TryFromKeyCode(keyCode, out var modifier));
            Assert.Equal(MacroModifiers.LeftWin, modifier);
        }

        // §5.1: "The modifier key set is VK_MENU, VK_LMENU, VK_RMENU, VK_SHIFT, VK_LSHIFT,
        // VK_RSHIFT, VK_CONTROL, VK_LCONTROL, VK_RCONTROL, VK_LWIN, VK_RWIN (standard Windows
        // codes)" — transcribed here from the spec rather than read back out of the table.
        [Theory]
        [InlineData(0x10)]
        [InlineData(0x11)]
        [InlineData(0x12)]
        [InlineData(0x5B)]
        [InlineData(0x5C)]
        [InlineData(0xA0)]
        [InlineData(0xA1)]
        [InlineData(0xA2)]
        [InlineData(0xA3)]
        [InlineData(0xA4)]
        [InlineData(0xA5)]
        public void IsModifierKeyCode_ForEveryModifierKeyOfTheSpec_IsTrue(int keyCode)
        {
            Assert.True(MacroModifierCodes.IsModifierKeyCode(keyCode));
        }

        [Theory]
        [InlineData(MacroModifiers.Shift, 0x10)]
        [InlineData(MacroModifiers.LeftShift, 0xA0)]
        [InlineData(MacroModifiers.RightShift, 0xA1)]
        [InlineData(MacroModifiers.Control, 0x11)]
        [InlineData(MacroModifiers.LeftControl, 0xA2)]
        [InlineData(MacroModifiers.RightControl, 0xA3)]
        [InlineData(MacroModifiers.Alt, 0x12)]
        [InlineData(MacroModifiers.LeftAlt, 0xA4)]
        [InlineData(MacroModifiers.RightAlt, 0xA5)]
        [InlineData(MacroModifiers.Win, 0x5B)]
        [InlineData(MacroModifiers.LeftWin, 0x5B)]
        [InlineData(MacroModifiers.RightWin, 0x5C)]
        public void GetKeyCode_ForEverySingleModifier_ReturnsTheWindowsVirtualKey(
            MacroModifiers modifier,
            int expectedKeyCode)
        {
            Assert.Equal(expectedKeyCode, MacroModifierCodes.GetKeyCode(modifier));
        }

        [Fact]
        public void IsModifierKeyCode_ForAnOrdinaryKey_IsFalse()
        {
            Assert.False(MacroModifierCodes.IsModifierKeyCode(ModelTokens.Key("a").Code));
        }

        [Theory]
        [InlineData(MacroModifiers.None, 0)]
        [InlineData(MacroModifiers.Shift, 1)]
        [InlineData(MacroModifiers.LeftControl | MacroModifiers.LeftAlt, 2)]
        [InlineData(MacroModifiers.LeftControl | MacroModifiers.LeftAlt | MacroModifiers.LeftShift, 3)]
        [InlineData(MacroModifiers.Win | MacroModifiers.LeftWin, 1)]
        [InlineData(MacroModifiers.Win | MacroModifiers.LeftWin | MacroModifiers.RightWin, 2)]
        public void Count_ForASet_CountsDistinctModifierKeys(MacroModifiers modifiers, int expected)
        {
            Assert.Equal(expected, MacroModifierCodes.Count(modifiers));
        }

        [Theory]
        [InlineData(MacroModifiers.Win | MacroModifiers.LeftWin, MacroModifiers.Win)]
        [InlineData(MacroModifiers.Win, MacroModifiers.Win)]
        [InlineData(MacroModifiers.LeftWin, MacroModifiers.Win)]
        [InlineData(MacroModifiers.LeftShift | MacroModifiers.RightShift, MacroModifiers.LeftShift | MacroModifiers.RightShift)]
        public void Canonicalize_ForCodesSharingAModifierKey_ReducesThemToOneSpelling(
            MacroModifiers modifiers,
            MacroModifiers expected)
        {
            // §5.1 maps "W " and "LW" to the same key, so every spelling of Left Win canonicalises
            // to the first of them in table order — comparing canonical sets compares held keys.
            Assert.Equal(expected, MacroModifierCodes.Canonicalize(modifiers));
        }

        [Fact]
        public void Canonicalize_ForBitsOutsideTheSpecTable_DropsThem()
        {
            Assert.Equal(
                MacroModifiers.LeftShift,
                MacroModifierCodes.Canonicalize(MacroModifiers.LeftShift | (MacroModifiers)0x8000));
        }

        [Fact]
        public void Known_ForTheSpecTable_HoldsTheTwelveDefinedFlagsAndNothingElse()
        {
            // §5.1 defines exactly twelve codes, and MacroModifiers assigns them bits 0..11 — so
            // the mask is 0x0FFF. Written out rather than re-derived from the table under test.
            Assert.Equal((MacroModifiers)0x0FFF, MacroModifierCodes.Known);
            Assert.Equal(12, MacroModifierCodes.All.Count);
        }
    }
}
