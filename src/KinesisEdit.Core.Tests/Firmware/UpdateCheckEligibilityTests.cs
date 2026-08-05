using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins who gets the "Check for Updates" dialog (specs/09-firmware.md §3: Edge RGB, TKO,
    /// Advantage 360; §4: no dialog in the FS Edge/Pro, Advantage2, and Savant Elite 2 apps) and
    /// which of them hides the lighting row.
    /// </summary>
    public class UpdateCheckEligibilityTests
    {
        [Fact]
        public void SupportedDevices_Always_AreEdgeRgbTkoAndAdvantage360()
        {
            Assert.Equal(
                new[] { DeviceId.FreestyleEdgeRgb, DeviceId.Tko, DeviceId.Advantage360 },
                UpdateCheckEligibility.SupportedDevices);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, true)]
        [InlineData(DeviceId.Tko, true)]
        [InlineData(DeviceId.Advantage360, true)]
        [InlineData(DeviceId.Advantage360Professional, false)]
        [InlineData(DeviceId.None, false)]
        [InlineData(DeviceId.SavantElite2, false)]
        [InlineData(DeviceId.Advantage2, false)]
        [InlineData(DeviceId.FreestyleEdge, false)]
        [InlineData(DeviceId.FreestylePro, false)]
        [InlineData(DeviceId.CrossfireKeypad, false)]
        public void IsSupported_ForEveryDeviceId_MatchesSpec09Sections3And4(DeviceId deviceId, bool expected)
        {
            Assert.Equal(expected, UpdateCheckEligibility.IsSupported(deviceId));
        }

        [Fact]
        public void IsSupported_ForAdvantage360Professional_IsFalseBecauseItIsNotProgrammable()
        {
            Assert.False(DeviceCatalog.GetById(DeviceId.Advantage360Professional).IsProgrammable);
            Assert.False(UpdateCheckEligibility.IsSupported(DeviceId.Advantage360Professional));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, true)]
        [InlineData(DeviceId.Tko, true)]
        [InlineData(DeviceId.Advantage360, false)]
        [InlineData(DeviceId.Advantage360Professional, false)]
        [InlineData(DeviceId.Advantage2, false)]
        public void HasLightingRow_PerDevice_HidesTheRowForTheAdvantage360Family(DeviceId deviceId, bool expected)
        {
            Assert.Equal(expected, UpdateCheckEligibility.HasLightingRow(deviceId));
        }
    }
}
