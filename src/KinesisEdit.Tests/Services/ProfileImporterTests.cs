using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Lighting;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.Transfer;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The Import flow of specs/10-apps-and-ui.md and its 07 §1.4 rules: pick, refuse over
    /// 50 KB, classify, hand to the session. Every message is app-chosen (the specs quote none)
    /// and pinned here so the wording cannot drift silently.
    /// </summary>
    public sealed class ProfileImporterTests
    {
        private readonly FakeFilePickerService _picker = new();
        private readonly FakeProfileSession _session = new(KeyboardLayout.Create(DeviceId.FreestyleEdgeRgb));
        private readonly ProfileImporter _importer;

        public ProfileImporterTests()
        {
            _importer = new ProfileImporter(_picker);
        }

        [Fact]
        public async Task ImportAsync_WhenThePickerIsCancelled_ChangesNothingAndSaysNothing()
        {
            _picker.FileToReturn = null;

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            Assert.Same(ProfileImportOutcome.Cancelled, outcome);
            Assert.False(outcome.WasApplied);
            Assert.Null(outcome.FailureMessage);
            Assert.Null(outcome.SuccessMessage);
            Assert.Empty(_session.ImportCalls);
            Assert.Equal(ProfileImporter.DialogTitle, Assert.Single(_picker.Titles));
        }

        [Fact]
        public async Task ImportAsync_WithAFileOverFiftyKilobytes_RefusesItWithoutReadingItsContent()
        {
            _picker.FileToReturn = new PickedFile(
                "huge.txt",
                null,
                ImportClassifier.MaxImportBytes + 1,
                ["[caps]>[a]"]);

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            Assert.False(outcome.WasApplied);
            Assert.Empty(_session.ImportCalls);
            Assert.Equal("'huge.txt' is 51 KB. An imported file may be at most 50 KB.", outcome.FailureMessage);
        }

        [Fact]
        public async Task ImportAsync_WithAFileExactlyAtTheMaximum_IsAccepted()
        {
            _picker.FileToReturn = new PickedFile(
                "atlimit.txt",
                null,
                ImportClassifier.MaxImportBytes,
                ["[F1]>[esc]"]);

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            Assert.True(outcome.WasApplied);
        }

        [Fact]
        public async Task ImportAsync_WithALayoutFile_ImportsItAsALayout()
        {
            _picker.SetFile("layout1.txt", "[caps]>[a]", "fn [F1]>[esc]");

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            var call = Assert.Single(_session.ImportCalls);

            Assert.Equal(ImportedFileKind.Layout, call.Kind);
            Assert.Equal(["[caps]>[a]", "fn [F1]>[esc]"], call.Lines);
            Assert.True(outcome.WasApplied);
            Assert.Equal(ImportedFileKind.Layout, outcome.Kind);
            Assert.Equal("Imported 'layout1.txt' as this profile's layout.", outcome.SuccessMessage);
            Assert.Null(outcome.FailureMessage);
        }

        [Fact]
        public async Task ImportAsync_WithALedFile_ImportsItAsLighting()
        {
            _session.LightingToImport = new LightingModel();

            _picker.SetFile("led1.txt", "[spectrum]>[spd3]");

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            var call = Assert.Single(_session.ImportCalls);

            Assert.Equal(ImportedFileKind.Lighting, call.Kind);
            Assert.True(outcome.WasApplied);
            Assert.Equal(ImportedFileKind.Lighting, outcome.Kind);
            Assert.Equal("Imported 'led1.txt' as this profile's lighting.", outcome.SuccessMessage);
            Assert.Same(_session.LightingToImport, _session.Lighting);
        }

        [Fact]
        public async Task ImportAsync_OnADeviceWithoutLedFiles_ClassifiesEverythingAsALayout()
        {
            // 07 §1.4's heuristic branches on the device's lighting hardware: the Freestyle Edge
            // has no per-key/indicator led file at all, so a led-shaped file is tried as a layout.
            var session = new FakeProfileSession(KeyboardLayout.Create(DeviceId.FreestyleEdge));

            _picker.SetFile("led1.txt", "[spectrum]>[spd3]");

            await _importer.ImportAsync(session, DeviceId.FreestyleEdge);

            Assert.Equal(ImportedFileKind.Layout, Assert.Single(session.ImportCalls).Kind);
        }

        [Fact]
        public async Task ImportAsync_WhenThePickerFaults_ReportsItAsAFailure()
        {
            _picker.ExceptionToThrow = new IOException("the file went away");

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            Assert.False(outcome.WasApplied);
            Assert.Equal(
                ProfileImporter.FailureMessagePrefix + "the file went away",
                outcome.FailureMessage);
        }

        [Fact]
        public async Task ImportAsync_WhenTheSessionRefusesTheImport_ReportsItAsAFailure()
        {
            _session.ImportExceptionToThrow = new NotSupportedException("no lighting file here");

            _picker.SetFile("layout1.txt", "[caps]>[a]");

            var outcome = await _importer.ImportAsync(_session, DeviceId.FreestyleEdgeRgb);

            Assert.False(outcome.WasApplied);
            Assert.Equal(
                ProfileImporter.FailureMessagePrefix + "no lighting file here",
                outcome.FailureMessage);
        }

        [Fact]
        public async Task ImportAsync_WithoutASession_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _importer.ImportAsync(null!, DeviceId.FreestyleEdgeRgb));
        }

        [Fact]
        public void Constructor_WithoutAPicker_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new ProfileImporter(null!));
        }
    }
}
