using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class DeviceMonitorServiceTests
    {
        private static readonly TimeSpan _neverPolls = TimeSpan.FromHours(1);

        [Fact]
        public void Refresh_WithWritableDrive_ReportsConnectedAndNotDemoMode()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.FreestyleEdgeRgb));
            scanner.SetResult(location);

            service.Refresh();

            var snapshot = GetSnapshot(service, DeviceId.FreestyleEdgeRgb);
            Assert.Equal(VDriveConnectionStatus.Connected, snapshot.Status);
            Assert.False(snapshot.IsDemoMode);
            Assert.Equal(VDriveHealth.Ok, snapshot.Health);
            Assert.Equal(location, snapshot.Location);
            Assert.False(snapshot.Firmware.IsDemoMode);
        }

        [Fact]
        public void Refresh_WithReadOnlyDrive_ReportsCannotAccessAndDemoMode()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Advantage2, isWritable: false);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Advantage2));
            scanner.SetResult(location);

            service.Refresh();

            var snapshot = GetSnapshot(service, DeviceId.Advantage2);
            Assert.Equal(VDriveConnectionStatus.CannotAccess, snapshot.Status);
            Assert.True(snapshot.IsDemoMode);
            Assert.True(snapshot.Firmware.IsDemoMode);
            Assert.Equal(VDriveHealth.Ok, snapshot.Health);
        }

        [Fact]
        public void Refresh_WithoutDrives_ReportsEveryTrackedDeviceNotDetected()
        {
            using var service = CreateService(out _, out _, out _);

            service.Refresh();

            Assert.NotEmpty(service.Snapshots);
            Assert.All(service.Snapshots, snapshot =>
            {
                Assert.Equal(VDriveConnectionStatus.NotDetected, snapshot.Status);
                Assert.Equal(VDriveHealth.Unknown, snapshot.Health);
                Assert.True(snapshot.IsDemoMode);
                Assert.Null(snapshot.Location);
                Assert.False(snapshot.IsDetected);
            });
        }

        [Fact]
        public void Refresh_Always_RereadsAndReparsesTheVersionFile()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Tko);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Tko, keyboardFirmware: "1.0.0"));
            scanner.SetResult(location);

            service.Refresh();
            var firstReadCount = fileService.ReadCount;
            var first = GetSnapshot(service, DeviceId.Tko).VersionFile.KeyboardFirmware;

            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Tko, keyboardFirmware: "1.0.9"));
            service.Refresh();

            Assert.Equal(1, firstReadCount);
            Assert.Equal(2, fileService.ReadCount);
            Assert.Equal(new FirmwareVersion(1, 0, 0), first);
            Assert.Equal(new FirmwareVersion(1, 0, 9), GetSnapshot(service, DeviceId.Tko).VersionFile.KeyboardFirmware);
        }

        [Fact]
        public void Refresh_WithMissingVersionFile_ReportsErrorWithoutThrowing()
        {
            using var service = CreateService(out var scanner, out _, out _);
            scanner.SetResult(TestDevices.CreateLocation(DeviceId.Tko));

            service.Refresh();

            var snapshot = GetSnapshot(service, DeviceId.Tko);
            Assert.Equal(VDriveHealth.Error, snapshot.Health);
            Assert.Equal(VDriveConnectionStatus.Connected, snapshot.Status);
        }

        [Fact]
        public void Refresh_WithUnreadableVersionFile_ReportsErrorWithoutThrowing()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Tko);
            fileService.SetUnreadable(location.VersionFilePath);
            scanner.SetResult(location);

            service.Refresh();

            Assert.Equal(VDriveHealth.Error, GetSnapshot(service, DeviceId.Tko).Health);
        }

        [Fact]
        public void Refresh_WithGarbageVersionFile_ReportsError()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Tko);
            fileService.SetFile(location.VersionFilePath, "this file is not a version file", string.Empty);
            scanner.SetResult(location);

            service.Refresh();

            var snapshot = GetSnapshot(service, DeviceId.Tko);
            Assert.Equal(VDriveHealth.Error, snapshot.Health);
            Assert.Null(snapshot.VersionFile.KeyboardFirmware);
        }

        [Fact]
        public void Refresh_WhenFreestyleModelNameChanges_RederivesTheResolvedDevice()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.FreestyleEdge);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.FreestyleEdge, modelName: "FS Edge"));
            scanner.SetResult(location);

            service.Refresh();
            var firstDeviceId = GetDetectedSnapshot(service).DeviceId;

            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.FreestyleEdge, modelName: "FS PRO"));
            service.Refresh();

            Assert.Equal(DeviceId.FreestyleEdge, firstDeviceId);
            Assert.Equal(DeviceId.FreestylePro, GetDetectedSnapshot(service).DeviceId);
        }

        [Fact]
        public void Refresh_WhenFreestyleModelNameChanges_KeepsTheScannedSlotAsTheKey()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.FreestylePro);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.FreestylePro, modelName: "FS Edge"));
            scanner.SetResult(location);

            service.Refresh();

            var snapshot = GetDetectedSnapshot(service);
            Assert.Equal(DeviceId.FreestylePro, snapshot.ScannedDeviceId);
            Assert.Equal(DeviceId.FreestyleEdge, snapshot.DeviceId);
        }

        [Fact]
        public void Refresh_WithUnreadableFreestyleVersionFile_KeepsTheScannedModel()
        {
            using var service = CreateService(out var scanner, out _, out _);
            scanner.SetResult(TestDevices.CreateLocation(DeviceId.FreestylePro));

            service.Refresh();

            Assert.Equal(DeviceId.FreestylePro, GetDetectedSnapshot(service).DeviceId);
        }

        [Fact]
        public void Refresh_WhenConnectedDriveDisappears_SurfacesTheConnectionLoss()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Advantage360);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Advantage360));
            scanner.SetResult(location);
            var updates = CollectUpdates(service);

            service.Refresh();
            scanner.SetResult();
            service.Refresh();

            Assert.Equal(2, updates.Count);
            Assert.False(updates[0].HasConnectionLoss);
            Assert.True(updates[1].HasConnectionLoss);
            var change = Assert.Single(updates[1].Changes);
            Assert.Equal(DeviceId.Advantage360, change.DeviceId);
        }

        [Fact]
        public void Refresh_WhenNothingChanges_ReportsNoChanges()
        {
            using var service = CreateService(out _, out _, out _);
            var updates = CollectUpdates(service);

            service.Refresh();
            service.Refresh();

            Assert.Equal(2, updates.Count);
            Assert.Empty(updates[1].Changes);
        }

        [Fact]
        public void Refresh_Always_RaisesUpdatedThroughTheDispatcher()
        {
            using var service = CreateService(out _, out _, out var dispatcher);
            dispatcher.IsDeferred = true;
            var updates = CollectUpdates(service);

            service.Refresh();

            Assert.Equal(1, dispatcher.PostCount);
            Assert.Empty(updates);

            dispatcher.DrainPending();

            Assert.Single(updates);
        }

        [Fact]
        public void Refresh_WhenAnExplicitScanLandsOnARunningPoll_StillRunsThatScan()
        {
            using var scanner = new GatedScanner();
            using var service = new DeviceMonitorService(
                new VDriveMonitor(scanner, _neverPolls),
                new FakeVDriveFileService(),
                new FakeUiDispatcher(),
                _neverPolls);
            var poll = new Thread(service.Refresh);

            poll.Start();
            scanner.WaitForFirstScan();

            // The user's 'Scan for v-Drive' arrives while the timer's poll is inside the scanner:
            // VDriveMonitor.Poll would drop this one outright, so the request has to survive as a
            // repeat of the running refresh instead.
            service.Refresh();
            scanner.ReleaseFirstScan();
            poll.Join();

            Assert.Equal(2, scanner.ScanCount);
        }

        [Fact]
        public void Refresh_WhenAnExplicitScanLandsOnARunningPoll_DoesNotBlockTheCaller()
        {
            using var scanner = new GatedScanner();
            using var service = new DeviceMonitorService(
                new VDriveMonitor(scanner, _neverPolls),
                new FakeVDriveFileService(),
                new FakeUiDispatcher(),
                _neverPolls);
            var poll = new Thread(service.Refresh);

            poll.Start();
            scanner.WaitForFirstScan();

            service.Refresh();

            // Returning while the first scan is still stalled is the whole point: serializing by
            // blocking would hand the stall straight to whoever asked for the scan.
            Assert.Equal(1, scanner.ScanCount);

            scanner.ReleaseFirstScan();
            poll.Join();
        }

        [Fact]
        public void Refresh_WhenAnotherRefreshArrivesMidPoll_PublishesEveryStatusChange()
        {
            var scanner = new FakeVDriveScanner();
            var fileService = new FakeVDriveFileService();
            var monitor = new VDriveMonitor(scanner, _neverPolls);
            using var service = new DeviceMonitorService(monitor, fileService, new FakeUiDispatcher(), _neverPolls);
            var location = TestDevices.CreateLocation(DeviceId.Tko);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Tko));
            scanner.SetResult(location);
            var updates = CollectUpdates(service);

            // The monitor raises StatusChanged after the service has recorded the change, so this
            // handler re-enters Refresh exactly in the window where the change is queued but not
            // yet published — the window the entry-time clear used to wipe.
            var hasReentered = false;
            monitor.StatusChanged += _ =>
            {
                if (hasReentered)
                {
                    return;
                }

                hasReentered = true;

                service.Refresh();
            };

            service.Refresh();

            Assert.True(hasReentered);
            Assert.Contains(updates, update => update.Changes.Any(change => change.DeviceId == DeviceId.Tko));
        }

        [Fact]
        public void Start_WhenCalled_PublishesSnapshotsSynchronously()
        {
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Advantage2);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Advantage2));
            scanner.SetResult(location);

            service.Start();
            service.Stop();

            Assert.Equal(VDriveConnectionStatus.Connected, GetSnapshot(service, DeviceId.Advantage2).Status);
        }

        [Fact]
        public void Refresh_AfterDispose_DoesNothing()
        {
            var service = CreateService(out _, out _, out var dispatcher);
            var updates = CollectUpdates(service);
            service.Dispose();
            service.Dispose();

            service.Refresh();

            Assert.Empty(updates);
            Assert.Equal(0, dispatcher.PostCount);
            Assert.Empty(service.Snapshots);
        }

        [Fact]
        public void IsPolling_FollowsStartAndStop()
        {
            using var service = CreateService(out _, out _, out _);

            Assert.False(service.IsPolling);

            service.Start();

            Assert.True(service.IsPolling);

            service.Stop();

            Assert.False(service.IsPolling);
        }

        private static DeviceMonitorService CreateService(
            out FakeVDriveScanner scanner,
            out FakeVDriveFileService fileService,
            out FakeUiDispatcher dispatcher)
        {
            scanner = new FakeVDriveScanner();
            fileService = new FakeVDriveFileService();
            dispatcher = new FakeUiDispatcher();

            return new DeviceMonitorService(new VDriveMonitor(scanner, _neverPolls), fileService, dispatcher, _neverPolls);
        }

        private static List<DeviceMonitorUpdate> CollectUpdates(DeviceMonitorService service)
        {
            var updates = new List<DeviceMonitorUpdate>();
            service.Updated += update => updates.Add(update);

            return updates;
        }

        private static DeviceSnapshot GetSnapshot(DeviceMonitorService service, DeviceId deviceId)
        {
            return Assert.Single(service.Snapshots, snapshot => snapshot.DeviceId == deviceId);
        }

        private static DeviceSnapshot GetDetectedSnapshot(DeviceMonitorService service)
        {
            return Assert.Single(service.Snapshots, snapshot => snapshot.IsDetected);
        }

        /// <summary>
        /// Scanner whose first scan parks until the test releases it, so a second refresh can be
        /// issued while a poll is provably in flight. Waits are bounded: a regression fails the
        /// test instead of hanging the suite.
        /// </summary>
        private sealed class GatedScanner : IVDriveScanner, IDisposable
        {
            private static readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(10);

            public int ScanCount => Volatile.Read(ref _scanCount);

            private readonly ManualResetEventSlim _firstScanStarted = new(false);
            private readonly ManualResetEventSlim _firstScanReleased = new(false);
            private int _scanCount;

            public IReadOnlyList<VDriveLocation> Scan()
            {
                if (Interlocked.Increment(ref _scanCount) == 1)
                {
                    _firstScanStarted.Set();
                    _firstScanReleased.Wait(_waitTimeout);
                }

                return [];
            }

            public void WaitForFirstScan()
            {
                Assert.True(_firstScanStarted.Wait(_waitTimeout));
            }

            public void ReleaseFirstScan()
            {
                _firstScanReleased.Set();
            }

            public void Dispose()
            {
                _firstScanStarted.Dispose();
                _firstScanReleased.Dispose();
            }
        }
    }
}
