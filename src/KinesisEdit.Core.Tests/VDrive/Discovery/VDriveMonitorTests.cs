using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Discovery;

namespace KinesisEdit.Core.Tests.VDrive.Discovery
{
    public class VDriveMonitorTests
    {
        [Fact]
        public void Statuses_BeforeFirstPoll_ReportsAllDetectableDevicesNotDetected()
        {
            using var monitor = CreateMonitor(out _);

            var expectedDeviceIds = new[]
            {
                DeviceId.SavantElite2,
                DeviceId.Advantage2,
                DeviceId.FreestyleEdge,
                DeviceId.FreestylePro,
                DeviceId.FreestyleEdgeRgb,
                DeviceId.Tko,
                DeviceId.Advantage360
            };

            Assert.Equal(expectedDeviceIds.Order(), monitor.Statuses.Keys.Order());
            Assert.All(monitor.Statuses.Values, status =>
            {
                Assert.Equal(VDriveConnectionStatus.NotDetected, status.Status);
                Assert.Null(status.Location);
            });
        }

        [Fact]
        public void Statuses_Always_ExcludesUndetectableDevices()
        {
            using var monitor = CreateMonitor(out _);

            Assert.DoesNotContain(DeviceId.CrossfireKeypad, monitor.Statuses.Keys);
            Assert.DoesNotContain(DeviceId.Advantage360Professional, monitor.Statuses.Keys);
        }

        [Fact]
        public void Poll_WhenWritableDeviceAppears_RaisesChangeToConnected()
        {
            using var monitor = CreateMonitor(out var scanner);
            var changes = CollectChanges(monitor);
            var location = CreateLocation(DeviceId.FreestyleEdgeRgb, isWritable: true);

            scanner.SetResult(location);
            monitor.Poll();

            var change = Assert.Single(changes);
            Assert.Equal(DeviceId.FreestyleEdgeRgb, change.DeviceId);
            Assert.Equal(VDriveConnectionStatus.NotDetected, change.Previous.Status);
            Assert.Equal(VDriveConnectionStatus.Connected, change.Current.Status);
            Assert.Equal(location, change.Current.Location);
            Assert.False(change.IsConnectionLost);
            Assert.Equal(change.Current, monitor.Statuses[DeviceId.FreestyleEdgeRgb]);
        }

        [Fact]
        public void Poll_WhenUnwritableDeviceAppears_RaisesChangeToCannotAccess()
        {
            using var monitor = CreateMonitor(out var scanner);
            var changes = CollectChanges(monitor);

            scanner.SetResult(CreateLocation(DeviceId.Advantage2, isWritable: false));
            monitor.Poll();

            var change = Assert.Single(changes);
            Assert.Equal(VDriveConnectionStatus.CannotAccess, change.Current.Status);
            Assert.False(change.IsConnectionLost);
            Assert.Equal(VDriveConnectionStatus.CannotAccess, monitor.Statuses[DeviceId.Advantage2].Status);
        }

        [Fact]
        public void Poll_WhenConnectedDeviceDisappears_RaisesChangeWithConnectionLost()
        {
            using var monitor = CreateMonitor(out var scanner);
            scanner.SetResult(CreateLocation(DeviceId.Tko, isWritable: true));
            monitor.Poll();
            var changes = CollectChanges(monitor);

            scanner.SetResult();
            monitor.Poll();

            var change = Assert.Single(changes);
            Assert.Equal(DeviceId.Tko, change.DeviceId);
            Assert.Equal(VDriveConnectionStatus.Connected, change.Previous.Status);
            Assert.Equal(VDriveConnectionStatus.NotDetected, change.Current.Status);
            Assert.True(change.IsConnectionLost);
            Assert.Equal(VDriveConnectionStatus.NotDetected, monitor.Statuses[DeviceId.Tko].Status);
        }

        [Fact]
        public void Poll_WhenConnectedDeviceBecomesUnwritable_RaisesChangeToCannotAccessWithConnectionLost()
        {
            using var monitor = CreateMonitor(out var scanner);
            scanner.SetResult(CreateLocation(DeviceId.Advantage360, isWritable: true));
            monitor.Poll();
            var changes = CollectChanges(monitor);

            scanner.SetResult(CreateLocation(DeviceId.Advantage360, isWritable: false));
            monitor.Poll();

            var change = Assert.Single(changes);
            Assert.Equal(VDriveConnectionStatus.Connected, change.Previous.Status);
            Assert.Equal(VDriveConnectionStatus.CannotAccess, change.Current.Status);
            Assert.True(change.IsConnectionLost);
        }

        [Fact]
        public void Poll_WhenNothingChanged_RaisesNoEvent()
        {
            using var monitor = CreateMonitor(out var scanner);
            scanner.SetResult(CreateLocation(DeviceId.FreestylePro, isWritable: true));
            monitor.Poll();
            var changes = CollectChanges(monitor);

            scanner.SetResult(CreateLocation(DeviceId.FreestylePro, isWritable: true));
            monitor.Poll();

            Assert.Empty(changes);
        }

        [Fact]
        public void Poll_WithMultipleDevicesAppearing_RaisesOneChangePerDevice()
        {
            using var monitor = CreateMonitor(out var scanner);
            var changes = CollectChanges(monitor);

            scanner.SetResult(
                CreateLocation(DeviceId.Advantage2, isWritable: true),
                CreateLocation(DeviceId.Tko, isWritable: true));
            monitor.Poll();

            Assert.Equal(2, changes.Count);
            Assert.Equal(
                new[] { DeviceId.Advantage2, DeviceId.Tko }.Order(),
                changes.Select(change => change.DeviceId).Order());
        }

        [Fact]
        public void StatusChanged_WhenRaised_SnapshotAlreadyReflectsCurrentStatus()
        {
            using var monitor = CreateMonitor(out var scanner);
            var snapshotMatchedCurrent = false;

            monitor.StatusChanged += change =>
            {
                snapshotMatchedCurrent = monitor.Statuses[change.DeviceId].Equals(change.Current);
            };

            scanner.SetResult(CreateLocation(DeviceId.SavantElite2, isWritable: true));
            monitor.Poll();

            Assert.True(snapshotMatchedCurrent);
        }

        [Fact]
        public void Start_ThenStopAndDispose_DoesNotThrow()
        {
            var monitor = CreateMonitor(out _);

            monitor.Start();
            monitor.Start();
            monitor.Stop();
            monitor.Stop();
            monitor.Dispose();
            monitor.Dispose();
        }

        [Fact]
        public void Start_Always_RunsAnInitialPoll()
        {
            using var monitor = CreateMonitor(out var scanner);
            var changes = CollectChanges(monitor);
            scanner.SetResult(CreateLocation(DeviceId.FreestyleEdge, isWritable: true));

            monitor.Start();

            var change = Assert.Single(changes);
            Assert.Equal(DeviceId.FreestyleEdge, change.DeviceId);
            Assert.Equal(VDriveConnectionStatus.Connected, monitor.Statuses[DeviceId.FreestyleEdge].Status);
        }

        private static VDriveMonitor CreateMonitor(out FakeVDriveScanner scanner)
        {
            scanner = new FakeVDriveScanner();

            return new VDriveMonitor(scanner, TimeSpan.FromHours(1));
        }

        private static List<VDriveStatusChange> CollectChanges(VDriveMonitor monitor)
        {
            var changes = new List<VDriveStatusChange>();

            monitor.StatusChanged += changes.Add;

            return changes;
        }

        private static VDriveLocation CreateLocation(DeviceId deviceId, bool isWritable)
        {
            return new VDriveLocation
            {
                Device = DeviceCatalog.GetById(deviceId),
                RootPath = Path.Combine("volumes", deviceId.ToString()),
                IsWritable = isWritable
            };
        }
    }
}
