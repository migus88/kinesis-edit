using System.Globalization;
using Avalonia.Data.Converters;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Converters
{
    /// <summary>
    /// Turns a <see cref="DeviceId"/> into its plan-view silhouette from Themes/DeviceArt.axaml —
    /// the drawing on a dashboard card's art frame and, at a third of the size, the row mark in
    /// the empty state's device picker.
    /// <para>
    /// A converter rather than four-elements-and-a-visibility-binding because the device is data:
    /// the card and the picker row both render whichever device they are handed, so the mark
    /// cannot be enumerated in markup the way a fixed set of states can. Every device is drawn
    /// with the same pen, so one binding is enough — which is exactly the case a converter suits.
    /// </para>
    /// <para>
    /// Only the seven programmable boards are drawn. The CROSSFIRE keypad never shipped and the
    /// Advantage 360 Professional is configured in Kinesis' web tool, so neither has artwork and
    /// both resolve to null: the design's rule is that a thing the app cannot show is not
    /// rendered at all rather than rendered blank.
    /// </para>
    /// </summary>
    public sealed class DeviceArtConverter : IValueConverter
    {
        /// <summary>Prefix every silhouette's resource key carries in Themes/DeviceArt.axaml.</summary>
        public const string KeyPrefix = "DeviceArt";

        /// <summary>
        /// The resource key of <paramref name="deviceId"/>'s silhouette, or null for a device with
        /// no artwork. Spelled out per member rather than concatenated so that a device added to
        /// the catalog without a drawing is a compile-time-visible gap here, not a key that
        /// silently fails to resolve at runtime.
        /// </summary>
        public static string? GetResourceKey(DeviceId deviceId)
        {
            return deviceId switch
            {
                DeviceId.SavantElite2 => KeyPrefix + nameof(DeviceId.SavantElite2),
                DeviceId.Advantage2 => KeyPrefix + nameof(DeviceId.Advantage2),
                DeviceId.FreestyleEdge => KeyPrefix + nameof(DeviceId.FreestyleEdge),
                DeviceId.FreestylePro => KeyPrefix + nameof(DeviceId.FreestylePro),
                DeviceId.FreestyleEdgeRgb => KeyPrefix + nameof(DeviceId.FreestyleEdgeRgb),
                DeviceId.Tko => KeyPrefix + nameof(DeviceId.Tko),
                DeviceId.Advantage360 => KeyPrefix + nameof(DeviceId.Advantage360),
                _ => null
            };
        }

        /// <summary>Returns the silhouette for a <see cref="DeviceId"/>; anything else has none.</summary>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value is DeviceId deviceId ? GeometryResources.Find(GetResourceKey(deviceId)) : null;
        }

        /// <summary>Not supported: the mapping is one-way, from the view model to the view.</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException($"{nameof(DeviceArtConverter)} is a one-way converter.");
        }
    }
}
