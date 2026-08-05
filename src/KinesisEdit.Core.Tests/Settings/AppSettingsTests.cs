using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class AppSettingsTests
    {
        [Fact]
        public void Empty_Always_HasAllFlagsNullAndTwelveUnsetColors()
        {
            var settings = AppSettings.Empty;

            Assert.Null(settings.IsAppIntroMessageHidden);
            Assert.Null(settings.IsSaveAsMessageHidden);
            Assert.Null(settings.IsSaveMessageHidden);
            Assert.Null(settings.IsMultiplayMessageHidden);
            Assert.Null(settings.IsSpeedMessageHidden);
            Assert.Null(settings.IsCopyMacroMessageHidden);
            Assert.Null(settings.IsResetKeyMessageHidden);
            Assert.Null(settings.IsFirmwareCheckMessageHidden);
            Assert.Null(settings.IsSaveLightingMessageHidden);
            Assert.Null(settings.IsSaveSettingsMessageHidden);
            Assert.Null(settings.IsWindowsCombinationMessageHidden);
            Assert.Null(settings.IsUpDownKeystrokeMessageHidden);
            Assert.Equal(AppSettings.CustomColorCount, settings.CustomColors.Count);
            Assert.All(settings.CustomColors, color => Assert.Null(color));
        }

        [Fact]
        public void CustomColors_WithWrongEntryCount_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new AppSettings
            {
                CustomColors = new SettingsColor?[5],
            });
        }

        [Fact]
        public void CustomColors_WithNullList_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => new AppSettings
            {
                CustomColors = null!,
            });
        }

        [Fact]
        public void CustomColors_WithTwelveEntries_KeepsList()
        {
            var colors = new SettingsColor?[AppSettings.CustomColorCount];
            colors[0] = new SettingsColor(1, 2, 3);

            var settings = new AppSettings
            {
                CustomColors = colors,
            };

            Assert.Equal(new SettingsColor(1, 2, 3), settings.CustomColors[0]);
        }
    }
}
