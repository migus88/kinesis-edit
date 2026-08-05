using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The picked-file DTO of the import flow (specs/07-lighting.md §1.4). It carries the file's
    /// true byte length so the caller can apply the 50 KB maximum itself; the record never
    /// enforces it.
    /// </summary>
    public sealed class PickedFileTests
    {
        [Fact]
        public void Constructor_WithoutATruncationFlag_ReportsAWholeFile()
        {
            var file = new PickedFile("led1.txt", null, 12, ["[1>1]"]);

            Assert.False(file.IsTruncated);
            Assert.Null(file.Path);
        }

        [Fact]
        public void Constructor_WithALengthOverTheImportMaximum_StillAcceptsIt()
        {
            // Refusing an oversized file is the import view model's job, not the DTO's — the
            // record must be able to carry the length the user has to be told about.
            var file = new PickedFile("huge.txt", "/tmp/huge.txt", 512L * 1024, []);

            Assert.Equal(512L * 1024, file.ByteLength);
            Assert.Empty(file.Lines);
        }

        [Fact]
        public void Equals_WithTheSameComponents_ComparesByValue()
        {
            IReadOnlyList<string> lines = ["[1>1]"];

            var first = new PickedFile("led1.txt", "/tmp/led1.txt", 5, lines);
            var second = new PickedFile("led1.txt", "/tmp/led1.txt", 5, lines);

            Assert.Equal(first, second);
            Assert.NotEqual(first, second with { IsTruncated = true });
        }
    }
}
