using KinesisEdit.Core.Input;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Input
{
    /// <summary>
    /// Pins the physical-position → key-table mapping required by specs/10-apps-and-ui.md
    /// ("Keyboard-input capture"): the left/right modifier distinction of
    /// specs/05-key-model.md §3.5, the keypad Enter as a distinct internal code
    /// (specs/12-savant-elite.md §"Programming a pedal" step 3), and the guarantee that every
    /// declared position resolves to a real key-table entry.
    /// </summary>
    public class PhysicalKeyMapTests
    {
        public static IEnumerable<object[]> AllDeclaredKeys()
        {
            foreach (var key in Enum.GetValues<PhysicalKeyCode>())
            {
                if (key != PhysicalKeyCode.None)
                {
                    yield return new object[] { key };
                }
            }
        }

        [Theory]
        [InlineData(PhysicalKeyCode.ShiftLeft, 160, "lshift", "lshft", "lshf")]
        [InlineData(PhysicalKeyCode.ShiftRight, 161, "rshift", "rshft", "rshf")]
        [InlineData(PhysicalKeyCode.ControlLeft, 162, "lctrl", "lctrl", "lctr")]
        [InlineData(PhysicalKeyCode.ControlRight, 163, "rctrl", "rctrl", "rctr")]
        [InlineData(PhysicalKeyCode.AltLeft, 164, "lalt", "lalt", "lalt")]
        [InlineData(PhysicalKeyCode.AltRight, 165, "ralt", "ralt", "ralt")]
        [InlineData(PhysicalKeyCode.MetaLeft, 91, "lwin", "lwin", "lwin")]
        [InlineData(PhysicalKeyCode.MetaRight, 92, "rwin", "rwin", "rwin")]
        public void Resolve_WithModifierPosition_ReturnsSpecSection35Entry(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedLegacyToken,
            string expectedGen1Token,
            string expectedGen2Token)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedLegacyToken, definition.LegacyToken);
            Assert.Equal(expectedGen1Token, definition.Gen1Token);
            Assert.Equal(expectedGen2Token, definition.Gen2Token);
        }

        [Fact]
        public void Resolve_AcrossModifierPositions_ReturnsEightDistinctCodes()
        {
            var modifiers = new[]
            {
                PhysicalKeyCode.ShiftLeft,
                PhysicalKeyCode.ShiftRight,
                PhysicalKeyCode.ControlLeft,
                PhysicalKeyCode.ControlRight,
                PhysicalKeyCode.AltLeft,
                PhysicalKeyCode.AltRight,
                PhysicalKeyCode.MetaLeft,
                PhysicalKeyCode.MetaRight
            };

            var codes = modifiers.Select(key => PhysicalKeyMap.Resolve(key)!.Code).ToList();

            Assert.Equal(8, codes.Distinct().Count());
        }

        [Fact]
        public void Resolve_WithNumPadEnter_ReturnsTheDistinctInternalCode10000()
        {
            var definition = PhysicalKeyMap.Resolve(PhysicalKeyCode.NumPadEnter);

            Assert.NotNull(definition);
            Assert.Equal(10000, definition.Code);
            Assert.Equal("kpenter", definition.LegacyToken);
            Assert.Equal("kpent", definition.Gen1Token);
            Assert.Equal("kpen", definition.Gen2Token);
        }

        [Fact]
        public void Resolve_WithNumPadEnterAndEnter_ReturnsDifferentEntries()
        {
            var keypadEnter = PhysicalKeyMap.Resolve(PhysicalKeyCode.NumPadEnter);
            var mainEnter = PhysicalKeyMap.Resolve(PhysicalKeyCode.Enter);

            Assert.NotNull(keypadEnter);
            Assert.NotNull(mainEnter);
            Assert.NotEqual(keypadEnter.Code, mainEnter.Code);
            Assert.Equal(0x0D, mainEnter.Code);
            Assert.NotSame(keypadEnter, mainEnter);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.A, 65, "a")]
        [InlineData(PhysicalKeyCode.M, 77, "m")]
        [InlineData(PhysicalKeyCode.Z, 90, "z")]
        [InlineData(PhysicalKeyCode.Digit0, 48, "0")]
        [InlineData(PhysicalKeyCode.Digit9, 57, "9")]
        public void Resolve_WithLetterOrDigit_ReturnsAsciiUppercaseVirtualKey(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedToken)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedToken, definition.LegacyToken);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.Backquote, 0xC0, "`")]
        [InlineData(PhysicalKeyCode.Minus, 0xBD, "hyphen")]
        [InlineData(PhysicalKeyCode.Equal, 0xBB, "=")]
        [InlineData(PhysicalKeyCode.BracketLeft, 0xDB, "obrack")]
        [InlineData(PhysicalKeyCode.BracketRight, 0xDD, "cbrack")]
        [InlineData(PhysicalKeyCode.Backslash, 0xDC, "\\")]
        [InlineData(PhysicalKeyCode.Semicolon, 0xBA, ";")]
        [InlineData(PhysicalKeyCode.Quote, 0xDE, "'")]
        [InlineData(PhysicalKeyCode.Comma, 0xBC, ",")]
        [InlineData(PhysicalKeyCode.Period, 0xBE, ".")]
        [InlineData(PhysicalKeyCode.Slash, 0xBF, "/")]
        [InlineData(PhysicalKeyCode.IntlBackslash, 0xE2, "intl-\\")]
        public void Resolve_WithPunctuationPosition_ReturnsSpecSection32Entry(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedLegacyToken)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedLegacyToken, definition.LegacyToken);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.F1, 0x70, "F1")]
        [InlineData(PhysicalKeyCode.F12, 0x7B, "F12")]
        [InlineData(PhysicalKeyCode.F13, 0x7C, "F13")]
        [InlineData(PhysicalKeyCode.F24, 0x87, "F24")]
        public void Resolve_WithFunctionKey_ReturnsSpecSection34Entry(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedToken)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedToken, definition.LegacyToken);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.Escape, 0x1B, "escape")]
        [InlineData(PhysicalKeyCode.Backspace, 0x08, "bspace")]
        [InlineData(PhysicalKeyCode.Tab, 0x09, "tab")]
        [InlineData(PhysicalKeyCode.Space, 0x20, "space")]
        [InlineData(PhysicalKeyCode.CapsLock, 0x14, "caps")]
        [InlineData(PhysicalKeyCode.Insert, 0x2D, "insert")]
        [InlineData(PhysicalKeyCode.Delete, 0x2E, "delete")]
        [InlineData(PhysicalKeyCode.Home, 0x24, "home")]
        [InlineData(PhysicalKeyCode.End, 0x23, "end")]
        [InlineData(PhysicalKeyCode.PageUp, 0x21, "pup")]
        [InlineData(PhysicalKeyCode.PageDown, 0x22, "pdown")]
        [InlineData(PhysicalKeyCode.ArrowUp, 0x26, "up")]
        [InlineData(PhysicalKeyCode.ArrowDown, 0x28, "down")]
        [InlineData(PhysicalKeyCode.ArrowLeft, 0x25, "left")]
        [InlineData(PhysicalKeyCode.ArrowRight, 0x27, "right")]
        [InlineData(PhysicalKeyCode.ScrollLock, 0x91, "scroll")]
        [InlineData(PhysicalKeyCode.Pause, 0x13, "pause")]
        [InlineData(PhysicalKeyCode.NumLock, 0x90, "numlk")]
        [InlineData(PhysicalKeyCode.ContextMenu, 0x5D, "menu")]
        public void Resolve_WithNavigationPosition_ReturnsSpecSection33Entry(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedLegacyToken)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedLegacyToken, definition.LegacyToken);
        }

        [Fact]
        public void Resolve_WithPrintScreen_ReturnsTheSnapshotVirtualKey()
        {
            var definition = PhysicalKeyMap.Resolve(PhysicalKeyCode.PrintScreen);

            Assert.NotNull(definition);
            Assert.Equal(0x2C, definition.Code);
            Assert.Equal("prtscr", definition.LegacyToken);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.NumPad0, 0x60, "kp0")]
        [InlineData(PhysicalKeyCode.NumPad9, 0x69, "kp9")]
        [InlineData(PhysicalKeyCode.NumPadDivide, 0x6F, "kpdiv")]
        [InlineData(PhysicalKeyCode.NumPadMultiply, 0x6A, "kpmult")]
        [InlineData(PhysicalKeyCode.NumPadSubtract, 0x6D, "kpmin")]
        [InlineData(PhysicalKeyCode.NumPadAdd, 0x6B, "kpplus")]
        [InlineData(PhysicalKeyCode.NumPadDecimal, 0x6E, "kp.")]
        [InlineData(PhysicalKeyCode.NumPadEqual, 10053, "kp=")]
        public void Resolve_WithKeypadPosition_ReturnsSpecSection36Entry(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedLegacyToken)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedLegacyToken, definition.LegacyToken);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.AudioVolumeMute, 173, "mute")]
        [InlineData(PhysicalKeyCode.AudioVolumeDown, 174, "vol-")]
        [InlineData(PhysicalKeyCode.AudioVolumeUp, 175, "vol+")]
        [InlineData(PhysicalKeyCode.MediaTrackNext, 176, "next")]
        [InlineData(PhysicalKeyCode.MediaTrackPrevious, 177, "prev")]
        [InlineData(PhysicalKeyCode.MediaStop, 178, "stop")]
        [InlineData(PhysicalKeyCode.MediaPlayPause, 179, "play")]
        [InlineData(PhysicalKeyCode.Eject, 11150, "ejct")]
        public void Resolve_WithMediaPosition_ReturnsSpecSection37Entry(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedLegacyToken)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedLegacyToken, definition.LegacyToken);
            Assert.Equal(KeyTable.MediaKeys, definition.Table);
        }

        [Fact]
        public void Resolve_WithNone_ReturnsNull()
        {
            var definition = PhysicalKeyMap.Resolve(PhysicalKeyCode.None);

            Assert.Null(definition);
        }

        [Fact]
        public void Resolve_WithUndefinedEnumValue_ReturnsNull()
        {
            var definition = PhysicalKeyMap.Resolve((PhysicalKeyCode)9999);

            Assert.Null(definition);
        }

        [Theory]
        [MemberData(nameof(AllDeclaredKeys))]
        public void Resolve_WithEveryDeclaredPosition_ReturnsAKeyTableEntry(PhysicalKeyCode key)
        {
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.True(
                definition.HasToken(TokenDialect.Legacy)
                || definition.HasToken(TokenDialect.Gen1)
                || definition.HasToken(TokenDialect.Gen2),
                $"{key} resolves to code {definition.Code}, which has no file token in any dialect.");
        }

        [Fact]
        public void Resolve_AcrossEveryDeclaredPosition_ReturnsADistinctCodePerPosition()
        {
            var codesByPosition = AllDeclaredKeys()
                .Select(data => (PhysicalKeyCode)data[0])
                .ToDictionary(key => key, key => PhysicalKeyMap.Resolve(key)!.Code);

            var duplicates = codesByPosition
                .GroupBy(pair => pair.Value)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key}: {string.Join(", ", group.Select(pair => pair.Key))}")
                .ToList();

            Assert.True(duplicates.Count == 0, $"Positions collapsed onto one code — {string.Join("; ", duplicates)}");
        }

        [Theory]
        [InlineData(PhysicalKeyCode.NumLock, 0x90, "numlk", KeyTable.Navigation)]
        [InlineData(PhysicalKeyCode.PrintScreen, 0x2C, "prtscr", KeyTable.Navigation)]
        [InlineData(PhysicalKeyCode.Enter, 0x0D, "enter", KeyTable.Navigation)]
        [InlineData(PhysicalKeyCode.Space, 0x20, "space", KeyTable.Navigation)]
        [InlineData(PhysicalKeyCode.NumPadDivide, 0x6F, "kpdiv", KeyTable.KeypadKeys)]
        [InlineData(PhysicalKeyCode.NumPadMultiply, 0x6A, "kpmult", KeyTable.KeypadKeys)]
        [InlineData(PhysicalKeyCode.NumPadSubtract, 0x6D, "kpmin", KeyTable.KeypadKeys)]
        [InlineData(PhysicalKeyCode.NumPadAdd, 0x6B, "kpplus", KeyTable.KeypadKeys)]
        [InlineData(PhysicalKeyCode.NumPadDecimal, 0x6E, "kp.", KeyTable.KeypadKeys)]
        [InlineData(PhysicalKeyCode.NumPadEnter, 10000, "kpenter", KeyTable.KeypadKeys)]
        [InlineData(PhysicalKeyCode.NumPadEqual, 10053, "kp=", KeyTable.KeypadKeys)]
        public void Resolve_WithAPositionWhoseTokenIsRegisteredTwice_ReturnsTheCanonicalCode(
            PhysicalKeyCode key,
            int expectedCode,
            string expectedLegacyToken,
            KeyTable expectedTable)
        {
            // These tokens (and captions) also appear on keypad-layer duplicate codes such as
            // 10052 numlk, 10054 kpdiv or 10041 rp-kpent (specs/05-key-model.md §3.6/§3.9 notes),
            // so a token-shaped assertion alone would not catch a mapping pointed at the wrong
            // registration. The exact code and table are what the capture layer must produce.
            var definition = PhysicalKeyMap.Resolve(key);

            Assert.NotNull(definition);
            Assert.Equal(expectedCode, definition.Code);
            Assert.Equal(expectedLegacyToken, definition.LegacyToken);
            Assert.Equal(expectedTable, definition.Table);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.ShiftLeft)]
        [InlineData(PhysicalKeyCode.ShiftRight)]
        [InlineData(PhysicalKeyCode.ControlLeft)]
        [InlineData(PhysicalKeyCode.ControlRight)]
        [InlineData(PhysicalKeyCode.AltLeft)]
        [InlineData(PhysicalKeyCode.AltRight)]
        [InlineData(PhysicalKeyCode.MetaLeft)]
        [InlineData(PhysicalKeyCode.MetaRight)]
        public void IsModifier_WithModifierPosition_ReturnsTrue(PhysicalKeyCode key)
        {
            var isModifier = PhysicalKeyMap.IsModifier(key);

            Assert.True(isModifier);
        }

        [Theory]
        [InlineData(PhysicalKeyCode.None)]
        [InlineData(PhysicalKeyCode.A)]
        [InlineData(PhysicalKeyCode.Digit5)]
        [InlineData(PhysicalKeyCode.CapsLock)]
        [InlineData(PhysicalKeyCode.Enter)]
        [InlineData(PhysicalKeyCode.NumPadEnter)]
        [InlineData(PhysicalKeyCode.F1)]
        [InlineData(PhysicalKeyCode.PrintScreen)]
        [InlineData(PhysicalKeyCode.ContextMenu)]
        [InlineData((PhysicalKeyCode)9999)]
        public void IsModifier_WithNonModifierPosition_ReturnsFalse(PhysicalKeyCode key)
        {
            var isModifier = PhysicalKeyMap.IsModifier(key);

            Assert.False(isModifier);
        }

        [Fact]
        public void IsModifier_AcrossDeclaredPositions_IsTrueForExactlyEight()
        {
            var modifierCount = Enum.GetValues<PhysicalKeyCode>().Count(PhysicalKeyMap.IsModifier);

            Assert.Equal(8, modifierCount);
        }
    }
}
