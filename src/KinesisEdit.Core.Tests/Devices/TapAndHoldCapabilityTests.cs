using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    /// <summary>
    /// Asserts the tap-and-hold capability record of specs/11-feature-dialogs.md §11.1 and the
    /// per-layout limit of specs/04-layout-file-format.md §5.3.
    /// </summary>
    public class TapAndHoldCapabilityTests
    {
        [Fact]
        public void None_Always_ReportsNoSupportAndNoLimits()
        {
            var capability = TapAndHoldCapability.None;

            Assert.False(capability.IsSupported);
            Assert.Null(capability.MaxPerLayout);
            Assert.Null(capability.DelayMilliseconds);
            Assert.Null(capability.DefaultDelayMilliseconds);
            Assert.Null(capability.MinimumFirmware);
        }

        [Fact]
        public void DefaultDelayMilliseconds_WithDelayRange_MirrorsRangeDefault()
        {
            var capability = new TapAndHoldCapability
            {
                IsSupported = true,
                MaxPerLayout = 10,
                DelayMilliseconds = new ValueRange(Minimum: 1, Maximum: 999, Default: 150)
            };

            Assert.Equal(150, capability.DefaultDelayMilliseconds);
        }

        [Fact]
        public void Equals_WithSameData_ComparesByValue()
        {
            var firmware = new FirmwareVersion(1, 0, 516);

            var first = new TapAndHoldCapability
            {
                IsSupported = true,
                MaxPerLayout = 10,
                DelayMilliseconds = new ValueRange(1, 999, 250),
                MinimumFirmware = firmware
            };

            var second = new TapAndHoldCapability
            {
                IsSupported = true,
                MaxPerLayout = 10,
                DelayMilliseconds = new ValueRange(1, 999, 250),
                MinimumFirmware = firmware
            };

            Assert.Equal(first, second);
        }
    }
}
