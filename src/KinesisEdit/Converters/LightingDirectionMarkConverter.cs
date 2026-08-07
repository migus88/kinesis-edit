using System.Globalization;
using Avalonia.Data.Converters;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Turns a <see cref="LightingDirection"/> into its arrow from Themes/Icons.axaml — the mark on
    /// one segment of the Lighting tab's direction control (docs/design/mockups.md 2f: "Direction"
    /// with <c>← → ↑ ↓</c>).
    /// <para>
    /// It exists because those four characters may not be typed: the icon law is "16px grid, 1.5px
    /// stroke, square caps, <b>geometry only</b>", and a direction is <i>data</i> — the set of
    /// directions a mode accepts comes out of the lighting catalog — so the four cannot be
    /// enumerated in a template the way a fixed set of states is.
    /// </para>
    /// <para>
    /// All four marks are outlines, so unlike <see cref="LightingModeMarkConverter"/> this one
    /// takes no parameter: there is no stroke/fill split to answer. <see cref="LightingDirection.None"/>
    /// converts to null, which draws nothing — it is the "no direction" member the file format
    /// never writes.
    /// </para>
    /// </summary>
    public sealed class LightingDirectionMarkConverter : IValueConverter
    {
        /// <summary>Prefix every direction arrow's resource key carries in Themes/Icons.axaml.</summary>
        public const string KeyPrefix = "IconArrow";

        /// <summary>
        /// The resource key of <paramref name="direction"/>'s arrow, or null for
        /// <see cref="LightingDirection.None"/>, which has no mark and never will: it means "this
        /// effect has no direction", not "this effect points nowhere".
        /// </summary>
        public static string? GetResourceKey(LightingDirection direction)
        {
            return direction == LightingDirection.None ? null : KeyPrefix + direction;
        }

        /// <summary>Returns the arrow for a <see cref="LightingDirection"/>; anything else is null.</summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is LightingDirection direction ? GeometryResources.Find(GetResourceKey(direction)) : null;
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(LightingDirectionMarkConverter)} is a one-way converter.");
        }
    }
}
