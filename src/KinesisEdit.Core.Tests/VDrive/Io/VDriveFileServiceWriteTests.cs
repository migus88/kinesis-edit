using System.Text;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.VDrive.Io
{
    public sealed class VDriveFileServiceWriteTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly VDriveFileService _service = new();

        public VDriveFileServiceWriteTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void WriteAllLines_WithMissingFileAndNoAllowCreate_ThrowsFileNotFoundException()
        {
            var path = Path.Combine(_tempDirectory, "missing.txt");

            Assert.Throws<FileNotFoundException>(() => _service.WriteAllLines(path, new[] { "line" }));
            Assert.False(File.Exists(path));
        }

        [Fact]
        public void WriteAllLines_WithMissingFileAndAllowCreate_CreatesFile()
        {
            var path = Path.Combine(_tempDirectory, "created.txt");

            _service.WriteAllLines(path, new[] { "first", "second" }, allowCreate: true);

            var expected = "first" + Environment.NewLine + "second" + Environment.NewLine;
            Assert.Equal(expected, File.ReadAllText(path, Encoding.Latin1));
        }

        [Fact]
        public void WriteAllLines_WithMissingParentDirectoryAndAllowCreate_CreatesDirectoryAndFile()
        {
            var path = Path.Combine(_tempDirectory, "no-such-folder", "file.txt");

            _service.WriteAllLines(path, new[] { "line" }, allowCreate: true);

            Assert.Equal("line" + Environment.NewLine, File.ReadAllText(path, Encoding.Latin1));
        }

        [Fact]
        public void WriteAllLines_WithNestedMissingParentDirectoriesAndAllowCreate_CreatesAllOfThem()
        {
            var path = Path.Combine(_tempDirectory, "outer", "inner", "file.txt");

            _service.WriteAllLines(path, new[] { "line" }, allowCreate: true);

            Assert.Equal("line" + Environment.NewLine, File.ReadAllText(path, Encoding.Latin1));
        }

        [Fact]
        public void WriteAllLines_WithMissingParentDirectoryAndNoAllowCreate_ThrowsAndCreatesNothing()
        {
            var directory = Path.Combine(_tempDirectory, "no-such-folder");
            var path = Path.Combine(directory, "file.txt");

            Assert.Throws<FileNotFoundException>(() => _service.WriteAllLines(path, new[] { "line" }));
            Assert.False(Directory.Exists(directory));
        }

        [Fact]
        public void WriteAllLines_WithShorterContentThanExistingFile_TruncatesFile()
        {
            var path = Path.Combine(_tempDirectory, "long.txt");
            File.WriteAllText(path, new string('x', 500), Encoding.Latin1);

            _service.WriteAllLines(path, new[] { "tiny" });

            Assert.Equal("tiny" + Environment.NewLine, File.ReadAllText(path, Encoding.Latin1));
        }

        [Fact]
        public void WriteAllLines_WithLines_EndsEachLineWithNativeNewline()
        {
            var path = Path.Combine(_tempDirectory, "newlines.txt");

            _service.WriteAllLines(path, new[] { "a", "b" }, allowCreate: true);

            var expected = "a" + Environment.NewLine + "b" + Environment.NewLine;
            Assert.Equal(expected, File.ReadAllText(path, Encoding.Latin1));
        }

        [Fact]
        public void WriteAllLines_WithEmptyLineList_WritesEmptyFile()
        {
            var path = Path.Combine(_tempDirectory, "empty.txt");
            File.WriteAllText(path, "old content", Encoding.Latin1);

            _service.WriteAllLines(path, Array.Empty<string>());

            Assert.Empty(File.ReadAllBytes(path));
        }

        [Fact]
        public void WriteAllLines_WithCharsAbove0x7F_WritesOneLatin1BytePerChar()
        {
            var path = Path.Combine(_tempDirectory, "high.txt");

            _service.WriteAllLines(path, new[] { "A\u00E9\u00FF" }, allowCreate: true);

            var bytes = File.ReadAllBytes(path);
            var newlineLength = Environment.NewLine.Length;
            Assert.Equal(3 + newlineLength, bytes.Length);
            Assert.Equal(0x41, bytes[0]);
            Assert.Equal(0xE9, bytes[1]);
            Assert.Equal(0xFF, bytes[2]);
        }

        [Fact]
        public void WriteAllLines_WithNewFile_NeverWritesBom()
        {
            var path = Path.Combine(_tempDirectory, "nobom.txt");

            _service.WriteAllLines(path, new[] { "key=value" }, allowCreate: true);

            var bytes = File.ReadAllBytes(path);
            Assert.Equal((byte)'k', bytes[0]);
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
