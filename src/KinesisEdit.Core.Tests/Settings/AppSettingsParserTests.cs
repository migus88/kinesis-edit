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
        [InlineData("warn_unsaved_msg")]
        [InlineData("reset_layer_msg")]
        [InlineData("capture_summary_msg")]
        [InlineData("switch_variant_msg")]
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
        [InlineData("warn_unsaved_msg")]
        [InlineData("reset_layer_msg")]
        [InlineData("capture_summary_msg")]
        [InlineData("switch_variant_msg")]
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

        [Theory]
        [InlineData("advisory_detail=on", true)]
        [InlineData("advisory_detail=off", false)]
        [InlineData("ADVISORY_DETAIL=on", true)]
        public void Parse_WithAdvisoryDetail_ReadsOnAsExpandedNotAsHidden(string line, bool expected)
        {
            // The one key in this file whose polarity is the other way round: "on" means EXPAND
            // the advisory, not hide it. The property name is what carries the difference.
            var settings = AppSettingsParser.Parse([line]);

            Assert.Equal(expected, settings.IsAdvisoryDetailExpanded);
        }

        [Fact]
        public void Parse_WithAdvisoryDetailMissing_LeavesItNullMeaningOneLine()
        {
            var settings = AppSettingsParser.Parse(["unrelated=on"]);

            Assert.Null(settings.IsAdvisoryDetailExpanded);
        }

        [Fact]
        public void Parse_WithBothConventionsInOneFile_ReadsEachTheItsOwnWay()
        {
            // Proven together on purpose: the same stored value "on" means opposite things two
            // lines apart, and only the property names say so.
            var settings = AppSettingsParser.Parse(["reset_layer_msg=on", "advisory_detail=on"]);

            Assert.True(settings.IsResetLayerConfirmationHidden);
            Assert.True(settings.IsAdvisoryDetailExpanded);
        }

        [Fact]
        public void Parse_WithResetKeyAndResetLayer_KeepsThePrefixSharingKeysApart()
        {
            // reset_key_msg and reset_layer_msg share a prefix, and SettingsLineReader matches on
            // the '=' separator — the same rule that keeps cust_color_1 out of cust_color_10.
            var settings = AppSettingsParser.Parse(["reset_key_msg=on", "reset_layer_msg=off"]);

            Assert.True(settings.IsResetKeyMessageHidden);
            Assert.False(settings.IsResetLayerConfirmationHidden);
        }

        [Fact]
        public void Parse_WithEverySeventeenthKeySet_ReadsThemIndependently()
        {
            // No key in this file may swallow another's line by prefix.
            var lines = new[]
            {
                "app_intro_msg=on",
                "saveas_msg=off",
                "save_msg=on",
                "multiplay_msg=off",
                "speed_msg=on",
                "copy_macro_msg=off",
                "reset_key_msg=on",
                "app_checkfirm_msg=off",
                "savelighting_msg=on",
                "savesettings_msg=off",
                "windowscombo_msg=on",
                "updownkeystroke_msg=off",
                "warn_unsaved_msg=on",
                "reset_layer_msg=off",
                "capture_summary_msg=on",
                "switch_variant_msg=off",
                "advisory_detail=on"
            };

            var settings = AppSettingsParser.Parse(lines);

            Assert.True(settings.IsAppIntroMessageHidden);
            Assert.False(settings.IsSaveAsMessageHidden);
            Assert.True(settings.IsSaveMessageHidden);
            Assert.False(settings.IsMultiplayMessageHidden);
            Assert.True(settings.IsSpeedMessageHidden);
            Assert.False(settings.IsCopyMacroMessageHidden);
            Assert.True(settings.IsResetKeyMessageHidden);
            Assert.False(settings.IsFirmwareCheckMessageHidden);
            Assert.True(settings.IsSaveLightingMessageHidden);
            Assert.False(settings.IsSaveSettingsMessageHidden);
            Assert.True(settings.IsWindowsCombinationMessageHidden);
            Assert.False(settings.IsUpDownKeystrokeMessageHidden);
            Assert.True(settings.IsUnsavedChangesWarningHidden);
            Assert.False(settings.IsResetLayerConfirmationHidden);
            Assert.True(settings.IsCaptureSummaryHidden);
            Assert.False(settings.IsSwitchVariantConfirmationHidden);
            Assert.True(settings.IsAdvisoryDetailExpanded);
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
                "warn_unsaved_msg" => settings.IsUnsavedChangesWarningHidden,
                "reset_layer_msg" => settings.IsResetLayerConfirmationHidden,
                "capture_summary_msg" => settings.IsCaptureSummaryHidden,
                "switch_variant_msg" => settings.IsSwitchVariantConfirmationHidden,

                // advisory_detail is deliberately absent: it is not a hide flag, and a switch that
                // accepted it here would be the first step to reading it as one.
                _ => throw new ArgumentOutOfRangeException(nameof(key), key, "Unknown hide-flag key."),
            };
        }
    }
}
