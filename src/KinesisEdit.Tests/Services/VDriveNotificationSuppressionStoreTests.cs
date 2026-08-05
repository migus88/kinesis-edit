using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public sealed class VDriveNotificationSuppressionStoreTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _filePath;
        private readonly VDriveFileService _fileService = new();

        public VDriveNotificationSuppressionStoreTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
            _filePath = Path.Combine(_tempDirectory, VDriveNotificationSuppressionStore.FileName);
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
        public void SetHidden_WithUnwritableLocation_DoesNotThrow()
        {
            // A merely missing folder is not unwritable any more — WriteAllLines(allowCreate: true)
            // creates it. So block the settings folder with a file of the same name: creating the
            // directory then fails, which is the I/O failure the store must swallow.
            var blockedFolder = Path.Combine(_tempDirectory, "blocked-folder");
            File.WriteAllText(blockedFolder, string.Empty);

            var store = new VDriveNotificationSuppressionStore(
                _fileService,
                Path.Combine(blockedFolder, VDriveNotificationSuppressionStore.FileName));

            store.SetHidden(NotificationKeys.Save, true);

            Assert.False(store.IsHidden(NotificationKeys.Save));
        }

        [Fact]
        public void GetFilePath_ForDrive_PointsAtTheSettingsFolder()
        {
            var location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb);

            var path = VDriveNotificationSuppressionStore.GetFilePath(location);

            Assert.Equal(Path.Combine(location.SettingsFolderPath, "app_settings.txt"), path);
        }

        private VDriveNotificationSuppressionStore CreateStore()
        {
            return new VDriveNotificationSuppressionStore(_fileService, _filePath);
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
