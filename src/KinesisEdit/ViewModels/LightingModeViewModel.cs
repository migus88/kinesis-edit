using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Lighting.Preview;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// One row of the lighting tab's mode rail (design mockup 2f: "Mode — click to preview on the
    /// board"): the mode's name over the parameters it accepts. Membership and firmware gating are
    /// <see cref="LightingAvailability"/>'s, the parameters and the summary line are
    /// <see cref="LightingModeParameters"/>'s, and the name is
    /// <see cref="LightingModeCaptions"/>'s — so no lighting rule and no mode name is restated
    /// here.
    /// </summary>
    public sealed class LightingModeViewModel : ViewModelBase
    {
        /// <summary>
        /// The device's key-backlight rail for <paramref name="firmware"/>, in the caption order
        /// of specs/07-lighting.md §3 — the effects first, "Off" last. The catalog lists
        /// <see cref="LightingMode.Disabled"/> first (it is the reset value), so it is moved to
        /// the end here rather than reordering the catalog, which parsing depends on.
        /// <para>
        /// Ripple and Fireball drop out below KBD 1.0.121 / LED 1.0.58 and Pitch Black is never
        /// offered, because <see cref="LightingAvailability.IsKeyBacklightModeAvailable"/> says so.
        /// Freestyle stays: mockup 2f omits it, but §3's RGB menu offers it and it is the only mode
        /// that is per-key painting and nothing else.
        /// </para>
        /// </summary>
        /// <param name="deviceId">The board whose menu is being built.</param>
        /// <param name="firmware">Its firmware, for the two gated modes.</param>
        /// <param name="isLayerCustomizationAvailable">
        /// The <c>LightingLayerCustomization</c> verdict, which decides whether a two-line effect
        /// reports a base colour (§3) and so changes what a row's summary reads.
        /// </param>
        public static IReadOnlyList<LightingModeViewModel> CreateAll(
            DeviceId deviceId,
            FirmwareState firmware,
            bool isLayerCustomizationAvailable)
        {
            var modes = new List<LightingModeViewModel>(LightingModeCatalog.All.Count);
            LightingModeViewModel? disabled = null;

            foreach (var definition in LightingModeCatalog.All)
            {
                if (!LightingAvailability.IsKeyBacklightModeAvailable(deviceId, definition.Mode, firmware))
                {
                    continue;
                }

                var mode = new LightingModeViewModel(
                    LightingModeParameters.For(deviceId, definition.Mode, isLayerCustomizationAvailable));

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

        /// <summary>The mode this row selects.</summary>
        public LightingMode Mode => Parameters.Mode;

        /// <summary>What the rail calls it (<see cref="LightingModeCaptions"/>).</summary>
        public string Caption { get; }

        /// <summary>
        /// The row's second line: the parameters the mode accepts, in the design's grammar —
        /// "spd · D/L/U/R", "2 colors · spd", "—". Core builds it
        /// (<see cref="LightingModeParameters.Summary"/>), so the row can never claim a parameter
        /// the mode does not have.
        /// </summary>
        public string Summary => Parameters.Summary;

        /// <summary>Everything the mode accepts, for the row and for the controls under the board.</summary>
        public LightingModeParameters Parameters { get; }

        /// <summary>Whether this is the layer's current mode.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private bool _isSelected;

        /// <summary>Creates one rail row over a mode's parameter set.</summary>
        public LightingModeViewModel(LightingModeParameters parameters)
        {
            Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            Caption = LightingModeCaptions.For(parameters.Mode);
        }
    }
}
