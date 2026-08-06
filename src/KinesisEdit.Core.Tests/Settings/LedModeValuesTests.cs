using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    /// <summary>
    /// The mode-string domain of <c>led_mode</c> (specs/08-settings.md §2, §5.3): brightness
    /// <c>0</c>-<c>9</c>, <c>P</c> and <c>B</c>. It is the set the serializer accepts, so the
    /// settings picker can be built from it instead of restating it.
    /// </summary>
    public class LedModeValuesTests
    {
        [Fact]
        public void All_IsTheTwelveValuesOfTheSpecInPickerOrder()
        {
            Assert.Equal(
                ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9", "P", "B"],
                LedModeValues.All);
        }

        [Fact]
        public void All_IsExactlyWhatTheSerializerAccepts()
        {
            // The load-bearing property: a picker built from All can never offer a value that
            // makes KeyboardSettingsSerializer throw.
            var capability = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Settings;

            Assert.Equal(LedModeKind.ModeString, capability.LedMode);

            foreach (var value in LedModeValues.All)
            {
                var pairs = KeyboardSettingsSerializer.Serialize(capability, new KeyboardSettings { LedMode = value });

                Assert.Contains(pairs, pair => pair.Key == SettingsKeys.LedMode && pair.Value == value);
            }
        }

        [Theory]
        [InlineData("0", "0")]
        [InlineData("9", "9")]
        [InlineData("p", "P")]
        [InlineData("B", "B")]
        [InlineData("  b  ", "B")]
        public void Normalize_ReturnsTheCanonicalSpelling(string value, string expected)
        {
            Assert.Equal(expected, LedModeValues.Normalize(value));
        }

        [Theory]
        [InlineData("")]
        [InlineData("  ")]
        [InlineData("10")]
        [InlineData("X")]
        [InlineData("led1.txt")]
        public void Normalize_ForAValueOutsideTheDomain_ReturnsNull(string value)
        {
            Assert.Null(LedModeValues.Normalize(value));
        }

        [Fact]
        public void Normalize_ForNull_ReturnsNull()
        {
            Assert.Null(LedModeValues.Normalize(null));
        }

        [Theory]
        [InlineData("0", true)]
        [InlineData("7", true)]
        [InlineData("P", false)]
        [InlineData("b", false)]
        [InlineData("X", false)]
        public void IsBrightness_SeparatesTheBrightnessesFromTheSpecialModes(string value, bool expected)
        {
            Assert.Equal(expected, LedModeValues.IsBrightness(value));
        }

        [Fact]
        public void IsBrightness_WithoutAValue_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => LedModeValues.IsBrightness(null!));
        }
    }
}
