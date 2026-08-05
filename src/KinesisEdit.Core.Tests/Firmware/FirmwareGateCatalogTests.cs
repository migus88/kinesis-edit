using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the gate catalog against the minimum-firmware gate table of specs/09-firmware.md §2
    /// (restated in specs 07 and 11 §11.1): one row per (device, feature) with its exact
    /// thresholds, and no rows for the pairs the spec leaves ungated.
    /// </summary>
    public class FirmwareGateCatalogTests
    {
        [Fact]
        public void All_Always_ContainsSeventeenGateRows()
        {
            Assert.Equal(17, FirmwareGateCatalog.All.Count);
        }

        [Fact]
        public void All_Always_HasAtMostOneGatePerDeviceAndFeature()
        {
            var pairs = FirmwareGateCatalog.All.Select(gate => (gate.Device, gate.Feature));

            Assert.Equal(FirmwareGateCatalog.All.Count, pairs.Distinct().Count());
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.ExpandedMacroCount, "1.0.340")]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.CustomMacroDelays, "1.0.340")]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.Multimodifiers, "1.0.480")]
        [InlineData(DeviceId.FreestyleEdge, FirmwareFeature.TapAndHold, "1.0.480")]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.ExpandedMacroCount, "1.0.340")]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.CustomMacroDelays, "1.0.340")]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.Multimodifiers, "1.0.480")]
        [InlineData(DeviceId.FreestylePro, FirmwareFeature.TapAndHold, "1.0.480")]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.Multimodifiers, "1.0.516")]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.TapAndHold, "1.0.516")]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.Multimodifiers, "1.0.1")]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.TapAndHold, "1.0.1")]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.TapAndHoldMacroActions, "1.0.69")]
        public void Find_WithMinimumKeyboardGatePair_HasThresholdPerSpec09(
            DeviceId deviceId,
            FirmwareFeature feature,
            string expectedMinimum)
        {
            var gate = FirmwareGateCatalog.Find(deviceId, feature);

            Assert.NotNull(gate);
            Assert.Equal(ParseVersion(expectedMinimum), gate.MinimumKeyboardFirmware);
            Assert.Null(gate.MinimumLedFirmware);
            Assert.Null(gate.ExactKeyboardFirmware);
            Assert.Empty(gate.ExactLedFirmwareVersions);
        }

        [Fact]
        public void Find_WithRgbRippleAndFireball_RequiresBothKeyboardAndLedMinimums()
        {
            var gate = FirmwareGateCatalog.Find(DeviceId.FreestyleEdgeRgb, FirmwareFeature.RippleAndFireballEffects);

            Assert.NotNull(gate);
            Assert.Equal(new FirmwareVersion(1, 0, 121), gate.MinimumKeyboardFirmware);
            Assert.Equal(new FirmwareVersion(1, 0, 58), gate.MinimumLedFirmware);
            Assert.Null(gate.ExactKeyboardFirmware);
            Assert.Empty(gate.ExactLedFirmwareVersions);
        }

        [Fact]
        public void Find_WithRgbLightingLayerCustomization_RequiresOnlyLedMinimum()
        {
            var gate = FirmwareGateCatalog.Find(DeviceId.FreestyleEdgeRgb, FirmwareFeature.LightingLayerCustomization);

            Assert.NotNull(gate);
            Assert.Null(gate.MinimumKeyboardFirmware);
            Assert.Equal(new FirmwareVersion(1, 0, 44), gate.MinimumLedFirmware);
        }

        [Fact]
        public void Find_WithRgbExpansionPackOffer_RequiresExactLedVersionFortyFourOrFiftyEight()
        {
            var gate = FirmwareGateCatalog.Find(DeviceId.FreestyleEdgeRgb, FirmwareFeature.ExpansionPackOffer);

            Assert.NotNull(gate);
            Assert.Equal(
                new[] { new FirmwareVersion(1, 0, 44), new FirmwareVersion(1, 0, 58) },
                gate.ExactLedFirmwareVersions);
            Assert.Null(gate.MinimumKeyboardFirmware);
            Assert.Null(gate.MinimumLedFirmware);
            Assert.Null(gate.ExactKeyboardFirmware);
        }

        [Fact]
        public void Find_WithTkoMacroFirmwareWarning_RequiresExactKeyboardVersionOneZeroZero()
        {
            var gate = FirmwareGateCatalog.Find(DeviceId.Tko, FirmwareFeature.MacroFirmwareWarning);

            Assert.NotNull(gate);
            Assert.Equal(new FirmwareVersion(1, 0, 0), gate.ExactKeyboardFirmware);
            Assert.Null(gate.MinimumKeyboardFirmware);
            Assert.Null(gate.MinimumLedFirmware);
            Assert.Empty(gate.ExactLedFirmwareVersions);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        public void Find_WithFreestyleRefusalDialogGates_CarriesSpecMessages(DeviceId deviceId)
        {
            var delaysGate = FirmwareGateCatalog.Find(deviceId, FirmwareFeature.CustomMacroDelays);
            var multimodifiersGate = FirmwareGateCatalog.Find(deviceId, FirmwareFeature.Multimodifiers);
            var tapAndHoldGate = FirmwareGateCatalog.Find(deviceId, FirmwareFeature.TapAndHold);

            Assert.Equal(
                "To utilize custom or random delays, please download and install the latest firmware.",
                delaysGate?.Message);
            Assert.Equal(
                "To utilize Multimodifiers, please download and install the latest firmware.",
                multimodifiersGate?.Message);
            Assert.Equal(
                "To utilize Tap and Hold Actions, please download and install the latest firmware.",
                tapAndHoldGate?.Message);
        }

        [Fact]
        public void Find_WithTkoMacroFirmwareWarning_CarriesSpecWarningText()
        {
            var gate = FirmwareGateCatalog.Find(DeviceId.Tko, FirmwareFeature.MacroFirmwareWarning);

            Assert.Equal(
                "Attention macro users: Update your firmware now for full functionality.",
                gate?.Message);
        }

        [Theory]
        [InlineData(DeviceId.Tko, FirmwareFeature.TapAndHold)]
        [InlineData(DeviceId.Tko, FirmwareFeature.Multimodifiers)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.TapAndHold)]
        [InlineData(DeviceId.Advantage360, FirmwareFeature.Multimodifiers)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.ExpandedMacroCount)]
        [InlineData(DeviceId.Advantage2, FirmwareFeature.CustomMacroDelays)]
        [InlineData(DeviceId.FreestyleEdgeRgb, FirmwareFeature.ExpandedMacroCount)]
        [InlineData(DeviceId.SavantElite2, FirmwareFeature.TapAndHold)]
        [InlineData(DeviceId.None, FirmwareFeature.TapAndHold)]
        public void Find_WithUngatedPair_ReturnsNull(DeviceId deviceId, FirmwareFeature feature)
        {
            Assert.Null(FirmwareGateCatalog.Find(deviceId, feature));
        }

        private static FirmwareVersion ParseVersion(string text)
        {
            Assert.True(FirmwareVersion.TryParse(text, out var version));

            return version;
        }
    }
}
