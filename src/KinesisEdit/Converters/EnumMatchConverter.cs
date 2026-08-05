using System.Globalization;
using Avalonia.Data.Converters;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Turns an enum value into the boolean a <c>Classes.someClass</c> binding needs, so a view
    /// model can expose a plain enum (<see cref="ViewModels.StatusSeverity"/>,
    /// <see cref="Services.MessageBoxIcon"/>) and XAML alone decides the color. The converter
    /// parameter is one enum member name, or several separated by commas — a value matching any
    /// of them yields true. Matching is by name, case-insensitively, so it survives a renamed
    /// underlying numeric value.
    /// </summary>
    public sealed class EnumMatchConverter : IValueConverter
    {
        private const char NameSeparator = ',';

        /// <summary>True when <paramref name="value"/>'s name is listed in <paramref name="parameter"/>.</summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is null || parameter is not string expectedNames)
            {
                return false;
            }

            var actualName = value.ToString();

            if (string.IsNullOrEmpty(actualName))
            {
                return false;
            }

            foreach (var expectedName in expectedNames.Split(NameSeparator))
            {
                if (string.Equals(expectedName.Trim(), actualName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(EnumMatchConverter)} is a one-way converter.");
        }
    }
}
