using System.Globalization;
using KinesisEdit.Converters;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Converters
{
    public class MessageBoxIconToGlyphConverterTests
    {
        [Theory]
        [InlineData(MessageBoxIcon.Information, "i")]
        [InlineData(MessageBoxIcon.Confirmation, "?")]
        [InlineData(MessageBoxIcon.Warning, "!")]
        [InlineData(MessageBoxIcon.Error, "×")]
        [InlineData(MessageBoxIcon.None, "")]
        public void GetGlyph_ForEveryDialogType_ReturnsItsGlyph(MessageBoxIcon icon, string expected)
        {
            Assert.Equal(expected, MessageBoxIconToGlyphConverter.GetGlyph(icon));
        }

        [Fact]
        public void Convert_ForAnIcon_ReturnsTheSameGlyphAsGetGlyph()
        {
            var converter = new MessageBoxIconToGlyphConverter();

            var glyph = converter.Convert(MessageBoxIcon.Error, typeof(string), null, CultureInfo.InvariantCulture);

            Assert.Equal(MessageBoxIconToGlyphConverter.ErrorGlyph, glyph);
        }

        [Fact]
        public void Convert_ForAValueThatIsNotAnIcon_ReturnsNoGlyph()
        {
            var converter = new MessageBoxIconToGlyphConverter();

            Assert.Equal(string.Empty, converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture));
            Assert.Equal(string.Empty, converter.Convert("Error", typeof(string), null, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void ConvertBack_Always_Throws()
        {
            var converter = new MessageBoxIconToGlyphConverter();

            Assert.Throws<NotSupportedException>(
                () => converter.ConvertBack("i", typeof(MessageBoxIcon), null, CultureInfo.InvariantCulture));
        }
    }
}
