using KinesisEdit.Core.Transfer;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The Export files overlay of specs/11-feature-dialogs.md §11.5: the three mutually
    /// exclusive checkboxes, the directory picker, the writes into the chosen directory, and the
    /// two failure messages plus the success message — all verbatim.
    /// </summary>
    public sealed class ExportOverlayViewModelTests
    {
        private const string Folder = "/tmp/export-target";

        private readonly FakeFolderPickerService _folderPicker = new() { FolderToReturn = Folder };
        private readonly FakeVDriveFileService _files = new();
        private readonly FakeNotificationService _notifications = new();

        [Fact]
        public void Strings_MatchTheSpecVerbatim()
        {
            Assert.Equal("Export files", ExportOverlayViewModel.OverlayTitle);
            Assert.Equal("Layout and Lighting", ExportOverlayViewModel.LayoutAndLightingCaption);
            Assert.Equal("Layout only", ExportOverlayViewModel.LayoutOnlyCaption);
            Assert.Equal("Lighting only", ExportOverlayViewModel.LightingOnlyCaption);
            Assert.Equal("Error exporting layout file: ", ExportOverlayViewModel.LayoutFailurePrefix);
            Assert.Equal("Error exporting lighting file: ", ExportOverlayViewModel.LightingFailurePrefix);
            Assert.Equal("Files exported successfully!", ExportOverlayViewModel.SuccessMessage);
        }

        [Fact]
        public void Constructor_Always_ChecksLayoutAndLighting()
        {
            var overlay = Create(out _);

            Assert.Equal(ExportOverlayViewModel.OverlayTitle, overlay.Title);
            Assert.Equal(ProfileExportSelection.LayoutAndLighting, overlay.Selection);
            Assert.True(overlay.IsLayoutAndLightingSelected);
            Assert.False(overlay.IsLayoutOnlySelected);
            Assert.False(overlay.IsLightingOnlySelected);
        }

        [Fact]
        public void IsLayoutOnlySelected_WhenChecked_UnchecksTheOtherTwo()
        {
            var overlay = Create(out _);

            overlay.IsLayoutOnlySelected = true;

            Assert.True(overlay.IsLayoutOnlySelected);
            Assert.False(overlay.IsLayoutAndLightingSelected);
            Assert.False(overlay.IsLightingOnlySelected);
            Assert.Equal(ProfileExportSelection.LayoutOnly, overlay.Selection);
        }

        [Fact]
        public void IsLightingOnlySelected_WhenChecked_UnchecksTheOtherTwo()
        {
            var overlay = Create(out _);

            overlay.IsLayoutOnlySelected = true;
            overlay.IsLightingOnlySelected = true;

            Assert.True(overlay.IsLightingOnlySelected);
            Assert.False(overlay.IsLayoutOnlySelected);
            Assert.False(overlay.IsLayoutAndLightingSelected);
        }

        [Fact]
        public void IsLayoutAndLightingSelected_WhenUnchecked_StaysCheckedSoOneChoiceIsAlwaysLive()
        {
            var overlay = Create(out _);

            overlay.IsLayoutAndLightingSelected = false;

            Assert.True(overlay.IsLayoutAndLightingSelected);
            Assert.Equal(ProfileExportSelection.LayoutAndLighting, overlay.Selection);
        }

        [Fact]
        public void ExportCommand_InDemoMode_IsDisabledAndDoesNothing()
        {
            var overlay = new ExportOverlayViewModel(null, _folderPicker, _files, _notifications);

            Assert.False(overlay.CanExport);
            Assert.False(overlay.ExportCommand.CanExecute(null));

            overlay.ExportCommand.Execute(null);

            Assert.Equal(0, _folderPicker.PickCount);
            Assert.Empty(_files.WrittenPaths);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public async Task ExportCommand_WithAChosenFolder_WritesEveryPlannedFileIntoItAndReportsSuccess()
        {
            var overlay = Create(out var session);

            session.ExportPlan = [Layout(), Lighting()];

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Equal([Path.Combine(Folder, "layout1.txt"), Path.Combine(Folder, "led1.txt")], _files.WrittenPaths);
            Assert.Equal(["[F1]>[a]"], _files.ReadAllLines(Path.Combine(Folder, "layout1.txt")));
            Assert.Equal(ExportOverlayViewModel.SuccessMessage, Assert.Single(_notifications.MessageBoxes).Message);
            Assert.True(overlay.WasAccepted);
            Assert.True(overlay.IsClosed);
        }

        [Fact]
        public async Task ExportCommand_Always_OpensThePickerUnderTheDialogTitle()
        {
            var overlay = Create(out var session);

            session.ExportPlan = [Layout()];

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Equal([ExportOverlayViewModel.OverlayTitle], _folderPicker.Titles);
        }

        [Fact]
        public async Task ExportCommand_WithLightingOnlySelected_PlansThatScope()
        {
            var overlay = Create(out var session);

            session.ExportPlan = [Lighting()];
            overlay.IsLightingOnlySelected = true;

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Equal([ProfileExportSelection.LightingOnly], session.ExportSelections);
            Assert.Equal([Path.Combine(Folder, "led1.txt")], _files.WrittenPaths);
        }

        [Fact]
        public async Task ExportCommand_WhenThePickerIsCancelled_WritesNothingAndSaysNothing()
        {
            var overlay = Create(out var session);

            session.ExportPlan = [Layout()];
            _folderPicker.FolderToReturn = null;

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Empty(_files.WrittenPaths);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.Empty(session.ExportSelections);
            Assert.Null(overlay.ErrorMessage);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public async Task ExportCommand_WhenThePickerFails_IsTreatedAsACancellation()
        {
            var overlay = Create(out var session);

            session.ExportPlan = [Layout()];
            _folderPicker.ExceptionToThrow = new InvalidOperationException("no storage provider");

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Empty(_files.WrittenPaths);
            Assert.Empty(_notifications.MessageBoxes);
            Assert.False(overlay.IsClosed);
        }

        [Fact]
        public async Task ExportCommand_WhenTheLayoutWriteFails_ReportsTheSpecMessageAndStaysOpen()
        {
            var failing = new FailingFileService("layout1.txt", "disk full");
            var session = new FakeProfileSession(TestLayouts.CreateLayout("esc")) { ExportPlan = [Layout(), Lighting()] };
            var overlay = new ExportOverlayViewModel(session, _folderPicker, failing, _notifications);

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Equal(
                ExportOverlayViewModel.LayoutFailurePrefix + "disk full",
                Assert.Single(_notifications.MessageBoxes).Message);
            Assert.False(overlay.IsClosed);
            Assert.Empty(failing.WrittenPaths);
        }

        [Fact]
        public async Task ExportCommand_WhenTheLightingWriteFails_ReportsTheSpecMessage()
        {
            var failing = new FailingFileService("led1.txt", "read-only volume");
            var session = new FakeProfileSession(TestLayouts.CreateLayout("esc")) { ExportPlan = [Layout(), Lighting()] };
            var overlay = new ExportOverlayViewModel(session, _folderPicker, failing, _notifications);

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Equal(
                ExportOverlayViewModel.LightingFailurePrefix + "read-only volume",
                Assert.Single(_notifications.MessageBoxes).Message);
            Assert.Equal([Path.Combine(Folder, "layout1.txt")], failing.WrittenPaths);
        }

        [Fact]
        public async Task ExportCommand_WhenTheOnlyLightingFileFails_StillReportsTheLightingMessage()
        {
            var failing = new FailingFileService("led1.txt", "read-only volume");
            var session = new FakeProfileSession(TestLayouts.CreateLayout("esc")) { ExportPlan = [Lighting()] };
            var overlay = new ExportOverlayViewModel(session, _folderPicker, failing, _notifications)
            {
                IsLightingOnlySelected = true
            };

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.StartsWith(
                ExportOverlayViewModel.LightingFailurePrefix,
                Assert.Single(_notifications.MessageBoxes).Message);
        }

        [Fact]
        public async Task ExportCommand_AfterASuccessfulExport_CannotRunAgain()
        {
            var overlay = Create(out var session);

            session.ExportPlan = [Layout()];

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.False(overlay.ExportCommand.CanExecute(null));

            await overlay.ExportCommand.ExecuteAsync(null);

            Assert.Equal(1, _folderPicker.PickCount);
        }

        [Fact]
        public void AcceptCommand_WithoutAnExport_ClosesNothing()
        {
            var overlay = Create(out _);

            overlay.AcceptCommand.Execute(null);

            Assert.False(overlay.IsClosed);
        }

        private ExportOverlayViewModel Create(out FakeProfileSession session)
        {
            session = new FakeProfileSession(TestLayouts.CreateLayout("esc"));

            return new ExportOverlayViewModel(session, _folderPicker, _files, _notifications);
        }

        private static ExportFile Layout()
        {
            return new ExportFile("layout1.txt", ["[F1]>[a]"]);
        }

        private static ExportFile Lighting()
        {
            return new ExportFile("led1.txt", ["[all]>[255,0,0]"]);
        }

        /// <summary>
        /// An <see cref="IVDriveFileService"/> that fails the write of one base name, which is
        /// how a full disk or a read-only target directory reaches the export flow.
        /// </summary>
        private sealed class FailingFileService : IVDriveFileService
        {
            public List<string> WrittenPaths { get; } = [];

            private readonly string _failingFileName;
            private readonly string _message;

            public FailingFileService(string failingFileName, string message)
            {
                _failingFileName = failingFileName;
                _message = message;
            }

            public IReadOnlyList<string> ReadAllLines(string path)
            {
                throw new NotSupportedException();
            }

            public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
            {
                if (Path.GetFileName(path) == _failingFileName)
                {
                    throw new IOException(_message);
                }

                WrittenPaths.Add(path);
            }

            public void UpdateSettingsFile(
                string path,
                IEnumerable<KeyValuePair<string, string>> values,
                IEnumerable<string>? removedKeys = null)
            {
                throw new NotSupportedException();
            }
        }
    }
}
