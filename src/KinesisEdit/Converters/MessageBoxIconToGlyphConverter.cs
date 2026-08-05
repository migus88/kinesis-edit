using System.Globalization;
using Avalonia.Data.Converters;
using KinesisEdit.Services;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Renders the message box's "icon selected by dialog type" (specs/11-feature-dialogs.md
    /// §11.9) as a single character. Glyphs are deliberately restricted to Latin-1 so they render
    /// in every bundled font on every platform; the color comes from the theme brushes, not from
    /// this converter.
    /// </summary>
    public sealed class MessageBoxIconToGlyphConverter : IValueConverter
    {
        /// <summary>Glyph of an informational dialog.</summary>
        public const string InformationGlyph = "i";

        /// <summary>Glyph of a question awaiting confirmation.</summary>
        public const string ConfirmationGlyph = "?";

        /// <summary>Glyph of a warning.</summary>
        public const string WarningGlyph = "!";

        /// <summary>Glyph of an error.</summary>
        public const string ErrorGlyph = "×";

        /// <summary>Glyph of a dialog without an icon.</summary>
        public const string NoGlyph = "";

        /// <summary>Returns the glyph for <paramref name="icon"/>.</summary>
        public static string GetGlyph(MessageBoxIcon icon)
        {
            return icon switch
            {
                MessageBoxIcon.Information => InformationGlyph,
                MessageBoxIcon.Confirmation => ConfirmationGlyph,
                MessageBoxIcon.Warning => WarningGlyph,
                MessageBoxIcon.Error => ErrorGlyph,
                _ => NoGlyph
            };
        }

        /// <summary>Returns the glyph for a <see cref="MessageBoxIcon"/> value; anything else has no glyph.</summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is MessageBoxIcon icon ? GetGlyph(icon) : NoGlyph;
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(MessageBoxIconToGlyphConverter)} is a one-way converter.");
        }
    }
}
