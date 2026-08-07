using System.Text;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.Settings
{
    public sealed class SettingsServiceTests : IDisposable
    {
        private static readonly VersionFileInfo _fourMegabyteInfo = new()
        {
            HasFourMegabyteMarker = true,
        };

        private readonly string _tempDirectory;
        private readonly SettingsService _service = new(new VDriveFileService());

        public SettingsServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void SaveKeyboardSettings_AfterLoadAndModify_PreservesUnknownAndReservedLinesByteForByte()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            WriteFile(
                location.SettingsFilePath,
                "power_user=true",
                "thumb_mode=café",
                "program_key_lock=OFF",
                "",
                "some unparseable garbage ±",
                "game_mode=OFF",
                "macro_speed=5",
                "v_drive_open_on_startup=ON");

            var settings = _service.LoadKeyboardSettings(location);
            _service.SaveKeyboardSettings(location, VersionFileInfo.Empty, settings with { MacroSpeed = 9 });

            var expectedBytes = BuildFileBytes(
                "power_user=true",
                "thumb_mode=café",
                "program_key_lock=OFF",
                "",
                "some unparseable garbage ±",
                "game_mode=OFF",
                "macro_speed=9",
                "v_drive_open_on_startup=ON");
            Assert.Equal(expectedBytes, File.ReadAllBytes(location.SettingsFilePath));
        }

        [Fact]
        public void SaveKeyboardSettings_WithOutOfRangeValueOnDevice_LeavesThatLineVerbatimInsteadOfThrowing()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            WriteFile(
                location.SettingsFilePath,
                "status_play_speed=9",
                "macro_speed=5");

            var settings = _service.LoadKeyboardSettings(location);
            _service.SaveKeyboardSettings(location, VersionFileInfo.Empty, settings with { MacroSpeed = 7 });

            var expectedBytes = BuildFileBytes(
                "status_play_speed=9",
                "macro_speed=7");
            Assert.Equal(expectedBytes, File.ReadAllBytes(location.SettingsFilePath));
        }

        [Fact]
        public void LoadKeyboardSettings_WithGen1SettingsFile_ParsesTypedModel()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            WriteFile(
                location.SettingsFilePath,
                "startup_file=layout4.txt",
                "led_mode=led4.txt",
                "macro_speed=5",
                "v_drive=manual");

            var settings = _service.LoadKeyboardSettings(location);

            Assert.Equal(4, settings.StartupProfileNumber);
            Assert.Equal("led4.txt", settings.LedMode);
            Assert.Equal(5, settings.MacroSpeed);
            Assert.False(settings.IsVDriveAutoMountEnabled);
        }

        [Fact]
        public void LoadKeyboardSettings_WithMissingFile_ThrowsFileNotFoundException()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);

            Assert.Throws<FileNotFoundException>(() => _service.LoadKeyboardSettings(location));
        }

        [Fact]
        public void SaveKeyboardSettings_ForAdvantage2WithoutFourMegabyteMarker_RefusesAndLeavesFileUntouched()
        {
            var location = CreateLocation(DeviceId.Advantage2);
            WriteFile(location.SettingsFilePath, "macro_speed=5");
            var originalBytes = File.ReadAllBytes(location.SettingsFilePath);
            var settings = new KeyboardSettings
            {
                MacroSpeed = 9,
            };

            Assert.Throws<InvalidOperationException>(
                () => _service.SaveKeyboardSettings(location, VersionFileInfo.Empty, settings));
            Assert.Equal(originalBytes, File.ReadAllBytes(location.SettingsFilePath));
        }

        [Fact]
        public void SaveKeyboardSettings_ForAdvantage2WithFourMegabyteMarker_WritesStateFile()
        {
            var location = CreateLocation(DeviceId.Advantage2);
            WriteFile(location.SettingsFilePath, "macro_speed=5");
            var settings = new KeyboardSettings
            {
                MacroSpeed = 9,
                IsKeyClickToneEnabled = true,
                IsVDriveOpenOnStartupEnabled = false,
            };

            _service.SaveKeyboardSettings(location, _fourMegabyteInfo, settings);

            var lines = File.ReadAllLines(location.SettingsFilePath);
            Assert.Equal(
                new[] { "macro_speed=9", "v_drive_open_on_startup=off", "key_click_tone=ON" },
                lines);
        }

        [Fact]
        public void SaveKeyboardSettings_ForOtherDeviceWithoutMarker_IsNotGated()
        {
            var location = CreateLocation(DeviceId.Advantage360);
            WriteFile(location.SettingsFilePath, "model=Adv360", "profile=1");
            var settings = new KeyboardSettings
            {
                StartupProfileNumber = 9,
            };

            _service.SaveKeyboardSettings(location, VersionFileInfo.Empty, settings);

            var lines = File.ReadAllLines(location.SettingsFilePath);
            Assert.Equal(new[] { "model=Adv360", "profile=9" }, lines);
        }

        [Fact]
        public void SaveKeyboardSettings_ForAdvantage2_NeverTouchesVDriveLineOfOtherDialect()
        {
            var location = CreateLocation(DeviceId.Advantage2);
            WriteFile(location.SettingsFilePath, "v_drive=auto");
            var settings = new KeyboardSettings
            {
                IsVDriveAutoMountEnabled = false,
                IsVDriveOpenOnStartupEnabled = false,
            };

            _service.SaveKeyboardSettings(location, _fourMegabyteInfo, settings);

            var lines = File.ReadAllLines(location.SettingsFilePath);
            Assert.Equal(new[] { "v_drive=auto", "v_drive_open_on_startup=off" }, lines);
        }

        [Fact]
        public void SaveKeyboardSettings_ForGen1Device_NeverTouchesVDriveOpenOnStartupLine()
        {
            var location = CreateLocation(DeviceId.Tko);
            WriteFile(location.SettingsFilePath, "v_drive_open_on_startup=ON");
            var settings = new KeyboardSettings
            {
                IsVDriveAutoMountEnabled = false,
                IsVDriveOpenOnStartupEnabled = true,
            };

            _service.SaveKeyboardSettings(location, VersionFileInfo.Empty, settings);

            var lines = File.ReadAllLines(location.SettingsFilePath);
            Assert.Equal(new[] { "v_drive_open_on_startup=ON", "v_drive=manual" }, lines);
        }

        [Fact]
        public void SaveKeyboardSettings_ForDeviceWithoutSettingsSupport_WritesNothing()
        {
            var location = CreateLocation(DeviceId.SavantElite2);
            var settings = new KeyboardSettings
            {
                MacroSpeed = 5,
                IsGameModeEnabled = true,
            };

            _service.SaveKeyboardSettings(location, VersionFileInfo.Empty, settings);

            Assert.False(File.Exists(location.SettingsFilePath));
        }

        [Fact]
        public void LoadAppSettings_WithMissingFile_ReturnsEmptySettings()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);

            var settings = _service.LoadAppSettings(location);

            Assert.Equal(AppSettings.Empty, settings);
        }

        [Fact]
        public void SaveAppSettings_WithMissingFile_CreatesItInSettingsFolder()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var colors = new SettingsColor?[AppSettings.CustomColorCount];
            colors[0] = new SettingsColor(255, 0, 128);
            var settings = new AppSettings
            {
                IsSaveMessageHidden = true,
                CustomColors = colors,
            };

            _service.SaveAppSettings(location, settings);

            var path = SettingsService.GetAppSettingsFilePath(location);
            Assert.Equal(Path.Combine(location.RootPath, "settings", "app_settings.txt"), path);
            Assert.Equal(new[] { "save_msg=on", "cust_color_1=[255][0][128]" }, File.ReadAllLines(path));
        }

        [Fact]
        public void SaveAppSettings_WithExistingFile_PreservesUnknownLinesAndCollidingColorKeys()
        {
            var location = CreateLocation(DeviceId.Tko);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(
                path,
                "future_key=whatever",
                "cust_color_10=[100][100][100]");

            var loaded = _service.LoadAppSettings(location);
            var colors = new SettingsColor?[AppSettings.CustomColorCount];

            for (var index = 0; index < colors.Length; index++)
            {
                colors[index] = loaded.CustomColors[index];
            }

            colors[0] = new SettingsColor(10, 10, 10);
            _service.SaveAppSettings(location, loaded with { CustomColors = colors });

            var lines = File.ReadAllLines(path);
            Assert.Equal(
                new[]
                {
                    "future_key=whatever",
                    "cust_color_10=[100][100][100]",
                    "cust_color_1=[10][10][10]",
                },
                lines);
        }

        [Fact]
        public void SaveAppSettings_WithMissingSettingsFolder_CreatesFolderAndFile()
        {
            var location = CreateLocation(DeviceId.Advantage2, createAppSettingsFolder: false);
            var settings = new AppSettings
            {
                IsSaveMessageHidden = true,
            };

            _service.SaveAppSettings(location, settings);

            var path = SettingsService.GetAppSettingsFilePath(location);
            Assert.Equal(new[] { "save_msg=on" }, File.ReadAllLines(path));
        }

        [Fact]
        public void SaveAppSettings_WithNothingSet_DoesNotCreateFile()
        {
            // An empty model names twelve colour keys for removal, but there is nothing to remove
            // from: a removal against a missing file is already satisfied, and only pairs may
            // conjure app_settings.txt onto a drive that never had one.
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);

            _service.SaveAppSettings(location, AppSettings.Empty);

            Assert.False(File.Exists(SettingsService.GetAppSettingsFilePath(location)));
        }

        [Fact]
        public void SaveAppSettings_WithAClearedColorSlot_TakesTheLineOffTheDrive()
        {
            // The clear-does-not-survive-a-restart bug of issue #95: the serializer never writes
            // `cust_color_1=` (spec 08 §3), so a merge alone left the old line in place and the
            // swatch came back on the next load.
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(path, "cust_color_1=[255][0][128]", "save_msg=on");

            var loaded = _service.LoadAppSettings(location);

            _service.SaveAppSettings(location, loaded.WithCustomColor(1, null));

            var lines = File.ReadAllLines(path);
            Assert.Equal(new[] { "save_msg=on" }, lines);
        }

        [Fact]
        public void SaveAppSettings_ClearingSlotOne_LeavesSlotTenAlone()
        {
            // cust_color_1 is a prefix of cust_color_10: a StartsWith removal would delete both.
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(
                path,
                "cust_color_1=[255][0][128]",
                "cust_color_10=[100][100][100]",
                "cust_color_12=[9][9][9]");

            var loaded = _service.LoadAppSettings(location);

            _service.SaveAppSettings(location, loaded.WithCustomColor(1, null));

            var lines = File.ReadAllLines(path);
            Assert.Equal(
                new[]
                {
                    "cust_color_10=[100][100][100]",
                    "cust_color_12=[9][9][9]",
                },
                lines);
        }

        [Fact]
        public void SaveAppSettings_WithNothingToWriteButSomethingToRemove_StillReachesTheDrive()
        {
            // "Nothing to write" and "nothing to remove" are different questions: a save that only
            // clears slots produces no pairs at all and must not be short-circuited.
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(path, "future_key=whatever", "cust_color_6=[1][2][3]");

            _service.SaveAppSettings(location, AppSettings.Empty);

            var lines = File.ReadAllLines(path);
            Assert.Equal(new[] { "future_key=whatever" }, lines);
        }

        [Fact]
        public void SaveAppSettings_WithAClearedColorSlot_StillPreservesForeignLinesByteForByte()
        {
            // Invariant 2 is qualified by the removal escape hatch, not weakened: only the keys
            // named for deletion go, everything the legacy app wrote survives verbatim.
            var location = CreateLocation(DeviceId.Tko);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(
                path,
                "power_user=true",
                "thumb_mode=café",
                "",
                "some unparseable garbage ±",
                "cust_color_2=[5][5][5]",
                "future_key=whatever");

            var loaded = _service.LoadAppSettings(location);

            _service.SaveAppSettings(location, loaded.WithCustomColor(2, null));

            var expectedBytes = BuildFileBytes(
                "power_user=true",
                "thumb_mode=café",
                "",
                "some unparseable garbage ±",
                "future_key=whatever");
            Assert.Equal(expectedBytes, File.ReadAllBytes(path));
        }

        [Fact]
        public void SaveAppSettings_NeverWritesAnEmptyColorKey()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(path, "cust_color_1=[255][0][128]");

            _service.SaveAppSettings(location, AppSettings.Empty with { IsSaveMessageHidden = true });

            var lines = File.ReadAllLines(path);
            Assert.DoesNotContain(lines, line => line.EndsWith('='));
        }

        [Fact]
        public void GetAppSettingsFilePath_ForAdvantage2_StillPointsToSettingsFolder()
        {
            var location = CreateLocation(DeviceId.Advantage2);

            var path = SettingsService.GetAppSettingsFilePath(location);

            Assert.Equal(Path.Combine(location.RootPath, "settings", "app_settings.txt"), path);
        }

        [Fact]
        public void SaveMacroNames_WritesTheProfilesNamesAndLeavesEveryOtherLineAlone()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(path, "save_msg=on", "future_key=whatever");

            var settings = _service.LoadAppSettings(location).WithMacroNamesForProfile(
                1,
                [KeyValuePair.Create(new MacroNameKey(1, 0, 65, 1), "Sign-off")]);

            _service.SaveMacroNames(location, settings, 1);

            Assert.Equal(
                new[] { "save_msg=on", "future_key=whatever", "macro_name_1_0_65_1=Sign-off" },
                File.ReadAllLines(path));
        }

        [Fact]
        public void SaveMacroNames_RemovesOnlyTheSavedProfilesStaleKeys()
        {
            // One app_settings.txt holds every profile's macro names, so a removal that was not
            // scoped to the profile being written would delete another profile's work.
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(
                path,
                "macro_name_1_0_65_1=Sign-off",
                "macro_name_1_0_66_1=Deleted",
                "macro_name_2_0_65_1=Profile two",
                "cust_color_1=[255][0][128]");

            var settings = _service.LoadAppSettings(location).WithMacroNamesForProfile(
                1,
                [KeyValuePair.Create(new MacroNameKey(1, 0, 65, 1), "Renamed")]);

            _service.SaveMacroNames(location, settings, 1);

            Assert.Equal(
                new[]
                {
                    "macro_name_1_0_65_1=Renamed",
                    "macro_name_2_0_65_1=Profile two",
                    "cust_color_1=[255][0][128]",
                },
                File.ReadAllLines(path));
        }

        [Fact]
        public void SaveMacroNames_WithNothingToWriteButSomethingToRemove_StillReachesTheDrive()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(path, "macro_name_1_0_65_1=Sign-off", "future_key=whatever");

            var settings = _service.LoadAppSettings(location).WithMacroNamesForProfile(1, []);

            _service.SaveMacroNames(location, settings, 1);

            Assert.Equal(new[] { "future_key=whatever" }, File.ReadAllLines(path));
        }

        [Fact]
        public void SaveMacroNames_WithNothingToSay_DoesNotCreateTheFile()
        {
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);

            _service.SaveMacroNames(location, AppSettings.Empty, 1);

            Assert.False(File.Exists(SettingsService.GetAppSettingsFilePath(location)));
        }

        [Fact]
        public void SaveAppSettings_NeverWritesOrRemovesAMacroName()
        {
            // The preference/swatch path has no profile to scope a removal to, so it must leave the
            // macro-name family entirely alone.
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);
            var path = SettingsService.GetAppSettingsFilePath(location);
            WriteFile(path, "macro_name_1_0_65_1=Sign-off");

            var settings = _service.LoadAppSettings(location)
                .WithMacroName(new MacroNameKey(1, 0, 66, 1), "Never written");

            _service.SaveAppSettings(location, settings with { IsSaveMessageHidden = true });

            Assert.Equal(new[] { "macro_name_1_0_65_1=Sign-off", "save_msg=on" }, File.ReadAllLines(path));
        }

        private VDriveLocation CreateLocation(DeviceId deviceId, bool createAppSettingsFolder = true)
        {
            var device = DeviceCatalog.GetById(deviceId);
            var rootPath = Path.Combine(_tempDirectory, deviceId.ToString());

            Directory.CreateDirectory(rootPath);
            Directory.CreateDirectory(Path.Combine(rootPath, device.SettingsFolder!));

            if (createAppSettingsFolder)
            {
                Directory.CreateDirectory(Path.Combine(rootPath, "settings"));
            }

            return new VDriveLocation
            {
                Device = device,
                RootPath = rootPath,
                IsWritable = true,
            };
        }

        private static void WriteFile(string path, params string[] lines)
        {
            File.WriteAllBytes(path, BuildFileBytes(lines));
        }

        private static byte[] BuildFileBytes(params string[] lines)
        {
            var content = new StringBuilder();

            foreach (var line in lines)
            {
                content.Append(line);
                content.Append(Environment.NewLine);
            }

            return Encoding.Latin1.GetBytes(content.ToString());
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
