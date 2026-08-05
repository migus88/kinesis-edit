using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Firmware;

namespace KinesisEdit.Core.Tests.Firmware
{
    /// <summary>
    /// Pins the comparison engine of the "Check for Updates" dialog (specs/09-firmware.md §3):
    /// the row set per device (step 4 hides the Advantage 360 lighting row), the unreadable-local
    /// short circuit (step 1), the four-component app version (step 2), the outcome matrix
    /// (step 5), the connection-error state (step 6), and the row targets (step 7). Also pins the
    /// empty-versus-unparseable rule: only an empty value is "not read" / "not fetched", while a
    /// present but unparseable value compares as the zero version of §1.1.
    /// </summary>
    public class UpdateCheckServiceTests
    {
        private const string ManifestJson = """
            {
              "keyboard_ver": "1.0.2",
              "lighting_ver": "1.0.3",
              "app_ver": "2.0.20",
              "pc_app_version": "3.0.9",
              "mac_app_ver": "2.1.3",
              "mac_app_version": "3.1.4",
              "tko_keyboard_version": "1.0.5",
              "tko_lighting_version": "1.0.6",
              "kb360_version": "1.0.7"
            }
            """;

        private const string RgbFirmwareUrl = "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#firmware";
        private const string RgbAppUrl = "https://gaming.kinesis-ergo.com/fs-edge-rgb-support/#smartset-app";
        private const string TkoFirmwareUrl = "https://gaming.kinesis-ergo.com/tko-support/#firmware";
        private const string TkoAppUrl = "https://gaming.kinesis-ergo.com/tko-support/#smartset-app";
        private const string Adv360FirmwareUrl = "https://kinesis-ergo.com/support/kb360/#firmware-updates";
        private const string Adv360AppUrl = "https://kinesis-ergo.com/support/kb360/#smartset-app";

        private static readonly FirmwareVersion _zeroVersion = new(0, 0, 0);

        [Fact]
        public void BuildRows_ForEdgeRgb_ReturnsKeyboardLightingAndAppRowsInOrder()
        {
            var rows = UpdateCheckService.BuildRows(CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.3", "2.0.20.5"));

            Assert.Equal(new[] { UpdateRowKind.Keyboard, UpdateRowKind.Lighting, UpdateRowKind.App }, rows.Select(row => row.Kind));
        }

        [Fact]
        public void BuildRows_ForAdvantage360_OmitsTheLightingRow()
        {
            var rows = UpdateCheckService.BuildRows(CreateRequest(DeviceId.Advantage360, "1.0.6", string.Empty, "2.0.20.5"));

            Assert.Equal(new[] { UpdateRowKind.Keyboard, UpdateRowKind.App }, rows.Select(row => row.Kind));
        }

        [Fact]
        public void BuildRows_WithOlderLocalFirmware_ReportsUpdateAvailable()
        {
            var rows = UpdateCheckService.BuildRows(CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.2", "2.0.19.0"));

            Assert.Equal(UpdateRowState.UpdateAvailable, FindRow(rows, UpdateRowKind.Keyboard).State);
            Assert.Equal(UpdateRowState.UpdateAvailable, FindRow(rows, UpdateRowKind.Lighting).State);
            Assert.Equal(UpdateRowState.UpdateAvailable, FindRow(rows, UpdateRowKind.App).State);
        }

        [Fact]
        public void BuildRows_WithEqualVersions_ReportsUpToDate()
        {
            var rows = UpdateCheckService.BuildRows(CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", "2.0.20.0"));

            Assert.All(rows, row => Assert.Equal(UpdateRowState.UpToDate, row.State));
        }

        [Fact]
        public void BuildRows_WithNewerLocalVersions_ReportsUpToDate()
        {
            var rows = UpdateCheckService.BuildRows(CreateRequest(DeviceId.FreestyleEdgeRgb, "1.1.0", "2.0.0", "9.9.9.9"));

            Assert.All(rows, row => Assert.Equal(UpdateRowState.UpToDate, row.State));
        }

        [Fact]
        public void BuildRows_WithParsedVersions_CarriesBothSidesOfTheComparison()
        {
            var rows = UpdateCheckService.BuildRows(CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.3", "2.0.20.5"));
            var keyboardRow = FindRow(rows, UpdateRowKind.Keyboard);

            Assert.Equal(new FirmwareVersion(1, 0, 1), keyboardRow.LocalVersion);
            Assert.Equal("1.0.1", keyboardRow.LocalVersionText);
            Assert.Equal(new FirmwareVersion(1, 0, 2), keyboardRow.RemoteVersion);
            Assert.Equal("1.0.2", keyboardRow.RemoteVersionText);
        }

        [Fact]
        public void BuildRows_WithDeviceVersionText_KeepsTheDeviceWording()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1709.us (4MB), 03/08/2019", "1.0.3", "2.0.20.5");

            var keyboardRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.Keyboard);

            Assert.Equal("1.0.1709.us (4MB), 03/08/2019", keyboardRow.LocalVersionText);
            Assert.Equal(new FirmwareVersion(1, 0, 1709), keyboardRow.LocalVersion);
            Assert.Equal(UpdateRowState.UpToDate, keyboardRow.State);
        }

        [Theory]
        [InlineData("""{"keyboard_ver":"","lighting_ver":"1.0.3","app_ver":"2.0.20"}""")]
        [InlineData("""{"keyboard_ver":"   ","lighting_ver":"1.0.3","app_ver":"2.0.20"}""")]
        [InlineData("""{"keyboard_ver":null,"lighting_ver":"1.0.3","app_ver":"2.0.20"}""")]
        [InlineData("""{"lighting_ver":"1.0.3","app_ver":"2.0.20"}""")]
        public void BuildRows_WithAnEmptyRemoteValue_ReportsRemoteMissing(string json)
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.3", "2.0.20.5", json: json);

            var rows = UpdateCheckService.BuildRows(request);

            Assert.Equal(UpdateRowState.RemoteMissing, FindRow(rows, UpdateRowKind.Keyboard).State);
            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.Lighting).State);
        }

        // §1.1: "a non-numeric token parses as 0", so a value the version parser rejects outright
        // is still a version — the all-zero one — and never an error.
        [Theory]
        [InlineData("not a version")]
        [InlineData("TBD")]
        [InlineData("us.1.0")]
        public void BuildRows_WithAnUnparseableRemoteValue_ComparesItAsTheZeroVersion(string remoteVersionText)
        {
            var json = $$"""{"keyboard_ver":"{{remoteVersionText}}","lighting_ver":"1.0.3","app_ver":"2.0.20"}""";
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.3", "2.0.20.5", json: json);

            var keyboardRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.Keyboard);

            Assert.Equal(UpdateRowState.UpToDate, keyboardRow.State);
            Assert.Equal(_zeroVersion, keyboardRow.RemoteVersion);
            Assert.Equal(remoteVersionText, keyboardRow.RemoteVersionText);
        }

        [Fact]
        public void BuildRows_WithARemoteValueCarryingTrailingText_ParsesItsFirstThreeTokens()
        {
            var json = """{"keyboard_ver":"1.0.1709.us (4MB), 03/08/2019","lighting_ver":"1.0.3","app_ver":"2.0.20"}""";
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.3", "2.0.20.5", json: json);

            var keyboardRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.Keyboard);

            Assert.Equal(new FirmwareVersion(1, 0, 1709), keyboardRow.RemoteVersion);
            Assert.Equal(UpdateRowState.UpdateAvailable, keyboardRow.State);
        }

        [Fact]
        public void BuildRows_WithEmptyManifest_ReportsRemoteMissingOnEveryRow()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.1", "1.0.3", "2.0.20.5") with
            {
                Manifest = VersionManifest.Empty
            };

            var rows = UpdateCheckService.BuildRows(request);

            Assert.All(rows, row => Assert.Equal(UpdateRowState.RemoteMissing, row.State));
            Assert.All(rows, row => Assert.Equal(string.Empty, row.RemoteVersionText));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, 3)]
        [InlineData(DeviceId.Tko, 3)]
        [InlineData(DeviceId.Advantage360, 2)]
        public void BuildRows_WithEmptyKeyboardFirmware_MarksEveryRowLocalUnreadable(DeviceId deviceId, int expectedRowCount)
        {
            var request = CreateRequest(deviceId, string.Empty, "1.0.3", "2.0.20.5");

            var rows = UpdateCheckService.BuildRows(request);

            Assert.Equal(expectedRowCount, rows.Count);
            Assert.All(rows, row => Assert.Equal(UpdateRowState.LocalUnreadable, row.State));
        }

        [Fact]
        public void BuildRows_WithWhitespaceKeyboardFirmware_MarksEveryRowLocalUnreadable()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "   ", "1.0.3", "2.0.20.5");

            var rows = UpdateCheckService.BuildRows(request);

            Assert.All(rows, row => Assert.Equal(UpdateRowState.LocalUnreadable, row.State));
        }

        // §3 step 1 short-circuits on a keyboard version that "comes back empty" only; a garbage
        // value was read, so it is compared as the zero version of §1.1 and the other rows run.
        [Fact]
        public void BuildRows_WithUnparseableKeyboardFirmware_ComparesItAsTheZeroVersion()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "unknown", "1.0.3", "2.0.20.5");

            var rows = UpdateCheckService.BuildRows(request);
            var keyboardRow = FindRow(rows, UpdateRowKind.Keyboard);

            Assert.Equal(UpdateRowState.UpdateAvailable, keyboardRow.State);
            Assert.Equal(_zeroVersion, keyboardRow.LocalVersion);
            Assert.Equal("unknown", keyboardRow.LocalVersionText);
            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.Lighting).State);
            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.App).State);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public void BuildRows_WithEmptyLightingFirmware_MarksOnlyThatRowLocalUnreadable(string ledFirmwareText)
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", ledFirmwareText, "2.0.20.5");

            var rows = UpdateCheckService.BuildRows(request);

            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.Keyboard).State);
            Assert.Equal(UpdateRowState.LocalUnreadable, FindRow(rows, UpdateRowKind.Lighting).State);
            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.App).State);
        }

        [Fact]
        public void BuildRows_WithUnparseableLightingFirmware_ComparesItAsTheZeroVersion()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "unknown", "2.0.20.5");

            var lightingRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.Lighting);

            Assert.Equal(UpdateRowState.UpdateAvailable, lightingRow.State);
            Assert.Equal(_zeroVersion, lightingRow.LocalVersion);
            Assert.Equal("unknown", lightingRow.LocalVersionText);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void BuildRows_WithoutAnAppVersion_MarksOnlyTheAppRowLocalUnreadable(string? appVersionText)
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", appVersionText);

            var rows = UpdateCheckService.BuildRows(request);

            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.Keyboard).State);
            Assert.Equal(UpdateRowState.LocalUnreadable, FindRow(rows, UpdateRowKind.App).State);
        }

        [Fact]
        public void BuildRows_WithUnparseableAppVersion_ComparesItAsTheZeroVersion()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", "not a version");

            var appRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.App);

            Assert.Equal(UpdateRowState.UpdateAvailable, appRow.State);
            Assert.Equal(_zeroVersion, appRow.LocalVersion);
        }

        [Theory]
        [InlineData("2.0.20.5", UpdateRowState.UpToDate)]
        [InlineData("2.0.20.0", UpdateRowState.UpToDate)]
        [InlineData("2.0.21.0", UpdateRowState.UpToDate)]
        [InlineData("2.0.19.99", UpdateRowState.UpdateAvailable)]
        [InlineData("1.9.99.99", UpdateRowState.UpdateAvailable)]
        public void BuildRows_WithFourComponentAppVersion_ComparesTheFirstThreeComponents(string appVersionText, UpdateRowState expectedState)
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", appVersionText);

            var appRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.App);

            Assert.Equal(expectedState, appRow.State);
        }

        [Fact]
        public void BuildRows_WithFourComponentAppVersion_DropsTheBuildComponent()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", "2.0.20.5");

            var appRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.App);

            Assert.Equal(new FirmwareVersion(2, 0, 20), appRow.LocalVersion);
            Assert.Equal("2.0.20.5", appRow.LocalVersionText);
        }

        [Fact]
        public void BuildRows_ForTko_ReadsTheTkoManifestKeys()
        {
            var request = CreateRequest(DeviceId.Tko, "1.0.4", "1.0.6", "2.0.20.5");

            var rows = UpdateCheckService.BuildRows(request);

            Assert.Equal("1.0.5", FindRow(rows, UpdateRowKind.Keyboard).RemoteVersionText);
            Assert.Equal(UpdateRowState.UpdateAvailable, FindRow(rows, UpdateRowKind.Keyboard).State);
            Assert.Equal("1.0.6", FindRow(rows, UpdateRowKind.Lighting).RemoteVersionText);
            Assert.Equal(UpdateRowState.UpToDate, FindRow(rows, UpdateRowKind.Lighting).State);
        }

        [Fact]
        public void BuildRows_ForTko_IgnoresTheEdgeRgbKeys()
        {
            var json = """{"keyboard_ver":"1.0.2","lighting_ver":"1.0.3","app_ver":"2.0.20"}""";
            var request = CreateRequest(DeviceId.Tko, "1.0.1", "1.0.1", "2.0.20.5", json: json);

            var rows = UpdateCheckService.BuildRows(request);

            Assert.Equal(UpdateRowState.RemoteMissing, FindRow(rows, UpdateRowKind.Keyboard).State);
            Assert.Equal(UpdateRowState.RemoteMissing, FindRow(rows, UpdateRowKind.Lighting).State);
        }

        [Fact]
        public void BuildRows_ForAdvantage360_ReadsTheKb360Key()
        {
            var request = CreateRequest(DeviceId.Advantage360, "1.0.6", string.Empty, "3.0.9.1");

            var keyboardRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.Keyboard);

            Assert.Equal("1.0.7", keyboardRow.RemoteVersionText);
            Assert.Equal(UpdateRowState.UpdateAvailable, keyboardRow.State);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateCheckPlatform.Windows, "2.0.20")]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateCheckPlatform.MacOs, "2.1.3")]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateCheckPlatform.Linux, "2.1.3")]
        [InlineData(DeviceId.Tko, UpdateCheckPlatform.Windows, "2.0.20")]
        [InlineData(DeviceId.Tko, UpdateCheckPlatform.MacOs, "2.1.3")]
        [InlineData(DeviceId.Tko, UpdateCheckPlatform.Linux, "2.1.3")]
        [InlineData(DeviceId.Advantage360, UpdateCheckPlatform.Windows, "3.0.9")]
        [InlineData(DeviceId.Advantage360, UpdateCheckPlatform.MacOs, "3.1.4")]
        [InlineData(DeviceId.Advantage360, UpdateCheckPlatform.Linux, "3.1.4")]
        public void BuildRows_PerPlatform_ReadsTheMatchingAppKey(DeviceId deviceId, UpdateCheckPlatform platform, string expectedRemoteVersion)
        {
            var request = CreateRequest(deviceId, "1.0.9", "1.0.9", "2.0.20.5", platform);

            var appRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.App);

            Assert.Equal(expectedRemoteVersion, appRow.RemoteVersionText);
        }

        [Fact]
        public void BuildRows_WithUnknownPlatform_ReportsRemoteMissingForTheAppRow()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", "2.0.20.5", UpdateCheckPlatform.None);

            var appRow = FindRow(UpdateCheckService.BuildRows(request), UpdateRowKind.App);

            Assert.Equal(UpdateRowState.RemoteMissing, appRow.State);
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateRowKind.Keyboard, RgbFirmwareUrl)]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateRowKind.Lighting, RgbFirmwareUrl)]
        [InlineData(DeviceId.FreestyleEdgeRgb, UpdateRowKind.App, RgbAppUrl)]
        [InlineData(DeviceId.Tko, UpdateRowKind.Keyboard, TkoFirmwareUrl)]
        [InlineData(DeviceId.Tko, UpdateRowKind.Lighting, TkoFirmwareUrl)]
        [InlineData(DeviceId.Tko, UpdateRowKind.App, TkoAppUrl)]
        [InlineData(DeviceId.Advantage360, UpdateRowKind.Keyboard, Adv360FirmwareUrl)]
        [InlineData(DeviceId.Advantage360, UpdateRowKind.App, Adv360AppUrl)]
        public void BuildRows_ForEveryRow_CarriesItsSpec09Section3Step7Target(DeviceId deviceId, UpdateRowKind kind, string expectedUrl)
        {
            var request = CreateRequest(deviceId, "1.0.1", "1.0.1", "2.0.20.5");

            Assert.Equal(expectedUrl, FindRow(UpdateCheckService.BuildRows(request), kind).TargetUrl);
        }

        [Theory]
        [InlineData(DeviceId.None)]
        [InlineData(DeviceId.SavantElite2)]
        [InlineData(DeviceId.Advantage2)]
        [InlineData(DeviceId.FreestyleEdge)]
        [InlineData(DeviceId.FreestylePro)]
        [InlineData(DeviceId.CrossfireKeypad)]
        [InlineData(DeviceId.Advantage360Professional)]
        public void BuildRows_ForDeviceWithoutTheDialog_ReturnsNoRows(DeviceId deviceId)
        {
            var request = CreateRequest(deviceId, "1.0.1", "1.0.1", "2.0.20.5");

            Assert.Empty(UpdateCheckService.BuildRows(request));
            Assert.Empty(UpdateCheckService.BuildCheckingRows(deviceId));
            Assert.Empty(UpdateCheckService.BuildUnreadableRows(deviceId));
            Assert.Empty(UpdateCheckService.BuildConnectionErrorRows(deviceId));
        }

        [Fact]
        public void BuildRows_WithNullRequest_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => UpdateCheckService.BuildRows(null!));
        }

        [Fact]
        public void BuildRows_WithNullLocalVersions_Throws()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", "2.0.20.5") with
            {
                LocalVersions = null!
            };

            Assert.Throws<ArgumentNullException>(() => UpdateCheckService.BuildRows(request));
        }

        [Fact]
        public void BuildRows_WithNullManifest_Throws()
        {
            var request = CreateRequest(DeviceId.FreestyleEdgeRgb, "1.0.2", "1.0.3", "2.0.20.5") with
            {
                Manifest = null!
            };

            Assert.Throws<ArgumentNullException>(() => UpdateCheckService.BuildRows(request));
        }

        [Theory]
        [InlineData("1.0.2", true)]
        [InlineData("unknown", true)]
        [InlineData("", false)]
        [InlineData("   ", false)]
        public void HasReadableKeyboardVersion_PerVersionFileText_ReportsOnlyEmptinessAsUnreadable(string keyboardFirmwareText, bool expected)
        {
            var versionFile = CreateVersionFile(keyboardFirmwareText, "1.0.3");

            Assert.Equal(expected, UpdateCheckService.HasReadableKeyboardVersion(versionFile));
        }

        [Fact]
        public void HasReadableKeyboardVersion_WithNullVersionFile_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => UpdateCheckService.HasReadableKeyboardVersion(null!));
        }

        [Theory]
        [InlineData(DeviceId.FreestyleEdgeRgb, 3)]
        [InlineData(DeviceId.Tko, 3)]
        [InlineData(DeviceId.Advantage360, 2)]
        public void BuildCheckingRows_ForSupportedDevice_MarksEveryRowChecking(DeviceId deviceId, int expectedRowCount)
        {
            var rows = UpdateCheckService.BuildCheckingRows(deviceId);

            Assert.Equal(expectedRowCount, rows.Count);
            Assert.All(rows, row => Assert.Equal(UpdateRowState.Checking, row.State));
            Assert.All(rows, row => Assert.Null(row.LocalVersion));
            Assert.All(rows, row => Assert.Null(row.RemoteVersion));
        }

        [Fact]
        public void BuildUnreadableRows_ForSupportedDevice_MarksEveryRowLocalUnreadable()
        {
            var rows = UpdateCheckService.BuildUnreadableRows(DeviceId.FreestyleEdgeRgb);

            Assert.All(rows, row => Assert.Equal(UpdateRowState.LocalUnreadable, row.State));
            Assert.Equal(new[] { UpdateRowKind.Keyboard, UpdateRowKind.Lighting, UpdateRowKind.App }, rows.Select(row => row.Kind));
        }

        [Fact]
        public void BuildConnectionErrorRows_ForSupportedDevice_MarksEveryRowConnectionError()
        {
            var rows = UpdateCheckService.BuildConnectionErrorRows(DeviceId.Tko);

            Assert.All(rows, row => Assert.Equal(UpdateRowState.ConnectionError, row.State));
            Assert.Equal(TkoFirmwareUrl, FindRow(rows, UpdateRowKind.Keyboard).TargetUrl);
            Assert.Equal(TkoAppUrl, FindRow(rows, UpdateRowKind.App).TargetUrl);
        }

        private static UpdateCheckRow FindRow(IReadOnlyList<UpdateCheckRow> rows, UpdateRowKind kind)
        {
            return rows.Single(row => row.Kind == kind);
        }

        private static UpdateCheckRequest CreateRequest(
            DeviceId deviceId,
            string keyboardFirmwareText,
            string ledFirmwareText,
            string? appVersionText,
            UpdateCheckPlatform platform = UpdateCheckPlatform.Windows,
            string json = ManifestJson)
        {
            Assert.True(VersionManifest.TryParse(json, out var manifest));

            return new UpdateCheckRequest
            {
                Device = deviceId,
                LocalVersions = CreateVersionFile(keyboardFirmwareText, ledFirmwareText),
                Manifest = manifest,
                AppVersionText = appVersionText,
                Platform = platform
            };
        }

        private static VersionFileInfo CreateVersionFile(string keyboardFirmwareText, string ledFirmwareText)
        {
            return new VersionFileInfo
            {
                KeyboardFirmwareText = keyboardFirmwareText,
                KeyboardFirmware = FirmwareVersion.TryParse(keyboardFirmwareText, out var keyboardFirmware) ? keyboardFirmware : null,
                LedFirmwareText = ledFirmwareText,
                LedFirmware = FirmwareVersion.TryParse(ledFirmwareText, out var ledFirmware) ? ledFirmware : null
            };
        }
    }
}
