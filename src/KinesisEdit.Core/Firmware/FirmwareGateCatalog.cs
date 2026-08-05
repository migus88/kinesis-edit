using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Firmware
{
    /// <summary>
    /// The immutable catalog of every minimum-firmware feature gate, encoding the gate table of
    /// specs/09-firmware.md §2 (restated in specs 07 and 11). Pure data — evaluation lives in
    /// <see cref="FirmwareGateService"/>. A (device, feature) pair without a row here is ungated.
    /// </summary>
    public static class FirmwareGateCatalog
    {
        private const string CustomDelaysRefusalMessage =
            "To utilize custom or random delays, please download and install the latest firmware.";
        private const string MultimodifiersRefusalMessage =
            "To utilize Multimodifiers, please download and install the latest firmware.";
        private const string TapAndHoldRefusalMessage =
            "To utilize Tap and Hold Actions, please download and install the latest firmware.";
        private const string TkoMacroFirmwareWarningMessage =
            "Attention macro users: Update your firmware now for full functionality.";

        /// <summary>All gate rows, grouped by device in legacy-app-id order.</summary>
        public static IReadOnlyList<FirmwareGate> All { get; }

        private static readonly IReadOnlyDictionary<(DeviceId Device, FirmwareFeature Feature), FirmwareGate> _byDeviceAndFeature;

        static FirmwareGateCatalog()
        {
            var gates = new List<FirmwareGate>();

            gates.AddRange(CreateAdvantage2Gates());
            gates.AddRange(CreateFreestyleGates(DeviceId.FreestyleEdge));
            gates.AddRange(CreateFreestyleGates(DeviceId.FreestylePro));
            gates.AddRange(CreateFreestyleEdgeRgbGates());
            gates.AddRange(CreateTkoGates());
            gates.AddRange(CreateAdvantage360Gates());

            All = gates;

            var byDeviceAndFeature = new Dictionary<(DeviceId Device, FirmwareFeature Feature), FirmwareGate>();

            foreach (var gate in gates)
            {
                byDeviceAndFeature.Add((gate.Device, gate.Feature), gate);
            }

            _byDeviceAndFeature = byDeviceAndFeature;
        }

        /// <summary>Returns the gate for the pair, or null when the pair is ungated.</summary>
        public static FirmwareGate? Find(DeviceId deviceId, FirmwareFeature feature)
        {
            return _byDeviceAndFeature.TryGetValue((deviceId, feature), out var gate) ? gate : null;
        }

        private static IEnumerable<FirmwareGate> CreateAdvantage2Gates()
        {
            var multimodifierGate = new FirmwareVersion(1, 0, 516);

            yield return new FirmwareGate
            {
                Device = DeviceId.Advantage2,
                Feature = FirmwareFeature.Multimodifiers,
                MinimumKeyboardFirmware = multimodifierGate
            };

            yield return new FirmwareGate
            {
                Device = DeviceId.Advantage2,
                Feature = FirmwareFeature.TapAndHold,
                MinimumKeyboardFirmware = multimodifierGate
            };
        }

        private static IEnumerable<FirmwareGate> CreateFreestyleGates(DeviceId deviceId)
        {
            var macroGate = new FirmwareVersion(1, 0, 340);
            var multimodifierGate = new FirmwareVersion(1, 0, 480);

            yield return new FirmwareGate
            {
                Device = deviceId,
                Feature = FirmwareFeature.ExpandedMacroCount,
                MinimumKeyboardFirmware = macroGate
            };

            yield return new FirmwareGate
            {
                Device = deviceId,
                Feature = FirmwareFeature.CustomMacroDelays,
                MinimumKeyboardFirmware = macroGate,
                Message = CustomDelaysRefusalMessage
            };

            yield return new FirmwareGate
            {
                Device = deviceId,
                Feature = FirmwareFeature.Multimodifiers,
                MinimumKeyboardFirmware = multimodifierGate,
                Message = MultimodifiersRefusalMessage
            };

            yield return new FirmwareGate
            {
                Device = deviceId,
                Feature = FirmwareFeature.TapAndHold,
                MinimumKeyboardFirmware = multimodifierGate,
                Message = TapAndHoldRefusalMessage
            };
        }

        private static IEnumerable<FirmwareGate> CreateFreestyleEdgeRgbGates()
        {
            var baselineGate = new FirmwareVersion(1, 0, 1);
            var lightingLayerGate = new FirmwareVersion(1, 0, 44);
            var effectsLedGate = new FirmwareVersion(1, 0, 58);

            yield return new FirmwareGate
            {
                Device = DeviceId.FreestyleEdgeRgb,
                Feature = FirmwareFeature.Multimodifiers,
                MinimumKeyboardFirmware = baselineGate
            };

            yield return new FirmwareGate
            {
                Device = DeviceId.FreestyleEdgeRgb,
                Feature = FirmwareFeature.TapAndHold,
                MinimumKeyboardFirmware = baselineGate
            };

            yield return new FirmwareGate
            {
                Device = DeviceId.FreestyleEdgeRgb,
                Feature = FirmwareFeature.RippleAndFireballEffects,
                MinimumKeyboardFirmware = new FirmwareVersion(1, 0, 121),
                MinimumLedFirmware = effectsLedGate
            };

            yield return new FirmwareGate
            {
                Device = DeviceId.FreestyleEdgeRgb,
                Feature = FirmwareFeature.LightingLayerCustomization,
                MinimumLedFirmware = lightingLayerGate
            };

            yield return new FirmwareGate
            {
                Device = DeviceId.FreestyleEdgeRgb,
                Feature = FirmwareFeature.ExpansionPackOffer,
                ExactLedFirmwareVersions = [lightingLayerGate, effectsLedGate]
            };
        }

        private static IEnumerable<FirmwareGate> CreateTkoGates()
        {
            yield return new FirmwareGate
            {
                Device = DeviceId.Tko,
                Feature = FirmwareFeature.MacroFirmwareWarning,
                ExactKeyboardFirmware = new FirmwareVersion(1, 0, 0),
                Message = TkoMacroFirmwareWarningMessage
            };
        }

        private static IEnumerable<FirmwareGate> CreateAdvantage360Gates()
        {
            yield return new FirmwareGate
            {
                Device = DeviceId.Advantage360,
                Feature = FirmwareFeature.TapAndHoldMacroActions,
                MinimumKeyboardFirmware = new FirmwareVersion(1, 0, 69)
            };
        }
    }
}
