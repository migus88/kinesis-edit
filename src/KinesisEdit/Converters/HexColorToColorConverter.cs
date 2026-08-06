using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// The two-way bridge between the <c>#RRGGBB</c> strings view models carry and the
    /// <see cref="Color"/> Avalonia's <c>ColorView</c> edits (specs/07-lighting.md §4). It exists
    /// so the colour picker can be a stock control while the view models stay free of Avalonia
    /// types (docs/app/app-shell.md, invariant 6).
    /// <para>
    /// Alpha is dropped on the way back: the lighting model has three 0-255 components and no
    /// alpha channel (§2.1), so a semi-transparent pick would silently become opaque anyway.
    /// </para>
    /// </summary>
    public sealed class HexColorToColorConverter : IValueConverter
    {
        /// <summary>Formats an opaque colour as <c>#RRGGBB</c>.</summary>
        public static string ToHex(Color color)
        {
            return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
        }

        /// <summary>Returns the opaque colour for a colour string, or transparent when it is not one.</summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string hex && Color.TryParse(hex.Trim(), out var color))
            {
                return Color.FromRgb(color.R, color.G, color.B);
            }

            return Colors.Transparent;
        }

        /// <summary>Returns the <c>#RRGGBB</c> string for an edited colour.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            // BindingOperations.DoNothing leaves the source untouched rather than writing a
            // fallback the model would then have to defend against.
            return value is Color color ? ToHex(color) : BindingOperations.DoNothing;
        }
    }
}
