using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// Evaluates the gates of <see cref="FirmwareGateCatalog"/> per specs/09-firmware.md §2.
    /// Query semantics: no gate for the (device, feature) pair means available; a gate whose
    /// relevant version is unknown means not available; a compound gate requires every declared
    /// condition; demo mode makes every gate pass. Answers gating only — whether the device
    /// supports the feature at all is device-catalog knowledge, not decided here.
    /// </summary>
    public static class FirmwareGateService
    {
        /// <summary>Whether <paramref name="feature"/> is available on the device given <paramref name="state"/>.</summary>
        public static bool IsAvailable(DeviceId deviceId, FirmwareFeature feature, FirmwareState state)
        {
            if (state.IsDemoMode)
            {
                return true;
            }

            var gate = FirmwareGateCatalog.Find(deviceId, feature);

            if (gate is null)
            {
                return true;
            }

            return IsSatisfied(gate, state);
        }

        private static bool IsSatisfied(FirmwareGate gate, FirmwareState state)
        {
            if (gate.MinimumKeyboardFirmware is not null && !MeetsMinimum(state.KeyboardFirmware, gate.MinimumKeyboardFirmware.Value))
            {
                return false;
            }

            if (gate.MinimumLedFirmware is not null && !MeetsMinimum(state.LedFirmware, gate.MinimumLedFirmware.Value))
            {
                return false;
            }

            if (gate.ExactKeyboardFirmware is not null && !MatchesExactly(state.KeyboardFirmware, gate.ExactKeyboardFirmware.Value))
            {
                return false;
            }

            if (gate.ExactLedFirmwareVersions.Count > 0 && !MatchesAnyExactly(state.LedFirmware, gate.ExactLedFirmwareVersions))
            {
                return false;
            }

            return true;
        }

        private static bool MeetsMinimum(FirmwareVersion? actual, FirmwareVersion minimum)
        {
            return actual is not null && actual.Value >= minimum;
        }

        private static bool MatchesExactly(FirmwareVersion? actual, FirmwareVersion required)
        {
            return actual is not null && actual.Value == required;
        }

        private static bool MatchesAnyExactly(FirmwareVersion? actual, IReadOnlyList<FirmwareVersion> candidates)
        {
            if (actual is null)
            {
                return false;
            }

            foreach (var candidate in candidates)
            {
                if (actual.Value == candidate)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
