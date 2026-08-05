using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;
using KinesisEdit.Core.Keys;

namespace KinesisEdit.Core.Model
{
    /// <summary>
    /// Turns the immutable geometry of a device (specs/05-key-model.md §4) into the editable layer
    /// model of §1.4: every position's default and position tokens are resolved through
    /// <see cref="KeyRegistry"/> in the device family's dialect (§1.5 lookup by token,
    /// case-insensitive first match) and become one <see cref="KeyboardKey"/> per position.
    /// </summary>
    internal static class KeyboardLayoutBuilder
    {
        // §4.5: the only positions without a file token are the Advantage2 physical Keypad and
        // Program buttons, whose key-table entries (§3.9 code 10013, §3.10 code 10014) are never
        // written to files, so they are resolved by code instead of by token.
        private const int Advantage2KeypadButtonIndex = 16;
        private const int Advantage2ProgramButtonIndex = 17;
        private const int KeypadButtonKeyCode = 10014;
        private const int ProgramButtonKeyCode = 10013;

        /// <summary>
        /// The token dialect a device's files are written in (specs/README.md, 05 §3 legend):
        /// Legacy for the Advantage2 and the Savant Elite2 pedal, Gen1 for the Freestyle boards and
        /// the TKO, Gen2 for the Advantage360 family.
        /// </summary>
        public static TokenDialect ResolveDialect(DeviceId deviceId)
        {
            return deviceId switch
            {
                DeviceId.SavantElite2 or DeviceId.Advantage2 => TokenDialect.Legacy,
                DeviceId.FreestyleEdge
                    or DeviceId.FreestylePro
                    or DeviceId.FreestyleEdgeRgb
                    or DeviceId.Tko => TokenDialect.Gen1,
                DeviceId.Advantage360 or DeviceId.Advantage360Professional => TokenDialect.Gen2,
                _ => TokenDialect.None
            };
        }

        /// <summary>Builds every layer of <paramref name="geometry"/> in geometry order (§1.4, §1.5).</summary>
        /// <param name="geometry">The device's physical geometry.</param>
        /// <param name="device">The device whose capabilities each key position inherits.</param>
        /// <param name="dialect">The token dialect its files are written in.</param>
        public static KeyboardLayer[] BuildLayers(
            DeviceGeometry geometry,
            DeviceDefinition device,
            TokenDialect dialect)
        {
            var layers = new KeyboardLayer[geometry.Layers.Count];

            for (var i = 0; i < geometry.Layers.Count; i++)
            {
                layers[i] = BuildLayer(geometry.Layers[i], geometry.Variant, device, dialect);
            }

            return layers;
        }

        private static KeyboardLayer BuildLayer(
            LayerGeometry layer,
            LayoutVariant variant,
            DeviceDefinition device,
            TokenDialect dialect)
        {
            return new KeyboardLayer(
                layer.Name,
                layer.Index,
                ResolveLayerType(layer.Index, variant, dialect),
                BuildKeys(layer.Keys, device, dialect),
                BuildEdgeKeys(layer.EdgeZones, dialect));
        }

        /// <summary>
        /// §1.4: the layout type is QWERTY 0 / Dvorak 1 on the devices that ship both, while the
        /// Advantage360 (Gen2) mirrors the layer index.
        /// </summary>
        private static int ResolveLayerType(int layerIndex, LayoutVariant variant, TokenDialect dialect)
        {
            if (dialect == TokenDialect.Gen2)
            {
                return layerIndex;
            }

            return variant == LayoutVariant.Dvorak ? 1 : 0;
        }

        private static KeyboardKey[] BuildKeys(
            IReadOnlyList<KeyPosition> positions,
            DeviceDefinition device,
            TokenDialect dialect)
        {
            var keys = new KeyboardKey[positions.Count];

            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                var originalKey = ResolveDefaultKey(position, dialect);
                var positionKey = position.PositionToken is null
                    ? null
                    : ResolveToken(position.PositionToken, dialect, position.Index);

                keys[i] = new KeyboardKey(
                    position,
                    originalKey,
                    positionKey,
                    device.SupportsMultiModifiers,
                    !device.Macros.UsesFlatMacroList);
            }

            return keys;
        }

        /// <summary>
        /// The TKO edge-lighting zones of §1.4/§3.13/§4.4. They are lighting zones, not typing
        /// keys: their lines live in <c>led*.txt</c> and never in a layout file (04 §3.4), and
        /// §4.4 lists no remap or macro for them — the geometry says as much ("the edit/macro flags
        /// carry no meaning for them"). The model gives them meaning by building the zones locked
        /// and slotless, so every editor path refuses them; the tolerant load paths can still write,
        /// which is why <see cref="KeyboardLayoutValidator"/> walks the zones too.
        /// </summary>
        private static KeyboardKey[] BuildEdgeKeys(IReadOnlyList<KeyPosition> zones, TokenDialect dialect)
        {
            var keys = new KeyboardKey[zones.Count];

            for (var i = 0; i < zones.Count; i++)
            {
                var zone = zones[i];
                var lightingOnly = new KeyPosition(
                    zone.Index,
                    zone.DefaultToken,
                    zone.PositionToken,
                    canEdit: false,
                    canAssignMacro: false);

                keys[i] = new KeyboardKey(
                    lightingOnly,
                    ResolveDefaultKey(zone, dialect),
                    positionKey: null,
                    supportsMultiModifiers: false,
                    usesMacroSlots: false);
            }

            return keys;
        }

        private static KeyDefinition ResolveDefaultKey(KeyPosition position, TokenDialect dialect)
        {
            return position.DefaultToken.Length == 0
                ? ResolveTokenlessKey(position.Index)
                : ResolveToken(position.DefaultToken, dialect, position.Index);
        }

        private static KeyDefinition ResolveTokenlessKey(int index)
        {
            var code = index switch
            {
                Advantage2KeypadButtonIndex => KeypadButtonKeyCode,
                Advantage2ProgramButtonIndex => ProgramButtonKeyCode,
                _ => throw new InvalidOperationException(
                    $"Geometry position {index} has no file token and is not one of the Advantage2 Keypad/Program buttons.")
            };

            return KeyRegistry.FindByCode(code)
                   ?? throw new InvalidOperationException($"Key-table entry {code} is missing from the registry.");
        }

        private static KeyDefinition ResolveToken(string token, TokenDialect dialect, int index)
        {
            return KeyRegistry.FindByToken(token, dialect)
                   ?? throw new InvalidOperationException(
                       $"Geometry token \"{token}\" at position {index} does not resolve in the {dialect} dialect.");
        }
    }
}
