using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public sealed class DashboardViewModelTests : IDisposable
    {
        private static readonly TimeSpan _neverPolls = TimeSpan.FromHours(1);

        private readonly FakeVDriveScanner _scanner = new();
        private readonly FakeVDriveFileService _fileService = new();
        private readonly DeviceMonitorService _monitor;
        private readonly DashboardViewModel _dashboard;

        public DashboardViewModelTests()
        {
            _monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _fileService,
                new FakeUiDispatcher(),
                _neverPolls);

            _dashboard = new DashboardViewModel(
                _monitor,
                new VDriveEjectNotifier(new FakeDeviceEjectService(), new FakeNotificationService()),
                new FakeFirmwareUpdatePresenter(),
                new FakeUrlLauncher());
        }

        [Fact]
        public void Devices_WithoutDetectedDrives_IsEmptyAndShowsTheEmptyState()
        {
            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
            Assert.True(_dashboard.IsEmpty);
            Assert.False(_dashboard.HasDevices);
            Assert.NotNull(_dashboard.EmptyState);
        }

        [Fact]
        public void Devices_WithDetectedDrives_HasOneCardPerDetectedDeviceInCatalogOrder()
        {
            SetDrives(
                CreateDrive(DeviceId.Tko),
                CreateDrive(DeviceId.Advantage2, isWritable: false));

            _monitor.Refresh();

            Assert.Equal(
                new[] { DeviceId.Advantage2, DeviceId.Tko },
                _dashboard.Devices.Select(card => card.DeviceId));
            Assert.True(_dashboard.HasDevices);
            Assert.False(_dashboard.IsEmpty);
        }

        [Fact]
        public void Devices_WhenADriveAppears_AddsItsCard()
        {
            _monitor.Refresh();
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));

            _monitor.Refresh();

            var card = Assert.Single(_dashboard.Devices);
            Assert.Equal(DeviceId.FreestyleEdgeRgb, card.DeviceId);
        }

        [Fact]
        public void Devices_WhenADriveDisappears_RemovesItsCard()
        {
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));
            _monitor.Refresh();

            _scanner.SetResult();
            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
            Assert.True(_dashboard.IsEmpty);
        }

        [Fact]
        public void Devices_AcrossRefreshes_UpdatesTheCardInPlaceWithoutDuplicates()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.Devices);

            SetDrives(CreateDrive(DeviceId.Tko, isWritable: false));
            _monitor.Refresh();
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.Devices));
            Assert.Equal("Cannot Access", card.StatusText);
        }

        [Fact]
        public void Devices_WhenTwoFreestyleDrivesResolveToTheSameModel_KeepsOneCardPerDrive()
        {
            // Both catalog slots re-derive their model from the version file, so an FS Edge and an
            // FS Pro mounted together can resolve to the same device. Cards are keyed by the
            // scanned slot, which the scanner guarantees is unique per refresh.
            SetDrives(
                CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS Edge"),
                CreateFreestyleDrive(DeviceId.FreestylePro, "FS Edge"));

            _monitor.Refresh();
            _monitor.Refresh();

            Assert.Equal(
                new[] { DeviceId.FreestyleEdge, DeviceId.FreestylePro },
                _dashboard.Devices.Select(card => card.ScannedDeviceId));
            Assert.All(_dashboard.Devices, card => Assert.Equal(DeviceId.FreestyleEdge, card.DeviceId));
        }

        [Fact]
        public void Devices_WhenAFreestyleDriveChangesModel_KeepsTheSameCard()
        {
            SetDrives(CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS Edge"));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.Devices);

            SetDrives(CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS PRO"));
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.Devices));
            Assert.Equal(DeviceId.FreestyleEdge, card.ScannedDeviceId);
            Assert.Equal(DeviceId.FreestylePro, card.DeviceId);
        }

        [Fact]
        public void Apply_WithSnapshotsSharingAKey_DoesNotThrow()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);

            _dashboard.Apply([snapshot, snapshot]);

            Assert.Single(_dashboard.Devices);
        }

        [Fact]
        public void ConfigureRequested_WhenACardIsConfigured_CarriesThatDevice()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var requested = new List<DeviceSnapshot>();
            _dashboard.ConfigureRequested += requested.Add;

            _dashboard.Devices[0].ConfigureCommand.Execute(null);

            Assert.Equal(DeviceId.Tko, Assert.Single(requested).DeviceId);
        }

        [Fact]
        public void ConfigureRequested_WhenTheEmptyStateLaunchesDemoMode_CarriesADemoSnapshot()
        {
            _monitor.Refresh();
            var requested = new List<DeviceSnapshot>();
            _dashboard.ConfigureRequested += requested.Add;

            _dashboard.EmptyState.LaunchDemoModeCommand.Execute(null);

            var snapshot = Assert.Single(requested);
            Assert.True(snapshot.IsDemoMode);
            Assert.Equal(_dashboard.EmptyState.SelectedDevice.Id, snapshot.DeviceId);
        }

        [Fact]
        public async Task ScanCommand_WhenExecuted_RunsAnotherDetectionPass()
        {
            SetDrives(CreateDrive(DeviceId.Tko));

            await _dashboard.ScanCommand.ExecuteAsync(null);

            Assert.Single(_dashboard.Devices);
        }

        [Fact]
        public async Task ScanAsync_WhenTheScanStalls_DoesNotBlockTheCaller()
        {
            using var gate = new ManualResetEventSlim(false);
            SetDrives(CreateDrive(DeviceId.Tko));
            _scanner.Gate = gate;

            var scan = _dashboard.ScanAsync();

            // Back before the stalled volume enumeration finished: on the UI thread that is the
            // difference between a responsive window and a frozen one.
            Assert.False(scan.IsCompleted);

            gate.Set();
            await scan;

            Assert.Single(_dashboard.Devices);
        }

        [Fact]
        public void Apply_AfterDispose_IsNoLongerDrivenByTheMonitor()
        {
            _dashboard.Dispose();
            SetDrives(CreateDrive(DeviceId.Tko));

            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
        }

        private VDriveLocation CreateDrive(DeviceId deviceId, bool isWritable = true)
        {
            var location = TestDevices.CreateLocation(deviceId, isWritable);
            _fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(deviceId));

            return location;
        }

        private VDriveLocation CreateFreestyleDrive(DeviceId deviceId, string modelName)
        {
            var location = TestDevices.CreateLocation(deviceId);
            _fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(deviceId, modelName));

            return location;
        }

        private void SetDrives(params VDriveLocation[] locations)
        {
            _scanner.SetResult(locations);
        }

        public void Dispose()
        {
            _dashboard.Dispose();
            _monitor.Dispose();
        }
    }
}
