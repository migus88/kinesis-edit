using System.Text;
using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.VDrive.Io
{
    public sealed class VDriveFileServiceReadTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly VDriveFileService _service = new();

        public VDriveFileServiceReadTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void ReadAllLines_WithMissingFile_ThrowsFileNotFoundException()
        {
            var path = Path.Combine(_tempDirectory, "missing.txt");

            Assert.Throws<FileNotFoundException>(() => _service.ReadAllLines(path));
        }

        [Theory]
        [InlineData("\r\n")]
        [InlineData("\n")]
        [InlineData("\r")]
        public void ReadAllLines_WithAnyLineEndingStyle_SplitsLines(string newline)
        {
            var path = WriteBytes("lines.txt", Encoding.Latin1.GetBytes($"first{newline}second{newline}third"));

            var lines = _service.ReadAllLines(path);

            Assert.Equal(new[] { "first", "second", "third" }, lines);
        }

        [Fact]
        public void ReadAllLines_WithMixedLineEndings_SplitsAllLines()
        {
            var path = WriteBytes("mixed.txt", Encoding.Latin1.GetBytes("a\r\nb\nc\rd"));

            var lines = _service.ReadAllLines(path);

            Assert.Equal(new[] { "a", "b", "c", "d" }, lines);
        }

        [Theory]
        [InlineData("\r\n")]
        [InlineData("\n")]
        [InlineData("\r")]
        public void ReadAllLines_WithTrailingFinalNewline_DoesNotProduceEmptyLastElement(string newline)
        {
            var path = WriteBytes("trailing.txt", Encoding.Latin1.GetBytes($"one{newline}two{newline}"));

            var lines = _service.ReadAllLines(path);

            Assert.Equal(new[] { "one", "two" }, lines);
        }

        [Fact]
        public void ReadAllLines_WithBlankLinesInMiddle_PreservesThem()
        {
            var path = WriteBytes("blank.txt", Encoding.Latin1.GetBytes("a\n\nb\n"));

            var lines = _service.ReadAllLines(path);

            Assert.Equal(new[] { "a", "", "b" }, lines);
        }

        [Fact]
        public void ReadAllLines_WithEmptyFile_ReturnsNoLines()
        {
            var path = WriteBytes("empty.txt", Array.Empty<byte>());

            var lines = _service.ReadAllLines(path);

            Assert.Empty(lines);
        }

        [Fact]
        public void ReadAllLines_WithBytesAbove0x7F_ReadsEachByteAsOneLatin1Char()
        {
            var path = WriteBytes("high.txt", new byte[] { 0x41, 0xE9, 0xFF, 0x80, 0x0A });

            var lines = _service.ReadAllLines(path);

            Assert.Equal(new[] { "A\u00E9\u00FF\u0080" }, lines);
        }

        [Fact]
        public void ReadAllLines_WithUtf8BomPrefixedFile_KeepsBomBytesAsChars()
        {
            var path = WriteBytes("bom.txt", new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x62, 0x63, 0x0A });

            var lines = _service.ReadAllLines(path);

            Assert.Equal(new[] { "\u00EF\u00BB\u00BFabc" }, lines);
        }

        [Fact]
        public void ReadAllLines_ThenWriteAllLines_RoundTripsHighBytesLosslessly()
        {
            var originalBytes = new byte[] { 0x6B, 0x65, 0x79, 0x3D, 0xE9, 0xFF, 0x80, 0x01, 0x0A, 0xA0, 0x7F, 0x0A };
            var path = WriteBytes("roundtrip.txt", originalBytes);

            var lines = _service.ReadAllLines(path);
            _service.WriteAllLines(path, lines);
            var writtenBytes = File.ReadAllBytes(path);

            Assert.Equal(NormalizeNewlines(originalBytes), NormalizeNewlines(writtenBytes));
            Assert.Contains((byte)0xE9, writtenBytes);
            Assert.DoesNotContain((byte)0xC3, writtenBytes);
        }

        [Fact]
        public void ReadAllLines_ThenWriteAllLines_KeepsUtf8BomBytesThroughTheCycle()
        {
            var originalBytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x6B, 0x65, 0x79, 0x3D, 0x31, 0x0A };
            var path = WriteBytes("bomcycle.txt", originalBytes);

            var lines = _service.ReadAllLines(path);
            _service.WriteAllLines(path, lines);
            var writtenBytes = File.ReadAllBytes(path);

            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, writtenBytes.Take(3).ToArray());
            Assert.Equal(NormalizeNewlines(originalBytes), NormalizeNewlines(writtenBytes));
        }

        private string WriteBytes(string fileName, byte[] bytes)
        {
            var path = Path.Combine(_tempDirectory, fileName);
            File.WriteAllBytes(path, bytes);

            return path;
        }

        private static string NormalizeNewlines(byte[] bytes)
        {
            return Encoding.Latin1.GetString(bytes).Replace("\r\n", "\n");
        }

        public void Dispose()
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }
}
