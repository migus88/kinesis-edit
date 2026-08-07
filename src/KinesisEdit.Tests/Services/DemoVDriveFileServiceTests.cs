using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Settings;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Both sides of the one line this decorator draws.
    /// <para>
    /// The refusal is scoped by <b>path</b>, not by mode, and that is the whole of the design:
    /// "Export layout to file…" writes through this very interface to a folder the user picked
    /// (specs/11-feature-dialogs.md §11.5), and demo mode is where export matters most. A service
    /// that refused every write would break export silently; one that refused none would break
    /// "demo mode saves nothing" (03 §3.5) just as silently. Neither failure is visible from one
    /// side of the line, so both sides are tested.
    /// </para>
    /// </summary>
    public class DemoVDriveFileServiceTests : IDisposable
    {
        private const string OutsidePath = "/somewhere/else/layout1.txt";

        private readonly VDriveLocation _location =
            DemoVDrive.CreateLocation(DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb));

        private readonly FakeVDriveFileService _inner = new();
        private readonly DemoVDriveFileService _fileService;
        private readonly string _temporaryDirectory =
            Path.Combine(Path.GetTempPath(), "kinesis-edit-demo-" + Guid.NewGuid().ToString("N"));

        public DemoVDriveFileServiceTests()
        {
            _fileService = new DemoVDriveFileService(_inner);
        }

        [Fact]
        public void ReadAllLines_ServesADemoPathFromTheFixturesWithoutTouchingTheRealService()
        {
            var lines = _fileService.ReadAllLines(_location.SettingsFilePath);

            Assert.NotEmpty(lines);
            Assert.Equal(0, _inner.ReadCount);
        }

        [Fact]
        public void ReadAllLines_DelegatesEveryOtherPath()
        {
            _inner.SetFile(OutsidePath, "[caps]>[lctrl]");

            Assert.Equal(["[caps]>[lctrl]"], _fileService.ReadAllLines(OutsidePath));
            Assert.Equal(1, _inner.ReadCount);
        }

        [Fact]
        public void ReadAllLines_ReportsAnUnknownDemoPathAsAMissingFile()
        {
            // Same failure the real service reports, so callers that already handle it — Core's
            // LoadAppSettings treats it as a fresh drive — behave identically here.
            Assert.Throws<FileNotFoundException>(
                () => _fileService.ReadAllLines(Path.Combine(_location.LayoutsFolderPath!, "layout7.txt")));
        }

        [Fact]
        public void WriteAllLines_RefusesADemoPath()
        {
            var exception = Assert.Throws<DemoVDriveWriteException>(
                () => _fileService.WriteAllLines(_location.SettingsFilePath, ["macro_speed=9"], allowCreate: true));

            Assert.Equal(_location.SettingsFilePath, exception.Path);
            Assert.Empty(_inner.WrittenPaths);
        }

        [Fact]
        public void WriteAllLines_RefusesEveryFileOfTheDemoDrive()
        {
            foreach (var path in DemoVDriveFixtures.Default.Paths)
            {
                Assert.Throws<DemoVDriveWriteException>(() => _fileService.WriteAllLines(path, ["x"], allowCreate: true));
            }

            Assert.Empty(_inner.WrittenPaths);
        }

        [Fact]
        public void WriteAllLines_LetsAnExportThrough()
        {
            _inner.SetFile(OutsidePath, "old");

            _fileService.WriteAllLines(OutsidePath, ["new"]);

            Assert.Equal([OutsidePath], _inner.WrittenPaths);
        }

        [Fact]
        public void WriteAllLines_LetsAnExportReachTheRealDisk()
        {
            // The fake above proves the call is delegated; this proves the delegation is useful —
            // an export in demo mode really does produce files in the folder the user picked.
            Directory.CreateDirectory(_temporaryDirectory);

            var exportPath = Path.Combine(_temporaryDirectory, "layout1.txt");
            var demoFileService = new DemoVDriveFileService(new VDriveFileService());

            demoFileService.WriteAllLines(exportPath, ["[caps]>[lctrl]"], allowCreate: true);

            Assert.Equal(["[caps]>[lctrl]"], File.ReadAllLines(exportPath));
        }

        [Fact]
        public void UpdateSettingsFile_RefusesTheDemoDrivesAppSettings()
        {
            // The path spec 08 §3's "never saved in demo mode" is about. A merge starts with a read
            // and ends with a truncating rewrite, so it is refused on the same rule as any write.
            var appSettingsPath = SettingsService.GetAppSettingsFilePath(_location);

            Assert.Throws<DemoVDriveWriteException>(
                () => _fileService.UpdateSettingsFile(appSettingsPath, [new KeyValuePair<string, string>("save_msg", "on")]));

            Assert.Empty(_inner.WrittenPaths);
            Assert.Empty(_inner.SettingsUpdates);
        }

        [Fact]
        public void UpdateSettingsFile_DelegatesEveryOtherPath()
        {
            _inner.SetFile(OutsidePath, "save_msg=off");

            _fileService.UpdateSettingsFile(OutsidePath, [new KeyValuePair<string, string>("save_msg", "on")]);

            Assert.Equal([OutsidePath], _inner.WrittenPaths);
            Assert.Equal([new KeyValuePair<string, string>("save_msg", "on")], _inner.SettingsUpdates);
        }

        [Fact]
        public void SettingsService_CannotSaveOntoTheDemoDrive()
        {
            // Stated through the real seam rather than the decorator alone: SaveAppSettings catches
            // FileNotFoundException to create a fresh file, and a refusal that looked like one
            // would have it write the whole file instead of refusing.
            var settingsService = new SettingsService(_fileService);
            var settings = AppSettings.Empty with { IsSaveMessageHidden = true };

            Assert.Throws<DemoVDriveWriteException>(() => settingsService.SaveAppSettings(_location, settings));
            Assert.Empty(_inner.WrittenPaths);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);

            if (Directory.Exists(_temporaryDirectory))
            {
                Directory.Delete(_temporaryDirectory, recursive: true);
            }
        }
    }
}
