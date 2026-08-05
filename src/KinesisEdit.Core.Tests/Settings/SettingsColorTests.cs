using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Tests.Settings
{
    public class SettingsColorTests
    {
        [Theory]
        [InlineData("[255][0][128]", 255, 0, 128)]
        [InlineData("[0][0][0]", 0, 0, 0)]
        [InlineData("[255][255][255]", 255, 255, 255)]
        [InlineData(" [1][2][3] ", 1, 2, 3)]
        [InlineData("[007][08][9]", 7, 8, 9)]
        public void TryParse_WithValidFileForm_ParsesComponents(string text, int red, int green, int blue)
        {
            var isParsed = SettingsColor.TryParse(text, out var color);

            Assert.True(isParsed);
            Assert.Equal(red, color.Red);
            Assert.Equal(green, color.Green);
            Assert.Equal(blue, color.Blue);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("[255][0]")]
        [InlineData("[255][0][128][7]")]
        [InlineData("[256][0][0]")]
        [InlineData("[1000][0][0]")]
        [InlineData("[-1][0][0]")]
        [InlineData("[a][b][c]")]
        [InlineData("255,0,128")]
        [InlineData("[255[0][128]")]
        [InlineData("[][0][128]")]
        [InlineData("[255][0][128]x")]
        public void TryParse_WithInvalidInput_ReturnsFalse(string? text)
        {
            var isParsed = SettingsColor.TryParse(text, out var color);

            Assert.False(isParsed);
            Assert.Equal(default, color);
        }

        [Fact]
        public void ToString_Always_ProducesSpecFileForm()
        {
            var color = new SettingsColor(255, 0, 128);

            Assert.Equal("[255][0][128]", color.ToString());
        }

        [Fact]
        public void ToString_AfterTryParse_RoundTripsCanonicalForm()
        {
            SettingsColor.TryParse("[12][34][56]", out var color);

            Assert.Equal("[12][34][56]", color.ToString());
        }

        [Fact]
        public void Equals_WithSameComponents_ReturnsTrue()
        {
            var left = new SettingsColor(1, 2, 3);
            var right = new SettingsColor(1, 2, 3);

            Assert.True(left == right);
            Assert.True(left.Equals(right));
            Assert.Equal(left.GetHashCode(), right.GetHashCode());
        }

        [Fact]
        public void Equals_WithDifferentComponents_ReturnsFalse()
        {
            var left = new SettingsColor(1, 2, 3);
            var right = new SettingsColor(3, 2, 1);

            Assert.True(left != right);
            Assert.False(left.Equals(right));
        }
    }
}
