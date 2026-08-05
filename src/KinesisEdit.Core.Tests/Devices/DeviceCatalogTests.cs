using KinesisEdit.Core.Devices;

namespace KinesisEdit.Core.Tests.Devices
{
    public class DeviceCatalogTests
    {
        [Fact]
        public void All_Always_ContainsNineDevicesInMasterTableOrder()
        {
            var expectedOrder = new[]
            {
                DeviceId.SavantElite2,
                DeviceId.Advantage2,
                DeviceId.FreestyleEdge,
                DeviceId.FreestylePro,
                DeviceId.FreestyleEdgeRgb,
                DeviceId.CrossfireKeypad,
                DeviceId.Tko,
                DeviceId.Advantage360,
                DeviceId.Advantage360Professional
            };

            Assert.Equal(9, DeviceCatalog.All.Count);
            Assert.Equal(expectedOrder, DeviceCatalog.All.Select(device => device.Id));
        }

        [Fact]
        public void All_Always_AssignsLegacyAppIdsZeroThroughEightInOrder()
        {
            for (var index = 0; index < DeviceCatalog.All.Count; index++)
            {
                Assert.Equal(index, DeviceCatalog.All[index].LegacyAppId);
            }
        }

        [Fact]
        public void All_Always_HasNonEmptyDisplayNamesAndAtMostThreeVolumeLabels()
        {
            foreach (var device in DeviceCatalog.All)
            {
                Assert.False(string.IsNullOrWhiteSpace(device.DisplayName));
                Assert.InRange(device.VolumeLabels.Count, 0, 3);
            }
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void GetById_WithCatalogDeviceId_ReturnsDefinitionWithThatId(DeviceId deviceId)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(deviceId, device.Id);
        }

        [Fact]
        public void GetById_WithNoneId_ThrowsArgumentOutOfRangeException()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => DeviceCatalog.GetById(DeviceId.None));
        }

        [Theory]
        [InlineData("FS EDGE RGB", DeviceId.FreestyleEdgeRgb)]
        [InlineData("FS EDGE", DeviceId.FreestyleEdge)]
        [InlineData("FS PRO", DeviceId.FreestylePro)]
        [InlineData("ADVANTAGE2", DeviceId.Advantage2)]
        [InlineData("KINESIS KB", DeviceId.Advantage2)]
        [InlineData("ADV2", DeviceId.Advantage2)]
        [InlineData("SE2", DeviceId.SavantElite2)]
        [InlineData("KINESIS FP", DeviceId.SavantElite2)]
        [InlineData("CROSSFIRE KEYPAD", DeviceId.CrossfireKeypad)]
        [InlineData("TKO", DeviceId.Tko)]
        [InlineData("ADV360", DeviceId.Advantage360)]
        public void FindByVolumeLabel_WithEverySpecLabel_ResolvesCorrectDevice(string volumeLabel, DeviceId expectedId)
        {
            var device = DeviceCatalog.FindByVolumeLabel(volumeLabel);

            Assert.NotNull(device);
            Assert.Equal(expectedId, device.Id);
        }

        [Theory]
        [InlineData("  FS EDGE  ", DeviceId.FreestyleEdge)]
        [InlineData("fs edge rgb", DeviceId.FreestyleEdgeRgb)]
        [InlineData("Fs Pro", DeviceId.FreestylePro)]
        [InlineData("adv360", DeviceId.Advantage360)]
        [InlineData("\tkinesis kb ", DeviceId.Advantage2)]
        [InlineData(" tko ", DeviceId.Tko)]
        public void FindByVolumeLabel_WithMixedCaseAndSurroundingWhitespace_ResolvesCorrectDevice(
            string volumeLabel,
            DeviceId expectedId)
        {
            var device = DeviceCatalog.FindByVolumeLabel(volumeLabel);

            Assert.NotNull(device);
            Assert.Equal(expectedId, device.Id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("FS")]
        [InlineData("EDGE")]
        [InlineData("FS EDGE R")]
        [InlineData("FS EDGE RGB PLUS")]
        [InlineData("ADVANTAGE")]
        [InlineData("KINESIS")]
        [InlineData("TKO2")]
        [InlineData("UNKNOWN DRIVE")]
        public void FindByVolumeLabel_WithUnknownLabel_ReturnsNull(string? volumeLabel)
        {
            Assert.Null(DeviceCatalog.FindByVolumeLabel(volumeLabel));
        }

        [Fact]
        public void FindByVolumeLabel_WithFsEdgeLabels_DistinguishesEdgeFromEdgeRgb()
        {
            var edge = DeviceCatalog.FindByVolumeLabel("FS EDGE");
            var edgeRgb = DeviceCatalog.FindByVolumeLabel("FS EDGE RGB");

            Assert.NotNull(edge);
            Assert.NotNull(edgeRgb);
            Assert.Equal(DeviceId.FreestyleEdge, edge.Id);
            Assert.Equal(DeviceId.FreestyleEdgeRgb, edgeRgb.Id);
        }

        [Fact]
        public void VolumeLabels_ForDevicesWithAlternates_KeepPrimaryLabelFirst()
        {
            var advantage2 = DeviceCatalog.GetById(DeviceId.Advantage2);
            var savantElite2 = DeviceCatalog.GetById(DeviceId.SavantElite2);
            var advantage360Professional = DeviceCatalog.GetById(DeviceId.Advantage360Professional);

            Assert.Equal(new[] { "ADVANTAGE2", "KINESIS KB", "ADV2" }, advantage2.VolumeLabels);
            Assert.Equal(new[] { "SE2", "KINESIS FP" }, savantElite2.VolumeLabels);
            Assert.Empty(advantage360Professional.VolumeLabels);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, "firmware", "version.txt")]
        [InlineData(DeviceId.FreestylePro, "firmware", "version.txt")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "firmware", "version.txt")]
        [InlineData(DeviceId.Tko, "firmware", "version.txt")]
        [InlineData(DeviceId.Advantage2, "active", "version.txt")]
        [InlineData(DeviceId.SavantElite2, "active", "version.txt")]
        [InlineData(DeviceId.Advantage360, "settings", "settings.txt")]
        public void GetById_WithDetectableDevice_HasMarkerFolderAndFilePerSpec03(
            DeviceId deviceId,
            string expectedMarkerFolder,
            string expectedMarkerFile)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedMarkerFolder, device.MarkerFolder);
            Assert.Equal(expectedMarkerFile, device.MarkerFile);
        }

        [Theory]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void GetById_WithUndetectableDevice_HasNoMarkerOrVersionPaths(DeviceId deviceId)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Null(device.MarkerFolder);
            Assert.Null(device.MarkerFile);
            Assert.Null(device.VersionFolder);
            Assert.Null(device.VersionFile);
            Assert.Null(device.SettingsFolder);
            Assert.Null(device.SettingsFile);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, "firmware", "version.txt", "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.FreestylePro, "firmware", "version.txt", "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "firmware", "version.txt", "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.Tko, "firmware", "version.txt", "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.Advantage2, "active", "version.txt", "active", "state.txt")]
        [InlineData(DeviceId.SavantElite2, "active", "version.txt", "settings", "kbd_settings.txt")]
        [InlineData(DeviceId.Advantage360, "settings", "settings.txt", "settings", "settings.txt")]
        public void GetById_WithDetectableDevice_HasVersionAndSettingsPathsPerSpec03(
            DeviceId deviceId,
            string expectedVersionFolder,
            string expectedVersionFile,
            string expectedSettingsFolder,
            string expectedSettingsFile)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedVersionFolder, device.VersionFolder);
            Assert.Equal(expectedVersionFile, device.VersionFile);
            Assert.Equal(expectedSettingsFolder, device.SettingsFolder);
            Assert.Equal(expectedSettingsFile, device.SettingsFile);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, 2)]
        [InlineData(DeviceId.FreestyleEdge, 2)]
        [InlineData(DeviceId.FreestylePro, 2)]
        [InlineData(DeviceId.FreestyleEdgeRgb, 2)]
        [InlineData(DeviceId.Tko, 2)]
        [InlineData(DeviceId.Advantage360, 5)]
        [InlineData(DeviceId.SavantElite2, null)]
        [InlineData(DeviceId.CrossfireKeypad, null)]
        [InlineData(DeviceId.Advantage360Professional, null)]
        public void GetById_WithDevice_HasLayerCountPerSpec02(DeviceId deviceId, int? expectedLayerCount)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedLayerCount, device.LayerCount);
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2, true, false)]
        [InlineData(DeviceId.Advantage2, true, false)]
        [InlineData(DeviceId.FreestyleEdge, true, false)]
        [InlineData(DeviceId.FreestylePro, true, false)]
        [InlineData(DeviceId.FreestyleEdgeRgb, true, false)]
        [InlineData(DeviceId.CrossfireKeypad, false, true)]
        [InlineData(DeviceId.Tko, true, false)]
        [InlineData(DeviceId.Advantage360, true, false)]
        [InlineData(DeviceId.Advantage360Professional, false, false)]
        public void GetById_WithDevice_HasProgrammableAndFutureFlagsPerSpec02(
            DeviceId deviceId,
            bool expectedIsProgrammable,
            bool expectedIsFutureDevice)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedIsProgrammable, device.IsProgrammable);
            Assert.Equal(expectedIsFutureDevice, device.IsFutureDevice);
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2, ServingApp.SmartSetMasterOffice)]
        [InlineData(DeviceId.Advantage2, ServingApp.SmartSetMasterOffice)]
        [InlineData(DeviceId.FreestyleEdge, ServingApp.StandaloneOnly)]
        [InlineData(DeviceId.FreestylePro, ServingApp.StandaloneOnly)]
        [InlineData(DeviceId.FreestyleEdgeRgb, ServingApp.SmartSetMasterGaming)]
        [InlineData(DeviceId.CrossfireKeypad, ServingApp.None)]
        [InlineData(DeviceId.Tko, ServingApp.SmartSetMasterGaming)]
        [InlineData(DeviceId.Advantage360, ServingApp.SmartSetMasterOffice)]
        [InlineData(DeviceId.Advantage360Professional, ServingApp.SmartSetMasterOffice)]
        public void GetById_WithDevice_HasServingAppPerSpec02(DeviceId deviceId, ServingApp expectedServingApp)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedServingApp, device.ServingApp);
        }

        [Fact]
        public void Macros_ForFreestyleEdge_CarryFirmwareGatedCountBumpAsData()
        {
            var macros = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Macros;

            Assert.True(macros.IsSupported);
            Assert.Equal(24, macros.MaxMacroCount);
            Assert.Equal(100, macros.GatedMaxMacroCount);
            Assert.Equal(new FirmwareVersion(1, 0, 340), macros.MacroCountGateFirmware);
            Assert.Equal(300, macros.MaxCharactersPerMacro);
            Assert.Equal(7200, macros.MaxTotalKeystrokes);
        }

        [Fact]
        public void Macros_ForFreestylePro_MatchFreestyleEdge()
        {
            var edgeMacros = DeviceCatalog.GetById(DeviceId.FreestyleEdge).Macros;
            var proMacros = DeviceCatalog.GetById(DeviceId.FreestylePro).Macros;

            Assert.Equal(edgeMacros, proMacros);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        public void Macros_ForRgbAndTko_AllowHundredMacrosWithSharedKeystrokeBudget(DeviceId deviceId)
        {
            var macros = DeviceCatalog.GetById(deviceId).Macros;

            Assert.True(macros.IsSupported);
            Assert.Equal(100, macros.MaxMacroCount);
            Assert.Equal(7200, macros.MaxTotalKeystrokes);
            Assert.Null(macros.GatedMaxMacroCount);
        }

        [Fact]
        public void Macros_ForAdvantage360_AllowHundredMacrosWithFiveHundredCharacters()
        {
            var macros = DeviceCatalog.GetById(DeviceId.Advantage360).Macros;

            Assert.True(macros.IsSupported);
            Assert.Equal(100, macros.MaxMacroCount);
            Assert.Equal(500, macros.MaxCharactersPerMacro);
            Assert.Equal(7200, macros.MaxTotalKeystrokes);
        }

        [Fact]
        public void Macros_ForAdvantage2_UsePerTriggerKeyModel()
        {
            var macros = DeviceCatalog.GetById(DeviceId.Advantage2).Macros;

            Assert.True(macros.IsSupported);
            Assert.Equal(3, macros.MacrosPerTriggerKey);
            Assert.Equal(3, macros.MaxCoTriggersPerMacro);
            Assert.Equal(300, macros.MaxCharactersPerMacro);
            Assert.Null(macros.MaxMacroCount);
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2, true)]
        [InlineData(DeviceId.CrossfireKeypad, false)]
        [InlineData(DeviceId.Advantage360Professional, false)]
        public void Macros_ForPedalAndNonProgrammableDevices_ReportSupportFlagOnly(DeviceId deviceId, bool expectedIsSupported)
        {
            var macros = DeviceCatalog.GetById(deviceId).Macros;

            Assert.Equal(expectedIsSupported, macros.IsSupported);
            Assert.Null(macros.MaxMacroCount);
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2, LightingKind.None)]
        [InlineData(DeviceId.Advantage2, LightingKind.None)]
        [InlineData(DeviceId.FreestyleEdge, LightingKind.BlueBacklight)]
        [InlineData(DeviceId.FreestylePro, LightingKind.None)]
        [InlineData(DeviceId.FreestyleEdgeRgb, LightingKind.PerKeyRgb)]
        [InlineData(DeviceId.CrossfireKeypad, LightingKind.None)]
        [InlineData(DeviceId.Tko, LightingKind.PerKeyRgb)]
        [InlineData(DeviceId.Advantage360, LightingKind.IndicatorLeds)]
        [InlineData(DeviceId.Advantage360Professional, LightingKind.None)]
        public void Lighting_ForEveryDevice_HasKindPerSpec02(DeviceId deviceId, LightingKind expectedKind)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedKind, device.Lighting.Kind);
        }

        [Fact]
        public void Lighting_ForTko_HasThirtyThreeEdgeLedsSplitNineFifteenNine()
        {
            var lighting = DeviceCatalog.GetById(DeviceId.Tko).Lighting;

            Assert.True(lighting.HasEdgeLighting);
            Assert.Equal(9, lighting.EdgeLedLeftCount);
            Assert.Equal(15, lighting.EdgeLedBottomCount);
            Assert.Equal(9, lighting.EdgeLedRightCount);
            Assert.Equal(33, lighting.EdgeLedTotalCount);
        }

        [Fact]
        public void Lighting_ForFreestyleEdgeRgb_HasNoEdgeLighting()
        {
            var lighting = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb).Lighting;

            Assert.False(lighting.HasEdgeLighting);
            Assert.Equal(0, lighting.EdgeLedTotalCount);
        }

        [Fact]
        public void Lighting_ForAdvantage360_HasSixIndicatorLeds()
        {
            var lighting = DeviceCatalog.GetById(DeviceId.Advantage360).Lighting;

            Assert.Equal(6, lighting.IndicatorLedCount);
            Assert.False(lighting.HasEdgeLighting);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "Program + F1")]
        [InlineData(DeviceId.FreestyleEdge, "SmartSet + F8")]
        [InlineData(DeviceId.FreestylePro, "SmartSet + F8")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "SmartSet + F8")]
        [InlineData(DeviceId.Tko, "SmartSet + Right Shift + V")]
        [InlineData(DeviceId.Advantage360, "SmartSet + v-Drive")]
        [InlineData(DeviceId.SavantElite2, null)]
        [InlineData(DeviceId.CrossfireKeypad, null)]
        [InlineData(DeviceId.Advantage360Professional, null)]
        public void VDriveShortcutHint_ForEveryDevice_MatchesSpec03Section1(DeviceId deviceId, string? expectedHint)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedHint, device.VDriveShortcutHint);
        }

        [Theory]
        [InlineData(DeviceId.SavantElite2, LayoutSchemeKind.PedalFile)]
        [InlineData(DeviceId.Advantage2, LayoutSchemeKind.QwertyDvorakPositions)]
        [InlineData(DeviceId.FreestyleEdge, LayoutSchemeKind.NumberedProfiles)]
        [InlineData(DeviceId.FreestylePro, LayoutSchemeKind.NumberedProfiles)]
        [InlineData(DeviceId.FreestyleEdgeRgb, LayoutSchemeKind.NumberedProfiles)]
        [InlineData(DeviceId.CrossfireKeypad, LayoutSchemeKind.None)]
        [InlineData(DeviceId.Tko, LayoutSchemeKind.NumberedProfiles)]
        [InlineData(DeviceId.Advantage360, LayoutSchemeKind.NumberedProfiles)]
        [InlineData(DeviceId.Advantage360Professional, LayoutSchemeKind.None)]
        public void LayoutScheme_ForEveryDevice_HasKindPerSpec(DeviceId deviceId, LayoutSchemeKind expectedKind)
        {
            var device = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expectedKind, device.LayoutScheme.Kind);
        }

        [Fact]
        public void LayoutScheme_ForFreestyleEdge_UsesNumberedLayoutAndLightingFoldersOneThroughNine()
        {
            var scheme = DeviceCatalog.GetById(DeviceId.FreestyleEdge).LayoutScheme;

            Assert.Equal("layouts", scheme.LayoutFolder);
            Assert.Equal("lighting", scheme.LightingFolder);
            Assert.Equal(1, scheme.FirstProfileNumber);
            Assert.Equal(9, scheme.LastProfileNumber);
            Assert.False(scheme.HasReadOnlyFactoryProfile);
        }

        [Fact]
        public void LayoutScheme_ForFreestylePro_HasNoLightingFolder()
        {
            var scheme = DeviceCatalog.GetById(DeviceId.FreestylePro).LayoutScheme;

            Assert.Equal("layouts", scheme.LayoutFolder);
            Assert.Null(scheme.LightingFolder);
        }

        [Fact]
        public void LayoutScheme_ForAdvantage360_MarksProfileZeroAsReadOnlyFactoryProfile()
        {
            var scheme = DeviceCatalog.GetById(DeviceId.Advantage360).LayoutScheme;

            Assert.True(scheme.HasReadOnlyFactoryProfile);
            Assert.Equal(1, scheme.FirstProfileNumber);
            Assert.Equal(9, scheme.LastProfileNumber);
        }

        [Fact]
        public void LayoutScheme_ForAdvantage2_UsesActiveFolder()
        {
            var scheme = DeviceCatalog.GetById(DeviceId.Advantage2).LayoutScheme;

            Assert.Equal("active", scheme.LayoutFolder);
            Assert.Null(scheme.LightingFolder);
        }

        [Fact]
        public void LayoutScheme_ForSavantElite2_UsesFixedPedalsFileInActiveFolder()
        {
            var scheme = DeviceCatalog.GetById(DeviceId.SavantElite2).LayoutScheme;

            Assert.Equal("active", scheme.LayoutFolder);
            Assert.Equal("pedals.txt", scheme.FixedLayoutFileName);
        }

        [Fact]
        public void Urls_ForAdvantage360Professional_PointToZmkGuiAndSupportPages()
        {
            var device = DeviceCatalog.GetById(DeviceId.Advantage360Professional);

            Assert.Equal("https://kinesiscorporation.github.io/Adv360-Pro-GUI/", device.ConfigurationUrl);
            Assert.Equal("https://kinesis-ergo.com/support/advantage360-pro", device.SupportUrl);
        }

        [Fact]
        public void Urls_ForSmartSetProgrammableDevices_AreNotSet()
        {
            var advantage360 = DeviceCatalog.GetById(DeviceId.Advantage360);

            Assert.Null(advantage360.ConfigurationUrl);
            Assert.Null(advantage360.SupportUrl);
        }
    }
}
