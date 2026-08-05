using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Lighting
{
    /// <summary>
    /// Answers lighting availability questions that combine the mode catalog with the firmware
    /// gates of specs/09-firmware.md §2 (restated in specs/07-lighting.md §3), and provides the
    /// lighting-side precondition of the RGB "Expansion Pack" offer: no led file contains any
    /// <c>fn </c>-prefixed line. Callers combine that predicate with
    /// <see cref="FirmwareFeature.ExpansionPackOffer"/>.
    /// </summary>
    public static class LightingAvailability
    {
        /// <summary>
        /// Whether the device's key-backlight mode menu offers <paramref name="mode"/> given
        /// <paramref name="state"/> (specs/07-lighting.md §3): catalog membership, and for
        /// gated modes (Ripple/Fireball) the <see cref="FirmwareGateService"/> verdict — the
        /// RGB is gated, the TKO has no gate row and passes automatically.
        /// </summary>
        public static bool IsKeyBacklightModeAvailable(DeviceId deviceId, LightingMode mode, FirmwareState state)
        {
            var definition = LightingModeCatalog.Find(mode);

            var offersInMenu = deviceId switch
            {
                DeviceId.FreestyleEdgeRgb => definition.OffersInRgbKeyBacklightMenu,
                DeviceId.Tko => definition.OffersInTkoKeyBacklightMenu,
                _ => false
            };

            if (!offersInMenu)
            {
                return false;
            }

            return definition.GatedByFeature == FirmwareFeature.None
                || FirmwareGateService.IsAvailable(deviceId, definition.GatedByFeature, state);
        }

        /// <summary>Whether the TKO edge mode menu offers <paramref name="mode"/> (§3; no edge mode is firmware-gated).</summary>
        public static bool IsEdgeModeAvailable(LightingMode mode)
        {
            return LightingModeCatalog.Find(mode).OffersInTkoEdgeMenu;
        }

        /// <summary>
        /// Whether Fn-layer lighting (the layer toggle in lighting mode) is available: the
        /// <see cref="FirmwareFeature.LightingLayerCustomization"/> gate — LED firmware ≥ 1.0.44
        /// on the RGB, ungated elsewhere (specs/07-lighting.md §3).
        /// </summary>
        public static bool IsFnLayerLightingAvailable(DeviceId deviceId, FirmwareState state)
        {
            return FirmwareGateService.IsAvailable(deviceId, FirmwareFeature.LightingLayerCustomization, state);
        }

        /// <summary>Whether any line of one led file carries the <c>fn </c> layer prefix (§2.1).</summary>
        public static bool ContainsFnLayerLines(IReadOnlyList<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            foreach (var line in lines)
            {
                if (line is not null && LedLineScanner.HasFnPrefix(line))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The Expansion-Pack lighting precondition (specs/07-lighting.md §3): true when no led
        /// file of <paramref name="ledFiles"/> contains any <c>fn </c>-prefixed line.
        /// </summary>
        public static bool HasNoFnLayerLines(IEnumerable<IReadOnlyList<string>> ledFiles)
        {
            ArgumentNullException.ThrowIfNull(ledFiles);

            foreach (var ledFile in ledFiles)
            {
                if (ContainsFnLayerLines(ledFile))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
