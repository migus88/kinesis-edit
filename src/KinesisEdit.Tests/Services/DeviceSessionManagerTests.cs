using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    public class DeviceSessionManagerTests
    {
        [Fact]
        public void Begin_WithConnectedDevice_UsesTheVDriveBackedStore()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());

            var session = manager.Begin(TestDevices.CreateSnapshot(DeviceId.FreestyleEdgeRgb));

            Assert.Same(session, manager.Active);
            Assert.False(session.IsDemoMode);
            Assert.IsType<VDriveNotificationSuppressionStore>(session.SuppressionStore);
        }

        [Fact]
        public void Begin_WithoutAnyDrive_UsesTheNullStore()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());

            var session = manager.Begin(TestDevices.CreateSnapshot(DeviceId.Advantage2, VDriveConnectionStatus.NotDetected));

            Assert.True(session.IsDemoMode);
            Assert.IsType<NullNotificationSuppressionStore>(session.SuppressionStore);
        }

        [Fact]
        public void Begin_WithUnwritableDrive_ReadsTheDrivesSettingsButNeverWritesThem()
        {
            // specs/08-settings.md §3 bans saving app_settings.txt in demo mode, not loading it:
            // notifications the user hid on this drive must stay hidden.
            var fileService = new FakeVDriveFileService();
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Advantage2, VDriveConnectionStatus.CannotAccess);
            fileService.SetFile(
                VDriveNotificationSuppressionStore.GetFilePath(snapshot.Location!),
                NotificationKeys.Save + "=on");
            var manager = new DeviceSessionManager(fileService);

            var session = manager.Begin(snapshot);
            session.SuppressionStore.SetHidden(NotificationKeys.Save, false);

            Assert.True(session.IsDemoMode);
            Assert.IsType<ReadOnlyNotificationSuppressionStore>(session.SuppressionStore);
            Assert.True(session.SuppressionStore.IsHidden(NotificationKeys.Save));
            Assert.Empty(fileService.WrittenPaths);
        }

        [Fact]
        public void UpdateActive_WithANewerSnapshot_MovesTheSessionWithoutChangingDemoMode()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());
            var session = manager.Begin(TestDevices.CreateSnapshot(DeviceId.Tko));
            var store = session.SuppressionStore;
            var lost = TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.NotDetected);

            manager.UpdateActive([lost]);

            Assert.Same(lost, session.Device);
            Assert.False(session.IsDemoMode);
            Assert.Equal(VDriveHealth.Error, session.Health);
            Assert.Same(store, session.SuppressionStore);
        }

        [Fact]
        public void UpdateActive_WhenAFreestyleSessionReresolves_StillMatchesTheScannedSlot()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());
            var session = manager.Begin(TestDevices.CreateSnapshot(DeviceId.FreestylePro));
            var reresolved = TestDevices.CreateSnapshot(DeviceId.FreestylePro) with
            {
                Device = DeviceCatalog.GetById(DeviceId.FreestyleEdge)
            };

            manager.UpdateActive([reresolved]);

            Assert.Same(reresolved, session.Device);
        }

        [Fact]
        public void UpdateActive_WithoutTheSessionsDevice_LeavesItAlone()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());
            var opened = TestDevices.CreateSnapshot(DeviceId.Tko);
            var session = manager.Begin(opened);

            manager.UpdateActive([TestDevices.CreateSnapshot(DeviceId.Advantage2)]);

            Assert.Same(opened, session.Device);
        }

        [Fact]
        public void UpdateActive_WithoutAnActiveSession_DoesNothing()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());

            manager.UpdateActive([TestDevices.CreateSnapshot(DeviceId.Tko)]);

            Assert.Null(manager.Active);
        }

        [Fact]
        public void End_WithActiveSession_ClearsItAndNotifies()
        {
            var manager = new DeviceSessionManager(new FakeVDriveFileService());
            var sessions = new List<DeviceSession?>();
            manager.ActiveChanged += session => sessions.Add(session);

            manager.Begin(TestDevices.CreateSnapshot(DeviceId.Tko));
            manager.End();
            manager.End();

            Assert.Null(manager.Active);
            Assert.Equal(2, sessions.Count);
            Assert.NotNull(sessions[0]);
            Assert.Null(sessions[1]);
        }
    }
}
