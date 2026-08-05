using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;
using KinesisEdit.Core.Layouts;
using KinesisEdit.Core.Model;

namespace KinesisEdit.Core.Tests.Layouts
{
    /// <summary>
    /// Shared helpers for the layout-file tests: one-call parse and parse→serialize round
    /// trips per device, and key lookups by file token so assertions can address positions the
    /// way the files do (specs/04-layout-file-format.md §4.2).
    /// </summary>
    internal static class LayoutFixtures
    {
        public static LayoutParseResult Parse(DeviceId deviceId, params string[] lines)
        {
            return new LayoutFileParser(deviceId).Parse(lines);
        }

        public static IReadOnlyList<string> RoundTrip(DeviceId deviceId, params string[] lines)
        {
            var result = Parse(deviceId, lines);

            return LayoutFileSerializer.Serialize(result.Layout, result.InvalidLines);
        }

        public static KeyDefinition Key(string token, TokenDialect dialect = TokenDialect.Gen1)
        {
            return KeyRegistry.FindByToken(token, dialect)
                   ?? throw new InvalidOperationException($"Token \"{token}\" does not resolve in {dialect}.");
        }

        public static KeyboardKey Position(KeyboardLayout layout, int layerIndex, string token, TokenDialect dialect)
        {
            var layer = layout.FindLayer(layerIndex)
                        ?? throw new InvalidOperationException($"Layer {layerIndex} does not exist.");

            return layer.FindByPositionKeyCode(Key(token, dialect).Code)
                   ?? throw new InvalidOperationException($"Position \"{token}\" not found on layer {layerIndex}.");
        }

        public static KeyboardLayout CreateLayout(DeviceId deviceId, LayoutVariant variant = LayoutVariant.None)
        {
            return KeyboardLayout.Create(deviceId, variant);
        }
    }
}
