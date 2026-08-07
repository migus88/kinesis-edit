using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class SettingsLineReaderTests
    {
        [Fact]
        public void FindValue_WithMissingKey_ReturnsNull()
        {
            var lines = new[] { "macro_speed=5" };

            Assert.Null(SettingsLineReader.FindValue(lines, "game_mode"));
        }

        [Fact]
        public void FindValue_WithMatchingKey_ReturnsEverythingAfterSeparator()
        {
            var lines = new[] { "startup_file=layout3.txt" };

            Assert.Equal("layout3.txt", SettingsLineReader.FindValue(lines, "startup_file"));
        }

        [Fact]
        public void FindValue_WithDifferentlyCasedKeyInFile_MatchesCaseInsensitively()
        {
            var lines = new[] { "GAME_MODE=ON" };

            Assert.Equal("ON", SettingsLineReader.FindValue(lines, "game_mode"));
        }

        [Fact]
        public void FindValue_WithEmptyValue_ReturnsEmptyString()
        {
            var lines = new[] { "led_mode=" };

            Assert.Equal(string.Empty, SettingsLineReader.FindValue(lines, "led_mode"));
        }

        [Fact]
        public void FindValue_WithValueContainingSeparator_ReturnsFullRemainder()
        {
            var lines = new[] { "country=a=b" };

            Assert.Equal("a=b", SettingsLineReader.FindValue(lines, "country"));
        }

        [Fact]
        public void FindValue_WithMultipleOccurrences_ReturnsLastOccurrence()
        {
            var lines = new[] { "macro_speed=1", "power_user=true", "macro_speed=7" };

            Assert.Equal("7", SettingsLineReader.FindValue(lines, "macro_speed"));
        }

        [Theory]
        [InlineData("v_drive", "v_drive_open_on_startup=ON")]
        [InlineData("cust_color_1", "cust_color_10=[1][2][3]")]
        [InlineData("status", "status_play_speed=3")]
        public void FindValue_WithLongerCollidingKeyOnly_DoesNotMatchShorterKey(string shorterKey, string longerKeyLine)
        {
            var lines = new[] { longerKeyLine };

            Assert.Null(SettingsLineReader.FindValue(lines, shorterKey));
        }

        [Theory]
        [InlineData("v_drive=auto", "v_drive_open_on_startup", "v_drive_open_on_startup=ON", "ON")]
        [InlineData("cust_color_1=[9][9][9]", "cust_color_10", "cust_color_10=[1][2][3]", "[1][2][3]")]
        [InlineData("status=2", "status_play_speed", "status_play_speed=3", "3")]
        public void FindValue_WithBothCollidingKeysPresent_ReturnsExactKeyValue(
            string shorterKeyLine,
            string longerKey,
            string longerKeyLine,
            string expectedValue)
        {
            var lines = new[] { shorterKeyLine, longerKeyLine };

            Assert.Equal(expectedValue, SettingsLineReader.FindValue(lines, longerKey));
        }

        [Fact]
        public void FindValue_WithKeyLineLackingSeparator_DoesNotMatch()
        {
            var lines = new[] { "game_mode", "game_modes=ON" };

            Assert.Null(SettingsLineReader.FindValue(lines, "game_mode"));
        }

        [Fact]
        public void FindValue_WithNullLines_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => SettingsLineReader.FindValue(null!, "key"));
        }

        [Fact]
        public void FindValue_WithEmptyKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => SettingsLineReader.FindValue([], string.Empty));
        }

        [Fact]
        public void FindPrefixedValues_ReturnsTheKeyRemainderAndValueInFileOrder()
        {
            var lines = new[]
            {
                "save_msg=on",
                "macro_name_1_0_65_1=Sign-off",
                "macro_name_2_1_70_3=Other",
                "macro_speed=5"
            };

            var matches = SettingsLineReader.FindPrefixedValues(lines, "macro_name_");

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("1_0_65_1", "Sign-off"),
                    KeyValuePair.Create("2_1_70_3", "Other"),
                },
                matches);
        }

        [Fact]
        public void FindPrefixedValues_IsCaseInsensitiveOnThePrefix()
        {
            var matches = SettingsLineReader.FindPrefixedValues(["MACRO_NAME_1_0_65_1=Sign-off"], "macro_name_");

            Assert.Equal("1_0_65_1", Assert.Single(matches).Key);
        }

        [Fact]
        public void FindPrefixedValues_KeepsDuplicatesInOrderSoTheLastOneCanWin()
        {
            var lines = new[] { "macro_name_1_0_65_1=First", "macro_name_1_0_65_1=Second" };

            var matches = SettingsLineReader.FindPrefixedValues(lines, "macro_name_");

            Assert.Equal(new[] { "First", "Second" }, matches.Select(match => match.Value));
        }

        [Theory]
        [InlineData("macro_name_")]
        [InlineData("macro_name_=orphan")]
        [InlineData("macro_names")]
        [InlineData("macro_speed=5")]
        public void FindPrefixedValues_WithNothingBetweenThePrefixAndTheSeparator_DoesNotMatch(string line)
        {
            Assert.Empty(SettingsLineReader.FindPrefixedValues([line], "macro_name_"));
        }

        [Fact]
        public void FindPrefixedValues_WithAValueContainingTheSeparator_KeepsEverythingAfterTheFirstOne()
        {
            var matches = SettingsLineReader.FindPrefixedValues(["macro_name_1_0_65_1=a=b"], "macro_name_");

            Assert.Equal("a=b", Assert.Single(matches).Value);
        }

        [Fact]
        public void FindPrefixedValues_WithNullLinesOrEmptyPrefix_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SettingsLineReader.FindPrefixedValues(null!, "macro_name_"));
            Assert.Throws<ArgumentException>(() => SettingsLineReader.FindPrefixedValues([], string.Empty));
        }
    }
}
