using System.Collections.ObjectModel;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Geometry;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// The immutable catalog of the per-key color zones of specs/07-lighting.md §4 — the zone
    /// buttons the Freestyle/Breathe editors expose (§3 "Zone buttons: Freestyle and Breathe
    /// only"). Pure data: each row names a zone and lists its Gen1 key codes, so the UI paints a
    /// whole zone by calling <see cref="LayerLightingState.SetKeyColor"/> once per code.
    /// <para>
    /// Only the Freestyle Edge RGB is populated; the TKO's ten zones (§4 "Zones (TKO)", six top
    /// layer + four Fn layer) are deferred with the TKO editor UI, so
    /// <see cref="Find(DeviceId)"/> returns an empty list for every other device.
    /// </para>
    /// <para>
    /// Zone membership is resolved against <see cref="GeometryCatalog.FreestyleEdgeRgb"/>'s top
    /// layer by position index (specs/05-key-model.md §4.2), because §4 names the zones without
    /// listing their keys. The chosen readings are documented on each row below and in
    /// docs/app/lighting.md.
    /// </para>
    /// </summary>
    public static class LightingZoneCatalog
    {
        /// <summary>All zone rows of every device, in each device's zone-button order.</summary>
        public static IReadOnlyList<LightingZoneDefinition> All { get; }

        private static readonly Dictionary<DeviceId, IReadOnlyList<LightingZoneDefinition>> _byDevice;
        private static readonly IReadOnlyList<LightingZoneDefinition> _noZones =
            new ReadOnlyCollection<LightingZoneDefinition>([]);

        static LightingZoneCatalog()
        {
            All = new ReadOnlyCollection<LightingZoneDefinition>(BuildDefinitions());

            _byDevice = All
                .GroupBy(definition => definition.Device)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<LightingZoneDefinition>)new ReadOnlyCollection<LightingZoneDefinition>(
                        group.ToList()));
        }

        /// <summary>
        /// The device's zones in zone-button order, or an empty list when the device has no
        /// zone buttons (§4 defines them for the Freestyle Edge RGB and the TKO only).
        /// </summary>
        public static IReadOnlyList<LightingZoneDefinition> Find(DeviceId deviceId)
        {
            return _byDevice.GetValueOrDefault(deviceId, _noZones);
        }

        /// <summary>
        /// Returns the device's zone whose <see cref="LightingZoneDefinition.DisplayName"/>
        /// matches <paramref name="displayName"/> case-insensitively, or null.
        /// </summary>
        public static LightingZoneDefinition? FindByName(DeviceId deviceId, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return null;
            }

            foreach (var definition in Find(deviceId))
            {
                if (string.Equals(definition.DisplayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private static List<LightingZoneDefinition> BuildDefinitions()
        {
            var rgbTopLayer = GeometryCatalog.FreestyleEdgeRgb.Layers[0].Keys;

            return
            [
                // Every addressable key of the board.
                CreateRgbZone(rgbTopLayer, "All", Span(0, 94)),

                // The ten digit keys 1-0. The row's tilde/hyphen/equals keys are excluded: the
                // TKO's equivalent zone is named "Number Row" (§4) while the RGB's is "Number".
                CreateRgbZone(rgbTopLayer, "Number", Span(20, 29)),

                // F1-F12. Esc, Print Screen, Pause and Delete share the row but are not F-keys.
                CreateRgbZone(rgbTopLayer, "Function", Span(2, 13)),

                // W, A, S, D.
                CreateRgbZone(rgbTopLayer, "WASD", [38, 54, 55, 56]),

                // The gaming cluster: the left module's typing keys except the F-row and the
                // Windows key, which Game Mode disables anyway (specs/08-settings.md §2).
                CreateRgbZone(
                    rgbTopLayer,
                    "Game",
                    [1, .. Span(19, 25), .. Span(36, 41), .. Span(53, 58), .. Span(69, 74), 85, 87, 88]),

                // The four arrow keys.
                CreateRgbZone(rgbTopLayer, "Arrow", [81, 92, 93, 94]),

                // The left half of the split board plus the hotkey column that sits on it
                // (specs/02-devices.md; the split is the one authored in FreestyleEdgeRgbVisual).
                CreateRgbZone(
                    rgbTopLayer,
                    "Left Module",
                    [.. Span(0, 7), .. Span(17, 25), .. Span(34, 41), .. Span(51, 58), .. Span(67, 74), .. Span(83, 88)]),

                // The right half of the split board.
                CreateRgbZone(
                    rgbTopLayer,
                    "Right Module",
                    [.. Span(8, 16), .. Span(26, 33), .. Span(42, 50), .. Span(59, 66), .. Span(75, 82), .. Span(89, 94)])
            ];
        }

        private static LightingZoneDefinition CreateRgbZone(
            IReadOnlyList<KeyPosition> positions,
            string displayName,
            IReadOnlyList<int> positionIndices)
        {
            var selected = new List<KeyPosition>(positionIndices.Count);

            foreach (var index in positionIndices)
            {
                selected.Add(positions[index]);
            }

            return new LightingZoneDefinition
            {
                Device = DeviceId.FreestyleEdgeRgb,
                DisplayName = displayName,
                KeyCodes = LightingKeyCodeResolver.Resolve(selected)
            };
        }

        private static int[] Span(int firstIndex, int lastIndex)
        {
            var indices = new int[lastIndex - firstIndex + 1];

            for (var offset = 0; offset < indices.Length; offset++)
            {
                indices[offset] = firstIndex + offset;
            }

            return indices;
        }
    }
}
