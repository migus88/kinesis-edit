using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One entry of the lighting tab's mode menu (specs/07-lighting.md §3). Membership and
    /// firmware gating are <see cref="LightingAvailability"/>'s and the caption is the catalog's
    /// <see cref="LightingModeDefinition.DisplayName"/>, so no mode name is spelled out here.
    /// </summary>
    public sealed class LightingModeViewModel : ViewModelBase
    {
        /// <summary>
        /// The device's key-backlight menu for <paramref name="firmware"/>, in the caption order
        /// of specs/07-lighting.md §3 — the effects first, "Disable" last. The catalog lists
        /// <see cref="LightingMode.Disabled"/> first (it is the reset value), so it is moved to
        /// the end here rather than reordering the catalog, which parsing depends on.
        /// <para>
        /// Ripple and Fireball drop out below KBD 1.0.121 / LED 1.0.58 and Pitch Black is never
        /// offered, because <see cref="LightingAvailability.IsKeyBacklightModeAvailable"/> says so.
        /// </para>
        /// </summary>
        public static IReadOnlyList<LightingModeViewModel> CreateAll(DeviceId deviceId, FirmwareState firmware)
        {
            var modes = new List<LightingModeViewModel>(LightingModeCatalog.All.Count);
            LightingModeViewModel? disabled = null;

            foreach (var definition in LightingModeCatalog.All)
            {
                if (!LightingAvailability.IsKeyBacklightModeAvailable(deviceId, definition.Mode, firmware))
                {
                    continue;
                }

                var mode = new LightingModeViewModel(definition.Mode, definition.DisplayName);

                if (definition.Mode == LightingMode.Disabled)
                {
                    disabled = mode;
                }
                else
                {
                    modes.Add(mode);
                }
            }

            if (disabled is not null)
            {
                modes.Add(disabled);
            }

            return modes;
        }

        /// <summary>The mode this entry selects.</summary>
        public LightingMode Mode { get; }

        /// <summary>The mode's UI caption (§3), quoted by the catalog.</summary>
        public string Caption { get; }

        /// <summary>Whether this is the layer's current mode.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates one menu entry.</summary>
        public LightingModeViewModel(LightingMode mode, string caption)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(caption);

            Mode = mode;
            Caption = caption;
        }
    }
}
