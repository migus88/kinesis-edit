using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Turns the <c>#RRGGBB</c> string a key cap carries
    /// (<see cref="ViewModels.KeyboardKeyViewModel.ColorOverlayHex"/>) into the brush its colour
    /// strip paints with. The conversion lives here rather than on the view model because view
    /// models expose values only and never Avalonia brushes (docs/app/app-shell.md, invariant 6).
    /// <para>
    /// Anything that is not a colour — null, empty, a value the lighting file never carried, or a
    /// string the parser cannot read — converts to <c>null</c>, which leaves the strip unpainted
    /// rather than throwing inside a binding.
    /// </para>
    /// </summary>
    public sealed class HexColorToBrushConverter : IValueConverter
    {
        /// <summary>Builds the brush for <paramref name="hex"/>, or null when it is not a colour.</summary>
        public static IBrush? TryCreateBrush(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return null;
            }

            if (!Color.TryParse(hex.Trim(), out var color))
            {
                return null;
            }

            return new SolidColorBrush(color);
        }

        /// <summary>Returns the brush for a colour string; every other input converts to null.</summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return TryCreateBrush(value as string);
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(HexColorToBrushConverter)} is a one-way converter.");
        }
    }
}
