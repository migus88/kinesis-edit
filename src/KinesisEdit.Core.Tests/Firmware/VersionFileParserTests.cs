using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Asserts the version-file parsing rules of specs/09-firmware.md §1 against the exact
    /// example files the spec quotes from real devices, plus the Savant Elite 2 free-text rule
    /// of specs/12-savant-elite.md §1.
    /// </summary>
    public class VersionFileParserTests
    {
        [Fact]
        public void Parse_WithRgbVersionFileFromSpec_ReadsModelBothFirmwaresAndMarker()
        {
            var lines = new[]
            {
                "Model name: FS Edge RGB",
                "KBD Firmware: 1.0.1709.us (4MB), 03/08/2019",
                "LED Firmware: 1.0.521",
                "LED Bootloader: 255.255"
            };

            var info = VersionFileParser.Parse(DeviceId.FreestyleEdgeRgb, lines);

            Assert.Equal("FS Edge RGB", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 1709), info.KeyboardFirmware);
            Assert.Equal("1.0.1709.us (4MB), 03/08/2019", info.KeyboardFirmwareText);
            Assert.Equal(new FirmwareVersion(1, 0, 521), info.LedFirmware);
            Assert.Equal("1.0.521", info.LedFirmwareText);
            Assert.True(info.HasFourMegabyteMarker);
        }

        [Fact]
        public void Parse_WithLedBootloaderLine_NeverParsesItAsLedFirmware()
        {
            var lines = new[]
            {
                "LED Bootloader: 255.255"
            };

            var info = VersionFileParser.Parse(DeviceId.FreestyleEdgeRgb, lines);

            Assert.Null(info.LedFirmware);
            Assert.Equal(string.Empty, info.LedFirmwareText);
        }

        [Fact]
        public void Parse_WithFreestyleEdgeVersionFileFromSpec_ReadsModelAndFirmwareVersionLine()
        {
            var lines = new[]
            {
                "Model name: FS Edge",
                "Firmware version: 1.0.340.us (2MB), 09/26/2016"
            };

            var info = VersionFileParser.Parse(DeviceId.FreestyleEdge, lines);

            Assert.Equal("FS Edge", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 340), info.KeyboardFirmware);
            Assert.Equal("1.0.340.us (2MB), 09/26/2016", info.KeyboardFirmwareText);
            Assert.Null(info.LedFirmware);
            Assert.False(info.HasFourMegabyteMarker);
        }

        [Fact]
        public void Parse_WithTkoVersionFile_UsesKbdAndLedFirmwarePrefixes()
        {
            var lines = new[]
            {
                "Model name: TKO",
                "KBD Firmware: 1.0.0",
                "LED Firmware: 1.0.5"
            };

            var info = VersionFileParser.Parse(DeviceId.Tko, lines);

            Assert.Equal("TKO", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 0), info.KeyboardFirmware);
            Assert.Equal(new FirmwareVersion(1, 0, 5), info.LedFirmware);
        }

        [Fact]
        public void Parse_WithAdvantage2FourMegabyteVersionFile_SetsMarker()
        {
            var lines = new[]
            {
                "Model name: Advantage2",
                "Firmware version: 1.0.516.us (4MB), 11/09/2017"
            };

            var info = VersionFileParser.Parse(DeviceId.Advantage2, lines);

            Assert.Equal("Advantage2", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 516), info.KeyboardFirmware);
            Assert.Equal("1.0.516.us (4MB), 11/09/2017", info.KeyboardFirmwareText);
            Assert.True(info.HasFourMegabyteMarker);
        }

        [Fact]
        public void Parse_WithAdvantage2TwoMegabyteVersionFile_LeavesMarkerUnset()
        {
            var lines = new[]
            {
                "Model name: Advantage2",
                "Firmware version: 1.0.430.us (2MB), 05/17/2016"
            };

            var info = VersionFileParser.Parse(DeviceId.Advantage2, lines);

            Assert.Equal(new FirmwareVersion(1, 0, 430), info.KeyboardFirmware);
            Assert.False(info.HasFourMegabyteMarker);
        }

        [Fact]
        public void Parse_WithSavantElite2FreeTextFileFromSpec_ScansFirstDottedNumericToken()
        {
            var lines = new[]
            {
                "Firmware version is 1.0.44",
                "01/20/2015"
            };

            var info = VersionFileParser.Parse(DeviceId.SavantElite2, lines);

            Assert.Equal(new FirmwareVersion(1, 0, 44), info.KeyboardFirmware);
            Assert.Equal("1.0.44", info.KeyboardFirmwareText);
            Assert.Equal(string.Empty, info.ModelName);
            Assert.Null(info.LedFirmware);
        }

        [Fact]
        public void Parse_WithSavantElite2FileWithoutDottedToken_LeavesKeyboardFirmwareNull()
        {
            var lines = new[]
            {
                "Firmware version is unknown",
                "01/20/2015"
            };

            var info = VersionFileParser.Parse(DeviceId.SavantElite2, lines);

            Assert.Null(info.KeyboardFirmware);
            Assert.Equal(string.Empty, info.KeyboardFirmwareText);
        }

        [Fact]
        public void Parse_WithAdvantage360SettingsFile_ReadsModelAndKbdFwRKeys()
        {
            var lines = new[]
            {
                "model=ADV360",
                "kbd_fw_r=1.0.69",
                "profile=1",
                "status=2"
            };

            var info = VersionFileParser.Parse(DeviceId.Advantage360, lines);

            Assert.Equal("ADV360", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 69), info.KeyboardFirmware);
            Assert.Equal("1.0.69", info.KeyboardFirmwareText);
            Assert.Null(info.LedFirmware);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, "MODEL NAME: FS PRO", "FIRMWARE VERSION: 1.0.480")]
        [InlineData(DeviceId.FreestyleEdge, "model name: FS PRO", "firmware version: 1.0.480")]
        [InlineData(DeviceId.FreestyleEdge, "Model Name: FS PRO", "Firmware Version: 1.0.480")]
        public void Parse_WithMixedCasePrefixes_MatchesCaseInsensitively(DeviceId deviceId, string modelLine, string versionLine)
        {
            var info = VersionFileParser.Parse(deviceId, [modelLine, versionLine]);

            Assert.Equal("FS PRO", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 480), info.KeyboardFirmware);
        }

        [Fact]
        public void Parse_WithUnknownAndGarbageLines_IgnoresThem()
        {
            var lines = new[]
            {
                "; comment line",
                "serial: 97BRNUSAA0000",
                "Model name: FS Edge",
                "not a version line at all",
                "Firmware version: 1.0.340.us (2MB), 09/26/2016",
                ""
            };

            var info = VersionFileParser.Parse(DeviceId.FreestyleEdge, lines);

            Assert.Equal("FS Edge", info.ModelName);
            Assert.Equal(new FirmwareVersion(1, 0, 340), info.KeyboardFirmware);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.SavantElite2)]
        public void Parse_WithEmptyInput_ReturnsEmptyInfo(DeviceId deviceId)
        {
            var info = VersionFileParser.Parse(deviceId, []);

            Assert.Equal(VersionFileInfo.Empty, info);
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void Parse_WithDeviceWithoutVersionData_ReturnsEmptyInfo(DeviceId deviceId)
        {
            var lines = new[]
            {
                "Model name: something",
                "Firmware version: 1.0.1"
            };

            var info = VersionFileParser.Parse(deviceId, lines);

            Assert.Equal(VersionFileInfo.Empty, info);
        }

        [Fact]
        public void Parse_WithNullLines_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => VersionFileParser.Parse(DeviceId.FreestyleEdge, null!));
        }

        [Fact]
        public void Parse_WithFreestyleProVersionFile_ResolvesFreestyleProModel()
        {
            var lines = new[]
            {
                "Model name: FS PRO",
                "Firmware version: 1.0.480.us (4MB), 06/20/2018"
            };

            var info = VersionFileParser.Parse(DeviceId.FreestyleEdge, lines);

            Assert.Equal(DeviceId.FreestylePro, info.ResolveFreestyleModel());
        }

        [Fact]
        public void Parse_WithFreestyleEdgeVersionFile_ResolvesFreestyleEdgeModel()
        {
            var lines = new[]
            {
                "Model name: FS Edge",
                "Firmware version: 1.0.340.us (2MB), 09/26/2016"
            };

            var info = VersionFileParser.Parse(DeviceId.FreestyleEdge, lines);

            Assert.Equal(DeviceId.FreestyleEdge, info.ResolveFreestyleModel());
        }

        [Fact]
        public void Parse_WithFourMegabyteMarkerOnUnparsedLine_StillSetsMarker()
        {
            var lines = new[]
            {
                "Model name: Advantage2",
                "Board: 4MB",
                "Firmware version: 1.0.516"
            };

            var info = VersionFileParser.Parse(DeviceId.Advantage2, lines);

            Assert.True(info.HasFourMegabyteMarker);
        }

        [Fact]
        public void Parse_WithLowercaseFourMegabyteMarker_SetsMarkerCaseInsensitively()
        {
            var lines = new[]
            {
                "firmware version: 1.0.516.us (4mb), 11/09/2017"
            };

            var info = VersionFileParser.Parse(DeviceId.Advantage2, lines);

            Assert.True(info.HasFourMegabyteMarker);
        }
    }
}
