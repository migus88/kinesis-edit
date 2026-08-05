using KinesisEdit.Core.Devices;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class NoDeviceViewModelTests
    {
        private const string PowerUserInstruction = "Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by first enabling Power User Mode (if necessary) using the onboard shortcut Program + Shift + Esc, and then connecting the v-Drive using the shortcut {0}. Please connect the v-Drive and then click the \"Scan for v-Drive\" button below.";
        private const string ShortcutInstruction = "Before launching the SmartSet App it is necessary to connect the keyboard's v-Drive to your PC by using the onboard shortcut {0}. Please connect the v-Drive and then click the \"Scan for v-Drive\" button below.";

        [Fact]
        public void Devices_Always_OffersEveryProgrammableCatalogDevice()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                new[]
                {
                    DeviceId.SavantElite2,
                    DeviceId.Advantage2,
                    DeviceId.FreestyleEdge,
                    DeviceId.FreestylePro,
                    DeviceId.FreestyleEdgeRgb,
                    DeviceId.Tko,
                    DeviceId.Advantage360
                },
                viewModel.Devices.Select(device => device.Id));
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "Keyboard not detected")]
        [InlineData(DeviceId.FreestyleEdge, "Keyboard not detected")]
        [InlineData(DeviceId.Advantage360, "Keyboard not detected")]
        [InlineData(DeviceId.SavantElite2, "Pedal not detected")]
        public void Title_PerSelectedDevice_UsesTheSpecCaption(DeviceId deviceId, string expected)
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expected, viewModel.Title);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "Program + F1")]
        [InlineData(DeviceId.SavantElite2, "Program + F1")]
        public void InstructionText_ForPowerUserDevices_QuotesTheSpecTextWithTheDeviceShortcut(DeviceId deviceId, string shortcut)
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.Equal(string.Format(PowerUserInstruction, shortcut), viewModel.InstructionText);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge, "SmartSet + F8")]
        [InlineData(DeviceId.FreestylePro, "SmartSet + F8")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "SmartSet + F8")]
        [InlineData(DeviceId.Tko, "SmartSet + Right Shift + V")]
        [InlineData(DeviceId.Advantage360, "SmartSet + v-Drive")]
        public void InstructionText_ForShortcutDevices_QuotesTheSpecTextWithTheDeviceShortcut(DeviceId deviceId, string shortcut)
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.Equal(string.Format(ShortcutInstruction, shortcut), viewModel.InstructionText);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        public void InstructionText_ForFreestyleBoards_DoesNotMentionPowerUserMode(DeviceId deviceId)
        {
            // Spec 11.8 groups the FS boards with the Adv2 row verbatim, but Power User Mode and
            // its "Program + Shift + Esc" shortcut are Adv2/SE2 concepts the Freestyle boards do
            // not have — the same copy-paste artifact that already forced the shortcut templating.
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.DoesNotContain("Power User Mode", viewModel.InstructionText, StringComparison.Ordinal);
            Assert.DoesNotContain("Program + Shift + Esc", viewModel.InstructionText, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/")]
        [InlineData(DeviceId.Tko, "https://gaming.kinesis-ergo.com/tko-support/")]
        [InlineData(DeviceId.FreestyleEdge, "https://gaming.kinesis-ergo.com/fs-edge-support/")]
        [InlineData(DeviceId.FreestylePro, "https://kinesis-ergo.com/support/freestyle-pro/")]
        [InlineData(DeviceId.Advantage2, "https://kinesis-ergo.com/support/advantage2/")]
        [InlineData(DeviceId.SavantElite2, "https://kinesis-ergo.com/support/savant-elite2/")]
        public void TroubleshootingTipsCommand_PerSelectedDevice_OpensThatDevicesSupportPage(DeviceId deviceId, string expectedUrl)
        {
            var viewModel = CreateViewModel(out var urlLauncher, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);
            viewModel.TroubleshootingTipsCommand.Execute(null);

            Assert.Equal(expectedUrl, viewModel.TroubleshootingUrl);
            Assert.Equal(expectedUrl, Assert.Single(urlLauncher.OpenedUrls));
        }

        [Fact]
        public void LaunchDemoModeCommand_WhenExecuted_RequestsTheSelectedDeviceInDemoMode()
        {
            var viewModel = CreateViewModel(out _, out var demoRequests, out _);
            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.Advantage360);

            viewModel.LaunchDemoModeCommand.Execute(null);

            var snapshot = Assert.Single(demoRequests);
            Assert.Equal(DeviceId.Advantage360, snapshot.DeviceId);
            Assert.True(snapshot.IsDemoMode);
            Assert.True(snapshot.Firmware.IsDemoMode);
            Assert.Null(snapshot.Location);
        }

        [Fact]
        public async Task ScanCommand_WhenExecuted_RequestsAScan()
        {
            var viewModel = CreateViewModel(out _, out _, out var scanCounter);

            await viewModel.ScanCommand.ExecuteAsync(null);

            Assert.Equal(1, scanCounter.Count);
        }

        [Fact]
        public void SelectedDevice_WhenChanged_NotifiesTheDerivedText()
        {
            var viewModel = CreateViewModel(out _, out _, out _);
            var changed = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.SavantElite2);

            Assert.Contains(nameof(NoDeviceViewModel.Title), changed);
            Assert.Contains(nameof(NoDeviceViewModel.InstructionText), changed);
            Assert.Contains(nameof(NoDeviceViewModel.TroubleshootingUrl), changed);
        }

        [Fact]
        public void ButtonCaptions_Always_MatchTheSpec()
        {
            Assert.Equal("Scan for v-Drive", NoDeviceViewModel.ScanButtonCaption);
            Assert.Equal("Launch in Demo Mode", NoDeviceViewModel.DemoModeButtonCaption);
            Assert.Equal("Troubleshooting Tips", NoDeviceViewModel.TroubleshootingButtonCaption);
        }

        private static NoDeviceViewModel CreateViewModel(
            out FakeUrlLauncher urlLauncher,
            out List<DeviceSnapshot> demoRequests,
            out ScanCounter scanCounter)
        {
            urlLauncher = new FakeUrlLauncher();
            demoRequests = [];
            scanCounter = new ScanCounter();
            var requests = demoRequests;
            var counter = scanCounter;

            return new NoDeviceViewModel(urlLauncher, requests.Add, counter.IncrementAsync);
        }

        private sealed class ScanCounter
        {
            public int Count { get; private set; }

            public Task IncrementAsync()
            {
                Count++;

                return Task.CompletedTask;
            }
        }
    }
}
