using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Integration
{
    /// <summary>
    /// Cross-module consistency check between the two places the spec 09 §2 minimum-firmware
    /// versions are encoded: the device catalog carries them as capability data
    /// (<see cref="TapAndHoldCapability.MinimumFirmware"/>,
    /// <see cref="MacroCapability.MacroCountGateFirmware"/>) so a device's limits can be read
    /// without a firmware probe, while <see cref="FirmwareGateCatalog"/> carries the same numbers
    /// as the rows the gate evaluation runs against. Nothing in the type system keeps the two in
    /// step — these tests do, in both directions: a capability version without a matching gate
    /// row, or a gate row without a matching capability version, fails here.
    /// </summary>
    public class FirmwareGateConsistencyTests
    {
        public static IEnumerable<object[]> AllDeviceIds()
        {
            foreach (var device in DeviceCatalog.All)
            {
                yield return new object[] { device.Id };
            }
        }

        /// <summary>The features whose spec 09 §2 threshold is mirrored onto a device capability.</summary>
        private static IReadOnlyDictionary<FirmwareFeature, Func<DeviceDefinition, FirmwareVersion?>> MirroredFeatures { get; } =
            new Dictionary<FirmwareFeature, Func<DeviceDefinition, FirmwareVersion?>>
            {
                [FirmwareFeature.TapAndHold] = device => device.TapAndHold.MinimumFirmware,
                [FirmwareFeature.ExpandedMacroCount] = device => device.Macros.MacroCountGateFirmware
            };

        [Theory]
        [MemberData(nameof(AllDeviceIds))]
        public void TapAndHoldMinimumFirmware_ForEveryDevice_MatchesGateCatalog(DeviceId deviceId)
        {
            // Both null is the agreement case, not a case to skip: TKO and Adv360 support tap and
            // hold with no firmware gate at all, so neither side may invent a version.
            var capabilityVersion = DeviceCatalog.GetById(deviceId).TapAndHold.MinimumFirmware;
            var gateVersion = FirmwareGateCatalog.Find(deviceId, FirmwareFeature.TapAndHold)?.MinimumKeyboardFirmware;

            Assert.Equal(capabilityVersion, gateVersion);
        }

        [Theory]
        [MemberData(nameof(AllDeviceIds))]
        public void MacroCountGateFirmware_ForEveryDevice_MatchesGateCatalog(DeviceId deviceId)
        {
            var capabilityVersion = DeviceCatalog.GetById(deviceId).Macros.MacroCountGateFirmware;
            var gateVersion = FirmwareGateCatalog.Find(deviceId, FirmwareFeature.ExpandedMacroCount)?.MinimumKeyboardFirmware;

            Assert.Equal(capabilityVersion, gateVersion);
        }

        [Fact]
        public void GateCatalogRows_ForMirroredFeatures_MatchTheDeviceCatalogCapability()
        {
            // The reverse sweep: the per-device theories start from the catalog, so a gate row
            // added for a device whose capability does not mirror it would slip past them.
            var mismatches = new List<string>();

            foreach (var gate in FirmwareGateCatalog.All)
            {
                if (!MirroredFeatures.TryGetValue(gate.Feature, out var capabilitySelector))
                {
                    continue;
                }

                var device = DeviceCatalog.All.FirstOrDefault(candidate => candidate.Id == gate.Device);

                if (device is null)
                {
                    mismatches.Add($"{gate.Device}/{gate.Feature}: no device catalog entry");
                    continue;
                }

                var capabilityVersion = capabilitySelector(device);

                if (!Equals(capabilityVersion, gate.MinimumKeyboardFirmware))
                {
                    mismatches.Add(
                        $"{gate.Device}/{gate.Feature}: gate {Describe(gate.MinimumKeyboardFirmware)} " +
                        $"vs capability {Describe(capabilityVersion)}");
                }
            }

            // Not Assert.Empty: xUnit truncates each rendered element, which would clip the
            // versions off the end of a mismatch line.
            Assert.True(
                mismatches.Count == 0,
                $"Gate rows out of sync with the device catalog: {string.Join("; ", mismatches)}");
        }

        private static string Describe(FirmwareVersion? version)
        {
            return version?.ToString() ?? "none";
        }
    }
}
