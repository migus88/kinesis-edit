using KinesisEdit.Core.Settings;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Converts between the lighting model's <see cref="LedColor"/> and the app-settings
    /// <see cref="SettingsColor"/> of the twelve <c>cust_color_N</c> picker slots
    /// (specs/07-lighting.md §4, specs/08-settings.md §3). Both types hold the same three
    /// decimal 0-255 components, so conversion is total and lossless in both directions — the
    /// two types stay separate only because the lighting engine and the settings engine own
    /// their own file forms.
    /// </summary>
    public static class LedColorConverter
    {
        /// <summary>Converts a lighting color to its app-settings custom-slot form.</summary>
        public static SettingsColor ToSettingsColor(LedColor color)
        {
            return new SettingsColor(color.Red, color.Green, color.Blue);
        }

        /// <summary>Converts an unset (null) slot value through unchanged.</summary>
        public static SettingsColor? ToSettingsColor(LedColor? color)
        {
            return color is { } value ? ToSettingsColor(value) : null;
        }

        /// <summary>Converts an app-settings custom-slot color to a lighting color.</summary>
        public static LedColor ToLedColor(SettingsColor color)
        {
            return new LedColor(color.Red, color.Green, color.Blue);
        }

        /// <summary>
        /// Converts an unset (null) custom-color slot through unchanged — an unset slot is not
        /// black, it is "no swatch stored" (spec 08 §3).
        /// </summary>
        public static LedColor? ToLedColor(SettingsColor? color)
        {
            return color is { } value ? ToLedColor(value) : null;
        }
    }
}
