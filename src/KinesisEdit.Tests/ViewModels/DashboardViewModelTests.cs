using System.Reflection;
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
                new FakeUiDispatcher());

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
            _monitor.Refresh();

            Assert.True(_dashboard.IsEmpty);

            SetDrives(CreateDrive(DeviceId.Tko));

            _monitor.Refresh();

            Assert.False(_dashboard.IsEmpty);
            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.Devices).DeviceId);
        }

        /// <summary>
        /// Issue #118's headline bug. The roster used to be append-only — a device detected once
        /// kept its card for the life of the window — which made <see cref="DashboardViewModel.HasDevices"/>
        /// permanently true and the "Keyboard not detected" screen unreachable after the first
        /// device was ever seen. The roster is now what the last scan found, so unplugging the only
        /// board and scanning brings the empty state back.
        /// </summary>
        [Fact]
        public void Devices_WhenTheLastDriveDisappears_LosesItsCardAndReturnsTheEmptyState()
        {
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));
            _monitor.Refresh();

            Assert.Single(_dashboard.Devices);
            Assert.True(_dashboard.HasDevices);
            Assert.False(_dashboard.IsEmpty);

            _scanner.SetResult();
            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
            Assert.False(_dashboard.HasDevices);
            Assert.True(_dashboard.IsEmpty);
        }

        /// <summary>
        /// The same rule with a survivor: only the drive that went away loses its card, and the
        /// screen stays on the grid because something is still present.
        /// </summary>
        [Fact]
        public void Devices_WhenOneOfTwoDrivesDisappears_KeepsOnlyTheOneThatIsStillThere()
        {
            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();

            Assert.Equal(2, _dashboard.Devices.Count);

            SetDrives(CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();

            Assert.Equal(DeviceId.Advantage2, Assert.Single(_dashboard.Devices).DeviceId);
            Assert.False(_dashboard.IsEmpty);
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
                _dashboard.Devices.Select(card => card.DeviceId));
            Assert.True(_dashboard.HasDevices);
            Assert.False(_dashboard.IsEmpty);
        }

        [Fact]
        public void Devices_WhenADriveAppears_AddsItsCard()
        {
            _monitor.Refresh();
            SetDrives(CreateDrive(DeviceId.FreestyleEdgeRgb));

            _monitor.Refresh();

            var card = Assert.Single(_dashboard.Devices);
            Assert.Equal(DeviceId.FreestyleEdgeRgb, card.DeviceId);
            Assert.Equal(DeviceCardState.Connected, card.State);
        }

        [Fact]
        public void Devices_WhenADeviceWasNeverDetected_HasNoCardForIt()
        {
            SetDrives(CreateDrive(DeviceId.Tko));

            _monitor.Refresh();

            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.Devices).DeviceId);
        }

        /// <summary>
        /// A device appearing between two scans lands at the end of the list and moves nothing above
        /// it. The Advantage2 precedes the TKO in catalog order, so a merge that re-sorted would put
        /// it first and shift the existing card down — exactly the reflow the insert animation exists
        /// to avoid.
        /// </summary>
        [Fact]
        public void Devices_WhenASecondDriveAppears_AppendsItWithoutMovingTheExistingCard()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var first = Assert.Single(_dashboard.Devices);

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();

            Assert.Equal(
                new[] { DeviceId.Tko, DeviceId.Advantage2 },
                _dashboard.Devices.Select(card => card.DeviceId));
            Assert.Same(first, _dashboard.Devices[0]);
        }

        [Fact]
        public void Devices_AcrossRefreshes_UpdatesTheCardInPlaceWithoutDuplicates()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.Devices);

            SetDrives(CreateDrive(DeviceId.Tko, isWritable: false));
            _monitor.Refresh();
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.Devices));
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
                _dashboard.Devices.Select(card => card.ScannedDeviceId));
            Assert.All(_dashboard.Devices, card => Assert.Equal(DeviceId.FreestyleEdge, card.DeviceId));
        }

        [Fact]
        public void Devices_WhenAFreestyleDriveChangesModel_KeepsTheSameCard()
        {
            SetDrives(CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS Edge"));
            _monitor.Refresh();
            var card = Assert.Single(_dashboard.Devices);

            SetDrives(CreateFreestyleDrive(DeviceId.FreestyleEdge, "FS PRO"));
            _monitor.Refresh();

            Assert.Same(card, Assert.Single(_dashboard.Devices));
            Assert.Equal(DeviceId.FreestyleEdge, card.ScannedDeviceId);
            Assert.Equal(DeviceId.FreestylePro, card.DeviceId);
        }

        [Fact]
        public void Apply_WithSnapshotsSharingAKey_DoesNotThrow()
        {
            var snapshot = TestDevices.CreateSnapshot(DeviceId.Tko);

            _dashboard.Apply([snapshot, snapshot]);

            Assert.Single(_dashboard.Devices);
        }

        /// <summary>
        /// Issue #118, item 4: a board this app cannot edit appears on no screen. The Advantage 360
        /// Professional exposes no v-Drive and has no card kind of its own any more, so nothing on
        /// the dashboard can be keyed to it — and the roster stays homogeneous, which is what keeps
        /// <see cref="DashboardViewModel.HasDevices"/> honest.
        /// </summary>
        [Fact]
        public void Devices_ForEveryUnprogrammableBoard_HasNoCardAtAll()
        {
            var unsupported = DeviceCatalog.All.Where(device => !device.IsProgrammable).ToArray();

            Assert.NotEmpty(unsupported);

            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            // Every unsupported board, offered to the roster as a detected drive. Built by hand
            // rather than through TestDevices: the Advantage 360 Professional carries no volume
            // label at all, which is the very reason a scan can never produce it.
            _dashboard.Apply(
            [
                .. unsupported.Select(CreateDetectedSnapshot),
                TestDevices.CreateSnapshot(DeviceId.Tko)
            ]);

            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.Devices).DeviceId);
            Assert.All(
                unsupported,
                device => Assert.DoesNotContain(
                    _dashboard.Devices,
                    card => card.DeviceId == device.Id || card.ScannedDeviceId == device.Id));
        }

        [Fact]
        public void HeaderSubtitle_CountsDetectedDevicesAgainstTheProgrammableCatalog()
        {
            Assert.Equal(7, DashboardViewModel.KnownDeviceCount);

            _monitor.Refresh();
            Assert.Equal("0 of 7 known devices present", _dashboard.HeaderSubtitle);

            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            Assert.Equal("1 of 7 known device present", _dashboard.HeaderSubtitle);

            SetDrives(CreateDrive(DeviceId.Tko), CreateDrive(DeviceId.Advantage2));
            _monitor.Refresh();
            Assert.Equal("2 of 7 known devices present", _dashboard.HeaderSubtitle);
        }

        /// <summary>
        /// The subtitle says what is present now and promises nothing about the future: scanning is
        /// manual, so the old "· list updates itself" clause was a claim the app can no longer make.
        /// </summary>
        [Fact]
        public void HeaderSubtitle_Never_ClaimsTheListRefreshesItself()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            Assert.DoesNotContain("updates itself", _dashboard.HeaderSubtitle, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("·", _dashboard.HeaderSubtitle, StringComparison.Ordinal);
        }

        [Fact]
        public void HeaderSubtitle_WhenADriveGoesAway_StopsCountingItAndLosesItsCard()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            _scanner.SetResult();
            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
            Assert.Equal("0 of 7 known devices present", _dashboard.HeaderSubtitle);
        }

        [Fact]
        public void HeaderCaptions_AreTheMockupsWording()
        {
            Assert.Equal("Devices", _dashboard.HeaderTitle);
            Assert.Equal("Scan all", _dashboard.ScanAllCaption);
        }

        /// <summary>
        /// Issue #118, acceptance criterion 2: <b>no string in the app implies the list refreshes
        /// itself.</b> Four surfaces used to say so in four different ways — the subtitle's
        /// "· list updates itself", the empty state's "no need to press anything", its
        /// "Still watching · rescanned N times", and the last connection step's promise that the
        /// screen would replace itself. All four are gone, and the guard is a sweep rather than four
        /// equality assertions so a fifth cannot be written.
        /// <para>
        /// Scoped to the dashboard, its cards, its empty state and the connection steps — the
        /// surfaces a user reads while nothing is plugged in. Both the authored constants and the
        /// text the view models actually compose are read: a promise assembled from a template is
        /// still a promise.
        /// </para>
        /// </summary>
        [Fact]
        public void Copy_AcrossTheDashboardAndItsEmptyState_Never_ClaimsTheAppScansOnItsOwn()
        {
            string[] forbidden =
            [
                "updates itself",
                "automatic",
                "no need to press",
                "still watching",
                "is watching",
                "rescanned",
                "refreshed",
                "replace itself",
                "the moment it appears"
            ];

            var authored = new List<string>();

            foreach (var type in new[]
            {
                typeof(DashboardViewModel),
                typeof(DeviceCardViewModel),
                typeof(NoDeviceViewModel),
                typeof(ConnectionSteps),
                typeof(MainWindowViewModel)
            })
            {
                authored.AddRange(ConstantStringsOf(type));
            }

            // ...and the sentences the templates above actually produce, for every board the picker
            // offers, so a promise assembled at runtime is caught too.
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();

            authored.Add(_dashboard.HeaderSubtitle);
            authored.Add(_dashboard.Devices[0].StatusText);
            authored.Add(_dashboard.EmptyState.Body);

            foreach (var option in _dashboard.EmptyState.Devices)
            {
                _dashboard.EmptyState.SelectedOption = option;

                authored.Add(_dashboard.EmptyState.StepsTitle);
                authored.AddRange(_dashboard.EmptyState.Steps.Select(step => step.Text));
            }

            // Anti-vacuity: a sweep that reflected nothing would pass while guarding nothing.
            Assert.True(authored.Count >= 40, $"Only {authored.Count} strings were swept.");

            var offenders = authored
                .Where(text => forbidden.Any(phrase => text.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(offenders.Length == 0, string.Join(Environment.NewLine, offenders));
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

            Assert.Equal(DeviceId.Tko, Assert.Single(_dashboard.Devices).DeviceId);
            Assert.Equal("1 of 7 known device present", _dashboard.HeaderSubtitle);
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
                _dashboard.Devices,
                card => Assert.Equal(DeviceCardState.Scanning, card.State));

            gate.Set();
            await scan;

            Assert.False(_dashboard.IsRefreshing);
            Assert.All(
                _dashboard.Devices,
                card => Assert.Equal(DeviceCardState.Connected, card.State));
        }

        [Fact]
        public void ConfigureRequested_WhenACardIsConfigured_CarriesThatDevice()
        {
            SetDrives(CreateDrive(DeviceId.Tko));
            _monitor.Refresh();
            var requested = new List<DeviceSnapshot>();
            _dashboard.ConfigureRequested += requested.Add;

            _dashboard.Devices[0].ConfigureCommand.Execute(null);

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
        public async Task ScanCommand_WhenExecuted_RunsAnotherDetectionPass()
        {
            SetDrives(CreateDrive(DeviceId.Tko));

            await _dashboard.ScanCommand.ExecuteAsync(null);

            Assert.Single(_dashboard.Devices);
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

            Assert.Single(_dashboard.Devices);
        }

        [Fact]
        public void Apply_AfterDispose_IsNoLongerDrivenByTheMonitor()
        {
            _dashboard.Dispose();
            SetDrives(CreateDrive(DeviceId.Tko));

            _monitor.Refresh();

            Assert.Empty(_dashboard.Devices);
        }

        /// <summary>
        /// Every <c>const string</c> declared on <paramref name="type"/>, public or not: display
        /// copy is as often a private template as a public caption, and a sweep that read only the
        /// public surface would miss half of it.
        /// </summary>
        private static IEnumerable<string> ConstantStringsOf(Type type)
        {
            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

            return type.GetFields(flags)
                .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                .Select(field => (string?)field.GetRawConstantValue())
                .Where(value => value is not null)
                .Select(value => value!);
        }

        /// <summary>
        /// A snapshot that claims a mounted drive for any catalog entry, label or no label. The
        /// roster filters on <c>IsDetected</c>, which is derived from the status alone, so this is
        /// the strongest input an unsupported board could ever arrive as.
        /// </summary>
        private static DeviceSnapshot CreateDetectedSnapshot(DeviceDefinition device)
        {
            return new DeviceSnapshot
            {
                ScannedDeviceId = device.Id,
                Device = device,
                Status = VDriveConnectionStatus.Connected,
                Health = VDriveHealth.Ok
            };
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
