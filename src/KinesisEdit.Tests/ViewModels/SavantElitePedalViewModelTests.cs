using KinesisEdit.Core.Devices;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive.Discovery;
using KinesisEdit.Tests.Services;
using KinesisEdit.ViewModels;

namespace KinesisEdit.Tests.ViewModels
{
    /// <summary>
    /// The read-only Savant Elite2 view (specs/12-savant-elite.md §5): always seven rows with the
    /// §5 assignment text, unparseable lines surfaced rather than dropped, and the three ways
    /// there is nothing to read — demo mode, a v-Drive without a pedals.txt, and a failed read.
    /// </summary>
    public sealed class SavantElitePedalViewModelTests
    {
        private const string PedalsFolder = "active";

        private const string PedalsFileName = "pedals.txt";

        /// <summary>Where 12 §4.6 puts the file on a discovered SE2 v-Drive.</summary>
        private static string PedalFilePath =>
            Path.Combine(
                TestDevices.CreateLocation(DeviceId.SavantElite2).RootPath,
                PedalsFolder,
                PedalsFileName);

        private readonly FakeVDriveFileService _fileService = new();
        private readonly PedalFileService _pedalFiles;

        public SavantElitePedalViewModelTests()
        {
            _pedalFiles = new PedalFileService(_fileService);
        }

        [Fact]
        public void Inputs_ForAnyFile_AreAllSevenInFileOrder()
        {
            // 12 §1: "your device will only have some of these inputs" — the file says nothing
            // about which, so every input is always listed.
            var viewModel = Load("[lpedal]>[lmouse]");

            Assert.Equal(PedalInputs.Count, viewModel.Inputs.Count);
            Assert.Equal(PedalInputs.All, viewModel.Inputs.Select(row => row.Id));
            Assert.Equal(
                new[] { "Left Pedal", "Middle Pedal", "Right Pedal", "Jack 1", "Jack 2", "Jack 3", "Jack 4" },
                viewModel.Inputs.Select(row => row.Name));
        }

        [Fact]
        public void Inputs_ForASingleAction_ShowTheBracketedAssignment()
        {
            var row = Load("[lpedal]>[lmouse]").Inputs[0];

            Assert.True(row.IsAssigned);
            Assert.Equal(PedalInputMode.Single, row.Mode);
            Assert.Equal(PedalInputRowViewModel.SingleModeCaption, row.ModeCaption);
            Assert.Equal("[lmouse]", row.AssignmentText);
        }

        [Fact]
        public void Inputs_ForAMacro_ShowTheDescribedMacroText()
        {
            var viewModel = Load(
                "{jack4}>{-shift}{-t}{+t}{+shift}{-h}{+h}{-a}{+a}{-n}{+n}{-k}{+k}{-space}{+space}{-y}{+y}{-o}{+o}{-u}{+u}{-.}{+.}",
                "{mpedal}>{-lmouse}{+lmouse}{125}{-lmouse}{+lmouse}");

            var jack4 = viewModel.Inputs[^1];
            var middle = viewModel.Inputs[1];

            Assert.Equal(PedalInputMode.Macro, jack4.Mode);
            Assert.Equal(PedalInputRowViewModel.MacroModeCaption, jack4.ModeCaption);
            Assert.Equal("{shift+t}hank you.", jack4.AssignmentText);
            Assert.Equal(PedalActionDescriber.LeftMouseDoubleClickText, middle.AssignmentText);
        }

        [Fact]
        public void Inputs_ForAnInputTheFileDoesNotProgram_AreFlaggedUnassigned()
        {
            var row = Load("[lpedal]>[lmouse]").Inputs[1];

            Assert.False(row.IsAssigned);
            Assert.Equal(PedalInputRowViewModel.UnassignedCaption, row.AssignmentText);
            Assert.Equal(string.Empty, row.ModeCaption);
        }

        [Fact]
        public void Inputs_ForAnEmptyMacroLine_AreFlaggedUnassigned()
        {
            // {jack1}> parses as macro mode with nothing in it; a row reading "Macro" with no
            // text would be a worse answer than one reading unassigned.
            var row = Load("{jack1}>").Inputs[3];

            Assert.False(row.IsAssigned);
            Assert.Equal(PedalInputRowViewModel.UnassignedCaption, row.AssignmentText);
            Assert.Equal(string.Empty, row.ModeCaption);
        }

        [Fact]
        public void InvalidLines_ForALineThatCannotBeParsed_AreSurfacedWithTheirNumberAndText()
        {
            var viewModel = Load("[lpedal]>[lmouse]", "[jack1]>[garbage]");

            Assert.True(viewModel.HasInvalidLines);

            var invalid = Assert.Single(viewModel.InvalidLines);

            Assert.Equal(2, invalid.LineNumber);
            Assert.Equal("Line 2", invalid.LineCaption);
            Assert.Equal("Jack 1", invalid.InputName);
            Assert.Equal("[jack1]>[garbage]", invalid.Text);

            // The rest of the file still applies, and the broken input stays unassigned.
            Assert.Equal("[lmouse]", viewModel.Inputs[0].AssignmentText);
            Assert.False(viewModel.Inputs[3].IsAssigned);
        }

        [Fact]
        public void InvalidLines_ForAFileThatParsesCompletely_AreEmpty()
        {
            var viewModel = Load("[lpedal]>[lmouse]");

            Assert.False(viewModel.HasInvalidLines);
            Assert.Empty(viewModel.InvalidLines);
        }

        [Fact]
        public void LoadState_ForAProgrammedFile_IsLoadedWithNoNoteAndThePedalFilePath()
        {
            var viewModel = Load("[lpedal]>[lmouse]");

            Assert.Equal(PedalLoadState.Loaded, viewModel.LoadState);
            Assert.False(viewModel.HasStatusMessage);
            Assert.Equal(string.Empty, viewModel.StatusMessage);
            Assert.True(viewModel.HasFilePath);
            Assert.Equal(PedalFilePath, viewModel.FilePath);
        }

        [Fact]
        public void LoadState_ForAFileThatProgramsNothing_ExplainsItIsEmpty()
        {
            var viewModel = Load("[lpedal]>", "[mpedal]>", "[rpedal]>");

            Assert.Equal(PedalLoadState.Loaded, viewModel.LoadState);
            Assert.Equal(SavantElitePedalViewModel.EmptyFileMessage, viewModel.StatusMessage);
            Assert.All(viewModel.Inputs, row => Assert.False(row.IsAssigned));
        }

        [Fact]
        public void LoadState_WithoutAPedalFileOnTheDrive_ExplainsItRatherThanThrowing()
        {
            // A pedal that has never been programmed simply has no file; 12 §5 step 8 only turns
            // that into a prompt when saving, which this read-only view never does.
            var viewModel = Create(VDriveConnectionStatus.Connected);

            Assert.Equal(PedalLoadState.FileMissing, viewModel.LoadState);
            Assert.Equal(SavantElitePedalViewModel.MissingFileMessage, viewModel.StatusMessage);
            Assert.Equal(PedalInputs.Count, viewModel.Inputs.Count);
            Assert.All(viewModel.Inputs, row => Assert.False(row.IsAssigned));
            Assert.Empty(viewModel.InvalidLines);
        }

        [Fact]
        public void LoadState_WhenTheFileCannotBeRead_ReportsAReadableError()
        {
            _fileService.SetFile(PedalFilePath, "[lpedal]>[lmouse]");
            _fileService.SetUnreadable(PedalFilePath);

            var viewModel = Create(VDriveConnectionStatus.Connected);

            Assert.Equal(PedalLoadState.LoadFailed, viewModel.LoadState);
            Assert.StartsWith(SavantElitePedalViewModel.LoadFailureMessagePrefix, viewModel.StatusMessage);
            Assert.Contains(PedalFilePath, viewModel.StatusMessage);
            Assert.Equal(PedalInputs.Count, viewModel.Inputs.Count);
            Assert.All(viewModel.Inputs, row => Assert.False(row.IsAssigned));
        }

        [Theory]
        [InlineData(VDriveConnectionStatus.NotDetected)]
        [InlineData(VDriveConnectionStatus.CannotAccess)]
        public void Load_InDemoMode_TouchesNoFilesAndShowsTheDemoNote(VDriveConnectionStatus status)
        {
            // Demo mode never touches the drive (03 §3.5) — not even the present-but-unwritable
            // one a 'Cannot Access' card opens.
            _fileService.SetFile(PedalFilePath, "[lpedal]>[lmouse]");

            var viewModel = Create(status);

            Assert.Equal(0, _fileService.ReadCount);
            Assert.Equal(PedalLoadState.DemoMode, viewModel.LoadState);
            Assert.Equal(SavantElitePedalViewModel.DemoModeMessage, viewModel.StatusMessage);
            Assert.True(viewModel.IsDemoMode);
            Assert.False(viewModel.HasFilePath);
            Assert.Equal(PedalInputs.Count, viewModel.Inputs.Count);
            Assert.All(viewModel.Inputs, row => Assert.False(row.IsAssigned));
        }

        [Fact]
        public void DeviceName_ForTheOpenPedal_ComesFromTheSnapshot()
        {
            var viewModel = Load("[lpedal]>[lmouse]");

            Assert.Equal(DeviceCatalog.GetById(DeviceId.SavantElite2).DisplayName, viewModel.DeviceName);
            Assert.False(viewModel.IsDemoMode);
        }

        private SavantElitePedalViewModel Load(params string[] lines)
        {
            _fileService.SetFile(PedalFilePath, lines);

            return Create(VDriveConnectionStatus.Connected);
        }

        private SavantElitePedalViewModel Create(VDriveConnectionStatus status)
        {
            return new SavantElitePedalViewModel(
                TestDevices.CreateSnapshot(DeviceId.SavantElite2, status),
                _pedalFiles);
        }
    }
}
