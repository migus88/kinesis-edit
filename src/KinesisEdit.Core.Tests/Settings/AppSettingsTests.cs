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

        [Theory]
        [InlineData(1, 0)]
        [InlineData(3, 2)]
        [InlineData(12, 11)]
        public void WithCustomColor_WithAValidSlot_ReplacesOnlyThatSlot(int slotNumber, int expectedIndex)
        {
            var color = new SettingsColor(255, 0, 128);

            var settings = AppSettings.Empty.WithCustomColor(slotNumber, color);

            Assert.Equal(color, settings.CustomColors[expectedIndex]);
            Assert.Equal(AppSettings.CustomColorCount, settings.CustomColors.Count);
            Assert.Equal(
                AppSettings.CustomColorCount - 1,
                settings.CustomColors.Count(slot => slot is null));
        }

        [Fact]
        public void WithCustomColor_WithNull_ClearsTheSlotAndKeepsTheOthers()
        {
            var settings = AppSettings.Empty
                .WithCustomColor(1, new SettingsColor(1, 1, 1))
                .WithCustomColor(2, new SettingsColor(2, 2, 2))
                .WithCustomColor(1, null);

            Assert.Null(settings.CustomColors[0]);
            Assert.Equal(new SettingsColor(2, 2, 2), settings.CustomColors[1]);
        }

        [Fact]
        public void WithCustomColor_Always_LeavesTheSourceInstanceAndItsFlagsIntact()
        {
            var source = AppSettings.Empty with { IsSaveSettingsMessageHidden = true };

            var updated = source.WithCustomColor(4, new SettingsColor(7, 7, 7));

            Assert.Null(source.CustomColors[3]);
            Assert.True(updated.IsSaveSettingsMessageHidden);
            Assert.Equal(new SettingsColor(7, 7, 7), updated.CustomColors[3]);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(13)]
        public void WithCustomColor_WithASlotOutsideOneToTwelve_ThrowsArgumentOutOfRangeException(int slotNumber)
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => AppSettings.Empty.WithCustomColor(slotNumber, new SettingsColor(1, 2, 3)));
        }

        [Fact]
        public void WithCustomColor_Always_UsesTheSameSlotNumberingAsTheSettingsKey()
        {
            // Slot 12 is cust_color_12, i.e. the last list entry (spec 08 §3).
            Assert.Equal("cust_color_12", SettingsKeys.GetCustomColorKey(12));
            Assert.Equal(
                new SettingsColor(3, 3, 3),
                AppSettings.Empty.WithCustomColor(12, new SettingsColor(3, 3, 3)).CustomColors[^1]);
        }
    }
}
