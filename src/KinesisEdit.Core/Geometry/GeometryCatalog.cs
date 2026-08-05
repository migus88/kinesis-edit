using System.Diagnostics.CodeAnalysis;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Geometry
{
    /// <summary>
    /// Static catalog of the physical layer geometries of every SmartSet-programmable
    /// keyboard, per specs/05-key-model.md §4 (seven layout families). Devices without
    /// key-layer geometry resolve to nothing: the Savant Elite2 is a pedal device
    /// (spec 05 §3.14), the CROSSFIRE keypad was never supported by any SmartSet app,
    /// and the Advantage360 Professional is configured via the ZMK web GUI (specs/02).
    /// </summary>
    public static class GeometryCatalog
    {
        /// <summary>Freestyle Edge: 2 layers x 95 positions (spec 05 §4.1).</summary>
        public static DeviceGeometry FreestyleEdge { get; } = FreestyleGeometry.CreateEdge();

        /// <summary>Freestyle Edge RGB: 2 layers x 95 positions (spec 05 §4.2).</summary>
        public static DeviceGeometry FreestyleEdgeRgb { get; } = FreestyleGeometry.CreateEdgeRgb();

        /// <summary>Freestyle Pro: 2 layers x 95 positions (spec 05 §4.3).</summary>
        public static DeviceGeometry FreestylePro { get; } = FreestyleGeometry.CreatePro();

        /// <summary>TKO: 2 layers x 63 key positions + 33 edge zones (spec 05 §4.4).</summary>
        public static DeviceGeometry Tko { get; } = TkoGeometry.Create();

        /// <summary>Advantage2 QWERTY: 2 layers x 89 positions (spec 05 §4.5).</summary>
        public static DeviceGeometry Advantage2Qwerty { get; } = Advantage2Geometry.CreateQwerty();

        /// <summary>Advantage2 Dvorak: 2 layers x 89 positions (spec 05 §4.6).</summary>
        public static DeviceGeometry Advantage2Dvorak { get; } = Advantage2Geometry.CreateDvorak();

        /// <summary>Advantage360 (SmartSet): 5 layers x 77 positions (spec 05 §4.7).</summary>
        public static DeviceGeometry Advantage360 { get; } = Advantage360Geometry.Create();

        /// <summary>
        /// Resolves the device's default geometry (Advantage2 defaults to QWERTY).
        /// Returns false for devices without key-layer geometry.
        /// </summary>
        public static bool TryGet(DeviceId deviceId, [NotNullWhen(true)] out DeviceGeometry? geometry)
        {
            return TryGet(deviceId, LayoutVariant.None, out geometry);
        }

        /// <summary>
        /// Resolves the device geometry for a specific layout variant.
        /// <see cref="LayoutVariant.None"/> selects the device's default geometry;
        /// any other variant must match the geometry's variant (only the Advantage2
        /// has a Dvorak alternative).
        /// </summary>
        public static bool TryGet(DeviceId deviceId, LayoutVariant variant, [NotNullWhen(true)] out DeviceGeometry? geometry)
        {
            geometry = deviceId switch
            {
                DeviceId.FreestyleEdge => FreestyleEdge,
                DeviceId.FreestyleEdgeRgb => FreestyleEdgeRgb,
                DeviceId.FreestylePro => FreestylePro,
                DeviceId.Tko => Tko,
                DeviceId.Advantage2 => variant == LayoutVariant.Dvorak ? Advantage2Dvorak : Advantage2Qwerty,
                DeviceId.Advantage360 => Advantage360,
                _ => null
            };

            if (geometry is not null && variant != LayoutVariant.None && geometry.Variant != variant)
            {
                geometry = null;
            }

            return geometry is not null;
        }
    }
}
