using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The store over a real <c>app_settings.txt</c>: it reads and writes exclusively through
    /// Core's settings engine, so these tests run the real
    /// <see cref="SettingsService"/>/<see cref="VDriveFileService"/> pair over a temp directory
    /// rather than a fake — the point of the seam is that there is only one merge implementation.
    /// </summary>
    public sealed class VDriveNotificationSuppressionStoreTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _filePath;
        private readonly VDriveLocation _location;
        private readonly VDriveFileService _fileService = new();
        private readonly ISettingsService _settings;

        public VDriveNotificationSuppressionStoreTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);

            _location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb) with { RootPath = _tempDirectory };
            _filePath = VDriveNotificationSuppressionStore.GetFilePath(_location);
            _settings = TestDevices.CreateSettingsService(_fileService);

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        [Theory]
        [InlineData("on", true)]
        [InlineData("ON", true)]
        [InlineData("off", false)]
        [InlineData("", false)]
        public void IsHidden_WithStoredValue_FollowsTheOnMeansHideRule(string value, bool expected)
        {
            CreateFile(NotificationKeys.Save + "=" + value);
            var store = CreateStore();

            Assert.Equal(expected, store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void IsHidden_WithMissingKey_ReturnsFalse()
        {
            CreateFile("saveas_msg=on", "cust_color_1=[255][0][128]");
            var store = CreateStore();

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void IsHidden_WithMissingFile_ReturnsFalse()
        {
            var store = CreateStore();

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void IsHidden_WithDifferentlyCasedKey_MatchesCaseInsensitively()
        {
            CreateFile("SAVE_MSG=on");
            var store = CreateStore();

            Assert.True(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void IsHidden_WithPrefixCollidingKey_DoesNotMatchTheLongerKey()
        {
            CreateFile("save_msg_extra=on");
            var store = CreateStore();

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void IsHidden_ForEveryNotificationKey_RoundTripsThroughTheSettingsModel()
        {
            // Every key of specs/08-settings.md §3 must map onto an AppSettings flag: a key this
            // store cannot address would silently read as "show" forever.
            CreateFile();
            var store = CreateStore();

            foreach (var key in NotificationKeys.All)
            {
                store.SetHidden(key, true);

                Assert.True(store.IsHidden(key));
            }

            Assert.All(NotificationKeys.All, key => Assert.True(store.IsHidden(key)));
        }

        [Fact]
        public void SetHidden_WithExistingFile_PreservesUnknownLines()
        {
            CreateFile("cust_color_1=[255][0][128]", "save_msg=off", "some unparseable garbage");
            var store = CreateStore();

            store.SetHidden(NotificationKeys.Save, true);

            Assert.Equal(
                new[] { "cust_color_1=[255][0][128]", "save_msg=on", "some unparseable garbage" },
                _fileService.ReadAllLines(_filePath));
        }

        [Fact]
        public void SetHidden_WithAbsentKey_AppendsIt()
        {
            CreateFile("cust_color_1=[255][0][128]");
            var store = CreateStore();

            store.SetHidden(NotificationKeys.Multiplay, true);

            Assert.Equal(
                new[] { "cust_color_1=[255][0][128]", "multiplay_msg=on" },
                _fileService.ReadAllLines(_filePath));
        }

        [Fact]
        public void SetHidden_WithACustomColorOnTheDrive_LeavesTheColorSlotIntact()
        {
            // The lighting pickers persist the twelve cust_color_N slots through the same seam;
            // a line-based store here would rewrite the file without them.
            CreateFile("cust_color_12=[1][2][3]");
            var store = CreateStore();

            store.SetHidden(NotificationKeys.SaveLighting, true);

            Assert.Equal(
                new[] { "cust_color_12=[1][2][3]", "savelighting_msg=on" },
                _fileService.ReadAllLines(_filePath));
            Assert.Equal("[1][2][3]", _settings.LoadAppSettings(_location).CustomColors[11]!.ToString());
        }

        [Fact]
        public void SetHidden_WithMissingFile_CreatesIt()
        {
            var store = CreateStore();

            store.SetHidden(NotificationKeys.AppIntro, true);

            Assert.True(File.Exists(_filePath));
            Assert.Equal(new[] { "app_intro_msg=on" }, _fileService.ReadAllLines(_filePath));
            Assert.True(store.IsHidden(NotificationKeys.AppIntro));
        }

        [Fact]
        public void SetHidden_WithFalse_WritesOff()
        {
            CreateFile("speed_msg=on");
            var store = CreateStore();

            store.SetHidden(NotificationKeys.Speed, false);

            Assert.Equal(new[] { "speed_msg=off" }, _fileService.ReadAllLines(_filePath));
            Assert.False(store.IsHidden(NotificationKeys.Speed));
        }

        [Fact]
        public void SetHidden_WithAKeyOutsideTheSpecTable_WritesNothing()
        {
            CreateFile("save_msg=off");
            var store = CreateStore();

            store.SetHidden("not_a_spec_msg", true);

            Assert.Equal(new[] { "save_msg=off" }, _fileService.ReadAllLines(_filePath));
            Assert.False(store.IsHidden("not_a_spec_msg"));
        }

        [Fact]
        public void SetHidden_WithUnwritableLocation_DoesNotThrow()
        {
            // A merely missing folder is not unwritable any more — WriteAllLines(allowCreate: true)
            // creates it. So block the settings folder with a file of the same name: creating the
            // directory then fails, which is the I/O failure the store must swallow.
            var blockedRoot = Path.Combine(_tempDirectory, "blocked-root");
            Directory.CreateDirectory(blockedRoot);
            File.WriteAllText(Path.Combine(blockedRoot, "settings"), string.Empty);

            var store = new VDriveNotificationSuppressionStore(
                _settings,
                _location with { RootPath = blockedRoot });

            store.SetHidden(NotificationKeys.Save, true);

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void GetFilePath_ForDrive_IsTheSettingsEnginesOwnPath()
        {
            // One path, resolved by Core: the Advantage2's keyboard settings live in active/ while
            // app_settings.txt always lives in settings/ (spec 08 §3), so a second answer here
            // would put the two writers on different files.
            var location = TestDevices.CreateLocation(DeviceId.Advantage2);

            var path = VDriveNotificationSuppressionStore.GetFilePath(location);

            Assert.Equal(SettingsService.GetAppSettingsFilePath(location), path);
            Assert.Equal(Path.Combine(location.RootPath, "settings", "app_settings.txt"), path);
        }

        [Fact]
        public void Constructor_WithoutACollaborator_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new VDriveNotificationSuppressionStore(null!, _location));
            Assert.Throws<ArgumentNullException>(() => new VDriveNotificationSuppressionStore(_settings, null!));
        }

        private VDriveNotificationSuppressionStore CreateStore()
        {
            return new VDriveNotificationSuppressionStore(_settings, _location);
        }

        private void CreateFile(params string[] lines)
        {
            File.WriteAllLines(_filePath, lines);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }
}
