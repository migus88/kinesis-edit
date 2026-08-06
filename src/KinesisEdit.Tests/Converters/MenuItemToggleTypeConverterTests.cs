using System.Globalization;
using Avalonia.Controls;
using KinesisEdit.Converters;

namespace KinesisEdit.Tests.Converters
{
    public class MenuItemToggleTypeConverterTests
    {
        [Fact]
        public void GetToggleType_ForACheckableItem_IsACheckBox()
        {
            Assert.Equal(MenuItemToggleType.CheckBox, MenuItemToggleTypeConverter.GetToggleType(true));
        }

        [Fact]
        public void GetToggleType_ForAPlainItem_IsNoToggleAtAll()
        {
            // Not "an unchecked check box": Avalonia's menu interaction handler check-marks any
            // CheckBox item it clicks, whatever the item's view model reports.
            Assert.Equal(MenuItemToggleType.None, MenuItemToggleTypeConverter.GetToggleType(false));
        }

        [Fact]
        public void Convert_ForABoolean_ReturnsTheSameToggleTypeAsGetToggleType()
        {
            var converter = new MenuItemToggleTypeConverter();

            Assert.Equal(
                MenuItemToggleType.CheckBox,
                converter.Convert(true, typeof(MenuItemToggleType), null, CultureInfo.InvariantCulture));
            Assert.Equal(
                MenuItemToggleType.None,
                converter.Convert(false, typeof(MenuItemToggleType), null, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void Convert_ForAValueThatIsNotABoolean_ReturnsNoToggle()
        {
            var converter = new MenuItemToggleTypeConverter();

            Assert.Equal(
                MenuItemToggleType.None,
                converter.Convert(null, typeof(MenuItemToggleType), null, CultureInfo.InvariantCulture));
            Assert.Equal(
                MenuItemToggleType.None,
                converter.Convert("true", typeof(MenuItemToggleType), null, CultureInfo.InvariantCulture));
        }

        [Fact]
        public void ConvertBack_Always_Throws()
        {
            var converter = new MenuItemToggleTypeConverter();

            Assert.Throws<NotSupportedException>(
                () => converter.ConvertBack(MenuItemToggleType.CheckBox, typeof(bool), null, CultureInfo.InvariantCulture));
        }
    }
}
