using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One zone button of the per-key modes (specs/07-lighting.md §4 "Zones"). The membership is
    /// Core's <see cref="LightingZoneCatalog"/> — a list of key codes — so the button carries no
    /// key knowledge and a device with no zones simply yields no buttons.
    /// <para>
    /// <b>Since issue #124 a zone is a selection, not a paint.</b> Clicking it toggles its keys in
    /// the paint selection and writes no colour; Apply is what commits one. So the button has a
    /// selected state, and it is pushed in by <see cref="LightingTabViewModel"/> rather than worked
    /// out here — the answer depends on which layer is shown and on the whole current selection,
    /// and neither is a fact this button has any business holding.
    /// </para>
    /// </summary>
    public sealed class LightingZoneViewModel : ViewModelBase
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

        /// <summary>
        /// Whether every one of this zone's keys — resolved for the layer on screen — is currently
        /// in the paint selection. It follows the selection <b>from any source</b>: a click on a
        /// cap, "Select all", "Clear", another zone, a layer switch. See
        /// <see cref="LightingPaintSelection.Changed"/>, which is what makes that true by
        /// construction rather than by a list of call sites.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private readonly LightingZoneDefinition _definition;
        private bool _isSelected;

        /// <summary>Creates one zone button over a catalog row.</summary>
        public LightingZoneViewModel(LightingZoneDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
