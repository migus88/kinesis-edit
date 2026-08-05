using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Asserts the gate evaluation semantics of specs/09-firmware.md §2: every spec-table row at
    /// its threshold and just below, exact-match gates on and around the exact version, the
    /// compound RGB rule, unknown versions, ungated pairs, and the demo-mode bypass.
    /// </summary>
    public class FirmwareGateServiceTests
    {
        [Theory]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.ExpandedMacroCount, "1.0.340", true)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.ExpandedMacroCount, "1.0.339", false)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.CustomMacroDelays, "1.0.340", true)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.CustomMacroDelays, "1.0.339", false)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.Multimodifiers, "1.0.480", true)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.Multimodifiers, "1.0.479", false)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold, "1.0.480", true)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold, "1.0.479", false)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.ExpandedMacroCount, "1.0.340", true)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.ExpandedMacroCount, "1.0.339", false)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.CustomMacroDelays, "1.0.340", true)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.CustomMacroDelays, "1.0.339", false)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.Multimodifiers, "1.0.480", true)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.Multimodifiers, "1.0.479", false)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.TapAndHold, "1.0.480", true)]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.TapAndHold, "1.0.479", false)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.Multimodifiers, "1.0.516", true)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.Multimodifiers, "1.0.515", false)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.TapAndHold, "1.0.516", true)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.TapAndHold, "1.0.515", false)]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.Multimodifiers, "1.0.1", true)]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.Multimodifiers, "1.0.0", false)]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.TapAndHold, "1.0.1", true)]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.TapAndHold, "1.0.0", false)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.TapAndHoldMacroActions, "1.0.69", true)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.TapAndHoldMacroActions, "1.0.68", false)]
        public void IsAvailable_WithKeyboardVersionAtAndBelowMinimum_MatchesSpec09GateTable(
            DeviceId deviceId,
            FirmwareFeature feature,
            string keyboardFirmware,
            bool expectedAvailability)
        {
            var state = CreateState(keyboardFirmware);

            Assert.Equal(expectedAvailability, FirmwareGateService.IsAvailable(deviceId, feature, state));
        }

        [Theory]
        [InlineData("1.0.341", true)]
        [InlineData("1.0.1709", true)]
        [InlineData("2.0.0", true)]
        [InlineData("0.9.999", false)]
        public void IsAvailable_WithKeyboardVersionAwayFromMinimum_TreatsMinimumAsInclusiveLowerBound(
            string keyboardFirmware,
            bool expectedAvailability)
        {
            var state = CreateState(keyboardFirmware);

            var isAvailable = FirmwareGateService.IsAvailable(
                DeviceId.FreestyleEdge,
                FirmwareFeature.ExpandedMacroCount,
                state);

            Assert.Equal(expectedAvailability, isAvailable);
        }

        [Theory]
        [InlineData("1.0.121", "1.0.58", true)]
        [InlineData("1.0.122", "1.0.59", true)]
        [InlineData("1.0.120", "1.0.58", false)]
        [InlineData("1.0.121", "1.0.57", false)]
        [InlineData("1.0.120", "1.0.57", false)]
        [InlineData("1.0.121", null, false)]
        [InlineData(null, "1.0.58", false)]
        public void IsAvailable_WithCompoundRgbRippleAndFireballGate_RequiresBothConditions(
            string? keyboardFirmware,
            string? ledFirmware,
            bool expectedAvailability)
        {
            var state = CreateState(keyboardFirmware, ledFirmware);

            var isAvailable = FirmwareGateService.IsAvailable(
                DeviceId.FreestyleEdgeRgb,
                FirmwareFeature.RippleAndFireballEffects,
                state);

            Assert.Equal(expectedAvailability, isAvailable);
        }

        [Theory]
        [InlineData("1.0.44", true)]
        [InlineData("1.0.45", true)]
        [InlineData("1.0.43", false)]
        [InlineData(null, false)]
        public void IsAvailable_WithRgbLightingLayerGate_ChecksLedMinimumOnly(
            string? ledFirmware,
            bool expectedAvailability)
        {
            var state = CreateState(keyboardFirmware: null, ledFirmware);

            var isAvailable = FirmwareGateService.IsAvailable(
                DeviceId.FreestyleEdgeRgb,
                FirmwareFeature.LightingLayerCustomization,
                state);

            Assert.Equal(expectedAvailability, isAvailable);
        }

        [Theory]
        [InlineData("1.0.44", true)]
        [InlineData("1.0.58", true)]
        [InlineData("1.0.43", false)]
        [InlineData("1.0.45", false)]
        [InlineData("1.0.57", false)]
        [InlineData("1.0.59", false)]
        [InlineData(null, false)]
        public void IsAvailable_WithRgbExpansionPackOfferGate_RequiresExactLedVersion(
            string? ledFirmware,
            bool expectedAvailability)
        {
            var state = CreateState(keyboardFirmware: null, ledFirmware);

            var isAvailable = FirmwareGateService.IsAvailable(
                DeviceId.FreestyleEdgeRgb,
                FirmwareFeature.ExpansionPackOffer,
                state);

            Assert.Equal(expectedAvailability, isAvailable);
        }

        [Theory]
        [InlineData("1.0.0", true)]
        [InlineData("1.0.1", false)]
        [InlineData("0.9.9", false)]
        [InlineData(null, false)]
        public void IsAvailable_WithTkoMacroFirmwareWarningGate_RequiresExactKeyboardVersion(
            string? keyboardFirmware,
            bool expectedAvailability)
        {
            var state = CreateState(keyboardFirmware);

            var isAvailable = FirmwareGateService.IsAvailable(
                DeviceId.Tko,
                FirmwareFeature.MacroFirmwareWarning,
                state);

            Assert.Equal(expectedAvailability, isAvailable);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.ExpandedMacroCount)]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.Multimodifiers)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.TapAndHoldMacroActions)]
        public void IsAvailable_WithGatedFeatureAndUnknownVersion_ReturnsFalse(DeviceId deviceId, FirmwareFeature feature)
        {
            var state = CreateState(keyboardFirmware: null);

            Assert.False(FirmwareGateService.IsAvailable(deviceId, feature, state));
        }

        [Theory]
        [InlineData(DeviceId.Tko, FirmwareFeature.TapAndHold)]
        [InlineData(DeviceId.Tko, FirmwareFeature.Multimodifiers)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.TapAndHold)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.Multimodifiers)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.ExpandedMacroCount)]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.ExpandedMacroCount)]
        public void IsAvailable_WithUngatedPair_ReturnsTrueEvenWithoutVersions(DeviceId deviceId, FirmwareFeature feature)
        {
            var state = CreateState(keyboardFirmware: null);

            Assert.True(FirmwareGateService.IsAvailable(deviceId, feature, state));
        }

        [Fact]
        public void IsAvailable_WithDemoMode_PassesEveryGateInTheCatalogWithoutAnyVersions()
        {
            var demoState = new FirmwareState
            {
                IsDemoMode = true
            };

            foreach (var gate in FirmwareGateCatalog.All)
            {
                Assert.True(FirmwareGateService.IsAvailable(gate.Device, gate.Feature, demoState));
            }
        }

        private static FirmwareState CreateState(string? keyboardFirmware, string? ledFirmware = null)
        {
            return new FirmwareState
            {
                KeyboardFirmware = ParseVersion(keyboardFirmware),
                LedFirmware = ParseVersion(ledFirmware)
            };
        }

        private static FirmwareVersion? ParseVersion(string? text)
        {
            if (text is null)
            {
                return null;
            }

            Assert.True(FirmwareVersion.TryParse(text, out var version));

            return version;
        }
    }
}
