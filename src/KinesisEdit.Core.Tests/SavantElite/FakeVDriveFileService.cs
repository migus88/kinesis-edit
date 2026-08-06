using KinesisEdit.Core.VDrive.Io;

namespace KinesisEdit.Core.Tests.SavantElite
{
    /// <summary>
    /// An in-memory <see cref="IVDriveFileService"/> whose write can be made to fail the way the
    /// real one does when the file disappears mid-save (specs/03-vdrive-and-files.md §5.2 refuses
    /// a write to a missing file with <see cref="FileNotFoundException"/> unless allowCreate is
    /// set). Used to pin that a lost file at write time does not fall through to the
    /// create-a-fresh-file path and discard the merge result.
    /// </summary>
    internal sealed class FakeVDriveFileService : IVDriveFileService
    {
        private readonly List<string> _lines;

        public FakeVDriveFileService(params string[] lines)
        {
            _lines = [.. lines];
        }

        public bool FailNextWrite { get; set; }

        public int WriteCount { get; private set; }

        public IReadOnlyList<string> WrittenLines { get; private set; } = [];

        public bool WrittenWithAllowCreate { get; private set; }

        public IReadOnlyList<string> ReadAllLines(string path)
        {
            return _lines;
        }

        public void WriteAllLines(string path, IReadOnlyList<string> lines, bool allowCreate = false)
        {
            WriteCount++;

            if (FailNextWrite && !allowCreate)
            {
                FailNextWrite = false;

                throw new FileNotFoundException("The file vanished mid-save.", path);
            }

            WrittenLines = lines;
            WrittenWithAllowCreate = allowCreate;
            _lines.Clear();
            _lines.AddRange(lines);
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
