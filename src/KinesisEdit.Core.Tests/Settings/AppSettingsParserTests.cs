using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class AppSettingsParserTests
    {
        [Fact]
        public void Parse_WithEmptyLines_ReturnsAllFlagsNullAndAllColorsUnset()
        {
            var settings = AppSettingsParser.Parse([]);

            Assert.Equal(AppSettings.Empty, settings with { CustomColors = AppSettings.Empty.CustomColors });
            Assert.All(settings.CustomColors, color => Assert.Null(color));
        }

        [Theory]
        [InlineData("app_intro_msg")]
        [InlineData("saveas_msg")]
        [InlineData("save_msg")]
        [InlineData("multiplay_msg")]
        [InlineData("speed_msg")]
        [InlineData("copy_macro_msg")]
        [InlineData("reset_key_msg")]
        [InlineData("app_checkfirm_msg")]
        [InlineData("savelighting_msg")]
        [InlineData("savesettings_msg")]
        [InlineData("windowscombo_msg")]
        [InlineData("updownkeystroke_msg")]
        public void Parse_WithFlagOn_HidesTheNotification(string key)
        {
            var settings = AppSettingsParser.Parse([$"{key}=on"]);

            Assert.True(GetHideFlag(settings, key));
        }

        [Theory]
        [InlineData("app_intro_msg")]
        [InlineData("saveas_msg")]
        [InlineData("save_msg")]
        [InlineData("multiplay_msg")]
        [InlineData("speed_msg")]
        [InlineData("copy_macro_msg")]
        [InlineData("reset_key_msg")]
        [InlineData("app_checkfirm_msg")]
        [InlineData("savelighting_msg")]
        [InlineData("savesettings_msg")]
        [InlineData("windowscombo_msg")]
        [InlineData("updownkeystroke_msg")]
        public void Parse_WithFlagMissing_LeavesFlagNullMeaningShow(string key)
        {
            var settings = AppSettingsParser.Parse(["unrelated=on"]);

            Assert.Null(GetHideFlag(settings, key));
        }

        [Theory]
        [InlineData("on", true)]
        [InlineData("ON", true)]
        [InlineData("On", true)]
        [InlineData("off", false)]
        [InlineData("garbage", false)]
        public void Parse_WithFlagValue_ParsesCaseInsensitively(string value, bool expected)
        {
            var settings = AppSettingsParser.Parse([$"save_msg={value}"]);

            Assert.Equal(expected, settings.IsSaveMessageHidden);
        }

        [Fact]
        public void Parse_WithCustomColors_FillsMatchingSlots()
        {
            var settings = AppSettingsParser.Parse(
            [
                "cust_color_1=[255][0][128]",
                "cust_color_12=[1][2][3]",
            ]);

            Assert.Equal(new SettingsColor(255, 0, 128), settings.CustomColors[0]);
            Assert.Equal(new SettingsColor(1, 2, 3), settings.CustomColors[11]);
            Assert.Null(settings.CustomColors[1]);
        }

        [Fact]
        public void Parse_WithColorPrefixCollision_KeepsSlotOneAndSlotTenSeparate()
        {
            var settings = AppSettingsParser.Parse(
            [
                "cust_color_1=[10][10][10]",
                "cust_color_10=[100][100][100]",
            ]);

            Assert.Equal(new SettingsColor(10, 10, 10), settings.CustomColors[0]);
            Assert.Equal(new SettingsColor(100, 100, 100), settings.CustomColors[9]);
        }

        [Fact]
        public void Parse_WithOnlySlotTenSet_LeavesSlotOneUnset()
        {
            var settings = AppSettingsParser.Parse(["cust_color_10=[100][100][100]"]);

            Assert.Null(settings.CustomColors[0]);
            Assert.Equal(new SettingsColor(100, 100, 100), settings.CustomColors[9]);
        }

        [Theory]
        [InlineData("cust_color_3=")]
        [InlineData("cust_color_3=[256][0][0]")]
        [InlineData("cust_color_3=garbage")]
        public void Parse_WithInvalidColorValue_LeavesSlotUnset(string line)
        {
            var settings = AppSettingsParser.Parse([line]);

            Assert.Null(settings.CustomColors[2]);
        }

        [Fact]
        public void Parse_WithDifferentlyCasedColorKey_MatchesCaseInsensitively()
        {
            var settings = AppSettingsParser.Parse(["CUST_COLOR_2=[4][5][6]"]);

            Assert.Equal(new SettingsColor(4, 5, 6), settings.CustomColors[1]);
        }

        [Fact]
        public void Parse_WithNullLines_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => AppSettingsParser.Parse(null!));
        }

        private static bool? GetHideFlag(AppSettings settings, string key)
        {
            return key switch
            {
                "app_intro_msg" => settings.IsAppIntroMessageHidden,
                "saveas_msg" => settings.IsSaveAsMessageHidden,
                "save_msg" => settings.IsSaveMessageHidden,
                "multiplay_msg" => settings.IsMultiplayMessageHidden,
                "speed_msg" => settings.IsSpeedMessageHidden,
                "copy_macro_msg" => settings.IsCopyMacroMessageHidden,
                "reset_key_msg" => settings.IsResetKeyMessageHidden,
                "app_checkfirm_msg" => settings.IsFirmwareCheckMessageHidden,
                "savelighting_msg" => settings.IsSaveLightingMessageHidden,
                "savesettings_msg" => settings.IsSaveSettingsMessageHidden,
                "windowscombo_msg" => settings.IsWindowsCombinationMessageHidden,
                "updownkeystroke_msg" => settings.IsUpDownKeystrokeMessageHidden,
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown hide-flag key."),
            };
        }
    }
}
