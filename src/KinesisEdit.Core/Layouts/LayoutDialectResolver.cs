using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Layouts
{
    /// <summary>
    /// Maps a device to the layout-file dialect its <c>layouts/layoutN.txt</c> (or Advantage2
    /// <c>qwerty.txt</c>/<c>dvorak.txt</c>) files are written in, per the dialect table of
    /// specs/04-layout-file-format.md §4.1. Devices without layout files — the SE2 pedal, the
    /// CROSSFIRE keypad, and the Advantage360 Professional — resolve to
    /// <see cref="LayoutDialect.None"/>. The SE2's <c>active/pedals.txt</c> is not a layout
    /// dialect at all: it has its own grammar and engine in
    /// <see cref="SavantElite.PedalFileParser"/> (spec 12 §4).
    /// </summary>
    public static class LayoutDialectResolver
    {
        /// <summary>Returns the layout dialect of <paramref name="deviceId"/>, or <see cref="LayoutDialect.None"/>.</summary>
        public static LayoutDialect Resolve(DeviceId deviceId)
        {
            return deviceId switch
            {
                DeviceId.FreestyleEdge or DeviceId.FreestylePro => LayoutDialect.Freestyle,
                DeviceId.FreestyleEdgeRgb or DeviceId.Tko => LayoutDialect.Gen1Rgb,
                DeviceId.Advantage360 => LayoutDialect.Gen2,
                DeviceId.Advantage2 => LayoutDialect.Advantage2,
                _ => LayoutDialect.None
            };
        }

        /// <summary>
        /// Returns the token dialect a layout dialect resolves its file tokens in
        /// (specs/05-key-model.md §3 legend).
        /// </summary>
        public static TokenDialect GetTokenDialect(LayoutDialect dialect)
        {
            return dialect switch
            {
                LayoutDialect.Freestyle or LayoutDialect.Gen1Rgb => TokenDialect.Gen1,
                LayoutDialect.Gen2 => TokenDialect.Gen2,
                LayoutDialect.Advantage2 => TokenDialect.Legacy,
                _ => TokenDialect.None
            };
        }
    }
}
