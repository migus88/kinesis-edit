using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using KinesisEdit.Converters;

namespace KinesisEdit.Tests.Converters
{
    /// <summary>
    /// Every case runs on the UI thread: the converter's product is a <see cref="SolidColorBrush"/>,
    /// which is an <c>AvaloniaObject</c> and refuses to be built anywhere else once an application
    /// exists. In the app that is never a question — a binding only ever evaluates on the UI thread
    /// — and the headless harness makes the test agree with it.
    /// </summary>
    public class HexColorToBrushConverterTests
    {
        private static object? Convert(object? value)
        {
            return new HexColorToBrushConverter().Convert(value, typeof(IBrush), null, CultureInfo.InvariantCulture);
        }

        [AvaloniaFact]
        public void Convert_WithAnRrggbbString_ReturnsThatColorsBrush()
        {
            var brush = Assert.IsType<SolidColorBrush>(Convert("#FF8800"));

            Assert.Equal(Color.FromRgb(0xFF, 0x88, 0x00), brush.Color);
        }

        [AvaloniaFact]
        public void Convert_WithSurroundingWhitespace_StillParses()
        {
            var brush = Assert.IsType<SolidColorBrush>(Convert("  #0072CE  "));

            Assert.Equal(Color.FromRgb(0x00, 0x72, 0xCE), brush.Color);
        }

        [AvaloniaFact]
        public void Convert_WithNullOrEmpty_ReturnsNull()
        {
            Assert.Null(Convert(null));
            Assert.Null(Convert(string.Empty));
            Assert.Null(Convert("   "));
        }

        [AvaloniaFact]
        public void Convert_WithAnUnparseableString_ReturnsNull()
        {
            Assert.Null(Convert("not a color"));
            Assert.Null(Convert("#ZZZZZZ"));
        }

        [AvaloniaFact]
        public void Convert_WithSomethingOtherThanAString_ReturnsNull()
        {
            Assert.Null(Convert(42));
        }

        [AvaloniaFact]
        public void TryCreateBrush_ForTheSameColor_ReturnsAnEquivalentBrush()
        {
            var brush = Assert.IsType<SolidColorBrush>(HexColorToBrushConverter.TryCreateBrush("#123456"));

            Assert.Equal(Color.FromRgb(0x12, 0x34, 0x56), brush.Color);
        }

        [AvaloniaFact]
        public void ConvertBack_Always_Throws()
        {
            var converter = new HexColorToBrushConverter();

            Assert.Throws<NotSupportedException>(
                () => converter.ConvertBack(Brushes.Red, typeof(string), null, CultureInfo.InvariantCulture));
        }
    }
}
