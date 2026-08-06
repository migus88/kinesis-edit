using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// The <see cref="LedColor"/> ↔ <see cref="SettingsColor"/> bridge between the lighting
    /// model (specs/07-lighting.md §2.1) and the twelve <c>cust_color_N</c> picker slots
    /// (specs/08-settings.md §3): same three 0-255 components, so conversion round-trips.
    /// </summary>
    public class LedColorConverterTests
    {
        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(0, 255, 0)]
        [InlineData(255, 128, 0)]
        [InlineData(255, 255, 255)]
        public void ToSettingsColor_WithAnyComponents_RoundTripsBackToTheSameLedColor(byte red, byte green, byte blue)
        {
            var original = new LedColor(red, green, blue);

            var settingsColor = LedColorConverter.ToSettingsColor(original);

            Assert.Equal(new SettingsColor(red, green, blue), settingsColor);
            Assert.Equal(original, LedColorConverter.ToLedColor(settingsColor));
        }

        [Fact]
        public void ToLedColor_WithASettingsColor_CopiesEveryComponent()
        {
            var converted = LedColorConverter.ToLedColor(new SettingsColor(12, 34, 56));

            Assert.Equal(new LedColor(12, 34, 56), converted);
        }

        [Fact]
        public void ToLedColor_WithAnUnsetSlot_ReturnsNullRatherThanBlack()
        {
            Assert.Null(LedColorConverter.ToLedColor((SettingsColor?)null));
            Assert.Equal(new LedColor(9, 8, 7), LedColorConverter.ToLedColor((SettingsColor?)new SettingsColor(9, 8, 7)));
        }

        [Fact]
        public void ToSettingsColor_WithANullLedColor_ReturnsNull()
        {
            Assert.Null(LedColorConverter.ToSettingsColor((LedColor?)null));
            Assert.Equal(
                new SettingsColor(0, 0, 0),
                LedColorConverter.ToSettingsColor((LedColor?)LedColor.Black));
        }
    }
}
