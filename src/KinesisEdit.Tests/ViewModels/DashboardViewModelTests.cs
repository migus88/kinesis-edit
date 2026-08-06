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
        private readonly FakeUrlLauncher _urlLauncher = new();
        private readonly DeviceMonitorService _monitor;
        private readonly DashboardViewModel _dashboard;

        public DashboardViewModelTests()
        {
            _monitor = new DeviceMonitorService(
                new VDriveMonitor(_scanner, _neverPolls),
                _fileService,
                new FakeUiDispatcher(),
                new FakeSystemClock(),
                _neverPolls);

            _dashboard = new DashboardViewModel(
                _monitor,
                new VDriveEjectNotifier(new FakeDeviceEjectService(), new FakeNotificationService()),
                _urlLauncher);
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
        public void IsEmpty_WhenADriveAppears_DropsTheEmptyStateForTheDeviceCard()
        {
            // The empty state's own promise: "This screen will replace itself with your device
            // card automatically." Nothing is pressed here — the loop's next pass is the whole
            // mechanism, and this is the regression guard over it.
            _monitor.Refresh();

            Assert.True(_dashboard.IsEmpty);

            SetDrives(CreateDrive(DeviceId.Tko));

            _monitor.Refresh();

            Assert.False(_dashboard.IsEmpty);
            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.DeviceCards).DeviceId);
        }

        [Fact]
        public void EmptyState_AfterEachPass_CountsTheRescansSinceTheScreenOpened()
        {
            // The baseline is captured when the dashboard is built, so the sentence is true of
            // this window rather than of the process.
            Assert.Equal(0, _dashboard.EmptyState.RescanCount);

            _monitor.Refresh();

            Assert.Equal(1, _dashboard.EmptyState.RescanCount);

            _monitor.Refresh();
            _monitor.Refresh();

            Assert.Equal(3, _dashboard.EmptyState.RescanCount);
            Assert.Equal(
                "Still watching · rescanned 3 times since you opened this window",
                _dashboard.EmptyState.RescanText);
        }

        [Fact]
        public void EmptyState_WhenBuiltOverALoopThatHasAlreadyRun_StartsCountingFromZero()
        {
            _monitor.Refresh();
            _monitor.Refresh();

            using var dashboard = new DashboardViewModel(
                _monitor,
                new VDriveEjectNotifier(new FakeDeviceEjectService(), new FakeNotificationService()),
                _urlLauncher);

            Assert.Equal(0, dashboard.EmptyState.RescanCount);

            _monitor.Refresh();

            Assert.Equal(1, dashboard.EmptyState.RescanCount);
        }

        [Fact]
        public async Task EmptyState_WhileAPassIsInFlight_SaysSoOnItsScanButton()
        {
            // The same fact the cards render as Scanning, on the one button this screen has for it.
            _monitor.Refresh();

            Assert.Equal(NoDeviceViewModel.ScanButtonCaption, _dashboard.EmptyState.ScanCaption);

            using var gate = new ManualResetEventSlim(false);
            _scanner.Gate = gate;

            var scan = _dashboard.ScanAsync();

            Assert.True(SpinWait.SpinUntil(() => _dashboard.EmptyState.IsRefreshing, TimeSpan.FromSeconds(5)));
            Assert.Equal(NoDeviceViewModel.ScanningButtonCaption, _dashboard.EmptyState.ScanCaption);
            Assert.False(_dashboard.EmptyState.ScanCommand.CanExecute(null));

            gate.Set();
            await scan;

            Assert.Equal(NoDeviceViewModel.ScanButtonCaption, _dashboard.EmptyState.ScanCaption);
            Assert.True(_dashboard.EmptyState.ScanCommand.CanExecute(null));
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
                _dashboard.DeviceCards.Select(card => card.DeviceId));
            Assert.True(_dashboard.HasDevices);
            Assert.False(_dashboard.IsEmpty);
        }

        [Fact]
        public void Devices_WhenADriveAppears_AddsItsCard()
        {
            _monitor.Refresh();
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));

            _monitor.Refresh();

            var card = Assert.Single(_dashboard.DeviceCards);
            Assert.Equal(DeviceId.FreestyleEdgeRgb, card.DeviceId);
            Assert.Equal(DeviceCardState.Connected, card.State);
        }

        /// <summary>
        /// The roster is session-lifetime: a device detected once keeps its card and falls to
        /// Resting when its drive goes away. Removing the card instead would make the "a device
        /// appearing animates in at the end of the list" choreography unreachable, because a device
        /// would only ever go Resting → Connected in place.
        /// </summary>
        [Fact]
        public void Devices_WhenADriveDisappears_KeepsTheCardResting()
        {
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.DeviceCards);

            _scanner.SetResult();
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.DeviceCards));
            Assert.Equal(DeviceCardState.Resting, card.State);
            Assert.Equal(StatusSeverity.Unknown, card.StatusSeverity);
            Assert.True(_dashboard.HasDevices);
            Assert.False(_dashboard.IsEmpty);
        }

        [Fact]
        public void Devices_WhenADeviceWasNeverDetected_HasNoCardForIt()
        {
            SetDrives(CreateDrive(DeviceId.Tko));

            _monitor.Refresh();

            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.DeviceCards).DeviceId);
        }

        /// <summary>
        /// A device appearing mid-session lands at the end of the list and moves nothing above it.
        /// The Advantage2 precedes the TKO in catalog order, so a merge that re-sorted would put it
        /// first and shift the existing card down — exactly the reflow the insert animation exists
        /// to avoid.
        /// </summary>
        [Fact]
        public void Devices_WhenASecondDriveAppears_AppendsItWithoutMovingTheExistingCard()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var first = Assert.Single(_dashboard.DeviceCards);

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();

            Assert.Equal(
                new[] { DeviceId.Tko, DeviceId.Advantage2 },
                _dashboard.DeviceCards.Select(card => card.DeviceId));
            Assert.Same(first, _dashboard.DeviceCards[0]);
        }

        [Fact]
        public void Devices_AcrossRefreshes_UpdatesTheCardInPlaceWithoutDuplicates()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.DeviceCards);

            SetDrives(CreateDrive(DeviceId.Tko, isWritable: false));
            _monitor.Refresh();
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.DeviceCards));
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
                _dashboard.DeviceCards.Select(card => card.ScannedDeviceId));
            Assert.All(_dashboard.DeviceCards, card => Assert.Equal(DeviceId.FreestyleEdge, card.DeviceId));
        }

        [Fact]
        public void Devices_WhenAFreestyleDriveChangesModel_KeepsTheSameCard()
        {
            SetDrives(CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS Edge"));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.DeviceCards);

            SetDrives(CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS PRO"));
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.DeviceCards));
            Assert.Equal(DeviceId.FreestyleEdge, card.ScannedDeviceId);
            Assert.Equal(DeviceId.FreestylePro, card.DeviceId);
        }

        [Fact]
        public void Apply_WithSnapshotsSharingAKey_DoesNotThrow()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);

            _dashboard.Apply([snapshot, snapshot]);

            Assert.Single(_dashboard.DeviceCards);
        }

        [Fact]
        public void WebToolCard_WithAtLeastOneDeviceCard_IsPinnedLast()
        {
            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));

            _monitor.Refresh();

            var last = Assert.IsType<WebToolCardViewModel>(_dashboard.Devices[^1]);

            Assert.Equal(DeviceId.Advantage360Professional.ToString(), last.Key);
            Assert.Equal(2, _dashboard.DeviceCardCount);
            Assert.Equal(3, _dashboard.Devices.Count);
        }

        /// <summary>
        /// The Advantage 360 Professional exposes no v-Drive, so its card can never be produced by
        /// a scan — and counting it as a device would make <see cref="DashboardViewModel.HasDevices"/>
        /// permanently true and the troubleshoot empty state unreachable.
        /// </summary>
        [Fact]
        public void WebToolCard_WithNoDeviceCards_IsNotShownAndDoesNotCountAsADevice()
        {
            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
            Assert.Equal(0, _dashboard.DeviceCardCount);
            Assert.False(_dashboard.HasDevices);
            Assert.True(_dashboard.IsEmpty);
        }

        [Fact]
        public void WebToolCard_WhenTheLastDeviceCardGoesAway_IsWithdrawnWithIt()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            Assert.Equal(2, _dashboard.Devices.Count);

            // Apply directly with an empty roster: under the session-lifetime roster a drive going
            // away leaves its card behind, so this is the only way back to "no device cards".
            _dashboard.Apply([]);

            Assert.Empty(_dashboard.Devices);
            Assert.True(_dashboard.IsEmpty);
        }

        [Fact]
        public void WebToolCard_AcrossRefreshes_IsNeitherDuplicatedNorRecreated()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var card = _dashboard.Devices[^1];

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();
            _monitor.Refresh();

            Assert.Same(card, _dashboard.Devices[^1]);
            Assert.Single(_dashboard.Devices.OfType<WebToolCardViewModel>());
        }

        [Fact]
        public void HeaderSubtitle_CountsDetectedDevicesAgainstTheProgrammableCatalog()
        {
            Assert.Equal(7, DashboardViewModel.KnownDeviceCount);

            _monitor.Refresh();
            Assert.Equal("0 of 7 known devices present · list updates itself", _dashboard.HeaderSubtitle);

            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            Assert.Equal("1 of 7 known device present · list updates itself", _dashboard.HeaderSubtitle);

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();
            Assert.Equal("2 of 7 known devices present · list updates itself", _dashboard.HeaderSubtitle);
        }

        /// <summary>
        /// The subtitle counts drives that are present, not cards on screen: a card that fell to
        /// Resting is still rendered but its device is not there.
        /// </summary>
        [Fact]
        public void HeaderSubtitle_WhenADriveGoesAway_StopsCountingItThoughTheCardRemains()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            _scanner.SetResult();
            _monitor.Refresh();

            Assert.Single(_dashboard.DeviceCards);
            Assert.Equal("0 of 7 known devices present · list updates itself", _dashboard.HeaderSubtitle);
        }

        [Fact]
        public void HeaderCaptions_AreTheMockupsWording()
        {
            Assert.Equal("Devices", _dashboard.HeaderTitle);
            Assert.Equal("Scan all", _dashboard.ScanAllCaption);
        }

        /// <summary>
        /// The whole point of the deferral: three passes land while a card's button is under the
        /// pointer, nothing moves until the pointer leaves, and what lands then is the newest
        /// picture — including the device that first appeared in the second pass.
        /// </summary>
        [Fact]
        public void Apply_WhileSuspended_ParksEveryRefreshAndAppliesTheNewestOnResume()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var tko = Assert.Single(_dashboard.DeviceCards);

            _dashboard.SuspendRefresh();
            Assert.True(_dashboard.IsRefreshSuspended);

            SetDrives(CreateDrive(DeviceId.Tko, isWritable: false));
            _monitor.Refresh();

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();

            SetDrives(
                CreateDrive(DeviceId.Tko),
                CreateDrive(DeviceId.Advantage2, isWritable: false),
                CreateDrive(DeviceId.FreestyleEdgeRgb));
            _monitor.Refresh();

            // Nothing moved while the pointer was down: still one card, still Connected.
            Assert.Same(tko, Assert.Single(_dashboard.DeviceCards));
            Assert.Equal(DeviceCardState.Connected, tko.State);

            _dashboard.ResumeRefresh();

            Assert.False(_dashboard.IsRefreshSuspended);
            Assert.Equal(
                new[] { DeviceId.Tko, DeviceId.Advantage2, DeviceId.FreestyleEdgeRgb },
                _dashboard.DeviceCards.Select(card => card.DeviceId));

            // The newest pass wins, and the Advantage2 that only ever appeared in a parked pass is
            // on screen: the parked lists are complete pictures, so nothing was dropped with them.
            Assert.Equal(DeviceCardState.Connected, _dashboard.DeviceCards[0].State);
            Assert.Equal(DeviceCardState.CannotAccess, _dashboard.DeviceCards[1].State);
            Assert.Equal(DeviceCardState.Connected, _dashboard.DeviceCards[2].State);
        }

        /// <summary>
        /// The one field a parked refresh cannot supersede. "Detected at least once this session"
        /// is an <em>accumulation</em>, not a picture of now, so the newest parked list does not
        /// subsume its predecessors for it: a drive plugged in and pulled out again entirely inside
        /// one suspend window is detected in a list that is never applied, and only the last list —
        /// which reports it absent — ever reaches the roster. Without accumulating while parked the
        /// device gets no card at all, while the identical sequence with the pointer elsewhere
        /// leaves a Resting one.
        /// </summary>
        [Fact]
        public void Apply_WhenADriveAppearsAndGoesAwayWhileSuspended_StillLeavesARestingCard()
        {
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));
            _monitor.Refresh();

            _dashboard.SuspendRefresh();

            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb), CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));
            _monitor.Refresh();

            _dashboard.ResumeRefresh();

            Assert.Equal(
                new[] { DeviceId.FreestyleEdgeRgb, DeviceId.Tko },
                _dashboard.DeviceCards.Select(card => card.DeviceId));

            var tko = _dashboard.DeviceCards.Single(card => card.DeviceId == DeviceId.Tko);

            Assert.Equal(DeviceCardState.Resting, tko.State);

            // The subtitle still counts what is present *now*, which is the one drive that stayed.
            Assert.Equal("1 of 7 known device present · list updates itself", _dashboard.HeaderSubtitle);
        }

        /// <summary>
        /// The header's two numbers come from one predicate. The scanner walks the whole catalog,
        /// and the never-shipped CROSSFIRE keypad is not programmable but is detectable — it
        /// carries a volume label — so counting detection alone would produce an eighth card with
        /// an empty meta line beside a subtitle reading "1 of 7".
        /// </summary>
        [Fact]
        public void Apply_WithADetectedButUnprogrammableDevice_NeitherCardsItNorCountsIt()
        {
            Assert.False(DashboardViewModel.IsDashboardDevice(DeviceCatalog.GetById(DeviceId.CrossfireKeypad)));

            _dashboard.Apply([
                TestDevices.CreateSnapshot(DeviceId.CrossfireKeypad),
                TestDevices.CreateSnapshot(DeviceId.Tko)
            ]);

            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.DeviceCards).DeviceId);
            Assert.Equal("1 of 7 known device present · list updates itself", _dashboard.HeaderSubtitle);
        }

        [Fact]
        public void ResumeRefresh_WithNothingParked_ChangesNothing()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            var changed = new List<string?>();
            _dashboard.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
            var cards = _dashboard.Devices.ToArray();

            _dashboard.SuspendRefresh();
            _dashboard.ResumeRefresh();

            Assert.Equal(cards, _dashboard.Devices);
            Assert.Equal(
                new[] { nameof(DashboardViewModel.IsRefreshSuspended), nameof(DashboardViewModel.IsRefreshSuspended) },
                changed);
        }

        [Fact]
        public void SuspendRefresh_And_ResumeRefresh_AreSafeToCallRedundantly()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            _dashboard.ResumeRefresh();
            _dashboard.SuspendRefresh();
            _dashboard.SuspendRefresh();

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();

            Assert.Single(_dashboard.DeviceCards);

            // A single resume always un-sticks the list, however many suspends the view sent.
            _dashboard.ResumeRefresh();
            _dashboard.ResumeRefresh();

            Assert.Equal(2, _dashboard.DeviceCardCount);
            Assert.False(_dashboard.IsRefreshSuspended);
        }

        [Fact]
        public async Task IsRefreshing_WhileAPassIsInFlight_PutsEveryCardIntoTheScanningState()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            Assert.False(_dashboard.IsRefreshing);

            using var gate = new ManualResetEventSlim(false);
            _scanner.Gate = gate;

            var scan = _dashboard.ScanAsync();

            Assert.True(SpinWait.SpinUntil(() => _dashboard.IsRefreshing, TimeSpan.FromSeconds(5)));
            Assert.All(
                _dashboard.DeviceCards,
                card => Assert.Equal(DeviceCardState.Scanning, card.State));

            gate.Set();
            await scan;

            Assert.False(_dashboard.IsRefreshing);
            Assert.All(
                _dashboard.DeviceCards,
                card => Assert.Equal(DeviceCardState.Connected, card.State));
        }

        [Fact]
        public void ConfigureRequested_WhenACardIsConfigured_CarriesThatDevice()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var requested = new List<DeviceSnapshot>();
            _dashboard.ConfigureRequested += requested.Add;

            _dashboard.DeviceCards[0].ConfigureCommand.Execute(null);

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
        public void WebToolCard_WhenOpened_LaunchesTheVendorsConfigurator()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            var card = Assert.IsType<WebToolCardViewModel>(_dashboard.Devices[^1]);

            card.OpenWebToolCommand.Execute(null);

            Assert.Equal(
                DeviceCatalog.GetById(DeviceId.Advantage360Professional).ConfigurationUrl,
                Assert.Single(_urlLauncher.OpenedUrls));
        }

        [Fact]
        public async Task ScanCommand_WhenExecuted_RunsAnotherDetectionPass()
        {
            SetDrives(CreateDrive(DeviceId.Tko));

            await _dashboard.ScanCommand.ExecuteAsync(null);

            Assert.Single(_dashboard.DeviceCards);
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

            Assert.Single(_dashboard.DeviceCards);
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
