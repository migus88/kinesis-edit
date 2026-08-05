using System.Text;
using KinesisEdit.Core.Devices;
using KinesisEdit.Core.Model;
using KinesisEdit.Core.SavantElite;
using KinesisEdit.Core.VDrive;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// The v-Drive binding of the pedal file (specs/12-savant-elite.md §4.6 save mechanics,
    /// §5 step 7-8): the path on the drive, byte-exact preservation of every non-pedal line, in
    /// place rewriting of pedal lines, and outright creation of the seven-line file when it is
    /// missing. Runs against real temp directories through <see cref="VDriveFileService"/>.
    /// </summary>
    public sealed class PedalFileServiceTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly PedalFileService _service = new(new VDriveFileService());

        public PedalFileServiceTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void GetPedalFilePath_ForSavantElite2_PointsAtTheActiveFolder()
        {
            var location = CreateLocation();

            Assert.Equal(
                Path.Combine(location.RootPath, "active", "pedals.txt"),
                PedalFileService.GetPedalFilePath(location));
        }

        [Fact]
        public void GetPedalFilePath_ForADeviceWithoutAPedalFile_Throws()
        {
            var location = CreateLocation(DeviceId.Advantage2);

            Assert.Throws<InvalidOperationException>(() => PedalFileService.GetPedalFilePath(location));
        }

        [Fact]
        public void Load_WithMissingFile_ThrowsFileNotFoundException()
        {
            var location = CreateLocation();

            Assert.Throws<FileNotFoundException>(() => _service.Load(location));
        }

        [Fact]
        public void Save_WithFactoryFile_LeavesEveryNonPedalLineByteIdentical()
        {
            var location = CreateLocation();
            var path = PedalFileService.GetPedalFilePath(location);

            WriteFile(path, PedalFixtures.FactoryFile);

            var result = _service.Load(location);

            _service.Save(location, result.Configuration);

            var expected = new List<string>(PedalFixtures.FactoryFile);

            expected[1] = "{mpedal}>{lmouse}{d125}{lmouse}";
            expected[6] = "{jack4}>{-shift}{t}{+shift}{h}{a}{n}{k}{ }{y}{o}{u}{.}";

            Assert.Equal(BuildFileBytes([.. expected]), File.ReadAllBytes(path));
        }

        [Fact]
        public void Save_WithAnEditedInput_RewritesOnlyThatLineAtTheSameIndex()
        {
            var location = CreateLocation();
            var path = PedalFileService.GetPedalFilePath(location);

            WriteFile(path, "* header", "[lpedal]>[a]", "[mpedal]>[b]", "* footer");

            var result = _service.Load(location);

            result.Configuration[PedalInputId.MiddlePedal].SetSingleAction(PedalFixtures.Key("pdown"));
            _service.Save(location, result.Configuration);

            Assert.Equal(
                BuildFileBytes("* header", "[lpedal]>[a]", "[mpedal]>[pdown]", "* footer"),
                File.ReadAllBytes(path));
        }

        [Fact]
        public void Save_WithAFileListingSomeInputsAndNoEdit_RewritesItWithoutAddingLines()
        {
            var location = CreateLocation();
            var path = PedalFileService.GetPedalFilePath(location);

            string[] file =
            [
                "[lpedal]>[a]",
                "[mpedal]>[b]",
                "[rpedal]>[c]",
                "[jack1]>[d]",
                "[jack2]>",
                "* Above are the assigned actions for 7 possible inputs"
            ];

            WriteFile(path, file);

            var result = _service.Load(location);

            _service.Save(location, result.Configuration);

            Assert.Equal(BuildFileBytes(file), File.ReadAllBytes(path));
        }

        [Fact]
        public void Save_WithAnInputFlippedToMacroMode_KeepsTheLineIndexAndSwitchesBracketForm()
        {
            var location = CreateLocation();
            var path = PedalFileService.GetPedalFilePath(location);

            WriteFile(path, "* header", "[lpedal]>[a]", "* footer");

            var result = _service.Load(location);
            var macro = new Macro();

            macro.AddKeystroke(new Keystroke(PedalFixtures.Key("b")));
            macro.AddKeystroke(new Keystroke(PedalTokenMap.Delay125Key));
            result.Configuration[PedalInputId.LeftPedal].SetMacro(macro);

            _service.Save(location, result.Configuration);

            var lines = File.ReadAllLines(path);

            Assert.Equal("* header", lines[0]);
            Assert.Equal("{lpedal}>{b}{d125}", lines[1]);
            Assert.Equal("* footer", lines[2]);
        }

        [Fact]
        public void Save_WithANewlyAssignedInputThatHasNoLine_InsertsItNextToThePedalBlock()
        {
            var location = CreateLocation();
            var path = PedalFileService.GetPedalFilePath(location);

            WriteFile(path, "[lpedal]>[a]", "* the factory instruction block");

            var result = _service.Load(location);

            result.Configuration[PedalInputId.Jack4].SetSingleAction(PedalFixtures.Key("rmouse"));
            _service.Save(location, result.Configuration);

            Assert.Equal(
                BuildFileBytes("[lpedal]>[a]", "[jack4]>[rmouse]", "* the factory instruction block"),
                File.ReadAllBytes(path));
        }

        [Fact]
        public void Save_WithMissingFile_CreatesSevenLinesInFixedOrder()
        {
            var location = CreateLocation();
            var configuration = new PedalConfiguration();

            configuration[PedalInputId.Jack1].SetSingleAction(PedalFixtures.Key("d"));
            _service.Save(location, configuration);

            var path = PedalFileService.GetPedalFilePath(location);

            Assert.Equal(
                BuildFileBytes("[lpedal]>", "[mpedal]>", "[rpedal]>", "[jack1]>[d]", "[jack2]>", "[jack3]>", "[jack4]>"),
                File.ReadAllBytes(path));
        }

        [Fact]
        public void Save_AfterLoad_KeepsHighByteCommentCharactersIntact()
        {
            var location = CreateLocation();
            var path = PedalFileService.GetPedalFilePath(location);

            WriteFile(path, "* café ±", "[lpedal]>[a]");

            var result = _service.Load(location);

            _service.Save(location, result.Configuration);

            Assert.Equal("* café ±", File.ReadAllLines(path, Encoding.Latin1)[0]);
        }

        private VDriveLocation CreateLocation(DeviceId deviceId = DeviceId.SavantElite2)
        {
            var device = DeviceCatalog.GetById(deviceId);
            var rootPath = Path.Combine(_tempDirectory, deviceId.ToString());

            Directory.CreateDirectory(Path.Combine(rootPath, "active"));

            return new VDriveLocation
            {
                Device = device,
                RootPath = rootPath,
                IsWritable = true,
            };
        }

        [Fact]
        public void Save_WhenTheFileVanishesBetweenTheReadAndTheWrite_DoesNotFallBackToCreatingAFreshFile()
        {
            var files = new FakeVDriveFileService(
                "[lpedal]>[lmouse]",
                "* Factory instructions the merge must never discard.")
            {
                FailNextWrite = true,
            };
            var service = new PedalFileService(files);
            var configuration = new PedalConfiguration();

            Assert.Throws<FileNotFoundException>(
                () => service.Save(CreateLocation(), configuration));

            // One refused write and no second, create-a-fresh-file attempt: the seven-line form
            // must never reach a drive that already held the factory block (12 §4.6).
            Assert.Equal(1, files.WriteCount);
            Assert.False(files.WrittenWithAllowCreate);
        }

        private static void WriteFile(string path, params string[] lines)
        {
            File.WriteAllBytes(path, BuildFileBytes(lines));
        }

        private static byte[] BuildFileBytes(params string[] lines)
        {
            var content = new StringBuilder();

            foreach (var line in lines)
            {
                content.Append(line);
                content.Append(Environment.NewLine);
            }

            return Encoding.Latin1.GetBytes(content.ToString());
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
