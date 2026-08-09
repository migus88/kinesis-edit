using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One zone button of the per-key modes (specs/07-lighting.md §4 "Zones"). The membership is
    /// Core's <see cref="LightingZoneCatalog"/> — a list of key codes — so the button carries no
    /// key knowledge and a device with no zones simply yields no buttons.
    /// <para>
    /// <b>It is a plain button, and it holds no state at all (issue #131).</b> Between #124 and
    /// #131 it carried an <c>IsSelected</c> that <see cref="LightingTabViewModel"/> pushed in on
    /// every selection change — "every one of my keys is currently selected" — and the zones
    /// <b>overlap</b>: <c>All</c> contains everything, <c>Left Module ⊃ Game ⊃ WASD</c>. So one
    /// button's click changed another button's face, which is what the user saw as the buttons
    /// interfering with each other: taking WASD back out of the selection un-lit <c>Game</c> while
    /// twenty-five of Game's keys stayed selected, and after <c>Select all</c> every button was lit
    /// so the next click on one of them re-selected instead of deselecting.
    /// </para>
    /// <para>
    /// A latch whose meaning is "a set relation with a set somebody else can move" cannot be shown
    /// honestly on an overlapping family, so it is not shown at all: each button adds its own keys
    /// or removes them (<c>LightingTabViewModel.SelectZoneCommand</c>) and nothing it does can
    /// change how another one looks.
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

        private readonly LightingZoneDefinition _definition;

        /// <summary>Creates one zone button over a catalog row.</summary>
        public LightingZoneViewModel(LightingZoneDefinition definition)
        {
            _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        }
    }
}
