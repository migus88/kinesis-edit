using System.Globalization;
using KinesisEdit.Converters;
using KinesisEdit.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.Converters
{
    public class EnumMatchConverterTests
    {
        private static object Convert(object? value, object? parameter)
        {
            return new EnumMatchConverter().Convert(value, typeof(bool), parameter, CultureInfo.InvariantCulture);
        }

        [Fact]
        public void Convert_WhenTheNameMatches_ReturnsTrue()
        {
            Assert.Equal(true, Convert(StatusSeverity.Ok, "Ok"));
        }

        [Fact]
        public void Convert_WhenTheNameDiffers_ReturnsFalse()
        {
            Assert.Equal(false, Convert(StatusSeverity.Ok, "Error"));
        }

        [Fact]
        public void Convert_WithSeveralNames_MatchesAnyOfThem()
        {
            Assert.Equal(true, Convert(MessageBoxIcon.Confirmation, "Information,Confirmation"));
            Assert.Equal(true, Convert(MessageBoxIcon.Information, "Information,Confirmation"));
            Assert.Equal(false, Convert(MessageBoxIcon.Error, "Information,Confirmation"));
        }

        [Fact]
        public void Convert_WithPaddedOrDifferentlyCasedNames_StillMatches()
        {
            Assert.Equal(true, Convert(StatusSeverity.Warning, " warning "));
        }

        [Fact]
        public void Convert_WithNullValue_ReturnsFalse()
        {
            Assert.Equal(false, Convert(null, "Ok"));
        }

        [Fact]
        public void Convert_WithoutAStringParameter_ReturnsFalse()
        {
            Assert.Equal(false, Convert(StatusSeverity.Ok, null));
            Assert.Equal(false, Convert(StatusSeverity.Ok, 1));
        }

        [Fact]
        public void ConvertBack_Always_Throws()
        {
            var converter = new EnumMatchConverter();

            Assert.Throws<NotSupportedException>(
                () => converter.ConvertBack(true, typeof(StatusSeverity), "Ok", CultureInfo.InvariantCulture));
        }
    }
}
