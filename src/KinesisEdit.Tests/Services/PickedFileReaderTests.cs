using System.Text;
using KinesisEdit.Core.VDrive.Io;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Decoding of an imported file (specs/07-lighting.md §1.4). The rules are not this module's
    /// own: they are the v-Drive reader's (specs/03-vdrive-and-files.md §5.1, docs/app/vdrive.md)
    /// — Latin1 bytes, no BOM handling, CRLF/LF/lone-CR split — because an imported file must
    /// parse identically to the same file read off the drive. The last test pins the two
    /// implementations against each other so the duplicated splitter cannot drift.
    /// </summary>
    public sealed class PickedFileReaderTests : IDisposable
    {
        private readonly string _tempDirectory;

        public PickedFileReaderTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "KinesisEditTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public async Task ReadAsync_WithEveryLineEnding_SplitsThemAll()
        {
            var file = await ReadTextAsync("first\r\nsecond\nthird\rfourth");

            Assert.Equal(new[] { "first", "second", "third", "fourth" }, file.Lines);
        }

        [Fact]
        public async Task ReadAsync_WithATrailingNewline_YieldsNoEmptyLastLine()
        {
            var file = await ReadTextAsync("first\r\nsecond\r\n");

            Assert.Equal(new[] { "first", "second" }, file.Lines);
        }

        [Fact]
        public async Task ReadAsync_WithABlankLineInTheMiddle_KeepsIt()
        {
            var file = await ReadTextAsync("first\n\nthird\n");

            Assert.Equal(new[] { "first", string.Empty, "third" }, file.Lines);
        }

        [Fact]
        public async Task ReadAsync_WithHighBytes_DecodesThemAsLatin1()
        {
            var file = await ReadBytesAsync([0x5B, 0xE9, 0xFF, 0x5D]);

            Assert.Equal("[éÿ]", Assert.Single(file.Lines));
        }

        [Fact]
        public async Task ReadAsync_WithAUtf8Bom_KeepsItAsOrdinaryCharacters()
        {
            var file = await ReadBytesAsync([0xEF, 0xBB, 0xBF, 0x61]);

            Assert.Equal("ï»¿a", Assert.Single(file.Lines));
        }

        [Fact]
        public async Task ReadAsync_WithAnEmptyStream_YieldsNoLinesAndNoLength()
        {
            var file = await ReadBytesAsync([]);

            Assert.Empty(file.Lines);
            Assert.Equal(0, file.ByteLength);
            Assert.False(file.IsTruncated);
        }

        [Fact]
        public async Task ReadAsync_ReportsTheNameAndPathItWasGiven()
        {
            using var stream = new MemoryStream(Encoding.Latin1.GetBytes("[1>1]"));

            var file = await PickedFileReader.ReadAsync("layout1.txt", "/tmp/layout1.txt", stream);

            Assert.Equal("layout1.txt", file.Name);
            Assert.Equal("/tmp/layout1.txt", file.Path);
            Assert.Equal(5, file.ByteLength);
        }

        [Fact]
        public async Task ReadAsync_WithoutAName_Throws()
        {
            using var stream = new MemoryStream();

            await Assert.ThrowsAsync<ArgumentException>(() => PickedFileReader.ReadAsync(string.Empty, null, stream));
        }

        [Fact]
        public async Task ReadAsync_WithoutAStream_Throws()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => PickedFileReader.ReadAsync("layout1.txt", null, null!));
        }

        [Fact]
        public async Task ReadAsync_AtExactlyTheReadCap_KeepsEverything()
        {
            var file = await ReadBytesAsync(CreateFiller(PickedFileReader.MaxReadBytes));

            Assert.Equal(PickedFileReader.MaxReadBytes, file.ByteLength);
            Assert.Equal((int)PickedFileReader.MaxReadBytes, Assert.Single(file.Lines).Length);
            Assert.False(file.IsTruncated);
        }

        [Fact]
        public async Task ReadAsync_OverTheReadCap_ReportsTheTrueLengthAndStopsAtTheCap()
        {
            var file = await ReadBytesAsync(CreateFiller(PickedFileReader.MaxReadBytes + 10));

            // The 50 KB refusal of specs/07-lighting.md §1.4 is the caller's, so the length must
            // be the file's real one even though only the cap was buffered.
            Assert.Equal(PickedFileReader.MaxReadBytes + 10, file.ByteLength);
            Assert.Equal((int)PickedFileReader.MaxReadBytes, Assert.Single(file.Lines).Length);
            Assert.True(file.IsTruncated);
        }

        [Fact]
        public async Task ReadAsync_OverTheReadCapOnAStreamWithNoLength_StillReportsTheTrueLength()
        {
            using var source = new MemoryStream(CreateFiller(PickedFileReader.MaxReadBytes + 10));
            using var stream = new NonSeekableStream(source);

            var file = await PickedFileReader.ReadAsync("huge.txt", null, stream);

            Assert.Equal(PickedFileReader.MaxReadBytes + 10, file.ByteLength);
            Assert.Equal((int)PickedFileReader.MaxReadBytes, Assert.Single(file.Lines).Length);
            Assert.True(file.IsTruncated);
        }

        [Fact]
        public async Task ReadAsync_OverTheSameBytes_MatchesTheVDriveFileService()
        {
            // Latin1 high bytes, a UTF-8 BOM, all three line endings, a blank line, and no
            // trailing newline — every rule the two implementations share, in one file.
            byte[] bytes =
            [
                0xEF, 0xBB, 0xBF,
                .. Encoding.Latin1.GetBytes("[1>1]\r\n"),
                .. Encoding.Latin1.GetBytes("café\n"),
                .. Encoding.Latin1.GetBytes("\r"),
                .. Encoding.Latin1.GetBytes("\n"),
                .. Encoding.Latin1.GetBytes("last line")
            ];

            var path = Path.Combine(_tempDirectory, "parity.txt");

            await File.WriteAllBytesAsync(path, bytes);

            var expected = new VDriveFileService().ReadAllLines(path);
            var actual = await ReadBytesAsync(bytes);

            Assert.Equal(expected, actual.Lines);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }

        private static Task<PickedFile> ReadTextAsync(string text)
        {
            return ReadBytesAsync(Encoding.Latin1.GetBytes(text));
        }

        private static async Task<PickedFile> ReadBytesAsync(byte[] bytes)
        {
            using var stream = new MemoryStream(bytes);

            return await PickedFileReader.ReadAsync("picked.txt", null, stream);
        }

        private static byte[] CreateFiller(long length)
        {
            var bytes = new byte[length];

            Array.Fill(bytes, (byte)'a');

            return bytes;
        }

        /// <summary>A stream that cannot report its length — the shape a portal-backed pick has.</summary>
        private sealed class NonSeekableStream : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            private readonly Stream _inner;

            public NonSeekableStream(Stream inner)
            {
                _inner = inner;
            }

            public override void Flush()
            {
                _inner.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _inner.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
