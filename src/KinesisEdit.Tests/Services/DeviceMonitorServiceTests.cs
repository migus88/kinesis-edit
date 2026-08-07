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
                Assert.False(snapshot.IsDetected);
            });
        }

        [Fact]
        public void Refresh_WithoutDrives_AsksTheDemoGateAboutEveryUndetectedDeviceAndCarriesWhatItAnswers()
        {
            // The loop builds a demo snapshot for all seven catalog boards, so this is the one
            // place where "offer demo content only where fixtures exist" is decided seven times a
            // pass. The provider is asked about each of them, and only the one it answers for gets
            // a drive — the other six keep the location-less snapshot they have always had, which
            // is what stops their editors being pointed at files that are not there.
            var demoDevices = new FakeDemoDeviceProvider(DeviceId.FreestyleEdgeRgb);

            using var service = CreateService(demoDevices);

            service.Refresh();

            Assert.Equal(
                service.Snapshots.Select(snapshot => snapshot.ScannedDeviceId).ToArray(),
                demoDevices.AskedFor);

            var demo = GetSnapshot(service, DeviceId.FreestyleEdgeRgb);

            Assert.Equal(DemoVDrive.GetRootPath(DeviceId.FreestyleEdgeRgb), demo.Location?.RootPath);
            Assert.False(demo.Location!.IsWritable);

            Assert.All(
                service.Snapshots.Where(snapshot => snapshot.ScannedDeviceId != DeviceId.FreestyleEdgeRgb),
                snapshot => Assert.Null(snapshot.Location));
        }

        [Fact]
        public void Refresh_WithoutDrives_WithAGateThatAnswersForNothing_LeavesEveryLocationNull()
        {
            // The other side of the same line, and the reason the gate is a collaborator rather
            // than a lookup here: nothing in this service names a device, so a fixture set that
            // shipped empty produces exactly the pre-demo-content behaviour for all seven boards.
            using var service = CreateService(new FakeDemoDeviceProvider());

            service.Refresh();

            Assert.NotEmpty(service.Snapshots);
            Assert.All(service.Snapshots, snapshot => Assert.Null(snapshot.Location));
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

            // RefreshActivityChanged shares the dispatcher, so the post count is no longer 1;
            // what this test guards is that no Updated reaches a handler before the drain.
            Assert.NotEqual(0, dispatcher.PostCount);
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
                new FakeSystemClock(),
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
                new FakeSystemClock(),
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
            using var service = new DeviceMonitorService(monitor, fileService, new FakeUiDispatcher(), new FakeSystemClock(), _neverPolls);
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

        [Fact]
        public void LastRefreshedUtc_BeforeTheFirstRefresh_IsNull()
        {
            using var service = CreateService(out _, out _, out _);

            Assert.Null(service.LastRefreshedUtc);
            Assert.False(service.IsRefreshing);
        }

        [Fact]
        public void LastRefreshedUtc_AfterARefresh_CarriesTheClock()
        {
            using var service = CreateService(out var scanner, out var fileService, out _, out _);
            var location = TestDevices.CreateLocation(DeviceId.FreestyleEdgeRgb);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.FreestyleEdgeRgb));
            scanner.SetResult(location);

            service.Refresh();

            Assert.Equal(FakeSystemClock.Start, service.LastRefreshedUtc);
        }

        [Fact]
        public void LastRefreshedUtc_AfterASecondRefresh_Advances()
        {
            using var service = CreateService(out _, out _, out _, out var clock);

            service.Refresh();
            clock.Advance(TimeSpan.FromSeconds(2));
            service.Refresh();

            Assert.Equal(FakeSystemClock.Start + TimeSpan.FromSeconds(2), service.LastRefreshedUtc);
        }

        [Fact]
        public void LastRefreshedUtc_WhenTheRefreshFindsNothing_IsStillStamped()
        {
            // "Refreshed" means the loop looked, not that it found something: an empty scan is the
            // complete, correct answer of a healthy pass, and the dashboard's readout must reset
            // for it — otherwise unplugging the only keyboard freezes the readout forever.
            using var service = CreateService(out _, out _, out _);

            service.Refresh();

            Assert.All(service.Snapshots, snapshot => Assert.False(snapshot.IsDetected));
            Assert.Equal(FakeSystemClock.Start, service.LastRefreshedUtc);
        }

        [Fact]
        public void LastRefreshedUtc_WhenAVersionFileCannotBeRead_IsStillStamped()
        {
            // Same rule one level down: an unreadable version file is a device-level fault the
            // snapshot already reports as v-Drive Error, not a failure of the pass.
            using var service = CreateService(out var scanner, out var fileService, out _);
            var location = TestDevices.CreateLocation(DeviceId.Tko);
            fileService.SetUnreadable(location.VersionFilePath);
            scanner.SetResult(location);

            service.Refresh();

            Assert.Equal(VDriveHealth.Error, GetSnapshot(service, DeviceId.Tko).Health);
            Assert.Equal(FakeSystemClock.Start, service.LastRefreshedUtc);
        }

        [Fact]
        public void LastRefreshedUtc_WhenTheScanThrows_KeepsThePreviousValue()
        {
            var scanner = new CallbackScanner();
            var clock = new FakeSystemClock();
            using var service = CreateService(scanner, clock);

            service.Refresh();
            clock.Advance(TimeSpan.FromSeconds(5));
            scanner.Failure = new InvalidOperationException("the volume enumerator fell over");

            Assert.Throws<InvalidOperationException>(service.Refresh);

            // The readout keeps ageing while the loop is broken instead of claiming a scan that
            // never completed.
            Assert.Equal(FakeSystemClock.Start, service.LastRefreshedUtc);
        }

        [Fact]
        public void IsRefreshing_WhileARefreshRuns_IsTrueAndClearsAfterwards()
        {
            var scanner = new CallbackScanner();
            using var service = CreateService(scanner, new FakeSystemClock());
            var isRefreshingDuringScan = false;
            scanner.OnScan = () => isRefreshingDuringScan = service.IsRefreshing;

            service.Refresh();

            Assert.True(isRefreshingDuringScan);
            Assert.False(service.IsRefreshing);
        }

        [Fact]
        public void IsRefreshing_WhenTheScanThrows_IsCleared()
        {
            var scanner = new CallbackScanner
            {
                Failure = new InvalidOperationException("the volume enumerator fell over")
            };
            using var service = CreateService(scanner, new FakeSystemClock());

            Assert.Throws<InvalidOperationException>(service.Refresh);

            // A stuck Scanning card would outlive the app otherwise: nothing retries the flag.
            Assert.False(service.IsRefreshing);
        }

        [Fact]
        public void IsRefreshing_WhenARefreshCoalescesIntoARepeat_NeverReportsIdleInBetween()
        {
            var scanner = new CallbackScanner();
            using var service = CreateService(scanner, new FakeSystemClock());
            var states = new List<bool>();
            service.RefreshActivityChanged += () => states.Add(service.IsRefreshing);
            scanner.OnScan = () =>
            {
                if (scanner.ScanCount == 1)
                {
                    service.Refresh();
                }
            };

            service.Refresh();

            Assert.Equal(2, scanner.ScanCount);
            Assert.All(states.Take(states.Count - 1), state => Assert.True(state));
            Assert.False(states[^1]);
        }

        [Fact]
        public void CompletedRefreshCount_BeforeTheFirstRefresh_IsZero()
        {
            using var service = CreateService(out _, out _, out _);

            Assert.Equal(0, service.CompletedRefreshCount);
        }

        [Fact]
        public void CompletedRefreshCount_PerCompletedPass_AdvancesByOne()
        {
            // This is what the empty state's "rescanned N times" counts, so it has to mean passes
            // rather than anything that merely looks like one.
            using var service = CreateService(out _, out _, out _);

            service.Refresh();

            Assert.Equal(1, service.CompletedRefreshCount);

            service.Refresh();

            Assert.Equal(2, service.CompletedRefreshCount);
        }

        [Fact]
        public void CompletedRefreshCount_WhenARefreshCoalescesIntoARepeat_CountsBothPasses()
        {
            // A call arriving while a pass runs marks the running one to repeat rather than
            // starting a second one, so the two callers share one gate — but the repeat is a real
            // second scan of the drives and completes on its own, so it is a second count.
            var scanner = new CallbackScanner();
            using var service = CreateService(scanner, new FakeSystemClock());
            scanner.OnScan = () =>
            {
                if (scanner.ScanCount == 1)
                {
                    service.Refresh();
                }
            };

            service.Refresh();

            Assert.Equal(2, scanner.ScanCount);
            Assert.Equal(2, service.CompletedRefreshCount);
        }

        [Fact]
        public void CompletedRefreshCount_WhenTheRefreshThatCoalescedNeverRanTwice_CountsOnce()
        {
            // The other half of the same rule: a refresh call that is dropped outright — one made
            // after Dispose — never becomes a pass and never counts.
            var service = CreateService(out _, out _, out _);

            service.Refresh();
            service.Dispose();
            service.Refresh();

            Assert.Equal(1, service.CompletedRefreshCount);
        }

        [Fact]
        public void CompletedRefreshCount_WhenTheScanThrows_DoesNotCountThatPass()
        {
            var scanner = new CallbackScanner { Failure = new IOException("the volume went away") };
            using var service = CreateService(scanner, new FakeSystemClock());

            Assert.Throws<IOException>(service.Refresh);

            Assert.Equal(0, service.CompletedRefreshCount);
            Assert.Null(service.LastRefreshedUtc);
        }

        [Fact]
        public void CompletedRefreshCount_AndLastRefreshedUtc_MoveTogether()
        {
            // Two views of one fact — "a pass finished" — so an observer can never see one without
            // the other.
            using var service = CreateService(out _, out _, out _);
            var observed = new List<(int Count, DateTimeOffset? Stamp)>();
            service.RefreshActivityChanged += () => observed.Add((service.CompletedRefreshCount, service.LastRefreshedUtc));

            service.Refresh();

            Assert.All(observed, entry => Assert.Equal(entry.Stamp is null, entry.Count == 0));
        }

        [Fact]
        public void RefreshActivityChanged_AroundARefresh_ReportsTheStartTheStampAndTheEnd()
        {
            using var service = CreateService(out _, out _, out _);
            var states = new List<(bool IsRefreshing, DateTimeOffset? LastRefreshedUtc)>();
            var expected = new List<(bool IsRefreshing, DateTimeOffset? LastRefreshedUtc)>
            {
                (true, null),
                (true, FakeSystemClock.Start),
                (false, FakeSystemClock.Start)
            };
            service.RefreshActivityChanged += () => states.Add((service.IsRefreshing, service.LastRefreshedUtc));

            service.Refresh();

            Assert.Equal(expected, states);
        }

        [Fact]
        public void RefreshActivityChanged_Always_IsRaisedThroughTheDispatcher()
        {
            using var service = CreateService(out _, out _, out var dispatcher);
            dispatcher.IsDeferred = true;
            var changeCount = 0;
            service.RefreshActivityChanged += () => changeCount++;

            service.Refresh();

            Assert.Equal(0, changeCount);

            dispatcher.DrainPending();

            Assert.Equal(3, changeCount);
        }

        [Fact]
        public void RefreshActivityChanged_AfterDispose_IsNotRaised()
        {
            var service = CreateService(out _, out _, out _);
            var changeCount = 0;
            service.RefreshActivityChanged += () => changeCount++;
            service.Dispose();

            service.Refresh();

            Assert.Equal(0, changeCount);
            Assert.Null(service.LastRefreshedUtc);
            Assert.False(service.IsRefreshing);
        }

        private static DeviceMonitorService CreateService(CallbackScanner scanner, FakeSystemClock clock)
        {
            return new DeviceMonitorService(
                new VDriveMonitor(scanner, _neverPolls),
                new FakeVDriveFileService(),
                new FakeUiDispatcher(),
                clock,
                _neverPolls);
        }

        private static DeviceMonitorService CreateService(IDemoDeviceProvider demoDevices)
        {
            return new DeviceMonitorService(
                new VDriveMonitor(new FakeVDriveScanner(), _neverPolls),
                new FakeVDriveFileService(),
                new FakeUiDispatcher(),
                new FakeSystemClock(),
                _neverPolls,
                demoDevices);
        }

        private static DeviceMonitorService CreateService(
            out FakeVDriveScanner scanner,
            out FakeVDriveFileService fileService,
            out FakeUiDispatcher dispatcher)
        {
            return CreateService(out scanner, out fileService, out dispatcher, out _);
        }

        private static DeviceMonitorService CreateService(
            out FakeVDriveScanner scanner,
            out FakeVDriveFileService fileService,
            out FakeUiDispatcher dispatcher,
            out FakeSystemClock clock)
        {
            scanner = new FakeVDriveScanner();
            fileService = new FakeVDriveFileService();
            dispatcher = new FakeUiDispatcher();
            clock = new FakeSystemClock();

            return new DeviceMonitorService(new VDriveMonitor(scanner, _neverPolls), fileService, dispatcher, clock, _neverPolls);
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
        /// Scanner that runs a test callback <em>inside</em> the scan — the window in which a
        /// refresh is provably in flight — and that can be told to fail. <c>VDriveMonitor.Poll</c>
        /// propagates a scanner throw (only its own timer tick swallows one), so this is also the
        /// failing-refresh path. Everything happens on the calling thread: no timing, no waits.
        /// </summary>
        private sealed class CallbackScanner : IVDriveScanner
        {
            public int ScanCount { get; private set; }

            public Action? OnScan { get; set; }

            public Exception? Failure { get; set; }

            public IReadOnlyList<VDriveLocation> Scan()
            {
                ScanCount++;

                OnScan?.Invoke();

                if (Failure is not null)
                {
                    throw Failure;
                }

                return [];
            }
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
