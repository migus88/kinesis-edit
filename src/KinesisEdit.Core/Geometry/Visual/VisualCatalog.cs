using System.Diagnostics.CodeAnalysis;
using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Geometry.Visual
{
    /// <summary>
    /// Static catalog of the authored board pictures, mirroring
    /// <see cref="GeometryCatalog"/>: where the geometry catalog says which key
    /// positions a device has, this one says where those positions physically sit.
    /// <para>
    /// Only the Freestyle Edge RGB is authored so far; every other device resolves to
    /// nothing until its visual lands (issues #39-#42). A device with a geometry but no
    /// visual simply cannot be drawn yet — callers must handle a false result.
    /// </para>
    /// </summary>
    public static class VisualCatalog
    {
        /// <summary>Freestyle Edge RGB: 95 key placements shared by both layers (spec 05 §4.2).</summary>
        public static KeyboardVisual FreestyleEdgeRgb { get; } = FreestyleEdgeRgbVisual.Create();

        /// <summary>
        /// Resolves the device's default board picture. Returns false for devices whose
        /// visual has not been authored yet.
        /// </summary>
        public static bool TryGet(DeviceId deviceId, [NotNullWhen(true)] out KeyboardVisual? visual)
        {
            return TryGet(deviceId, LayoutVariant.None, out visual);
        }

        /// <summary>
        /// Resolves the board picture for a specific layout variant.
        /// <see cref="LayoutVariant.None"/> selects the device's default; any other
        /// variant must match the authored one, exactly as
        /// <see cref="GeometryCatalog.TryGet(DeviceId, LayoutVariant, out DeviceGeometry?)"/>
        /// behaves — there is no silent fallback.
        /// </summary>
        public static bool TryGet(DeviceId deviceId, LayoutVariant variant, [NotNullWhen(true)] out KeyboardVisual? visual)
        {
            visual = deviceId switch
            {
                DeviceId.FreestyleEdgeRgb => FreestyleEdgeRgb,
                _ => null
            };

            if (visual is not null && variant != LayoutVariant.None && visual.Variant != variant)
            {
                visual = null;
            }

            return visual is not null;
        }
    }
}
