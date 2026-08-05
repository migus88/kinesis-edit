using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Tests.Keys
{
    public class KeyDefinitionTests
    {
        [Theory]
        [InlineData(TokenDialect.Legacy, "prtscr")]
        [InlineData(TokenDialect.Gen1, "prnt")]
        [InlineData(TokenDialect.Gen2, "prnt")]
        [InlineData(TokenDialect.None, "")]
        public void GetToken_WithEachDialect_ReturnsThatDialectsToken(TokenDialect dialect, string expectedToken)
        {
            var definition = CreatePrintScreenDefinition();

            Assert.Equal(expectedToken, definition.GetToken(dialect));
        }

        [Theory]
        [InlineData(TokenDialect.Legacy, true)]
        [InlineData(TokenDialect.Gen1, false)]
        [InlineData(TokenDialect.Gen2, false)]
        [InlineData(TokenDialect.None, false)]
        public void HasToken_WithLegacyOnlyEntry_ReturnsTrueOnlyForLegacy(TokenDialect dialect, bool expected)
        {
            var definition = CreateLegacyOnlyDefinition();

            Assert.Equal(expected, definition.HasToken(dialect));
        }

        [Theory]
        [InlineData(TokenDialect.Legacy, true)]
        [InlineData(TokenDialect.Gen1, false)]
        [InlineData(TokenDialect.Gen2, false)]
        [InlineData(TokenDialect.None, false)]
        public void IsAvailableIn_WithLegacyOnlyEntry_ReturnsTrueOnlyForLegacy(TokenDialect dialect, bool expected)
        {
            var definition = CreateLegacyOnlyDefinition();

            Assert.Equal(expected, definition.IsAvailableIn(dialect));
        }

        [Theory]
        [InlineData(TokenDialect.Legacy, "Key-\npad")]
        [InlineData(TokenDialect.Gen1, "Key-\npad")]
        [InlineData(TokenDialect.Gen2, "Kp\nToggle")]
        [InlineData(TokenDialect.None, "Key-\npad")]
        public void GetDisplayText_WithGen2Override_ReturnsOverrideOnlyForGen2(TokenDialect dialect, string expected)
        {
            var definition = new KeyDefinition
            {
                Code = 10042,
                Table = KeyTable.SpecialActions,
                Dialects = TokenDialects.All,
                LegacyToken = "kptoggle",
                Gen1Token = "kptoggle",
                Gen2Token = "kp",
                DisplayText = "Key-\npad",
                Gen2DisplayText = "Kp\nToggle"
            };

            Assert.Equal(expected, definition.GetDisplayText(dialect));
        }

        [Fact]
        public void GetDisplayText_WithoutOverrides_ReturnsDefaultDisplayTextForEveryDialect()
        {
            var definition = CreatePrintScreenDefinition();

            Assert.Equal("Print\nScrn", definition.GetDisplayText(TokenDialect.Legacy));
            Assert.Equal("Print\nScrn", definition.GetDisplayText(TokenDialect.Gen1));
            Assert.Equal("Print\nScrn", definition.GetDisplayText(TokenDialect.Gen2));
        }

        private static KeyDefinition CreatePrintScreenDefinition()
        {
            return new KeyDefinition
            {
                Code = 0x2C,
                Table = KeyTable.Navigation,
                Dialects = TokenDialects.All,
                LegacyToken = "prtscr",
                Gen1Token = "prnt",
                Gen2Token = "prnt",
                DisplayText = "Print\nScrn"
            };
        }

        private static KeyDefinition CreateLegacyOnlyDefinition()
        {
            return new KeyDefinition
            {
                Code = 10048,
                Table = KeyTable.KeypadKeys,
                Dialects = TokenDialects.Legacy,
                LegacyToken = "kpshft",
                DisplayText = "Kp\nShift"
            };
        }
    }
}
