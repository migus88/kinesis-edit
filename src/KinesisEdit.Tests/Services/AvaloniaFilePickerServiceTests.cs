using KinesisEdit.Services;

namespace KinesisEdit.Tests.Services
{
    /// <summary>
    /// The import flow's file picker (specs/07-lighting.md §1.4). Only the ownerless path is
    /// unit-testable — the native dialog itself needs a running Avalonia application and is
    /// hand-verified; everything the picked bytes go through is
    /// <see cref="PickedFileReaderTests"/>.
    /// </summary>
    public sealed class AvaloniaFilePickerServiceTests
    {
        [Fact]
        public void Constructor_WithoutAnOwnerAccessor_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new AvaloniaFilePickerService(null!));
        }

        [Fact]
        public async Task PickTextFileAsync_WithoutAnOwnerWindow_ReportsACancel()
        {
            // The pickers are wired before the shell window exists (docs/app/app-shell.md), so
            // an owner-less pick must report a cancel rather than throw.
            var picker = new AvaloniaFilePickerService(() => null);

            Assert.Null(await picker.PickTextFileAsync("Import"));
        }
    }
}
