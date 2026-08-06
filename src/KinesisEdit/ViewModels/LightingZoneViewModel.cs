using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One zone button of the per-key modes (specs/07-lighting.md §4 "Zones"). The membership is
    /// Core's <see cref="LightingZoneCatalog"/> — a list of key codes — so the button carries no
    /// key knowledge and a device with no zones simply yields no buttons.
    /// </summary>
    public sealed class LightingZoneViewModel
    {
        /// <summary>The device's zones in zone-button order, or an empty list when it has none.</summary>
        public static IReadOnlyList<LightingZoneViewModel> CreateAll(DeviceId deviceId)
        {
            var definitions = LightingZoneCatalog.Find(deviceId);

            if (definitions.Count == 0)
            {
                return [];
            }

            var zones = new List<LightingZoneViewModel>(definitions.Count);

            foreach (var definition in definitions)
            {
                zones.Add(new LightingZoneViewModel(definition));
            }

            return zones;
        }

        /// <summary>The zone's button caption (§4).</summary>
        public string Caption => _definition.DisplayName;

        /// <summary>
        /// The zone's key codes, as authored against the device's <b>top layer</b>. On the Fn
        /// layer they are re-resolved to the same physical positions (§2.4 item 6), which is
        /// <see cref="LightingTabViewModel"/>'s job, not this button's.
        /// </summary>
        public IReadOnlyList<int> KeyCodes => _definition.KeyCodes;

        private readonly LightingZoneDefinition _definition;

        /// <summary>Creates one zone button over a catalog row.</summary>
        public LightingZoneViewModel(LightingZoneDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
