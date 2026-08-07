using KinesisEdit.Core.Devices;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    public class NoDeviceViewModelTests
    {
        [Fact]
        public void Devices_Always_OffersEveryProgrammableCatalogDeviceInTheMockupsOrder()
        {
            // docs/design/mockups.md §1d lists the seven in this order. DeviceCatalog.All is in
            // legacy-app-id order, which puts the pedal first and interleaves the families.
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                new[]
                {
                    DeviceId.Advantage2,
                    DeviceId.Advantage360,
                    DeviceId.FreestyleEdge,
                    DeviceId.FreestylePro,
                    DeviceId.FreestyleEdgeRgb,
                    DeviceId.Tko,
                    DeviceId.SavantElite2
                },
                viewModel.Devices.Select(option => option.Id));
        }

        [Fact]
        public void Devices_Always_CoverExactlyTheProgrammableCatalog()
        {
            // The order above is hand-written; the membership is not. A board added to the catalog
            // has to reach the picker even before anybody thinks to place it.
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                DeviceCatalog.All.Where(device => device.IsProgrammable).Select(device => device.Id).Order(),
                viewModel.Devices.Select(option => option.Id).Order());
        }

        [Fact]
        public void Devices_Always_TagOnlyTheAdvantage2AsTheDefault()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            var tagged = Assert.Single(viewModel.Devices, option => option.IsDefault);

            Assert.Equal(DeviceId.Advantage2, tagged.Id);
            Assert.Equal("default", NoDeviceViewModel.DefaultTagCaption);
        }

        [Fact]
        public void Devices_ForThePedal_CarryTheMockupsQualifier()
        {
            // "Savant Elite 2 (pedal)" (mockup §1d) — derived from the form factor, so it is not a
            // per-device string in the view.
            var viewModel = CreateViewModel(out _, out _, out _);

            var pedal = viewModel.Devices.Single(option => option.Id == DeviceId.SavantElite2);
            var keyboard = viewModel.Devices.Single(option => option.Id == DeviceId.Advantage2);

            Assert.Equal("Savant Elite 2 (pedal)", pedal.Caption);
            Assert.Equal("Advantage2", keyboard.Caption);
        }

        [Fact]
        public void SelectedDevice_ByDefault_IsTheAdvantage2()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(DeviceId.Advantage2, viewModel.SelectedDevice.Id);
            Assert.True(viewModel.SelectedOption.IsDefault);
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

        [Fact]
        public void Body_Always_QuotesTheMockup()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal(
                "KinesisEdit is watching for a v-Drive and will pick one up the moment it appears — "
                + "no need to press anything. Meanwhile, pick your device for connection steps, or "
                + "work without hardware.",
                viewModel.Body);
        }

        [Fact]
        public void PickerCopy_Always_QuotesTheMockup()
        {
            Assert.Equal("Which device do you have?", NoDeviceViewModel.PickerTitle);
            Assert.Equal(
                "Your pick drives the steps at right and which board Demo Mode opens.",
                NoDeviceViewModel.PickerHelperText);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "Program + F1")]
        [InlineData(DeviceId.FreestyleEdge, "SmartSet + F8")]
        [InlineData(DeviceId.FreestylePro, "SmartSet + F8")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "SmartSet + F8")]
        [InlineData(DeviceId.Tko, "SmartSet + Right Shift + V")]
        [InlineData(DeviceId.Advantage360, "SmartSet + v-Drive")]
        public void Steps_PerKeyboard_NameThatBoardsOnboardShortcut(DeviceId deviceId, string shortcut)
        {
            // Spec 11 §11.8's second row quotes "+ F8" for the TKO alongside the RGB; the catalog
            // carries the real "SmartSet + Right Shift + V" (specs/03-vdrive-and-files.md §1) and
            // that is what the step is built from.
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            var mount = Assert.Single(viewModel.Steps, step => step.ValueText == shortcut);

            Assert.Equal("On the keyboard, press ", mount.LeadText);
            Assert.Equal(" to mount the v-Drive.", mount.TrailText);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "ADVANTAGE2")]
        [InlineData(DeviceId.Advantage360, "ADV360")]
        [InlineData(DeviceId.FreestyleEdge, "FS EDGE")]
        [InlineData(DeviceId.FreestylePro, "FS PRO")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "FS EDGE RGB")]
        [InlineData(DeviceId.Tko, "TKO")]
        [InlineData(DeviceId.SavantElite2, "SE2")]
        public void Steps_PerDevice_EndByNamingThatDevicesPrimaryVolumeLabel(DeviceId deviceId, string label)
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            var last = viewModel.Steps[^1];

            Assert.Equal("A drive named ", last.LeadText);
            Assert.Equal(label, last.ValueText);
            Assert.Equal(
                " appears. This screen will replace itself with your device card automatically.",
                last.TrailText);
        }

        [Fact]
        public void Steps_ForTheAdvantage2_CarryThePowerUserModeStep()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.Advantage2);

            var step = Assert.Single(viewModel.Steps, candidate => candidate.Text.Contains("Power User Mode", StringComparison.Ordinal));

            Assert.Equal("Program + Shift + Esc", step.ValueText);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.SavantElite2)]
        public void Steps_ForEveryOtherDevice_DoNotMentionPowerUserMode(DeviceId deviceId)
        {
            // Spec 11.8's first row groups the FS boards and the SE2 pedal with the Adv2 verbatim,
            // and is wrong about both: the Freestyle boards have no such mode, and the pedal has no
            // keys to press it with — the same copy-paste artifact that already forced the shortcut
            // templating and the pedal's slide-switch step. Telling a user to press a key their
            // hardware does not have is the one failure this panel exists to prevent.
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            var text = string.Join(" ", viewModel.Steps.Select(step => step.Text));

            Assert.DoesNotContain("Power User Mode", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Program + Shift + Esc", text, StringComparison.Ordinal);
        }

        [Fact]
        public void Steps_ForThePedal_UseTheSlideSwitchRatherThanAKeyCombination()
        {
            // specs/12-savant-elite.md §3 quotes "Program + F1" for the SE2, which is legacy
            // copy-paste text for a device with no keyboard; §1 documents the real mechanism, a
            // physical slide switch between "play mode" and programming mode, and §3's own remedy
            // for a drive that does not appear is to replug the pedal.
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.SavantElite2);

            var text = string.Join(" ", viewModel.Steps.Select(step => step.Text));

            Assert.Contains("Slide the switch on the pedal from play mode to programming mode.", text, StringComparison.Ordinal);
            Assert.Contains("unplug the pedal and plug it back in", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Program + F1", text, StringComparison.Ordinal);
            Assert.Contains("Plug the pedal into this computer with its USB cable.", text, StringComparison.Ordinal);
        }

        [Theory]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.Advantage360)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.FreestyleEdgeRgb)]
        [InlineData(DeviceId.Tko)]
        [InlineData(DeviceId.SavantElite2)]
        public void Steps_ForEveryDevice_AreNumberedFromOneWithoutAGap(DeviceId deviceId)
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.Equal(
                Enumerable.Range(1, viewModel.Steps.Count),
                viewModel.Steps.Select(step => step.Number));
            Assert.All(viewModel.Steps, step => Assert.NotEqual(string.Empty, step.Text));
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "Get an Advantage2 into a detectable state — 4 steps")]
        [InlineData(DeviceId.Advantage360, "Get an Advantage 360 into a detectable state — 3 steps")]
        [InlineData(DeviceId.FreestyleEdge, "Get a Freestyle Edge into a detectable state — 3 steps")]
        [InlineData(DeviceId.FreestylePro, "Get a Freestyle Pro into a detectable state — 3 steps")]
        [InlineData(DeviceId.FreestyleEdgeRgb, "Get a Freestyle Edge RGB into a detectable state — 3 steps")]
        [InlineData(DeviceId.Tko, "Get a TKO into a detectable state — 3 steps")]
        [InlineData(DeviceId.SavantElite2, "Get a Savant Elite 2 into a detectable state — 3 steps")]
        public void StepsTitle_PerDevice_AgreesWithItsArticleAndItsStepCount(DeviceId deviceId, string expected)
        {
            // "Get an Advantage2 into a detectable state — 3 steps" (mockup §1d). The Advantage2
            // alone carries one step more than the mockup draws, because Power User Mode is its own
            // action rather than a clause hidden in the mount step.
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expected, viewModel.StepsTitle);
            Assert.Equal(expected, NoDeviceViewModel.FormatStepsTitle(viewModel.SelectedDevice.DisplayName, viewModel.Steps.Count));
        }

        [Fact]
        public void FormatStepsTitle_WithASingleStep_DropsThePlural()
        {
            Assert.Equal("Get a TKO into a detectable state — 1 step", NoDeviceViewModel.FormatStepsTitle("TKO", 1));
        }

        [Fact]
        public void RescanText_BeforeAnyPassCompletes_CountsFromTheConstructionBaseline()
        {
            // The baseline makes the sentence true: the loop has been running since the app
            // started, and this screen only claims the passes that happened while it was open.
            var viewModel = CreateViewModel(out _, out _, out _, completedRefreshBaseline: 41);

            Assert.Equal(0, viewModel.RescanCount);
            Assert.Equal("Still watching · rescanned 0 times since you opened this window", viewModel.RescanText);
        }

        [Fact]
        public void RescanText_AfterOnePass_IsSingular()
        {
            var viewModel = CreateViewModel(out _, out _, out _, completedRefreshBaseline: 41);

            viewModel.SetRefreshActivity(isRefreshing: false, completedRefreshCount: 42);

            Assert.Equal(1, viewModel.RescanCount);
            Assert.Equal("Still watching · rescanned 1 time since you opened this window", viewModel.RescanText);
        }

        [Fact]
        public void RescanText_AfterSeveralPasses_QuotesTheMockupsSentence()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SetRefreshActivity(isRefreshing: false, completedRefreshCount: 8);

            Assert.Equal("Still watching · rescanned 8 times since you opened this window", viewModel.RescanText);
        }

        [Fact]
        public void SetRefreshActivity_WhenTheCountMoves_NotifiesTheLine()
        {
            var viewModel = CreateViewModel(out _, out _, out _);
            var changed = new List<string?>();
            viewModel.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

            viewModel.SetRefreshActivity(isRefreshing: false, completedRefreshCount: 3);

            Assert.Contains(nameof(NoDeviceViewModel.RescanCount), changed);
            Assert.Contains(nameof(NoDeviceViewModel.RescanText), changed);
        }

        [Fact]
        public void ScanCaption_WhileAPassIsInFlight_SaysSoAndTheButtonRefuses()
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            Assert.Equal("Scan now", viewModel.ScanCaption);
            Assert.True(viewModel.ScanCommand.CanExecute(null));

            viewModel.SetRefreshActivity(isRefreshing: true, completedRefreshCount: 0);

            Assert.True(viewModel.IsRefreshing);
            Assert.Equal("Scanning", viewModel.ScanCaption);
            Assert.False(viewModel.ScanCommand.CanExecute(null));

            viewModel.SetRefreshActivity(isRefreshing: false, completedRefreshCount: 1);

            Assert.Equal("Scan now", viewModel.ScanCaption);
            Assert.True(viewModel.ScanCommand.CanExecute(null));
        }

        [Theory]
        [InlineData(DeviceId.Advantage2, "Launch Advantage2 in Demo Mode")]
        [InlineData(DeviceId.SavantElite2, "Launch Savant Elite 2 in Demo Mode")]
        [InlineData(DeviceId.Tko, "Launch TKO in Demo Mode")]
        public void DemoModeCaption_PerSelectedDevice_NamesThePick(DeviceId deviceId, string expected)
        {
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedDevice = DeviceCatalog.GetById(deviceId);

            Assert.Equal(expected, viewModel.DemoModeCaption);
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

            // No fixtures for this board, so no drive and an empty editor — exactly as before.
            Assert.Null(snapshot.Location);
        }

        [Fact]
        public void LaunchDemoModeCommand_ForABoardWithDemoContent_CarriesItsSyntheticDrive()
        {
            // The empty state is the second of the two places a demo snapshot is born, and it has
            // to answer the same way the detection loop does: the picked board opens over the
            // fixture v-Drive, unwritable, so its editor loads a real profile and saves nothing.
            var viewModel = CreateViewModel(out _, out var demoRequests, out _);
            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);

            viewModel.LaunchDemoModeCommand.Execute(null);

            var snapshot = Assert.Single(demoRequests);

            Assert.True(snapshot.IsDemoMode);
            Assert.Equal(DemoVDrive.GetRootPath(DeviceId.FreestyleEdgeRgb), snapshot.Location?.RootPath);
            Assert.False(snapshot.Location!.IsWritable);
        }

        [Fact]
        public void LaunchDemoModeCommand_AsksTheDemoGateAboutThePickRatherThanNamingABoard()
        {
            // Nothing on this screen knows which board has fixtures, and that is the invariant:
            // the gate is asked about whatever is picked, and a gate that answers for nothing
            // produces the location-less snapshot every board used to get.
            var demoDevices = new FakeDemoDeviceProvider();
            var viewModel = CreateViewModel(out _, out var demoRequests, out _, demoDevices: demoDevices);

            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.FreestyleEdgeRgb);
            viewModel.LaunchDemoModeCommand.Execute(null);

            Assert.Equal(DeviceId.FreestyleEdgeRgb, Assert.Single(demoDevices.AskedFor));
            Assert.Null(Assert.Single(demoRequests).Location);
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

            Assert.Contains(nameof(NoDeviceViewModel.SelectedDevice), changed);
            Assert.Contains(nameof(NoDeviceViewModel.Title), changed);
            Assert.Contains(nameof(NoDeviceViewModel.Steps), changed);
            Assert.Contains(nameof(NoDeviceViewModel.StepsTitle), changed);
            Assert.Contains(nameof(NoDeviceViewModel.DemoModeCaption), changed);
            Assert.Contains(nameof(NoDeviceViewModel.TroubleshootingUrl), changed);
        }

        [Fact]
        public void SelectedOption_WhenMovedByThePicker_MovesTheDeviceWithIt()
        {
            // The list selects rows; everything else asks about the device. The two are one value.
            var viewModel = CreateViewModel(out _, out _, out _);

            viewModel.SelectedOption = viewModel.Devices.Single(option => option.Id == DeviceId.Tko);

            Assert.Equal(DeviceId.Tko, viewModel.SelectedDevice.Id);

            viewModel.SelectedDevice = DeviceCatalog.GetById(DeviceId.FreestylePro);

            Assert.Same(
                viewModel.Devices.Single(option => option.Id == DeviceId.FreestylePro),
                viewModel.SelectedOption);
        }

        [Fact]
        public void SelectedOption_WhenThePickerClearsItsSelection_KeepsThePick()
        {
            // A single-selection ListBox drops the row that is already selected on a Ctrl/Cmd-click
            // and the two-way binding writes that through as null. There is no "nothing picked"
            // state on this screen: the title, the steps, the demo target and the support link all
            // describe one board, so a null is refused rather than stored — and refused quietly,
            // because an exception thrown out of a binding is swallowed and logged, not surfaced.
            var viewModel = CreateViewModel(out _, out _, out _);
            var picked = viewModel.Devices.Single(option => option.Id == DeviceId.Tko);
            var changed = new List<string?>();

            viewModel.SelectedOption = picked;
            viewModel.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            viewModel.SelectedOption = null!;

            Assert.Same(picked, viewModel.SelectedOption);
            Assert.Equal(DeviceId.Tko, viewModel.SelectedDevice.Id);

            // ...and it says so, so the view can put the row's selected face back.
            Assert.Contains(nameof(NoDeviceViewModel.SelectedOption), changed);
        }

        [Fact]
        public void ButtonCaptions_Always_MatchTheMockup()
        {
            Assert.Equal("Scan now", NoDeviceViewModel.ScanButtonCaption);
            Assert.Equal("Scanning", NoDeviceViewModel.ScanningButtonCaption);
            Assert.Equal("Troubleshooting tips", NoDeviceViewModel.TroubleshootingButtonCaption);
        }

        [Fact]
        public void TroubleshootingButtonCaption_Always_LeavesTheArrowToTheIconSystem()
        {
            // Mockup §1d writes "Troubleshooting tips ↗". The arrow is IconExternalLink beside the
            // caption, not a character inside it — the shipped font is not asked to carry it.
            Assert.DoesNotContain('↗', NoDeviceViewModel.TroubleshootingButtonCaption);
        }

        private static NoDeviceViewModel CreateViewModel(
            out FakeUrlLauncher urlLauncher,
            out List<DeviceSnapshot> demoRequests,
            out ScanCounter scanCounter,
            int completedRefreshBaseline = 0,
            IDemoDeviceProvider? demoDevices = null)
        {
            urlLauncher = new FakeUrlLauncher();
            demoRequests = [];
            scanCounter = new ScanCounter();
            var requests = demoRequests;
            var counter = scanCounter;

            return new NoDeviceViewModel(
                urlLauncher,
                requests.Add,
                counter.IncrementAsync,
                completedRefreshBaseline,
                demoDevices: demoDevices);
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
