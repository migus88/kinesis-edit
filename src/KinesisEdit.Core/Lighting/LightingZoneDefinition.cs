using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// One color zone of specs/07-lighting.md §4: a named group of keys that the Freestyle /
    /// Breathe zone buttons paint with the currently selected color in one click. All instances
    /// live in <see cref="LightingZoneCatalog"/>.
    /// </summary>
    public sealed record LightingZoneDefinition
    {
        /// <summary>The device whose zone-button row this zone belongs to (§4).</summary>
        public required DeviceId Device { get; init; }

        /// <summary>The zone button's caption, spelled as in specs/07-lighting.md §4.</summary>
        public required string DisplayName { get; init; }

        /// <summary>
        /// The zone's key codes, in the device's top-layer geometry order. These are the Gen1
        /// <c>KeyRegistry</c> codes used as keys of <see cref="LayerLightingState.KeyColors"/>,
        /// so a zone is applied by calling <see cref="LayerLightingState.SetKeyColor"/> once per
        /// code (and cleared by passing <see cref="LedColor.Black"/>).
        /// </summary>
        public required IReadOnlyList<int> KeyCodes { get; init; }
    }
}
