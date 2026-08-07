using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Services;

namespace KinesisEdit.ViewModels
{
    /// <summary>
    /// The one device limit that is not simply a field of <see cref="MacroCapability"/>: how many
    /// macros a profile may hold, which specs/09-firmware.md §2 lets the firmware raise.
    /// <para>
    /// It lived on the old <c>MacroPanelViewModel</c> until issue #93 turned the Macros tab into a
    /// library and decomposed that class. Two surfaces still ask the question — the key inspector's
    /// Macro panel, which refuses a macro that would exceed it, and the library's own count meter —
    /// so it is a static of its own rather than a member of either.
    /// </para>
    /// </summary>
    public static class MacroLimits
    {
        /// <summary>
        /// The macro count the profile is held to: <see cref="MacroCapability.MaxMacroCount"/>,
        /// raised to <see cref="MacroCapability.GatedMaxMacroCount"/> when
        /// <see cref="FirmwareFeature.ExpandedMacroCount"/> is available on this firmware
        /// (specs/09-firmware.md §2 — the Freestyle Edge/Pro gate at 1.0.340; demo mode passes
        /// every gate, which the service already answers).
        /// <para>
        /// <b>Null is "no limit", not "zero"</b>: 06 §6's table lists a macros-per-layout figure for
        /// the Freestyle, RGB and Advantage360 families and none at all for the Advantage2, so that
        /// board's macro count is unbounded and every consumer treats a null as such.
        /// </para>
        /// </summary>
        public static int? ResolveMaxMacroCount(DeviceSnapshot device)
        {
            ArgumentNullException.ThrowIfNull(device);

            var macros = device.Device.Macros;
            var baseline = macros.MaxMacroCount;

            if (macros.GatedMaxMacroCount is not int gated)
            {
                return baseline;
            }

            return FirmwareGateService.IsAvailable(device.DeviceId, FirmwareFeature.ExpandedMacroCount, device.Firmware)
                ? gated
                : baseline;
        }
    }
}
