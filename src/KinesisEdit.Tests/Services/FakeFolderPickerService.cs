using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// Hand-rolled <see cref="IFolderPickerService"/>: hands back the staged directory, records
    /// every title it was opened with, and can be made to fail the way a platform without a
    /// storage backend would.
    /// </summary>
    internal sealed class FakeFolderPickerService : IFolderPickerService
    {
        /// <summary>The directory to hand back; null is the user cancelling.</summary>
        public string? FolderToReturn { get; set; }

        /// <summary>When set, the pick faults with this instead of returning.</summary>
        public Exception? ExceptionToThrow { get; set; }

        /// <summary>Every title the picker was opened with, in call order.</summary>
        public List<string> Titles { get; } = [];

        /// <summary>How often the picker was opened.</summary>
        public int PickCount => Titles.Count;

        public Task<string?> PickFolderAsync(string title)
        {
            Titles.Add(title);

            if (ExceptionToThrow is not null)
            {
                // Faulted rather than thrown: a real picker fails asynchronously, and a caller
                // that only awaits later must see it there and not at the call site.
                return Task.FromException<string?>(ExceptionToThrow);
            }

            return Task.FromResult(FolderToReturn);
        }
    }
}
