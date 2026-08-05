using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Profiles
{
    /// <summary>
    /// Dispatches a numbered-profile device to its <see cref="LedFileParser"/>/
    /// <see cref="LedFileSerializer"/> pair (specs/07-lighting.md §1.1-§1.4). The Freestyle Edge
    /// structurally ships a <c>lighting/</c> folder on its v-Drive (specs/03-vdrive-and-files.md
    /// §4.1), but its led file is a plain brightness/mode string owned by the <c>led_mode</c>
    /// settings key (specs/08-settings.md §2), not the per-key/edge/indicator grammar
    /// <c>KinesisEdit.Core.Lighting</c> implements (docs/app/lighting.md "Deliberately not
    /// here") — so, like the Freestyle Pro (no lighting folder at all), it has no
    /// profile-orchestrated lighting file here.
    /// </summary>
    internal static class ProfileLightingCodec
    {
        /// <summary>Whether <paramref name="device"/> pairs a led file with each numbered profile, in this module's scope.</summary>
        public static bool HasSupportedLighting(DeviceId device)
        {
            return device is DeviceId.FreestyleEdgeRgb or DeviceId.Tko or DeviceId.Advantage360;
        }

        /// <summary>Parses <paramref name="lines"/> with the led-file dialect of <paramref name="device"/>.</summary>
        public static object Parse(DeviceId device, IReadOnlyList<string> lines)
        {
            return device switch
            {
                DeviceId.FreestyleEdgeRgb => LedFileParser.ParseRgb(lines),
                DeviceId.Tko => LedFileParser.ParseTko(lines),
                DeviceId.Advantage360 => LedFileParser.ParseAdvantage360(lines),
                _ => throw Unsupported(device)
            };
        }

        /// <summary>Serializes <paramref name="model"/> with the led-file dialect of <paramref name="device"/>.</summary>
        public static IReadOnlyList<string> Serialize(DeviceId device, object model)
        {
            return device switch
            {
                DeviceId.FreestyleEdgeRgb => LedFileSerializer.SerializeRgb((LightingModel)model),
                DeviceId.Tko => LedFileSerializer.SerializeTko((TkoLightingModel)model),
                DeviceId.Advantage360 => LedFileSerializer.SerializeAdvantage360((Advantage360LightingModel)model),
                _ => throw Unsupported(device)
            };
        }

        private static ArgumentOutOfRangeException Unsupported(DeviceId device)
        {
            return new ArgumentOutOfRangeException(
                nameof(device), device, "Device has no profile-orchestrated lighting dialect.");
        }
    }
}
