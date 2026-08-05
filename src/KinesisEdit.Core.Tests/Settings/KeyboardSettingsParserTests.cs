using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class KeyboardSettingsParserTests
    {
        [Fact]
        public void Parse_WithEmptyLines_ReturnsAllNullProperties()
        {
            var settings = KeyboardSettingsParser.Parse([]);

            Assert.Equal(KeyboardSettings.Empty, settings);
        }

        [Fact]
        public void Parse_WithFullGen1SettingsFile_ReadsEveryKey()
        {
            var settings = KeyboardSettingsParser.Parse(
            [
                "startup_file=layout3.txt",
                "led_mode=led3.txt",
                "macro_speed=5",
                "status_play_speed=3",
                "v_drive=auto",
                "game_mode=ON",
            ]);

            Assert.Equal(3, settings.StartupProfileNumber);
            Assert.Equal("led3.txt", settings.LedMode);
            Assert.Equal(5, settings.MacroSpeed);
            Assert.Equal(3, settings.StatusPlaySpeed);
            Assert.True(settings.IsVDriveAutoMountEnabled);
            Assert.True(settings.IsGameModeEnabled);
            Assert.Null(settings.IsVDriveOpenOnStartupEnabled);
            Assert.Null(settings.IsProgramLockEnabled);
            Assert.Null(settings.IsKeyClickToneEnabled);
            Assert.Null(settings.IsToggleToneEnabled);
        }

        [Fact]
        public void Parse_WithAdvantage2StateFile_ReadsEveryKey()
        {
            var settings = KeyboardSettingsParser.Parse(
            [
                "macro_speed=0",
                "status_play_speed=4",
                "key_click_tone=ON",
                "toggle_tone=OFF",
                "v_drive_open_on_startup=off",
            ]);

            Assert.Equal(0, settings.MacroSpeed);
            Assert.Equal(4, settings.StatusPlaySpeed);
            Assert.True(settings.IsKeyClickToneEnabled);
            Assert.False(settings.IsToggleToneEnabled);
            Assert.False(settings.IsVDriveOpenOnStartupEnabled);
            Assert.Null(settings.IsVDriveAutoMountEnabled);
        }

        [Fact]
        public void Parse_WithAdvantage360SettingsFile_ReadsProfileStatusAndLock()
        {
            var settings = KeyboardSettingsParser.Parse(
            [
                "model=Adv360",
                "profile=5",
                "status=2",
                "lock=ON",
            ]);

            Assert.Equal(5, settings.StartupProfileNumber);
            Assert.Equal(2, settings.StatusPlaySpeed);
            Assert.True(settings.IsProgramLockEnabled);
        }

        [Theory]
        [InlineData("layout1.txt", 1)]
        [InlineData("layout9.txt", 9)]
        [InlineData("LAYOUT4.TXT", 4)]
        public void Parse_WithStartupFileName_ExtractsFileNumber(string value, int expectedNumber)
        {
            var settings = KeyboardSettingsParser.Parse([$"startup_file={value}"]);

            Assert.Equal(expectedNumber, settings.StartupProfileNumber);
        }

        [Fact]
        public void Parse_WithStartupFileNameWithoutDigits_ReturnsNullStartupProfileNumber()
        {
            var settings = KeyboardSettingsParser.Parse(["startup_file=qwerty.txt"]);

            Assert.Null(settings.StartupProfileNumber);
        }

        [Fact]
        public void Parse_WithBothProfileAndStartupFile_PrefersProfile()
        {
            var settings = KeyboardSettingsParser.Parse(["startup_file=layout3.txt", "profile=7"]);

            Assert.Equal(7, settings.StartupProfileNumber);
        }

        [Fact]
        public void Parse_WithBothStatusKeys_PrefersStatusPlaySpeed()
        {
            var settings = KeyboardSettingsParser.Parse(["status=1", "status_play_speed=4"]);

            Assert.Equal(4, settings.StatusPlaySpeed);
        }

        [Theory]
        [InlineData("on", true)]
        [InlineData("ON", true)]
        [InlineData("On", true)]
        [InlineData("off", false)]
        [InlineData("OFF", false)]
        [InlineData("garbage", false)]
        [InlineData("", false)]
        public void Parse_WithGameModeValue_ParsesCaseInsensitively(string value, bool expected)
        {
            var settings = KeyboardSettingsParser.Parse([$"game_mode={value}"]);

            Assert.Equal(expected, settings.IsGameModeEnabled);
        }

        [Theory]
        [InlineData("auto", true)]
        [InlineData("AUTO", true)]
        [InlineData("manual", false)]
        [InlineData("on", false)]
        public void Parse_WithVDriveValue_TreatsAutoAsTrue(string value, bool expected)
        {
            var settings = KeyboardSettingsParser.Parse([$"v_drive={value}"]);

            Assert.Equal(expected, settings.IsVDriveAutoMountEnabled);
        }

        [Fact]
        public void Parse_WithOnlyVDriveOpenOnStartup_LeavesVDriveAutoMountNull()
        {
            var settings = KeyboardSettingsParser.Parse(["v_drive_open_on_startup=ON"]);

            Assert.Null(settings.IsVDriveAutoMountEnabled);
            Assert.True(settings.IsVDriveOpenOnStartupEnabled);
        }

        [Fact]
        public void Parse_WithOnlyVDrive_LeavesVDriveOpenOnStartupNull()
        {
            var settings = KeyboardSettingsParser.Parse(["v_drive=auto"]);

            Assert.True(settings.IsVDriveAutoMountEnabled);
            Assert.Null(settings.IsVDriveOpenOnStartupEnabled);
        }

        [Theory]
        [InlineData("5", 5)]
        [InlineData("0", 0)]
        [InlineData("9", 9)]
        [InlineData(" 7 ", 7)]
        public void Parse_WithNumericMacroSpeed_ParsesInteger(string value, int expected)
        {
            var settings = KeyboardSettingsParser.Parse([$"macro_speed={value}"]);

            Assert.Equal(expected, settings.MacroSpeed);
        }

        [Theory]
        [InlineData("fast")]
        [InlineData("")]
        [InlineData("-1")]
        [InlineData("10")]
        [InlineData("12")]
        [InlineData("99")]
        public void Parse_WithUnparseableOrOutOfRangeMacroSpeed_ReturnsNull(string value)
        {
            var settings = KeyboardSettingsParser.Parse([$"macro_speed={value}"]);

            Assert.Null(settings.MacroSpeed);
        }

        [Theory]
        [InlineData("5")]
        [InlineData("9")]
        public void Parse_WithOutOfRangeStatusPlaySpeed_ReturnsNull(string value)
        {
            var settings = KeyboardSettingsParser.Parse([$"status_play_speed={value}"]);

            Assert.Null(settings.StatusPlaySpeed);
        }

        [Theory]
        [InlineData("profile=0")]
        [InlineData("profile=10")]
        [InlineData("startup_file=layout10.txt")]
        [InlineData("startup_file=layout0.txt")]
        public void Parse_WithOutOfRangeProfileNumber_ReturnsNull(string line)
        {
            var settings = KeyboardSettingsParser.Parse([line]);

            Assert.Null(settings.StartupProfileNumber);
        }

        [Fact]
        public void Parse_WithOutOfRangeProfileButValidStartupFile_FallsBackToStartupFile()
        {
            var settings = KeyboardSettingsParser.Parse(["profile=12", "startup_file=layout3.txt"]);

            Assert.Equal(3, settings.StartupProfileNumber);
        }

        [Fact]
        public void Parse_WithReservedKeys_ReadsThemIntoState()
        {
            var settings = KeyboardSettingsParser.Parse(
            [
                "thumb_mode=mac",
                "macro_disable=on",
                "power_user=true",
                "country=us",
                "program_key_lock=OFF",
                "profile_sync_mode=ON",
            ]);

            Assert.Equal("mac", settings.ThumbMode);
            Assert.True(settings.IsMacroDisabled);
            Assert.True(settings.IsPowerUser);
            Assert.Equal("us", settings.Country);
            Assert.False(settings.IsProgramKeyLockEnabled);
            Assert.True(settings.IsProfileSyncModeEnabled);
        }

        [Fact]
        public void Parse_WithPowerUserNotTrue_ReturnsFalse()
        {
            var settings = KeyboardSettingsParser.Parse(["power_user=on"]);

            Assert.False(settings.IsPowerUser);
        }

        [Fact]
        public void Parse_WithDifferentlyCasedKeys_MatchesCaseInsensitively()
        {
            var settings = KeyboardSettingsParser.Parse(["MACRO_SPEED=5", "Game_Mode=on"]);

            Assert.Equal(5, settings.MacroSpeed);
            Assert.True(settings.IsGameModeEnabled);
        }

        [Fact]
        public void Parse_WithEmptyLedModeValue_ReturnsNull()
        {
            var settings = KeyboardSettingsParser.Parse(["led_mode="]);

            Assert.Null(settings.LedMode);
        }

        [Fact]
        public void Parse_WithFreestyleLedModeString_KeepsRawValue()
        {
            var settings = KeyboardSettingsParser.Parse(["led_mode=P"]);

            Assert.Equal("P", settings.LedMode);
        }

        [Fact]
        public void Parse_WithUnknownAndUnparseableLines_IgnoresThem()
        {
            var settings = KeyboardSettingsParser.Parse(
            [
                "some unparseable garbage",
                "",
                "future_key=whatever",
                "macro_speed=5",
            ]);

            Assert.Equal(5, settings.MacroSpeed);
        }

        [Fact]
        public void Parse_WithNullLines_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => KeyboardSettingsParser.Parse(null!));
        }
    }
}
