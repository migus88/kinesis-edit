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
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb);

            _service.SaveAppSettings(location, AppSettings.Empty);

            Assert.False(File.Exists(SettingsService.GetAppSettingsFilePath(location)));
        }

        [Fact]
        public void GetAppSettingsFilePath_ForAdvantage2_StillPointsToSettingsFolder()
        {
            var location = CreateLocation(DeviceId.Advantage2);

            var path = SettingsService.GetAppSettingsFilePath(location);

            Assert.Equal(Path.Combine(location.RootPath, "settings", "app_settings.txt"), path);
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
