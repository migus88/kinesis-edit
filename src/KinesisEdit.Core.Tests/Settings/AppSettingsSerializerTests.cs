using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class AppSettingsSerializerTests
    {
        [Fact]
        public void Serialize_WithEmptySettings_ReturnsEmptyList()
        {
            var pairs = AppSettingsSerializer.Serialize(AppSettings.Empty);

            Assert.Empty(pairs);
        }

        [Fact]
        public void Serialize_WithAllFlagsSet_EmitsAllTwelveKeysInSpecOrderAsLowercase()
        {
            var settings = new AppSettings
            {
                IsAppIntroMessageHidden = true,
                IsSaveAsMessageHidden = false,
                IsSaveMessageHidden = true,
                IsMultiplayMessageHidden = false,
                IsSpeedMessageHidden = true,
                IsCopyMacroMessageHidden = false,
                IsResetKeyMessageHidden = true,
                IsFirmwareCheckMessageHidden = false,
                IsSaveLightingMessageHidden = true,
                IsSaveSettingsMessageHidden = false,
                IsWindowsCombinationMessageHidden = true,
                IsUpDownKeystrokeMessageHidden = false,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("app_intro_msg", "on"),
                    KeyValuePair.Create("saveas_msg", "off"),
                    KeyValuePair.Create("save_msg", "on"),
                    KeyValuePair.Create("multiplay_msg", "off"),
                    KeyValuePair.Create("speed_msg", "on"),
                    KeyValuePair.Create("copy_macro_msg", "off"),
                    KeyValuePair.Create("reset_key_msg", "on"),
                    KeyValuePair.Create("app_checkfirm_msg", "off"),
                    KeyValuePair.Create("savelighting_msg", "on"),
                    KeyValuePair.Create("savesettings_msg", "off"),
                    KeyValuePair.Create("windowscombo_msg", "on"),
                    KeyValuePair.Create("updownkeystroke_msg", "off"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithUnsetFlags_SkipsTheirKeys()
        {
            var settings = new AppSettings
            {
                IsSaveMessageHidden = true,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal([KeyValuePair.Create("save_msg", "on")], pairs);
        }

        [Fact]
        public void Serialize_WithSetColors_EmitsOnlySetSlotsInFileForm()
        {
            var colors = new SettingsColor?[AppSettings.CustomColorCount];
            colors[0] = new SettingsColor(255, 0, 128);
            colors[9] = new SettingsColor(1, 2, 3);

            var settings = new AppSettings
            {
                CustomColors = colors,
            };

            var pairs = AppSettingsSerializer.Serialize(settings);

            Assert.Equal(
                new[]
                {
                    KeyValuePair.Create("cust_color_1", "[255][0][128]"),
                    KeyValuePair.Create("cust_color_10", "[1][2][3]"),
                },
                pairs);
        }

        [Fact]
        public void Serialize_WithUnsetColors_NeverEmitsEmptyColorKeys()
        {
            var pairs = AppSettingsSerializer.Serialize(AppSettings.Empty);

            Assert.DoesNotContain(pairs, pair => pair.Key.StartsWith("cust_color_", StringComparison.Ordinal));
        }

        [Fact]
        public void Serialize_WithNullSettings_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => AppSettingsSerializer.Serialize(null!));
        }
    }
}
