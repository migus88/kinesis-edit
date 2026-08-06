using System.Text;
using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IFilePickerService"/>: hands back the staged file, records every
    /// title it was opened with, and can be made to fail the way an unreadable pick would.
    /// </summary>
    internal sealed class FakeFilePickerService : IFilePickerService
    {
        /// <summary>The file to hand back; null is the user cancelling.</summary>
        public PickedFile? FileToReturn { get; set; }

        /// <summary>When set, the pick faults with this instead of returning.</summary>
        public Exception? ExceptionToThrow { get; set; }

        /// <summary>Every title the picker was opened with, in call order.</summary>
        public List<string> Titles { get; } = [];

        /// <summary>How often the picker was opened.</summary>
        public int PickCount => Titles.Count;

        /// <summary>
        /// Stages a picked file from its lines, with the byte length a real Latin1 file of those
        /// lines would have. Tests that need a specific length (the 50 KB refusal of
        /// specs/07-lighting.md §1.4) set <see cref="FileToReturn"/> themselves.
        /// </summary>
        public void SetFile(string name, params string[] lines)
        {
            var byteLength = Encoding.Latin1.GetByteCount(string.Join(Environment.NewLine, lines));

            FileToReturn = new PickedFile(name, null, byteLength, lines);
        }

        public Task<PickedFile?> PickTextFileAsync(string title)
        {
            Titles.Add(title);

            if (ExceptionToThrow is not null)
            {
                // Faulted rather than thrown: a real picker fails asynchronously, and a caller
                // that only awaits later must see it there and not at the call site.
                return Task.FromException<PickedFile?>(ExceptionToThrow);
            }

            return Task.FromResult(FileToReturn);
        }
    }
}
