using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Lighting;

namespace KinesisEdit.Core.Tests.Lighting
{
    /// <summary>
    /// Asserts the firmware hooks of the lighting module: Ripple/Fireball gating through
    /// <see cref="FirmwareGateService"/> (RGB gated at KBD ≥ 1.0.121 + LED ≥ 1.0.58, TKO
    /// ungated; specs/07-lighting.md §3, specs/09-firmware.md §2), Fn-layer lighting at
    /// LED ≥ 1.0.44 on the RGB, and the Expansion-Pack precondition that no led file contains
    /// an <c>fn </c>-prefixed line.
    /// </summary>
    public class LightingAvailabilityTests
    {
        [Theory]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        public void IsKeyBacklightModeAvailable_OnRgbBelowTheGate_ReturnsFalse(LightingMode mode)
        {
            var belowLed = RgbState("1.0.121", "1.0.57");
            var belowKeyboard = RgbState("1.0.120", "1.0.58");

            Assert.False(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.FreestyleEdgeRgb, mode, belowLed));
            Assert.False(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.FreestyleEdgeRgb, mode, belowKeyboard));
        }

        [Theory]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        public void IsKeyBacklightModeAvailable_OnRgbAtOrAboveTheGate_ReturnsTrue(LightingMode mode)
        {
            var atGate = RgbState("1.0.121", "1.0.58");
            var aboveGate = RgbState("1.0.1709", "1.0.521");

            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.FreestyleEdgeRgb, mode, atGate));
            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.FreestyleEdgeRgb, mode, aboveGate));
        }

        [Theory]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        public void IsKeyBacklightModeAvailable_OnRgbWithUnknownVersions_ReturnsFalse(LightingMode mode)
        {
            Assert.False(LightingAvailability.IsKeyBacklightModeAvailable(
                DeviceId.FreestyleEdgeRgb,
                mode,
                new FirmwareState()));
        }

        [Theory]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        public void IsKeyBacklightModeAvailable_OnRgbInDemoMode_ReturnsTrue(LightingMode mode)
        {
            var demoState = new FirmwareState { IsDemoMode = true };

            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.FreestyleEdgeRgb, mode, demoState));
        }

        [Theory]
        [InlineData(LightingMode.Ripple)]
        [InlineData(LightingMode.Fireball)]
        public void IsKeyBacklightModeAvailable_OnTko_IsUngatedPerCatalogSemantics(LightingMode mode)
        {
            // The TKO has no RippleAndFireballEffects gate row, and no row means available.
            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.Tko, mode, new FirmwareState()));
        }

        [Theory]
        [InlineData(LightingMode.Wave)]
        [InlineData(LightingMode.Reactive)]
        [InlineData(LightingMode.Disabled)]
        [InlineData(LightingMode.Freestyle)]
        public void IsKeyBacklightModeAvailable_WithUngatedMenuModes_ReturnsTrueOnBothDevices(LightingMode mode)
        {
            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(
                DeviceId.FreestyleEdgeRgb,
                mode,
                new FirmwareState()));
            Assert.True(LightingAvailability.IsKeyBacklightModeAvailable(DeviceId.Tko, mode, new FirmwareState()));
        }

        [Fact]
        public void IsKeyBacklightModeAvailable_WithModesOutsideTheMenu_ReturnsFalse()
        {
            var demoState = new FirmwareState { IsDemoMode = true };

            // Frozen Wave is edge-only and Pitch Black reserved (§3); no firmware unlocks them.
            Assert.False(LightingAvailability.IsKeyBacklightModeAvailable(
                DeviceId.FreestyleEdgeRgb,
                LightingMode.FrozenWave,
                demoState));
            Assert.False(LightingAvailability.IsKeyBacklightModeAvailable(
                DeviceId.Tko,
                LightingMode.PitchBlack,
                demoState));
        }

        [Fact]
        public void IsKeyBacklightModeAvailable_OnDevicesWithoutKeyBacklight_ReturnsFalse()
        {
            var demoState = new FirmwareState { IsDemoMode = true };

            Assert.False(LightingAvailability.IsKeyBacklightModeAvailable(
                DeviceId.Advantage2,
                LightingMode.Monochrome,
                demoState));
        }

        [Theory]
        [InlineData(LightingMode.FrozenWave, true)]
        [InlineData(LightingMode.Monochrome, true)]
        [InlineData(LightingMode.Reactive, false)]
        [InlineData(LightingMode.Ripple, false)]
        [InlineData(LightingMode.Fireball, false)]
        [InlineData(LightingMode.Starlight, false)]
        [InlineData(LightingMode.Rain, false)]
        public void IsEdgeModeAvailable_PerMode_MatchesTheTkoEdgeMenu(LightingMode mode, bool expected)
        {
            Assert.Equal(expected, LightingAvailability.IsEdgeModeAvailable(mode));
        }

        [Fact]
        public void GetKeyBacklightDirections_ForFireball_IsEmptyOnRgbAndLeftRightOnTko()
        {
            // §3: "Fireball … yes (fw-gated; no direction UI on RGB) | yes (direction UI)".
            Assert.Empty(LightingAvailability.GetKeyBacklightDirections(
                DeviceId.FreestyleEdgeRgb,
                LightingMode.Fireball));
            Assert.Equal(
                new[] { LightingDirection.Left, LightingDirection.Right },
                LightingAvailability.GetKeyBacklightDirections(DeviceId.Tko, LightingMode.Fireball));

            // The catalog stays the parse-side data source: RGB files may carry [dirright].
            Assert.Equal(
                new[] { LightingDirection.Left, LightingDirection.Right },
                LightingModeCatalog.Find(LightingMode.Fireball).KeyBacklightDirections);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        public void GetKeyBacklightDirections_ForTheDeviceAgnosticModes_MatchesTheCatalog(DeviceId deviceId)
        {
            // §3: the direction panel shows for Wave, Rebound and Loop on both key-backlight menus.
            Assert.Equal(
                new[] { LightingDirection.Down, LightingDirection.Left, LightingDirection.Up, LightingDirection.Right },
                LightingAvailability.GetKeyBacklightDirections(deviceId, LightingMode.Wave));
            Assert.Equal(
                new[] { LightingDirection.Down, LightingDirection.Left, LightingDirection.Up, LightingDirection.Right },
                LightingAvailability.GetKeyBacklightDirections(deviceId, LightingMode.Loop));
            Assert.Equal(
                new[] { LightingDirection.Left, LightingDirection.Up },
                LightingAvailability.GetKeyBacklightDirections(deviceId, LightingMode.Rebound));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, LightingMode.Monochrome)]
        [InlineData(DeviceId.FreestyleEdgeRgb, LightingMode.Breathe)]
        [InlineData(DeviceId.FreestyleEdgeRgb, LightingMode.FrozenWave)]
        [InlineData(DeviceId.Tko, LightingMode.Rain)]
        [InlineData(DeviceId.Tko, LightingMode.PitchBlack)]
        [InlineData(DeviceId.Advantage2, LightingMode.Wave)]
        [InlineData(DeviceId.Advantage360, LightingMode.Loop)]
        public void GetKeyBacklightDirections_WithoutADirectionPanel_ReturnsEmpty(DeviceId deviceId, LightingMode mode)
        {
            Assert.Empty(LightingAvailability.GetKeyBacklightDirections(deviceId, mode));
        }

        [Theory]
        [InlineData(LightingMode.Wave, new[] { LightingDirection.Left, LightingDirection.Right })]
        [InlineData(LightingMode.Loop, new[] { LightingDirection.Left, LightingDirection.Right })]
        [InlineData(LightingMode.Rebound, new LightingDirection[0])]
        [InlineData(LightingMode.Monochrome, new LightingDirection[0])]
        [InlineData(LightingMode.Fireball, new LightingDirection[0])]
        public void GetEdgeDirections_PerMode_MatchesTheTkoEdgeRulesOfSpec23(
            LightingMode mode,
            LightingDirection[] expectedDirections)
        {
            Assert.Equal(expectedDirections, LightingAvailability.GetEdgeDirections(mode));
        }

        [Fact]
        public void IsFnLayerLightingAvailable_OnRgb_RequiresLedFirmwareOneZeroFortyFour()
        {
            var below = RgbState("1.0.121", "1.0.43");
            var atGate = RgbState("1.0.121", "1.0.44");

            Assert.False(LightingAvailability.IsFnLayerLightingAvailable(DeviceId.FreestyleEdgeRgb, below));
            Assert.False(LightingAvailability.IsFnLayerLightingAvailable(DeviceId.FreestyleEdgeRgb, new FirmwareState()));
            Assert.True(LightingAvailability.IsFnLayerLightingAvailable(DeviceId.FreestyleEdgeRgb, atGate));
        }

        [Fact]
        public void IsFnLayerLightingAvailable_OnTko_IsUngated()
        {
            Assert.True(LightingAvailability.IsFnLayerLightingAvailable(DeviceId.Tko, new FirmwareState()));
        }

        [Theory]
        [InlineData("fn [wave]>[spd5]", true)]
        [InlineData("FN [wave]>[spd5]", true)]
        [InlineData("[wave]>[spd5]", false)]
        [InlineData("fnord [wave]", false)]
        [InlineData("", false)]
        public void ContainsFnLayerLines_PerLineShape_DetectsTheFnPrefix(string line, bool expected)
        {
            Assert.Equal(expected, LightingAvailability.ContainsFnLayerLines([line]));
        }

        [Fact]
        public void HasNoFnLayerLines_WithNoFnLineInAnyFile_ReturnsTrue()
        {
            var ledFiles = new[]
            {
                new[] { "[wave]" },
                Array.Empty<string>(),
                new[] { "[mono]>[1][2][3]" }
            };

            Assert.True(LightingAvailability.HasNoFnLayerLines(ledFiles));
        }

        [Fact]
        public void HasNoFnLayerLines_WithOneFnLineInOneFile_ReturnsFalse()
        {
            var ledFiles = new[]
            {
                new[] { "[wave]" },
                new[] { "[mono]>[1][2][3]", "fn [breathe]>[spd5]" }
            };

            Assert.False(LightingAvailability.HasNoFnLayerLines(ledFiles));
        }

        private static FirmwareState RgbState(string keyboardVersion, string ledVersion)
        {
            return new FirmwareState
            {
                KeyboardFirmware = Parse(keyboardVersion),
                LedFirmware = Parse(ledVersion)
            };
        }

        private static FirmwareVersion Parse(string text)
        {
            Assert.True(FirmwareVersion.TryParse(text, out var version));

            return version;
        }
    }
}
