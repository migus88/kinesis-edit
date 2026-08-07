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
    /// The store is also the session's single in-memory copy of that file, so the tests below
    /// cover both halves: the on-disk round trip, and the shared state every consumer reads.
    /// </summary>
    public sealed class VDriveAppPreferencesStoreTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _filePath;
        private readonly VDriveLocation _location;
        private readonly VDriveFileService _fileService = new();
        private readonly ISettingsService _settings;

        public VDriveAppPreferencesStoreTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);

            _location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb) with { RootPath = _tempDirectory };
            _filePath = VDriveAppPreferencesStore.GetFilePath(_location);
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
            Assert.Same(AppSettings.Empty, store.Current);
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
            // Every suppression key must map onto an AppSettings flag: a key this store cannot
            // address would silently read as "show" forever.
            CreateFile();
            var store = CreateStore();

            foreach (var key in NotificationKeys.All)
            {
                store.SetHidden(key, true);

                Assert.True(store.IsHidden(key));
            }

            Assert.All(NotificationKeys.All, key => Assert.True(store.IsHidden(key)));
            Assert.All(
                NotificationKeys.All,
                key => Assert.Contains(key + "=on", _fileService.ReadAllLines(_filePath)));
        }

        [Fact]
        public void IsHidden_ForADisplayPreference_IsFalseEvenWhenTheKeyIsOn()
        {
            // advisory_detail stores "on" to mean EXPAND, not HIDE. Reading it as a hide flag
            // would let a dialog suppress itself off an unrelated display preference.
            CreateFile(SettingsKeys.AdvisoryDetail + "=on");
            var store = CreateStore();

            Assert.False(store.IsHidden(SettingsKeys.AdvisoryDetail));
            Assert.True(store.Current.IsAdvisoryDetailExpanded);
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
        public void SetHidden_WithAKeyOutsideTheCatalog_WritesNothing()
        {
            // The rule used to be "outside the twelve of spec 08 §3". It is now "outside
            // AppPreferenceCatalog", because this app adds five keys of its own — but the point is
            // unchanged: a key with no descriptor has no AppSettings property to carry it, so
            // writing it would drop it on the next save. Nothing is written at all instead.
            CreateFile("save_msg=off");
            var store = CreateStore();

            store.SetHidden("not_a_spec_msg", true);

            Assert.Null(AppPreferenceCatalog.Find("not_a_spec_msg"));
            Assert.Equal(new[] { "save_msg=off" }, _fileService.ReadAllLines(_filePath));
            Assert.False(store.IsHidden("not_a_spec_msg"));
        }

        [Fact]
        public void SetHidden_WithADisplayPreferenceKey_WritesNothing()
        {
            // advisory_detail is in the catalog but is not a suppression flag, so SetHidden — the
            // "Don't ask this again" route — must refuse it as firmly as an unknown key.
            CreateFile("save_msg=off");
            var store = CreateStore();

            store.SetHidden(SettingsKeys.AdvisoryDetail, true);

            Assert.Equal(new[] { "save_msg=off" }, _fileService.ReadAllLines(_filePath));
            Assert.Null(store.Current.IsAdvisoryDetailExpanded);
        }

        [Fact]
        public void SetHidden_ForANewKeyOfThisApp_WritesItAlongsideTheSpecKeys()
        {
            CreateFile("save_msg=off");
            var store = CreateStore();

            store.SetHidden(NotificationKeys.ResetLayer, true);

            Assert.Equal(
                new[] { "save_msg=off", "reset_layer_msg=on" },
                _fileService.ReadAllLines(_filePath));
            Assert.True(store.IsHidden(NotificationKeys.ResetLayer));
        }

        [Fact]
        public void SetHidden_WithUnwritableLocation_DoesNotThrowAndKeepsTheAnswerForTheSession()
        {
            // A merely missing folder is not unwritable any more — WriteAllLines(allowCreate: true)
            // creates it. So block the settings folder with a file of the same name: creating the
            // directory then fails, which is the I/O failure the store must swallow.
            //
            // The answer still stands for this session. The user ticked "don't ask again" and the
            // box must stop appearing; that the drive refused the write is not a reason to keep
            // asking. Nothing reaches the disk, so the next session asks again.
            var blockedRoot = Path.Combine(_tempDirectory, "blocked-root");
            Directory.CreateDirectory(blockedRoot);
            File.WriteAllText(Path.Combine(blockedRoot, "settings"), string.Empty);

            var store = new VDriveAppPreferencesStore(
                _settings,
                _location with { RootPath = blockedRoot });

            store.SetHidden(NotificationKeys.Save, true);

            Assert.True(store.IsHidden(NotificationKeys.Save));
            Assert.False(File.Exists(Path.Combine(blockedRoot, "settings", "app_settings.txt")));
        }

        [Fact]
        public void Current_WithAStoredFile_ReadsItAndIsWritable()
        {
            CreateFile("advisory_detail=on", "cust_color_2=[9][8][7]");
            var store = CreateStore();

            Assert.True(store.IsWritable);
            Assert.True(store.Current.IsAdvisoryDetailExpanded);
            Assert.Equal("[9][8][7]", store.Current.CustomColors[1]!.ToString());
        }

        [Fact]
        public void Update_WhenItChangesSomething_PersistsItAndRaisesChanged()
        {
            CreateFile("save_msg=off");
            var store = CreateStore();
            var changes = 0;
            store.Changed += () => changes++;

            store.Update(settings => settings with { IsAdvisoryDetailExpanded = true });

            Assert.Equal(1, changes);
            Assert.True(store.Current.IsAdvisoryDetailExpanded);
            Assert.Equal(
                new[] { "save_msg=off", "advisory_detail=on" },
                _fileService.ReadAllLines(_filePath));
        }

        [Fact]
        public void Update_ThenAnotherConsumerReads_SeesTheChangeWithoutAReload()
        {
            // The reason this type exists. Before it, the colour picker and the suppression store
            // each loaded app_settings.txt for themselves, so a swatch stored in one was invisible
            // to the other until something re-read the file. One store, one AppSettings, one event.
            CreateFile();
            var store = CreateStore();
            AppSettings? seenByTheOtherConsumer = null;
            store.Changed += () => seenByTheOtherConsumer = store.Current;

            store.Update(settings => settings.WithCustomColor(3, new SettingsColor(1, 2, 3)));
            store.SetHidden(NotificationKeys.ResetLayer, true);

            Assert.NotNull(seenByTheOtherConsumer);
            Assert.Equal("[1][2][3]", seenByTheOtherConsumer.CustomColors[2]!.ToString());
            Assert.True(seenByTheOtherConsumer.IsResetLayerConfirmationHidden);
            Assert.Equal("[1][2][3]", store.Current.CustomColors[2]!.ToString());
        }

        [Fact]
        public void Update_AfterAnExternalRewrite_KeepsTheSessionsOwnCopy()
        {
            // Load once, mutate in memory, write through: a file rewritten behind the app's back
            // is not re-read mid-session, so two consumers can never disagree about what is
            // current. The read-modify-write merge still keeps the foreign line on disk.
            CreateFile("save_msg=off");
            var store = CreateStore();
            _ = store.Current;

            File.WriteAllLines(_filePath, ["save_msg=off", "some unparseable garbage"]);
            store.Update(settings => settings with { IsCaptureSummaryHidden = true });

            Assert.Equal(
                new[] { "save_msg=off", "some unparseable garbage", "capture_summary_msg=on" },
                _fileService.ReadAllLines(_filePath));
        }

        [Fact]
        public void Update_WithAnUnwritableLocation_KeepsTheValueAndDoesNotThrow()
        {
            var blockedRoot = Path.Combine(_tempDirectory, "blocked-update-root");
            Directory.CreateDirectory(blockedRoot);
            File.WriteAllText(Path.Combine(blockedRoot, "settings"), string.Empty);
            var store = new VDriveAppPreferencesStore(_settings, _location with { RootPath = blockedRoot });
            var changes = 0;
            store.Changed += () => changes++;

            store.Update(settings => settings with { IsAdvisoryDetailExpanded = true });

            Assert.True(store.Current.IsAdvisoryDetailExpanded);
            Assert.Equal(1, changes);
        }

        [Fact]
        public void Update_WithoutAMutation_Throws()
        {
            var store = CreateStore();

            Assert.Throws<ArgumentNullException>(() => store.Update(null!));
        }

        [Fact]
        public void GetFilePath_ForDrive_IsTheSettingsEnginesOwnPath()
        {
            // One path, resolved by Core: the Advantage2's keyboard settings live in active/ while
            // app_settings.txt always lives in settings/ (spec 08 §3), so a second answer here
            // would put the two writers on different files.
            var location = TestDevices.CreateLocation(DeviceId.Advantage2);

            var path = VDriveAppPreferencesStore.GetFilePath(location);

            Assert.Equal(SettingsService.GetAppSettingsFilePath(location), path);
            Assert.Equal(Path.Combine(location.RootPath, "settings", "app_settings.txt"), path);
        }

        [Fact]
        public void Constructor_WithoutACollaborator_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new VDriveAppPreferencesStore(null!, _location));
            Assert.Throws<ArgumentNullException>(() => new VDriveAppPreferencesStore(_settings, null!));
        }

        private VDriveAppPreferencesStore CreateStore()
        {
            return new VDriveAppPreferencesStore(_settings, _location);
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
