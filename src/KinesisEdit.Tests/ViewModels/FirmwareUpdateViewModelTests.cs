using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Services;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The "Check for Updates" dialog of specs/09-firmware.md §3, step by step: the initial
    /// "Checking for update..." state, the local-version short circuit of step 1, the endpoint
    /// choice of step 3 with its firmware-debug dump, the per-row outcomes of steps 4-5, the
    /// failure dialog of step 6, and the support links of step 7. Every caption is pinned
    /// verbatim; nothing here touches the network; and nothing is allowed to escape the check,
    /// which the window runs through a command that rethrows on the UI thread.
    /// </summary>
    public class FirmwareUpdateViewModelTests
    {
        private const string WindowTitle = "Check for Updates";
        private const string CheckingCaption = "Checking for update...";
        private const string UpdateNowCaption = "Update Now";
        private const string UpToDateCaption = "No update available";
        private const string ReadErrorCaption = "Error reading firmware file";
        private const string KeyboardFetchErrorCaption = "Error fetching keyboard firmware";
        private const string LightingFetchErrorCaption = "Error fetching lighting firmware";
        private const string AppFetchErrorCaption = "Error fetching app version";
        private const string ConnectionErrorCaption = "Check connection";
        private const string ConnectionErrorPrefix = "Error accessing internet or firmware website: ";
        private const string GamingEndpoint = "https://gaming.kinesis-ergo.com/wp-json/ksv/v1/get_versions";
        private const string OfficeEndpoint = "https://kinesis-ergo.com/wp-json/ksv/v1/get_versions";

        private readonly FakeVersionManifestClient _manifestClient = new();
        private readonly FakeAppVersionProvider _appVersion = new();
        private readonly FakeNotificationService _notifications = new();
        private readonly FakeUrlLauncher _urlLauncher = new();

        [Fact]
        public void Title_ForAnyDevice_IsTheSpecWording()
        {
            Assert.Equal(WindowTitle, CreateViewModel(CreateTko()).Title);
        }

        [Fact]
        public void Rows_BeforeTheCheckRuns_AreTheThreeRowsInCheckingState()
        {
            var viewModel = CreateViewModel(CreateTko());

            Assert.Equal(
                new[] { UpdateRowKind.Keyboard, UpdateRowKind.Lighting, UpdateRowKind.App },
                viewModel.Rows.Select(row => row.Kind));
            Assert.All(viewModel.Rows, row => Assert.Equal(CheckingCaption, row.ButtonCaption));
            Assert.All(viewModel.Rows, row => Assert.False(row.IsActionable));
        }

        [Fact]
        public async Task CheckAsync_WhenEveryVersionIsCurrent_ReportsNoUpdateAvailable()
        {
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(UpToDateCaption, row.ButtonCaption));
            Assert.All(viewModel.Rows, row => Assert.False(row.IsActionable));
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task CheckAsync_WhenAVersionIsOutdated_ReportsUpdateNowOnThatRowOnly()
        {
            _manifestClient.Json = BuildTkoJson("1.0.200", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.Equal(UpdateNowCaption, FindRow(viewModel, UpdateRowKind.Keyboard).ButtonCaption);
            Assert.Equal(UpToDateCaption, FindRow(viewModel, UpdateRowKind.Lighting).ButtonCaption);
            Assert.Equal(UpToDateCaption, FindRow(viewModel, UpdateRowKind.App).ButtonCaption);
            Assert.True(FindRow(viewModel, UpdateRowKind.Keyboard).IsActionable);
        }

        [Fact]
        public async Task CheckAsync_WhenTheVersionFileCouldNotBeRead_ReportsTheReadErrorAndSkipsTheRequest()
        {
            var viewModel = CreateViewModel(TestDevices.CreateSnapshot(DeviceId.Tko));

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(ReadErrorCaption, row.ButtonCaption));
            Assert.Empty(_manifestClient.RequestedUrls);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task CheckAsync_WhenTheLocalVersionIsPresentButUnparseable_StillComparesIt()
        {
            // Only an empty value is the step 1 short circuit: §1.1 parses a non-numeric token as
            // 0, so an unreadable value compares as 0.0.0 and every remote version beats it.
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.Tko,
                versionFile: TestDevices.CreateVersionFile(DeviceId.Tko, keyboardFirmware: "unknown"));
            var viewModel = CreateViewModel(snapshot);

            await viewModel.CheckAsync();

            Assert.Single(_manifestClient.RequestedUrls);
            Assert.Equal(UpdateNowCaption, FindRow(viewModel, UpdateRowKind.Keyboard).ButtonCaption);
        }

        [Theory]
        [InlineData(UpdateRowKind.Keyboard, KeyboardFetchErrorCaption)]
        [InlineData(UpdateRowKind.Lighting, LightingFetchErrorCaption)]
        [InlineData(UpdateRowKind.App, AppFetchErrorCaption)]
        public async Task CheckAsync_WhenAManifestValueIsMissing_ReportsThatRowsFetchError(UpdateRowKind kind, string expected)
        {
            _manifestClient.Json = BuildTkoJson(
                keyboard: kind == UpdateRowKind.Keyboard ? "" : "1.0.100",
                lighting: kind == UpdateRowKind.Lighting ? "" : "1.0.58",
                macGamingApp: kind == UpdateRowKind.App ? "" : "2.0.20");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.Equal(expected, FindRow(viewModel, kind).ButtonCaption);
            Assert.All(
                viewModel.Rows.Where(row => row.Kind != kind),
                row => Assert.Equal(UpToDateCaption, row.ButtonCaption));
        }

        [Fact]
        public async Task CheckAsync_WhenTheRequestFails_ReportsCheckConnectionAndShowsTheSpecDialog()
        {
            _manifestClient.ExceptionToThrow = new HttpRequestException("No such host is known.");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(ConnectionErrorCaption, row.ButtonCaption));

            var messageBox = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(WindowTitle, messageBox.Title);
            Assert.Equal(ConnectionErrorPrefix + "No such host is known.", messageBox.Message);
            Assert.Equal(MessageBoxIcon.Error, messageBox.Icon);
        }

        [Fact]
        public async Task CheckAsync_WhenTheFailureDialogCannotBeShown_StillReportsCheckConnectionAndDoesNotThrow()
        {
            // Nothing may escape CheckAsync: the window runs it through a command that rethrows on
            // the UI thread, so an escaped exception is a crash. A check easily outlives its owner
            // window (quit the app mid-fetch) and showing a modal over a closed owner throws.
            _manifestClient.ExceptionToThrow = new HttpRequestException("No such host is known.");
            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException(
                "Cannot show a window with a closed owner.");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(ConnectionErrorCaption, row.ButtonCaption));
            Assert.Single(_notifications.MessageBoxes);
            Assert.False(viewModel.IsChecking);
        }

        [Fact]
        public async Task CheckAsync_WhenTheFlowThrowsOutsideTheRequest_ReportsCheckConnection()
        {
            // The row builder runs on values the dialog does not control; whatever throws, the user
            // gets the one failure outcome §3 step 6 defines rather than an unhandled exception.
            _appVersion.ExceptionToThrow = new InvalidOperationException("No version resource.");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(ConnectionErrorCaption, row.ButtonCaption));
            Assert.Equal(ConnectionErrorPrefix + "No version resource.", Assert.Single(_notifications.MessageBoxes).Message);
        }

        [Fact]
        public async Task CheckAsync_WithFirmwareDebugMode_ShowsTheRawResponse()
        {
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko(debugFlags: VDriveDebugFlags.FirmwareDebug));

            await viewModel.CheckAsync();

            var messageBox = Assert.Single(_notifications.MessageBoxes);

            Assert.Equal(_manifestClient.Json, messageBox.Message);
        }

        [Fact]
        public async Task CheckAsync_WithFirmwareDebugMode_FillsTheRowsBeforeShowingTheDump()
        {
            // The dump blocks until it is dismissed, so the rows behind it must already carry the
            // result instead of reading "Checking for update..." for as long as the box is up.
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko(debugFlags: VDriveDebugFlags.FirmwareDebug));
            var captionsWhenShown = Array.Empty<string>();

            _notifications.MessageBoxShowing = _ => captionsWhenShown = viewModel.Rows
                .Select(row => row.ButtonCaption)
                .ToArray();

            await viewModel.CheckAsync();

            Assert.Equal(
                new[] { UpToDateCaption, UpToDateCaption, UpToDateCaption },
                captionsWhenShown);
        }

        [Fact]
        public async Task CheckAsync_WhenTheDebugDumpCannotBeShown_KeepsTheFetchedResult()
        {
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");
            _notifications.MessageBoxExceptionToThrow = new InvalidOperationException(
                "Cannot show a window with a closed owner.");

            var viewModel = CreateViewModel(CreateTko(debugFlags: VDriveDebugFlags.FirmwareDebug));

            await viewModel.CheckAsync();

            // A diagnostic that could not be shown never turns a successful check into a failure.
            Assert.All(viewModel.Rows, row => Assert.Equal(UpToDateCaption, row.ButtonCaption));
        }

        [Fact]
        public async Task CheckAsync_WithoutFirmwareDebugMode_ShowsNoDialog()
        {
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko(debugFlags: VDriveDebugFlags.Debug | VDriveDebugFlags.DevMode));

            await viewModel.CheckAsync();

            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task CheckAsync_ForTheAdvantage360_OmitsTheLightingRow()
        {
            _manifestClient.Json = """{"kb360_version":"1.0.100","mac_app_version":"2.0.20"}""";

            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.Advantage360,
                versionFile: TestDevices.CreateVersionFile(DeviceId.Advantage360));
            var viewModel = CreateViewModel(snapshot);

            Assert.Equal(
                new[] { UpdateRowKind.Keyboard, UpdateRowKind.App },
                viewModel.Rows.Select(row => row.Kind));

            await viewModel.CheckAsync();

            Assert.Equal(
                new[] { UpdateRowKind.Keyboard, UpdateRowKind.App },
                viewModel.Rows.Select(row => row.Kind));
            Assert.All(viewModel.Rows, row => Assert.Equal(UpToDateCaption, row.ButtonCaption));
        }

        [Fact]
        public async Task CheckAsync_ForADeviceWithoutAnUpdateDialog_HasNoRowsAndSkipsTheRequest()
        {
            // §4: the Advantage2's app has no update dialog, so Core builds no rows for it — and a
            // check with nothing to compare must not spend a request on the office endpoint.
            var snapshot = TestDevices.CreateSnapshot(
                DeviceId.Advantage2,
                versionFile: TestDevices.CreateVersionFile(DeviceId.Advantage2));
            var viewModel = CreateViewModel(snapshot);

            await viewModel.CheckAsync();

            Assert.Empty(viewModel.Rows);
            Assert.Empty(_manifestClient.RequestedUrls);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Theory]
        [InlineData(DeviceId.Tko, GamingEndpoint)]
        [InlineData(DeviceId.FreestyleEdgeRgb, GamingEndpoint)]
        [InlineData(DeviceId.Advantage360, OfficeEndpoint)]
        public async Task CheckAsync_ForEachDevice_UsesTheEndpointOfItsMasterApp(DeviceId deviceId, string expectedUrl)
        {
            var snapshot = TestDevices.CreateSnapshot(deviceId, versionFile: TestDevices.CreateVersionFile(deviceId));
            var viewModel = CreateViewModel(snapshot);

            await viewModel.CheckAsync();

            Assert.Equal(expectedUrl, Assert.Single(_manifestClient.RequestedUrls));
        }

        [Theory]
        [InlineData(UpdateCheckPlatform.MacOs, UpToDateCaption)]
        [InlineData(UpdateCheckPlatform.Linux, UpToDateCaption)]
        [InlineData(UpdateCheckPlatform.Windows, UpdateNowCaption)]
        public async Task CheckAsync_OnEachPlatform_ReadsThatPlatformsAppKey(UpdateCheckPlatform platform, string expected)
        {
            // Only the Windows key advertises a newer build, so the app row's caption reports
            // which key was read (specs/09-firmware.md §3 step 4).
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20", windowsGamingApp: "9.9.9");

            var viewModel = CreateViewModel(CreateTko(), platform);

            await viewModel.CheckAsync();

            Assert.Equal(expected, FindRow(viewModel, UpdateRowKind.App).ButtonCaption);
        }

        [Fact]
        public async Task CheckAsync_ForAnAvailableUpdate_LinksTheRowToItsSupportPage()
        {
            _manifestClient.Json = BuildTkoJson("1.0.200", "1.0.200", macGamingApp: "9.9.9");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            FindRow(viewModel, UpdateRowKind.Keyboard).OpenCommand.Execute(null);
            FindRow(viewModel, UpdateRowKind.App).OpenCommand.Execute(null);

            Assert.Equal(
                new[] { UpdateCheckUrls.FindFirmwareUrl(DeviceId.Tko), UpdateCheckUrls.FindAppUrl(DeviceId.Tko) },
                _urlLauncher.OpenedUrls);
        }

        [Fact]
        public async Task CheckAsync_ForRowsWithoutAnUpdate_OpensNothing()
        {
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            foreach (var row in viewModel.Rows)
            {
                row.OpenCommand.Execute(null);
            }

            Assert.Empty(_urlLauncher.OpenedUrls);
        }

        [Fact]
        public async Task CheckAsync_WhileACheckIsInFlight_DoesNotStartASecondRequest()
        {
            _manifestClient.Gate = new TaskCompletionSource();
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko());
            var first = viewModel.CheckAsync();

            await viewModel.CheckAsync();

            Assert.True(viewModel.IsChecking);
            Assert.False(viewModel.CheckCommand.CanExecute(null));

            _manifestClient.Gate.SetResult();

            await first;

            Assert.Single(_manifestClient.RequestedUrls);
            Assert.False(viewModel.IsChecking);
        }

        [Fact]
        public async Task CheckAsync_AfterAPreviousCheckCompleted_RunsAgain()
        {
            // The gate is "one at a time", not "once": the window fires the command on every open.
            _manifestClient.Json = BuildTkoJson("1.0.100", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko());

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(UpToDateCaption, row.ButtonCaption));

            _manifestClient.Json = BuildTkoJson("1.0.200", "1.0.58", macGamingApp: "2.0.20");

            await viewModel.CheckAsync();

            Assert.Equal(2, _manifestClient.RequestedUrls.Count);
            Assert.Equal(UpdateNowCaption, FindRow(viewModel, UpdateRowKind.Keyboard).ButtonCaption);
        }

        [Fact]
        public async Task CheckAsync_AfterDisposal_DoesNothing()
        {
            _manifestClient.Json = BuildTkoJson("1.0.200", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko());

            viewModel.Dispose();
            viewModel.Dispose();

            await viewModel.CheckAsync();

            Assert.All(viewModel.Rows, row => Assert.Equal(CheckingCaption, row.ButtonCaption));
            Assert.Empty(_manifestClient.RequestedUrls);
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task CheckAsync_WhenTheDialogClosesMidRequest_LeavesTheRowsCheckingAndShowsNothing()
        {
            _manifestClient.Gate = new TaskCompletionSource();
            _manifestClient.Json = BuildTkoJson("1.0.200", "1.0.58", macGamingApp: "2.0.20");

            var viewModel = CreateViewModel(CreateTko(debugFlags: VDriveDebugFlags.FirmwareDebug));
            var check = viewModel.CheckAsync();

            viewModel.Dispose();
            _manifestClient.Gate.SetResult();

            await check;

            Assert.All(viewModel.Rows, row => Assert.Equal(CheckingCaption, row.ButtonCaption));
            Assert.Empty(_notifications.MessageBoxes);
        }

        [Fact]
        public async Task CheckAsync_WhenTheRequestIsCancelledByClosing_ShowsNoFailureDialog()
        {
            _manifestClient.Gate = new TaskCompletionSource();
            _manifestClient.ExceptionToThrow = new OperationCanceledException();

            var viewModel = CreateViewModel(CreateTko());
            var check = viewModel.CheckAsync();

            viewModel.Dispose();
            _manifestClient.Gate.SetResult();

            await check;

            Assert.All(viewModel.Rows, row => Assert.Equal(CheckingCaption, row.ButtonCaption));
            Assert.Empty(_notifications.MessageBoxes);
        }

        private static DeviceSnapshot CreateTko(VDriveDebugFlags debugFlags = VDriveDebugFlags.None)
        {
            return TestDevices.CreateSnapshot(
                DeviceId.Tko,
                versionFile: TestDevices.CreateVersionFile(DeviceId.Tko),
                debugFlags: debugFlags);
        }

        private static string BuildTkoJson(
            string keyboard,
            string lighting,
            string macGamingApp,
            string windowsGamingApp = "")
        {
            return $$"""
                {
                  "tko_keyboard_version": "{{keyboard}}",
                  "tko_lighting_version": "{{lighting}}",
                  "mac_app_ver": "{{macGamingApp}}",
                  "app_ver": "{{windowsGamingApp}}"
                }
                """;
        }

        private static FirmwareUpdateRowViewModel FindRow(FirmwareUpdateViewModel viewModel, UpdateRowKind kind)
        {
            return viewModel.Rows.Single(row => row.Kind == kind);
        }

        private FirmwareUpdateViewModel CreateViewModel(
            DeviceSnapshot device,
            UpdateCheckPlatform platform = UpdateCheckPlatform.MacOs)
        {
            return new FirmwareUpdateViewModel(
                device,
                _manifestClient,
                _appVersion,
                _notifications,
                _urlLauncher,
                platform);
        }
    }
}
