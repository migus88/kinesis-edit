using System.Reflection;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Issue #123, bug 1: an ejected or unplugged drive must stop being described. The watcher is a
    /// <b>liveness</b> check over drives a completed scan already found — never discovery — so every
    /// test here starts by staging a scan and then takes the drive away.
    /// <para>
    /// The waits are all on signals: the probe releases a semaphore per call and the scanner one per
    /// scan. The only sleeps are the negative assertions, where proving that nothing happens needs a
    /// window in which it could have.
    /// </para>
    /// </summary>
    public class DeviceLivenessWatcherTests
    {
        private static readonly TimeSpan _tickInterval = TimeSpan.FromMilliseconds(20);
        private static readonly TimeSpan _quietWindow = TimeSpan.FromMilliseconds(400);
        private static readonly TimeSpan _neverPolls = TimeSpan.FromHours(1);

        /// <summary>
        /// The state the app spends most of its life in. No drive means no timer, so the watcher
        /// costs nothing at all — this is what keeps it from becoming the background poll of #118
        /// under another name.
        /// </summary>
        [Fact]
        public void Watcher_WithNothingConnected_ArmsNoTimerAndNeverProbes()
        {
            using var monitor = CreateMonitor(out var scanner, out _);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);

            monitor.Refresh();

            Assert.False(probe.WaitForProbe(_quietWindow));
            Assert.Equal(0, probe.ProbeCount);
            Assert.Equal(1, scanner.ScanCount);
        }

        /// <summary>Nothing arms on construction either — only a pass that found a drive does.</summary>
        [Fact]
        public void Watcher_BeforeAnyPass_NeverProbes()
        {
            using var monitor = CreateMonitor(out _, out _);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);

            Assert.False(probe.WaitForProbe(_quietWindow));
            Assert.Equal(0, probe.ProbeCount);
        }

        [Fact]
        public void Watcher_WhenAPassFindsADrive_ArmsAndProbesThatRoot()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);
            var location = StageDrive(scanner, fileService, DeviceId.Tko);

            monitor.Refresh();

            Assert.True(probe.WaitForProbe());
            Assert.Contains(location.RootPath, probe.ProbedPaths);
        }

        /// <summary>
        /// A demo snapshot carries a synthetic <c>kinesis-edit://demo/</c> root, which is not a path
        /// on this machine. Probing one would report "gone" on every tick forever, so the watched
        /// set is narrowed to drives that are both detected and not in demo mode.
        /// </summary>
        [Fact]
        public void Watcher_WithOnlyDemoDrives_NeverProbesAFixturePath()
        {
            using var monitor = CreateMonitor(out _, out _, new DemoDeviceProvider());
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);

            monitor.Refresh();

            // The pass really did hand out a demo drive, so this case is not vacuous.
            Assert.Contains(monitor.Snapshots, snapshot => snapshot.IsDemoMode && snapshot.Location is not null);
            Assert.False(probe.WaitForProbe(_quietWindow));
            Assert.Equal(0, probe.ProbeCount);
        }

        /// <summary>
        /// An unwritable drive is watched exactly like a writable one. It is physically mounted, it
        /// has a real root path and it draws a real card, so unplugging it has to lose that card —
        /// writability is a question about editing, not about presence. This is the case a
        /// <c>!IsDemoMode</c> filter would silently drop, because <c>IsDemoMode</c> is "not
        /// connected OR not writable" (specs/03-vdrive-and-files.md §3.5) and so is true here.
        /// </summary>
        [Fact]
        public void Watcher_WithAnUnwritableDrive_WatchesItLikeAnyOther()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);

            var location = TestDevices.CreateLocation(DeviceId.Tko, isWritable: false);

            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(DeviceId.Tko));
            scanner.SetResult(location);

            monitor.Refresh();

            // The drive really is the awkward shape this test is about: detected, mounted, and
            // reported as demo mode because it cannot be written.
            Assert.Contains(
                monitor.Snapshots,
                snapshot => snapshot.IsDetected && snapshot.IsDemoMode && snapshot.Location?.RootPath == location.RootPath);

            Assert.True(probe.WaitForProbe());
            Assert.Contains(location.RootPath, probe.ProbedPaths);

            // And losing it drops it, rather than leaving a card for a drive that is gone.
            scanner.SetResult();
            probe.SetMissing(location.RootPath);

            Assert.True(scanner.WaitForScanCount(2));

            // The scan is counted as it starts; Snapshots is published once it has built them.
            Thread.Sleep(_quietWindow);

            Assert.DoesNotContain(monitor.Snapshots, snapshot => snapshot.IsDetected);
        }

        /// <summary>
        /// The headline: a root path that stops existing produces exactly one scan, and that scan
        /// drops the drive from <see cref="DeviceMonitorService.Snapshots"/> — which is what takes
        /// the dashboard card with it.
        /// </summary>
        [Fact]
        public void Watcher_WhenAWatchedRootDisappears_RefreshesOnceAndDropsTheDrive()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);
            var location = StageDrive(scanner, fileService, DeviceId.Tko);

            monitor.Refresh();

            Assert.True(probe.WaitForProbe());
            Assert.Equal(1, scanner.ScanCount);

            // The drive goes: the mount point disappears, and a scan would no longer find it.
            scanner.SetResult();
            probe.SetMissing(location.RootPath);

            Assert.True(scanner.WaitForScanCount(2));

            Thread.Sleep(_quietWindow);

            Assert.Equal(2, scanner.ScanCount);
            Assert.DoesNotContain(monitor.Snapshots, snapshot => snapshot.IsDetected);
        }

        /// <summary>
        /// ...and the watcher disarms itself off the very update that scan published, so a drive
        /// that vanished does not leave a timer stat-ing its ghost.
        /// </summary>
        [Fact]
        public void Watcher_AfterTheLastDriveIsGone_Disarms()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);
            var location = StageDrive(scanner, fileService, DeviceId.Tko);

            monitor.Refresh();

            Assert.True(probe.WaitForProbe());

            scanner.SetResult();
            probe.SetMissing(location.RootPath);

            Assert.True(scanner.WaitForScanCount(2));

            Thread.Sleep(_quietWindow);

            var settled = probe.ProbeCount;

            Thread.Sleep(_quietWindow);

            Assert.Equal(settled, probe.ProbeCount);
        }

        /// <summary>
        /// A user-driven scan that loses the drive disarms the watcher just as well — the set is
        /// re-derived from every update, whoever asked for it.
        /// </summary>
        [Fact]
        public void Watcher_WhenAUserScanLosesTheDrive_StopsProbing()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);

            StageDrive(scanner, fileService, DeviceId.Tko);
            monitor.Refresh();

            Assert.True(probe.WaitForProbe());

            scanner.SetResult();
            monitor.Refresh();

            Thread.Sleep(_quietWindow);

            var settled = probe.ProbeCount;

            Thread.Sleep(_quietWindow);

            Assert.Equal(settled, probe.ProbeCount);
        }

        /// <summary>
        /// A save writes several files to a FAT volume; stat-ing its mount point in the middle of
        /// one, and worse re-reading it from a scan, is exactly what the write bracket exists to
        /// prevent. The tick is skipped entirely — the probe is not even called.
        /// </summary>
        [Fact]
        public void Tick_WhileAVDriveWriteIsInFlight_IsSkippedEntirely()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            var writeActivity = new VDriveWriteActivity();
            using var watcher = CreateWatcher(monitor, probe, writeActivity);
            var location = StageDrive(scanner, fileService, DeviceId.Tko);

            monitor.Refresh();

            Assert.True(probe.WaitForProbe());

            var write = writeActivity.Begin();

            // Let any tick that was already in flight finish before the count is read.
            Thread.Sleep(_quietWindow);

            var duringWrite = probe.ProbeCount;
            probe.SetMissing(location.RootPath);

            Thread.Sleep(_quietWindow);

            Assert.Equal(duringWrite, probe.ProbeCount);
            Assert.Equal(1, scanner.ScanCount);

            // ...and the moment the write closes, the very next tick notices the drive is gone.
            write.Dispose();

            Assert.True(scanner.WaitForScanCount(2));
        }

        /// <summary>
        /// A tick that finds every watched drive present does nothing at all. That is load-bearing:
        /// a refresh lights <c>IsRefreshing</c>, which puts every dashboard card into its Scanning
        /// face, so a watcher that scanned on a schedule would flicker the whole grid.
        /// </summary>
        [Fact]
        public void Tick_WhileEveryWatchedDriveIsPresent_NeverScans()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);

            StageDrive(scanner, fileService, DeviceId.Tko);
            monitor.Refresh();

            Assert.True(probe.WaitForProbe());

            Thread.Sleep(_quietWindow);

            Assert.Equal(1, scanner.ScanCount);
            Assert.False(monitor.IsRefreshing);

            // Many ticks really did run, so the case is not vacuous.
            Assert.True(probe.ProbeCount > 1);
        }

        /// <summary>
        /// Probing and the rescan it triggers both happen off the thread that asked for the last
        /// scan — a stat of a stalled mount must not land on the UI thread. <c>Updated</c> still
        /// travels through the monitor's own <see cref="IUiDispatcher"/> (invariant 7).
        /// </summary>
        [Fact]
        public void Watcher_Always_ProbesAndRescansOffTheThreadThatAskedForTheLastScan()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService, out var dispatcher);
            using var probe = new FakeVolumeLivenessProbe();
            using var watcher = CreateWatcher(monitor, probe);
            var location = StageDrive(scanner, fileService, DeviceId.Tko);

            monitor.Refresh();

            var callerThreadId = Environment.CurrentManagedThreadId;

            Assert.True(probe.WaitForProbe());

            probe.SetMissing(location.RootPath);

            Assert.True(scanner.WaitForScanCount(2));

            Assert.DoesNotContain(callerThreadId, probe.ProbeThreadIds);
            Assert.True(probe.RanOnlyOnThreadPoolThreads);
            Assert.DoesNotContain(callerThreadId, scanner.ScanThreadIds.Skip(1));
            Assert.True(dispatcher.PostCount > 0);
        }

        [Fact]
        public void Dispose_Always_StopsProbingAndUnsubscribes()
        {
            using var monitor = CreateMonitor(out var scanner, out var fileService);
            using var probe = new FakeVolumeLivenessProbe();
            var watcher = CreateWatcher(monitor, probe);

            StageDrive(scanner, fileService, DeviceId.Tko);
            monitor.Refresh();

            Assert.True(probe.WaitForProbe());

            watcher.Dispose();

            Thread.Sleep(_quietWindow);

            var settled = probe.ProbeCount;

            // A pass after disposal must not re-arm anything: the subscription is gone.
            monitor.Refresh();

            Thread.Sleep(_quietWindow);

            Assert.Equal(settled, probe.ProbeCount);
        }

        [Fact]
        public void Dispose_Twice_IsANoOp()
        {
            using var monitor = CreateMonitor(out _, out _);
            using var probe = new FakeVolumeLivenessProbe();
            var watcher = CreateWatcher(monitor, probe);

            watcher.Dispose();
            watcher.Dispose();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("probe")]
        [InlineData("writeActivity")]
        public void Constructor_WithAMissingCollaborator_Throws(string? missing)
        {
            using var monitor = CreateMonitor(out _, out _);
            using var probe = new FakeVolumeLivenessProbe();

            Assert.Throws<ArgumentNullException>(() => new DeviceLivenessWatcher(
                missing is null ? null! : monitor,
                missing == "probe" ? null! : probe,
                missing == "writeActivity" ? null! : new VDriveWriteActivity()));
        }

        /// <summary>
        /// The watcher arms itself off the monitor's updates and disarms when the last drive is
        /// gone, so construction and <c>Dispose</c> are its whole public surface. There is no
        /// <c>Start</c>, <c>Stop</c> or <c>IsPolling</c> for a future caller to turn back into the
        /// poll of #118 — the same statement <c>DeviceMonitorServiceTests</c> makes about the
        /// detection service, made about the thing that actually owns a timer.
        /// </summary>
        [Fact]
        public void DeviceLivenessWatcher_Exposes_NoStartStopOrIsPolling()
        {
            const BindingFlags everything =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            var type = typeof(DeviceLivenessWatcher);

            Assert.DoesNotContain(
                type.GetMembers(everything),
                member => member.Name is "Start" or "Stop" or "IsPolling");

            var publicMembers = type.GetMembers(BindingFlags.Public | BindingFlags.Instance)
                .Where(member => member.DeclaringType == type)
                .Select(member => member.Name)
                .ToArray();

            Assert.Equal(new[] { ".ctor", "Dispose" }, publicMembers.OrderBy(name => name, StringComparer.Ordinal));
        }

        /// <summary>
        /// The timer belongs to this type and to nothing else: #118's reflection test forbids one on
        /// <see cref="DeviceMonitorService"/>, and that must stay true with the watcher in place.
        /// </summary>
        [Fact]
        public void DeviceMonitorService_EvenWithAWatcherOverIt_StillHasNoTimerField()
        {
            const BindingFlags everything =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

            Assert.DoesNotContain(
                typeof(DeviceMonitorService).GetFields(everything),
                field => typeof(Timer).IsAssignableFrom(field.FieldType));

            Assert.Contains(
                typeof(DeviceLivenessWatcher).GetFields(everything),
                field => typeof(Timer).IsAssignableFrom(field.FieldType));
        }

        private static DeviceMonitorService CreateMonitor(
            out CountingScanner scanner,
            out FakeVDriveFileService fileService,
            IDemoDeviceProvider? demoDevices = null)
        {
            return CreateMonitor(out scanner, out fileService, out _, demoDevices);
        }

        private static DeviceMonitorService CreateMonitor(
            out CountingScanner scanner,
            out FakeVDriveFileService fileService,
            out FakeUiDispatcher dispatcher,
            IDemoDeviceProvider? demoDevices = null)
        {
            scanner = new CountingScanner();
            fileService = new FakeVDriveFileService();
            dispatcher = new FakeUiDispatcher();

            return new DeviceMonitorService(
                new VDriveMonitor(scanner, _neverPolls),
                fileService,
                dispatcher,
                demoDevices);
        }

        private static DeviceLivenessWatcher CreateWatcher(
            DeviceMonitorService monitor,
            IVolumeLivenessProbe probe,
            IVDriveWriteActivity? writeActivity = null)
        {
            return new DeviceLivenessWatcher(
                monitor,
                probe,
                writeActivity ?? new VDriveWriteActivity(),
                _tickInterval);
        }

        private static VDriveLocation StageDrive(
            CountingScanner scanner,
            FakeVDriveFileService fileService,
            DeviceId deviceId)
        {
            var location = TestDevices.CreateLocation(deviceId);
            fileService.SetFile(location.VersionFilePath, TestDevices.CreateVersionFileLines(deviceId));
            scanner.SetResult(location);

            return location;
        }

        /// <summary>
        /// Scripted scanner that counts its passes, records the thread each ran on, and signals one
        /// per scan — the seam every "did a rescan happen?" assertion here waits on, since
        /// <c>DeviceMonitorService.Refresh</c> is not itself observable.
        /// </summary>
        private sealed class CountingScanner : IVDriveScanner
        {
            private static readonly TimeSpan _waitTimeout = TimeSpan.FromSeconds(10);

            public int ScanCount
            {
                get
                {
                    lock (_syncRoot)
                    {
                        return _scanThreadIds.Count;
                    }
                }
            }

            public IReadOnlyList<int> ScanThreadIds
            {
                get
                {
                    lock (_syncRoot)
                    {
                        return [.. _scanThreadIds];
                    }
                }
            }

            private readonly object _syncRoot = new();
            private readonly List<int> _scanThreadIds = [];
            private readonly SemaphoreSlim _scanned = new(0);
            private IReadOnlyList<VDriveLocation> _result = [];

            public void SetResult(params VDriveLocation[] locations)
            {
                lock (_syncRoot)
                {
                    _result = locations;
                }
            }

            /// <summary>
            /// Waits until at least <paramref name="count"/> scans have run. Counted rather than
            /// signalled one-for-one, because the pass that armed the watcher has already released
            /// a token by the time a test starts waiting for the next one.
            /// </summary>
            public bool WaitForScanCount(int count)
            {
                var deadline = DateTime.UtcNow + _waitTimeout;

                while (ScanCount < count)
                {
                    var remaining = deadline - DateTime.UtcNow;

                    if (remaining <= TimeSpan.Zero || !_scanned.Wait(remaining))
                    {
                        return false;
                    }
                }

                return true;
            }

            public IReadOnlyList<VDriveLocation> Scan()
            {
                IReadOnlyList<VDriveLocation> result;

                lock (_syncRoot)
                {
                    _scanThreadIds.Add(Environment.CurrentManagedThreadId);
                    result = _result;
                }

                _scanned.Release();

                return result;
            }
        }
    }
}
