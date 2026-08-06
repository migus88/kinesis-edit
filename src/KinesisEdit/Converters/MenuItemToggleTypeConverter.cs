using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Turns "this item latches" into the <see cref="MenuItemToggleType"/> a <c>MenuItem</c> needs,
    /// so a menu whose items come from a collection can make <b>some</b> of them check boxes while
    /// the view models stay free of menu types.
    /// <para>
    /// That the others are <see cref="MenuItemToggleType.None"/> is the whole point: Avalonia's menu
    /// interaction handler flips <c>IsChecked</c> on every check-box item it clicks — through
    /// <c>SetCurrentValue</c>, which a one-way binding never corrects and a view model that never
    /// changes value cannot overwrite — so a blanket <c>ToggleType="CheckBox"</c> leaves a check
    /// mark on every item that has ever been picked.
    /// </para>
    /// </summary>
    public sealed class MenuItemToggleTypeConverter : IValueConverter
    {
        /// <summary>The toggle type of an item that does or does not latch.</summary>
        public static MenuItemToggleType GetToggleType(bool isCheckable)
        {
            return isCheckable ? MenuItemToggleType.CheckBox : MenuItemToggleType.None;
        }

        /// <summary>Converts true to a check box; every other value, null included, to no toggle.</summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return GetToggleType(value is true);
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(MenuItemToggleTypeConverter)} is a one-way converter.");
        }
    }
}
