using KinesisEdit.Core.Devices;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The device card of specs/10-apps-and-ui.md: status wording, the Configure / Demo Mode
    /// button, and the secondary button that swaps to "Check for Updates" on a connected device
    /// whose app carries the firmware dialog (specs/09-firmware.md §3-§4).
    /// </summary>
    public class DeviceCardViewModelTests
    {
        private const string ScanCaption = "Scan for v-Drive";
        private const string CheckForUpdatesCaption = "Check for Updates";

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
                new FakeFirmwareUpdatePresenter(),
                requested.Add,
                () => Task.CompletedTask);

            card.ConfigureCommand.Execute(null);

            Assert.Same(snapshot, Assert.Single(requested));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        public void SecondaryActionCaption_ForAConnectedDeviceWithTheUpdateDialog_IsCheckForUpdates(DeviceId deviceId)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(deviceId), out _, out _);

            Assert.True(card.CanCheckForUpdates);
            Assert.Equal(CheckForUpdatesCaption, card.SecondaryActionCaption);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.SavantElite2)]
        public void SecondaryActionCaption_ForADeviceWithoutTheUpdateDialog_StaysScanForVDrive(DeviceId deviceId)
        {
            var card = CreateCard(TestDevices.CreateSnapshot(deviceId), out _, out _);

            Assert.False(card.CanCheckForUpdates);
            Assert.Equal(ScanCaption, card.SecondaryActionCaption);
        }

        [Fact]
        public void SecondaryActionCaption_WhenTheDriveIsNotWritable_StaysScanForVDrive()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess), out _, out _);

            Assert.False(card.CanCheckForUpdates);
            Assert.Equal(ScanCaption, card.SecondaryActionCaption);
        }

        [Fact]
        public async Task SecondaryActionCommand_ForAConnectedEligibleDevice_OpensTheUpdateDialog()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);
            var card = CreateCard(snapshot, out _, out var updatePresenter, out var scans);

            await card.SecondaryActionCommand.ExecuteAsync(null);

            Assert.Same(snapshot, Assert.Single(updatePresenter.PresentedDevices));
            Assert.Empty(scans);
        }

        [Fact]
        public async Task SecondaryActionCommand_ForAnIneligibleDevice_RunsAScan()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Advantage2), out _, out var updatePresenter, out var scans);

            await card.SecondaryActionCommand.ExecuteAsync(null);

            Assert.Single(scans);
            Assert.Empty(updatePresenter.PresentedDevices);
        }

        [Fact]
        public async Task SecondaryActionCommand_ForAnEligibleDeviceThatIsNotConnected_RunsAScan()
        {
            var card = CreateCard(
                TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess),
                out _,
                out var updatePresenter,
                out var scans);

            await card.SecondaryActionCommand.ExecuteAsync(null);

            Assert.Single(scans);
            Assert.Empty(updatePresenter.PresentedDevices);
        }

        [Fact]
        public void Update_WhenTheDriveBecomesConnected_SwapsTheSecondaryCaption()
        {
            var card = CreateCard(TestDevices.CreateSnapshot(DeviceId.Tko, VDriveConnectionStatus.CannotAccess), out _, out _);
            var changed = new List<string?>();
            card.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            card.Update(TestDevices.CreateSnapshot(DeviceId.Tko));

            Assert.Equal(CheckForUpdatesCaption, card.SecondaryActionCaption);
            Assert.Contains(nameof(DeviceCardViewModel.SecondaryActionCaption), changed);
            Assert.Contains(nameof(DeviceCardViewModel.CanCheckForUpdates), changed);
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
            out FakeFirmwareUpdatePresenter updatePresenter)
        {
            return CreateCard(snapshot, out ejectService, out updatePresenter, out _);
        }

        private static DeviceCardViewModel CreateCard(
            DeviceSnapshot snapshot,
            out FakeDeviceEjectService ejectService,
            out FakeFirmwareUpdatePresenter updatePresenter,
            out List<int> scans)
        {
            ejectService = new FakeDeviceEjectService();
            updatePresenter = new FakeFirmwareUpdatePresenter();

            var scanCalls = new List<int>();
            scans = scanCalls;

            return new DeviceCardViewModel(
                snapshot,
                new VDriveEjectNotifier(ejectService, new FakeNotificationService()),
                updatePresenter,
                _ => { },
                () =>
                {
                    scanCalls.Add(scanCalls.Count);

                    return Task.CompletedTask;
                });
        }
    }
}
