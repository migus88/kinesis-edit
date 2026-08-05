using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The device card of specs/10-apps-and-ui.md: status wording, the Configure / Demo Mode
    /// button, and the secondary button that always rescans for the v-Drive.
    /// </summary>
    public class DeviceCardViewModelTests
    {
        private const string ScanCaption = "Scan for v-Drive";

        [Theory]
        [InlineData(VDriveConnectionStatus.Connected, "Connected", StatusSeverity.Ok)]
        [InlineData(VDriveConnectionStatus.NotDetected, "Not Detected", StatusSeverity.Error)]
        [InlineData(VDriveConnectionStatus.CannotAccess, "Cannot Access", StatusSeverity.Error)]
        public void StatusText_PerConnectionState_UsesTheSpecWording(VDriveConnectionStatus status, string expectedText, StatusSeverity expectedSeverity)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, status), out _, out _);

            Assert.Equal(expectedText, card.StatusText);
            Assert.Equal(expectedSeverity, card.StatusSeverity);
        }

        [Fact]
        public void PrimaryActionCaption_WhenConnected_IsConfigure()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            Assert.Equal("Configure", card.PrimaryActionCaption);
            Assert.False(card.IsDemoMode);
        }

        [Fact]
        public void PrimaryActionCaption_WhenDriveIsNotWritable_IsDemoMode()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess), out _, out _);

            Assert.Equal("Demo Mode", card.PrimaryActionCaption);
            Assert.True(card.IsDemoMode);
        }

        [Fact]
        public void CanEject_WhenEjectionIsUnsupported_IsFalse()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out var ejectService, out _);
            ejectService.IsSupported = false;

            Assert.False(card.CanEject);
            Assert.False(card.EjectCommand.CanExecute(null));
        }

        [Fact]
        public void CanEject_WhenConnectedAndSupported_IsTrue()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);

            Assert.True(card.CanEject);
            Assert.True(card.EjectCommand.CanExecute(null));
        }

        [Fact]
        public void CanEject_WhenDriveIsNotWritable_IsFalse()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess), out _, out _);

            Assert.False(card.CanEject);
        }

        [Fact]
        public async Task EjectCommand_WhenExecuted_EjectsTheCardsDrive()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var card = CreateCard(snapshot, out var ejectService, out _);

            await card.EjectCommand.ExecuteAsync(null);

            Assert.Equal(snapshot.Location!.RootPath, Assert.Single(ejectService.EjectedPaths));
        }

        [Fact]
        public void ConfigureCommand_WhenExecuted_RequestsTheCardsSnapshot()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var requested = new List<DeviceSnapshot>();
            var card = new DeviceCardViewModel(
                snapshot,
                new VDriveEjectNotifier(new FakeDeviceEjectService(), new FakeNotificationService()),
                requested.Add,
                () => Task.CompletedTask);

            card.ConfigureCommand.Execute(null);

            Assert.Same(snapshot, Assert.Single(requested));
        }

        /// <summary>
        /// The secondary button is the rescan button for every device and every connection state,
        /// including the connected Freestyle Edge RGB that used to swap to 'Check for Updates'.
        /// </summary>
        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.FreestyleEdgeRgb, VDriveConnectionStatus.CannotAccess)]
        [InlineData(DeviceId.Tko, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.Advantage360, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.FreestyleEdge, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.FreestylePro, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.Advantage2, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.SavantElite2, VDriveConnectionStatus.Connected)]
        [InlineData(DeviceId.Tko, VDriveConnectionStatus.CannotAccess)]
        [InlineData(DeviceId.Tko, VDriveConnectionStatus.NotDetected)]
        public async Task SecondaryAction_ForEveryDeviceAndStatus_ScansForTheVDrive(
            DeviceId deviceId,
            VDriveConnectionStatus status)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(deviceId, status), out _, out var scans);

            Assert.Equal(ScanCaption, card.SecondaryActionCaption);

            await card.SecondaryActionCommand.ExecuteAsync(null);

            Assert.Single(scans);
        }

        [Fact]
        public void Update_WithNewStatus_NotifiesEveryDerivedValue()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko), out _, out _);
            var changed = new List<string?>();
            card.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            card.Update(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess));

            Assert.Equal("Cannot Access", card.StatusText);
            Assert.Equal("Demo Mode", card.PrimaryActionCaption);
            Assert.False(card.CanEject);
            Assert.Contains(nameof(DeviceCardViewModel.StatusText), changed);
            Assert.Contains(nameof(DeviceCardViewModel.PrimaryActionCaption), changed);
            Assert.Contains(nameof(DeviceCardViewModel.CanEject), changed);
        }

        [Fact]
        public void Update_WithTheSameSnapshot_NotifiesNothing()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var card = CreateCard(snapshot, out _, out _);
            var changed = new List<string?>();
            card.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            card.Update(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Empty(changed);
        }

        private static DeviceCardViewModel CreateCard(
            DeviceSnapshot snapshot,
            out FakeDeviceEjectService ejectService,
            out List<int> scans)
        {
            ejectService = new FakeDeviceEjectService();

            var scanCalls = new List<int>();
            scans = scanCalls;

            return new DeviceCardViewModel(
                snapshot,
                new VDriveEjectNotifier(ejectService, new FakeNotificationService()),
                _ => { },
                () =>
                {
                    scanCalls.Add(scanCalls.Count);

                    return Task.CompletedTask;
                });
        }
    }
}
